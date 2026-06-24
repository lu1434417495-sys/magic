# 当前 Godot 项目的上下文装载单元

更新日期：`2026-06-23`

## 文档定位

- 这份文档只用于划分“改一类问题时应该先读哪些文件”。
- 这不是系统设计说明，也不是实现细节、迁移状态、测试清单或变更日志。
- 这里描述的是项目架构边界、职责归属、依赖关系和推荐读图组合。
- 具体规则、数值、运行时细节、回归入口，应该放在对应设计文档、源码或测试目录内。

## 使用规则

- 优先按“推荐装载组合”选读。
- 没有命中时，从“单元总览”中选择 1 个桥接单元，再补 1 到 2 个叶子单元。
- 先读“文件”列表；只有确认跨边界时，才补读“邻接单元”。
- 涉及 save/schema/兼容策略时，先结合 `AGENTS.md` 的兼容性约束判断，不自行扩散到无关单元。
- 常规回归不默认包含 battle simulation、balance simulation、benchmark 或交互式工具入口。

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
GameSession -> GameRoot -> GameContentCatalog -> typed content registries
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
  - `scripts/utils/WorldPresetRegistry.cs`
- 负责：启动入口、世界预设入口、存档选择、显示设置、建卡入口；`WorldPresetRegistry` / `DisplaySettingsService` 的固定值和预设查询只作为内部 typed/service 边界，不要恢复 public GD helper、公开常量或把 preset info 当 Godot dictionary 业务态回读。
- 适合：开始菜单、建卡 UI、预设入口、存档列表、显示设置。
- 邻接单元：CU-02、CU-03、CU-14。

### CU-02 GameSession、存档、序列化、全局内容缓存

- 文件：
  - `scripts/systems/persistence/*.cs`
  - `scripts/systems/content/GameRoot.cs`
  - `scripts/systems/content/GameContentCatalog.cs`
  - `scripts/systems/content/skills/*.cs`
  - `scripts/player/progression/*content_registry.*`
  - `scripts/player/warehouse/*ContentRegistry.cs`
  - `scripts/enemies/EnemyContentRegistry.cs`
  - `scripts/enemies/EnemyContentSeed.cs`
  - `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs`
  - `scripts/utils/GodotObjectOwnership.cs`
  - `scripts/utils/GodotTypedResourceGraphWalker.cs`
- 负责：active save、slot meta、save payload、save index、内容注册表、全局会话边界；`GameSession` 现在持有统一 `GameRoot`，`GameContentCatalog` 是正式内容类型的组合根读入口，并持有自己的 typed 内容快照缓存（覆盖 skill / trait / profession / achievement / quest / item / recipe / enemy template / enemy brain / wild roster / progression identity catalog / battle special profile snapshot）：`GameSession` 在刷新 progression / item / recipe / enemy / battle special profile 内容后显式调用 catalog 重建，catalog getter 返回自己缓存的只读视图而非每次转发回 session 重建 typed index，重建会自增 catalog revision 供下游做有效性校验；catalog 的 typed 字典读侧是防御性只读视图（`ReadOnlyDictionary` 包装），下游即便 downcast 也拿不到内部可变 `Dictionary`、不能改写 catalog 快照，要修改正式内容应回到 `GameSession` 内容缓存再触发重建；catalog 生命周期绑定 owning `GameRoot`，root dispose 时会解绑并使 catalog 失效（清空 typed 快照并自增 revision），任何仍持有旧 catalog 引用的下游此后读到的是空内容而非 stale 快照；runtime 装配应优先从 catalog 读取 typed 内容表，再由各领域 owner 持有自己的运行期状态；trait 内容正式读侧走 `ProgressionContentRegistry.GetTraitDefsTyped()` / `GameSession.GetTraitDefsTyped()` / `GameContentCatalog.GetTraitDefsTyped()`，其中 `ProgressionContentRegistry.GetTraitDefsTyped()` 是 progression trait content source，`GameSession.GetTraitDefsTyped()` 是 session 正式 getter，`GameContentCatalog.GetTraitDefsTyped()` 是 catalog typed trait snapshot，不要把 trait content 回投成 `GDictionary` 业务态再读；quest 内容正式读侧走 typed `get_quest_def(StringName)` / `get_quest_defs_typed()`，runtime caller 不要再 duplicate quest payload、回读 `Variant`、`QuestDef.from_dict()` 或 string-key fallback；identity 内容正式读侧现在只通过 `ProgressionContentRegistry.GetIdentityCatalogTyped()` / `GameSession.get_progression_identity_catalog_typed()` 进入 `ProgressionIdentityCatalogData`，registry/content seed 是允许把 Godot 字典资源投成 typed catalog 的边界，但 typed index 只投正式 `StringName` key，runtime caller 不要把 identity catalog 再投影成 bundle/dictionary 业务态后回读；`GameSession.get_skill_defs_typed()` / `get_profession_defs_typed()` / `get_achievement_defs_typed()` / `get_item_defs_typed()` 现在直接反映 session 自己维护的 content cache，并继续只认 `StringName` key，不要假设底层 registry typed getter 或共享 `GDictionary` 变更会自动代表 formal runtime 输入；`BattleSpecialProfileRegistry.Rebuild(GDictionary)` 的 public 边界也只从正式 `StringName` key 建 skill index，manifest 的 `owning_skill_ids -> profile_id` runtime 映射必须由当前 typed skill catalog 上的 `special_resolution_profile_id` 授权，不要再按 `SkillDef.skill_id` value 或 manifest 自身 owner 列表恢复 string-key-only skill_defs。
- 补充约束：`GameContentCatalog.GetSkillCatalogTyped()` 是正式 skill 查询门面，门面只读当前 catalog typed skill 快照与 revision，不自行扫描技能资源、不持有角色运行期技能状态，也不恢复 string-key fallback。
- 生命周期约束：`GD.Load` / `.tres` borrowed content 与 registry build/merge/template/patch 产生的 derived static content 都是 process-lifetime static content，由 `GodotContentOwnership` 强持有整个 Resource graph；normal/test finalizer drain 只能 suppress known borrowed/derived graph 后 retain store，不得 clear static keep-alive 后 forced GC。`GodotTypedResourceGraphWalker` 是 static content owner graph 的统一 walker，必须覆盖 project Godot wrapper field、auto-property backing field、`Godot.Collections.Array` / `Dictionary` / `Variant` wrapper；新增 content wrapper 字段时不要在 consumer getter 里补生命周期逻辑。`SkillCatalog` / `SkillEffectiveCombatProfileResolver` 缓存的 skill-level effective combat profile 是静态内容派生视图；`CombatSkillDef.GetUnlockedCastVariants(...)` 产生的 `Duplicate(true)` cast variant graph 及返回的 Godot collection wrapper 必须注册为 process-lifetime `DerivedStaticContent`。AI / runtime scoring 只校验 ownership，不在消费点 suppress/dispose 或按类型做全局兜底。
- Save owner 约束：`SaveRepository` 现在拥有底层 save payload 文件 IO，包括 save path 构建、压缩 payload 读写、原子替换/重命名/删除和 save 目录确保；`GameSession` 继续拥有 active save、schema/meta 组装、save index 归并和运行时 pending/dirty 状态；`PartyState` 这类子 payload 发生破坏性 schema 变化时应同步升级 owning save version，并且反序列化只接受当前版本，不要恢复旧版本兼容、legacy alias 或 fallback migration；不要把新的底层文件写入细节继续内联回 `GameSession`。
- World save boundary 约束：`world_data` 的 runtime owner 是 `WorldRuntimeData`，`SaveSerializer` 只在正式 save payload 入口/出口调用 `WorldRuntimeData.FromDictionary(...)` 与 `WorldMapDataProjection.Project(WorldRuntimeData)`，并继续先做当前 schema 校验；不要在 save decode/normalize/serialize 之外新增 runtime 字典 owner，也不要为旧 world payload 加兼容迁移。
- 生命周期约束：`GameSession.Dispose()` 是 session/root/content registry 的 owning teardown，必须释放 plain C# `GameRoot` / `GameContentCatalog` 绑定、内容 registry、plain C# `GameLogService` / log sink、runtime 辅助资源并 `Free()` native node；`GameRoot` / `GameLogService` 不参与 Godot native wrapper 生命周期，root dispose 只负责解绑 catalog/session 并使 catalog typed snapshot 失效，不要恢复 `GlobalClass` / `RefCounted` 或通过 `GodotObject.IsInstanceValid` 管理 root/log service；`ProgressionContentRegistry` 及其 skill / profession / identity / barrier sub-registry 家族、`ItemContentRegistry` / `RecipeContentRegistry` / `EnemyContentRegistry` / `BattleSpecialProfileRegistry` 是 plain C# `IDisposable` 内容 owner，直接 `Dispose()` 清空 typed/resource 缓存，不要恢复 `GlobalClass` / `RefCounted` 或用 GodotObject validity/dispose helper 管理；仍为 Godot wrapper 的内容 registry 必须通过自己的 dispose 路径清空缓存，避免把 `RefCounted` 资源留到进程退出；底层文件替换/清理入口 `FileIOCoordinator` 与 save payload serializer `SaveSerializer` 都是 plain C# utility/service，不参与 Godot object 生命周期，也不要恢复 `GlobalClass` / `RefCounted`。
- 适合：save schema、序列化、内容接入、全局注册表问题。
- 邻接单元：CU-01、CU-03、CU-04、CU-10、CU-11、CU-13、CU-20、CU-21。

### CU-03 世界配置资源与预设数据

- 文件：
  - `scripts/utils/WorldMap*Config.cs`
  - `scripts/utils/Settlement*.cs`
  - `scripts/utils/Facility*.cs`
  - `scripts/utils/WildSpawnRule.cs`
  - `scripts/utils/WorldMapContentValidator.cs`
  - `data/configs/world_map/*.tres`
  - `data/configs/world_map/shared/*.tres`
- 负责：world preset、世界生成配置、据点/设施/野外遭遇的静态资源；`WorldMapContentValidator` 是 plain C# validator，public generation config / preset 校验只从 enemy template / roster catalog 的正式 `StringName` key 建 known-id set，不要从 string key 或 value id 恢复，也不要恢复 `RefCounted` / `GlobalClass` 生命周期。
- 适合：世界预设、设施分布、遭遇配置、世界内容校验。
- 邻接单元：CU-01、CU-02、CU-04。

### CU-04 世界生成、据点服务注入、遭遇锚点

- 文件：
  - `docs/design/settlement.md`
  - `scripts/systems/world/WorldMapSpawnSystem.cs`
  - `scripts/systems/world/EncounterAnchorData.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/WorldEventConfig.cs`
  - `scripts/utils/MountedSubmapConfig.cs`
- 负责：世界生成、据点注入、挂载子地图事件、遭遇锚点和世界初始状态；`EncounterAnchorData` 是 plain C# world runtime DTO，由 `WorldRuntimeData` typed owner 持有，`encounter_anchors` public/save 边界只投影 dictionary payload，不要恢复 `RefCounted` / `GlobalClass` 或把 live anchor object 放进 Godot array。
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
- 适合：世界移动判定、迷雾刷新、地图 cell 数据。
- 邻接单元：CU-04、CU-06、CU-07。

### CU-06 世界/战斗运行时总编排与场景适配

- 文件：
  - `scenes/main/world_map.tscn`
  - `scripts/systems/game_runtime/*.cs`
  - `scripts/systems/world/WorldMapDataContext.cs`
  - `scripts/systems/world/WorldRuntimeData.cs`
  - `scripts/systems/world/WorldMapDataProjection.cs`
  - `scripts/systems/world/WorldTimeSystem.cs`
  - `scripts/systems/game_runtime/WorldMapSystem.cs`
  - `scripts/systems/settlement/*.cs`
  - `scripts/ui/RuntimeLogDock.cs`
  - `scripts/ui/SubmapEntryWindow.cs`
- 负责：world/battle 切换、窗口互斥、命令编排、场景同步、战后回写、运行时总入口；quest runtime setup 正式链应继续直接消费 `GameSession.get_quest_defs_typed()` / `QuestDef`，不要把 `GameRuntimeFacade` / settlement handler / quest 命令流退回 quest `GDictionary` 业务态；contract board 的任务显示、状态判断和提交也应继续基于 typed `QuestDef` 视图构建，只在 modal/window payload 边界投影字典，不要恢复 `QuestDef.ToDictionary()` 后 normalize 再回读的业务链；`GameRuntimeQuestCommandHandler` 的 direct progress 命令现在通过 typed payload / typed progress result 进入 `CharacterManagementModule`，不要恢复 `event Dictionary -> GArray -> summary Dictionary` 的正式请求链；普通 settlement action 命令输入以 `SettlementActionRequest` 为正式边界，`SettlementWindow` / headless 文本命令只能提交 settlement/service/action/member/quantity/source 显式字段，不能把 `pending_character_rewards`、`quest_progress_events` 或 `emit_default_quest_progress_event` 这类业务副作用塞进请求；forge / contract board 的字典只保留在 modal/window 投影和提交白名单边界，允许当前配方或契约选择字段进入对应 typed 服务，不作为普通 settlement action overrides；settlement action finalization 直接消费 typed `SettlementServiceResult`，不要恢复 `SettlementServiceResult.FromDictionary()` 或 `ToDictionary()` 回读 helper；promotion choice 的正式 command input 是 `PromotionSelectionData`，`WorldMapSystem` 只能在 `PromotionChoiceWindow` UI 信号边界把 selection payload 转成 DTO，`GameRuntimeFacade` / `GameRuntimeRewardFlowHandler` / `WorldMapRuntimeProxy` / battle promotion path 不要恢复 `GDictionary selection`；warehouse use-item 正式链现在通过 typed `PartyItemUseOptions` / `PartyItemUseResult` / `CharacterManagementModule.LearnSkillOptionsData` 进入 `PartyItemUseService` 和 `GameRuntimeWarehouseHandler`，`WorldMapSystem._on_party_warehouse_use_requested()` / `WorldMapRuntimeProxy.CommandWarehouseUseItem()` 应继续直接走 typed request，不要在 scene/runtime owner 内恢复 `new GDictionary()` options 包装，也不要在成功后漏掉正式 `_render_from_runtime()` 刷新；`GameRuntimeFacade` 自己的 wild encounter roster 正式缓存现在只保留 typed `_wild_encounter_roster_defs`，不要恢复 `_wild_encounter_rosters` 这类 `GDictionary` owner 级业务态。
- 补充约束：`GameRuntimeFacade.Setup(...)` 和世界场景初始化中的正式内容读取应通过 `GameContentCatalog` 进入 typed 内容表；`GameRuntimeFacade` 应通过当前 root/session 解析 content catalog，只在缓存 catalog 仍绑定当前 `_game_session`（`GameContentCatalog.IsBoundToSession`）时才复用，不要长期持有可能已失效或绑定了其他 session 的旧 catalog 实例（只检查 `HasSessionTyped()` 不够，绑定别的 session 的 catalog 仍“有效”却不是当前 session 的；catalog 重建在原实例上原地进行，下游可用 revision 做有效性校验）；`GameSession` 仍是 autoload/session 根，运行期状态继续由各领域模块拥有，不要把 battle/party/UI 临时状态塞回 content catalog。
- Party root 约束：`PartyState` 替换必须保持 session/runtime/services 的 canonical root 一致；`GameSession.SetPartyState(...)` 传入当前 session root 时只标记 dirty，不应 normalize 成新 root 后 dispose 当前 root；需要替换 root 的 runtime 命令必须继续通过 `RuntimeTransaction` / facade rebind 路径恢复 `GameRuntimeFacade._party_state`、`GameSession` party state 和 party services。
- 生命周期约束：settlement shop/forge/research 服务是 plain C# runtime helpers，由 `GameRuntimeSettlementCommandHandler` 创建和重建；shop/forge 只通过 `IDisposable` 释放自己内部持有的 Godot helper/resource registry，handler 不应把这些服务当 `RefCounted` / `GlobalClass` 或通过 GodotObject disposal helper 管理。
- 生命周期约束：`GameRuntimePendingBattleGenerationRequest` 是 pending battle start 的 typed runtime owner，内部 duplicate 的 context key/value wrapper 必须通过 `RuntimeStateLifecycle.MarkValueGraphFinalizerless(...)` 注册为 runtime state；进入 `BattleRuntimeModule.StartBattle(...)` 后由 StartBattle 的 transient scope 统一承接 context，`BattleUnitFactory` 不应为同一个 context 在 ally/enemy/terrain 子路径重复开 scope 接管。terrain generator 只接收瘦身 terrain context（terrain profile/map size/spawn override 等），不要把 `battle_party` / `enemy_units` 这类单位 payload 带入 terrain context ownership graph。
- 生命周期约束：`GameRuntimeBattleWritebackService` 与 `GameRuntimeBattleLootCommitService` 是 `GameRuntimeFacade` 持有的 plain C# sidecar，只通过 `Setup(GameRuntimeFacade)` 绑定 weak runtime ref 并在 facade dispose 时直接 `IDisposable.Dispose()` 清理，不要恢复 `RefCounted` / `GlobalClass` 或纳入 `DisposeOwned<T GodotObject>` 路径。
- 生命周期约束：`GameRuntimeFacade`、`BattleSessionFacade`、`GameRuntimeBattleSelection`、`GameRuntimeSettlementCommandHandler` 都是 plain C# runtime owners/helpers，`WorldMapSystem` 或 headless runner 直接持有 typed 引用并调用 `IDisposable.Dispose()`；这些 runtime owner 的 weak-ref 解析只做 weak target/null 判断，不做 `GodotObject.IsInstanceValid`，也不要恢复 `GlobalClass` / `RefCounted`、`GetInstanceId()` 作为 runtime 身份、或 `DisposeOwned<T GodotObject>` 释放路径。
- 写回约束：runtime/command handler 需要同时提交 party/world/player coord 时应通过 `RuntimeTransaction` stage 后统一 `CommitRuntimeState(...)`；`GameRuntimeFacade.PersistPartyStateInternal()` / `PersistWorldDataInternal()` / `PersistPlayerCoord()` 和 settlement command persist 已经走 transaction，不要在新 handler 中继续新增分散的 `GameSession.SetPartyState/SetWorldData/SetPlayerCoord` + flush/commit 组合。
- Research catalog 约束：`SettlementResearchService` 的正式奖励目录应由 `SettlementResearchRewardEntry` 持有，raw `GArray/GDictionary` 只保留在 catalog import/test override 边界；未知 `SettlementResearchRewardKind` / `entry_type` 必须作为目录 schema error 返回，不允许被候选筛选静默跳过。
- World runtime data 约束：`WorldMapDataContext` 内部 root/active 世界状态应通过 `WorldRuntimeData` 持有，settlement state、submap stack、mounted submap、encounter anchors、world events、fog/world npc 等运行时读写先进入 typed owner，再通过 `WorldMapDataProjection.Project(...)` 写回 save/window/proxy 边界；`root_world_data` / `active_world_data` 只作为 Godot/save/proxy 边界投影保留，不应在业务逻辑新增字典真相源。`RuntimeTransactionRollbackState` 的 world rollback snapshot 也应保存 `WorldRuntimeData`，不要恢复按 key 手写捕获/回投的 nested dictionary snapshot。
- ViewModel 约束：世界 UI/proxy 的基础只读状态应优先通过 typed `WorldRuntimeViewModel` 读取（status、modal、player/selected coord、active map、附近 encounter/world event 摘要），`WorldMapSystem.RenderFromRuntime(...)` 不应继续为这些基础字段新增多处散读 runtime getter；需要 Godot payload 的窗口/日志/地图数据仍留在各自 projection/window 边界。
- 适合：runtime 接线、模式切换、世界场景同步、据点/仓库/奖励/任务命令入口。
- 邻接单元：CU-02、CU-04、CU-05、CU-07、CU-08、CU-09、CU-10、CU-12、CU-15、CU-18、CU-21。

### CU-07 世界地图渲染叶子单元

- 文件：
  - `scripts/ui/WorldMapView.cs`
  - `assets/main/basic_map/*.png`
- 负责：大地图绘制、图标、选中反馈、点击表现。
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
- 负责：队伍编成、转职选择、奖励弹窗、角色摘要展示、触发术 setup 窗口；`PromotionChoiceWindow` 的 prompt/selection 字典只属于 UI 投影与 Godot signal 边界，提交后必须在 scene/runtime adapter 处转换为 `PromotionSelectionData`，不要让 selection 字典继续进入 runtime/progression 规则链；`ContingencySetupWindow` 只渲染当前 setup 状态并通过 save/charge/clear 信号提交意图，不能直接改 `PartyMemberState`。
- 适合：队伍窗口、转职 UI、触发术 setup UI、角色奖励弹窗。
- 邻接单元：CU-06、CU-10、CU-11、CU-12、CU-14。

### CU-10 队伍共享背包、物品定义与装备基础流转

- 文件：
  - `docs/design/battle_weapon_dice_and_equipment.md`
  - `scripts/player/equipment/*.gd`
  - `scripts/player/equipment/*.cs`
  - `scripts/player/warehouse/*.gd`
  - `scripts/player/warehouse/WarehouseState.cs`
  - `scripts/player/warehouse/WarehouseStackState.cs`
  - `scripts/player/warehouse/TraitRollGroupDef.cs`
  - `scripts/player/warehouse/TraitRollGroupEntryDef.cs`
  - `scripts/player/warehouse/ItemTraitContentValidator.cs`
  - `scripts/systems/inventory/*.gd`
  - `scripts/systems/inventory/*.cs`
  - `scenes/ui/party_warehouse_window.tscn`
  - `scripts/ui/PartyWarehouseWindow.cs`
  - `data/configs/items/*.tres`
  - `data/configs/items_templates/*.tres`
  - `data/configs/recipes/*.tres`
- 负责：共享背包、堆叠/容量、物品与配方定义、装备实例、装备/卸装、物品使用；`WarehouseState` / `WarehouseStackState` 是 plain C# runtime/save state，内部正式集合使用 `List<T>`，只在 `ToDictionary()` / `FromDictionary()` 存档边界投影 `Godot.Collections` payload，不要恢复 `RefCounted`、Godot Array 作为运行时 owner，或把仓库根传入 GodotObject disposal/validity helper；`EquipmentState` / `EquipmentEntryState` / `EquipmentInstanceState` 也是 plain C# runtime/save state，正式槽位索引和装备实例集合由 .NET collection 与 typed owner API 承载，装备实例本体不得塞入 `Godot.Collections.Dictionary` value 或传给 GodotObject disposal/validity helper，Godot 边界只接受 `EquipmentInstanceState.ToDictionary()` payload；`ItemDef` 的 item category / equipment type / weapon physical damage tag、固定 trait ids、trait roll groups 与 `TraitRollGroupDef` / `TraitRollGroupEntryDef` 都属于物品内容边界，跨 trait catalog 规则由 `ItemTraitContentValidator` 校验；`EquipmentRules` 的装备槽位、`EquipmentInstanceState.RarityTier`、装备 trait mint 时机都由 typed runtime 拥有，`EquipmentTraitRollService` 只在仓库分配稳定装备 instance id 后生成 `equipment_roll` trait instance，不在 transient drop 阶段或已有实例入库时重 roll；C# caller 不要恢复 GD helper 函数或合法值 HashSet；`WeaponDamageDiceDef.ValidateDice` 属于程序集内部校验入口，不要作为 public Godot-facing helper 复用；`PartyWarehouseService` 是 plain C# inventory service，runtime/progression/equipment owner 只通过 `IDisposable` 清引用，不要恢复 `RefCounted` 或 GodotObject validity/dispose 路径；`PartyWarehouseService` / `PartyEquipmentService` / `PartyItemUseService` 的正式 setup 现在优先走 typed item/skill/trait catalog，`PartyItemUseService` 的 `GDictionary` setup 边界只做严格 `StringName` key 解码，不要恢复 string-key fallback；`RecipeContentRegistry` 正式 setup 只消费 typed item catalog，不要恢复 `GDictionary -> ItemDef.item_id` value-index helper；forge/settlement 服务调用 `PartyWarehouseService.Preview/CommitBatchSwapEntriesTyped` 时应传 `{ item_id = StringName }` 这类正式 batch entry 字典，不要用裸 item id `Variant` 作为批量输入。
- 适合：物品内容、装备流转、仓库规则、仓库窗口。
- 邻接单元：CU-02、CU-06、CU-09、CU-11、CU-12、CU-19、CU-21。

### CU-11 队伍与角色成长运行时数据模型

- 文件：
  - `scripts/player/progression/PartyState.cs`
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
  - `scripts/systems/progression/PendingCharacterReward*.cs`
  - `scripts/systems/progression/CharacterProgressionDelta.cs`
- 负责：队伍状态、成员状态、成长状态、任务状态、角色奖励载体；`PartyState.member_states` 的正式 owner 是 `PartyMemberStateCollection`，`PartyMemberState` 拥有 `ContingencyMatrixSetupState` 及其 trigger / target resolver / stored spell / material cost 子状态的持久 setup 事实，只保存战斗外 setup、充能与消耗回写事实，不承载战斗内 release queue 或 hook 状态；`AttributeSnapshot`、`UnitProgress`、`UnitBaseAttributes`、`UnitReputationState`、`AchievementProgressState`、`UnitSkillProgress`、`UnitProfessionProgress`、`ProfessionPromotionRecord`、`PendingProfessionChoice`、`CharacterProgressionDelta`、`QuestState`、`PendingCharacterReward`、`PendingCharacterRewardEntry` 以及 `Contingency*State` 都是 plain C# runtime/save DTO，运行时只通过 typed getter/backing collection 和 `ToDictionary()` / `FromDictionary()` 边界投影读写，不要恢复 `GlobalClass` / `RefCounted`、GodotObject disposal，或把这些 DTO 本体塞进 `Godot.Collections.Dictionary` value；`UnitProfessionProgress.promotion_history` 正式 owner 是 `List<ProfessionPromotionRecord>`，只在 save payload 边界投影 dictionary array；`UnitProgress.pending_profession_choices` 正式 owner 是 `List<PendingProfessionChoice>`，公共 Godot array 只投影 pending choice dictionary payload；`CharacterProgressionDelta` 的 progression fact / promotion choice 集合正式 owner 是 typed backing list，对外只在 public Godot property 和 batch payload 边界投影 array/dictionary payload；`PartyState.pending_character_rewards` 的 live owner 是 `List<PendingCharacterReward>`，`PendingCharacterReward.entries` 的 live owner 是 `List<PendingCharacterRewardEntry>`，pending reward 只在 save/snapshot/facade payload 边界投影为 dictionary array；`PartyState.active_quests` / `claimable_quests` 的 live owner 是 `List<QuestState>`，`QuestState.objective_progress` / `last_progress_context` 的正式 owner 是 `QuestObjectiveProgressState` / `QuestProgressContext`，quest 只在 save/snapshot/facade payload 边界投影为 `Godot.Collections.Dictionary` 或 dictionary array；`UnitBaseAttributes.custom_stats` 与 `UnitReputationState.custom_states` 的正式 owner 是 `UnitCustomStatMap` / `UnitReputationMap`；这些 owner 只在 `ToDictionary()` / `FromDictionary()` 存档边界投影 `Godot.Collections.Dictionary`，运行时规则和状态读写不得重新直接持有弱类型字典；`TraitInstanceState` / `TraitInstanceCollection` 是角色 trait 与装备 roll trait 的持久实例状态边界，`TraitInstanceState` 和 `TraitRollValueState` 都是 plain C# DTO，`PartyMemberState.trait_instances` 与 `EquipmentInstanceState.trait_instances` 的 live owner 都是 `List<TraitInstanceState>`，`TraitInstanceState.roll_values` 的 live owner 是 `List<TraitRollValueState>`，只有 save/schema 边界投影为 `trait_instances` array 与 `roll_values` dictionary；`PartyMemberState.trait_instances` 只承载 `character` source，`EquipmentInstanceState.trait_instances` 只承载 `equipment_roll` source，反序列化必须继续按严格字段集与 source kind 校验；`UnitProgress` 的 skill / profession / achievement 进度 owner 正式承载面现在是 internal typed `IReadOnlyDictionary<StringName, UnitSkillProgress>` / `IReadOnlyDictionary<StringName, UnitProfessionProgress>` / `IReadOnlyDictionary<StringName, AchievementProgressState>`，公共 `skills` / `professions` / `achievement_progress` Godot dictionary 只保留 payload 边界投影，不得重新存放 `RefCounted` state object；combat resource id 正式合法性与默认解锁资源由 `CombatResourceIds` enum utility 统一拥有，不要在 `UnitProgress` / `BattleUnitState` 重新维护 `hp/stamina/mp/aura` HashSet。
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
- 负责：角色管理门面、奖励归并、成就/任务推进、身份与成长桥接；`PartyContingencySetupService` 是世界侧 contingency setup save/charge/clear/status mutation owner，通过 `CharacterManagementModule` 进入仓库扣费、内容校验与属性快照，charge mutation 不进 UI 节点、文本命令、战斗启动或存档加载；`CharacterBattleWritebackService` 拥有战斗结束后的 consumed contingency setup 写回，battle writeback 不应散落到 UI 节点或 battle finalization caller；`QuestProgressService` 和 `CharacterManagementModule` 的 quest setup 正式链现在都直接持有 typed `Dictionary<StringName, QuestDef>`，submit-item / reward claim / progress 匹配通过 strict typed quest index 读取 `QuestDef`，objective/reward 展开也应继续通过 `QuestDef.GetObjectiveEntriesTyped()` / `GetRewardEntriesTyped()`，不要回读公开 `objective_defs` / `reward_entries` 字典字段；`CharacterManagementModule.setup(...)` 对 skill/profession/achievement/item/trait 这组正式输入也已切到 typed catalog，owner 内部不再把 `_skill_defs` / `_profession_defs` / `_achievement_defs` / `_item_defs` 当业务索引；generic trait 聚合由 `CharacterTraitService` 负责，`CharacterManagementModule.build_attribute_source_context(...)` 只把身份、角色、装备 fixed/roll trait 预解析成 `AttributeSourceContext.trait_attribute_modifiers`，不要让 `AttributeService` 反查 trait catalog、item catalog 或角色/装备状态；`learn_skill()` 的正式选项输入现在先解码成 typed `LearnSkillOptionsData`，不要恢复 `confirm_practice_replacement` 字典业务态；promotion flow 进入 `CharacterManagementModule.PromoteProfession(...)` 后必须继续携带 `PromotionSelectionData`，不要恢复 `GDictionary selection` 或 `hp_roll_override` 这类规则后门；attribute_delta 奖励写入属性时必须通过 typed `AttributePermanentChangeSource`，普通奖励源不得授予 protected custom stat 写权限，不要恢复 `source_context` 字典授权；direct progress / submit-item 内部推进已改走 typed progress request/result，不要恢复 `_quest_defs` `Variant` 业务态、string-key fallback 或 `event Dictionary -> GArray` 往返；identity apply / racial grant / runtime setup 这侧现在正式持有 `ProgressionIdentityCatalogData`，`CharacterManagementModule.setup(...)`、血脉/升华/阶段修正/种族技能服务不再接受 progression content bundle 字典入口；`CharacterManagementModule` 现在通过 `ProgressionServiceFactory` 构建 transient `ProgressionService` 图，并把战后 HP/MP/aura、死亡/KO、装备回收与 roster 移除写回委托给 `CharacterBattleWritebackService`，不要把 progression service 装配细节或 battle writeback/salvage 逻辑重新内联回门面。
- 生命周期约束：`CharacterManagementModule` 是 plain C# runtime gateway/service，只通过 `IDisposable.Dispose()` 释放内部 `PartyWarehouseService` / `PartyEquipmentService` / `QuestProgressService` / battle writeback / trait aggregation 引用，不参与 Godot native wrapper 生命周期；`GameRuntimeFacade`、`BattleSimFormalCombatFixture` 和测试 fixture 都必须直接调用 `Dispose()`，不要把它传入 `GodotObject` / `RefCounted` validity 或 disposal helper，也不要恢复 `GlobalClass` / `RefCounted` 基类。
- 适合：奖励入账、任务推进、成就记录、跨系统成长接线。
- 邻接单元：CU-06、CU-08、CU-09、CU-10、CU-11、CU-13、CU-14、CU-15、CU-19。

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `scripts/player/progression/*Def.cs`
  - `scripts/player/progression/*Requirement.cs`
  - `scripts/player/progression/*ContentRegistry.cs`
  - `scripts/player/progression/*content_validator.gd`
  - `scripts/player/progression/QuestContentValidator.cs`
  - `scripts/systems/progression/*ContentValidator.cs`
  - `data/configs/skills/*.tres`
  - `data/configs/professions/*.tres`
  - `data/configs/races/*.tres`
  - `data/configs/subraces/*.tres`
  - `data/configs/traits/*.tres`
  - `data/configs/bloodlines/*.tres`
  - `data/configs/ascensions/*.tres`
  - `data/configs/stage_advancements/*.tres`
  - `data/configs/barriers/*.tres`
  - `data/configs/faith/*.tres`
- 负责：技能、职业、种族、通用 trait、任务、血脉、升华、信仰等静态内容与内容校验；fixed schema 名称优先由 enum/typed 规则拥有，包括 skill type / learn source / practice tier、profession BAB progression、racial skill charge kind、generic trait effect / trigger dispatch / source scope / stack policy / charge scope / charge reset / roll value type、achievement/pending reward entry kind、attribute growth tier、quest provider、body-size category、damage tag / mitigation tier / damage category、battle save tag/ability、barrier area/outcome、target-team filter、combat targeting modes 与 forced-move mode，不要恢复 public GD helper 函数或 HashSet/IReadOnlySet 白名单；`TraitDef` / `TraitContentRegistry` / `data/configs/traits/*.tres` 是通用 trait 内容定义边界，source scope、effect/stack/charge/roll schema 等固定值由 `TraitContentRules` 统一校验，identity `trait_ids` reference 校验通过 generic trait source scope 判断是否允许 `identity`，不要恢复 `TraitDef.@params`；trait effect-specific 配置必须是显式 typed 字段，也不要把 content bucket `GDictionary` 当运行时业务态直接回读；registry config directory 不是 public API，内容校验需要路径时在 validator/test 边界显式给定目录；quest 内容这侧的正式 schema/reference 校验现在优先消费 `QuestDef` 的 typed objective/reward/pending-reward 视图、`ProgressionContentRegistry.GetQuestDefsTyped()` 和 `GetQuestRegistrationErrorsTyped()`，`QuestContentValidator` 已收成 plain static typed validator，只保留 `ValidateTyped(...)` 正式入口；`SkillDef.tags` owner 现在是 internal typed `IReadOnlyList<StringName>`，runtime / registry / battle helper 不要再从公共 Godot `Array<StringName>` projection 回读 tag 集合；`SkillDef.attribute_growth_progress` owner 现在是 internal typed `IReadOnlyDictionary<StringName, int>` + typed schema entry list，runtime / registry / validator 不要再从公共 Godot dictionary 回读 `Variant` key/value；`SkillDef.learn_requirements` / `knowledge_requirements` / `achievement_requirements` / `upgrade_source_skill_ids` / `mastery_sources` 以及 `skill_level_requirements` / `attribute_requirements` 现在也都由 internal typed list/dictionary + typed requirement-entry list 持有，runtime / registry / validator 不要把这些公共 Godot array/dictionary projection 当正式业务态回读；`SkillDef.attribute_modifiers` 现在也由 internal typed `IReadOnlyList<AttributeModifier>` 持有，属性计算链不要再从公共 `Array<Resource>` projection 回读技能 attribute modifier；`SkillDef.level_description_configs` 现在也由 internal typed `Dictionary<int, Dictionary<string, Variant>>` + typed schema entry list 持有，formatter / validator 不要再从公共 Godot dictionary projection 回读等级描述配置；`ProfessionContentRegistry` 的 skill/profession reference 校验现在只认正式 `StringName` key，不要恢复 `ContainsKeyFlexible` / string-key fallback；identity 内容正式聚合现在消费 `ProgressionContentRegistry.GetIdentityCatalogTyped()` / `ProgressionIdentityCatalogData`，不要恢复 `FromContentBundle(...)`、bundle alias bucket、或血脉/升华/阶段修正/身份校验链的 `GDictionary` content-source 入口；公开 registry 边界 `validate(...)` 只应接受 `StringName` key 的正式内容表，不要把 `QuestContentValidator` / `QuestDef.validate_schema()` 退回 objective/reward `GDictionary` 逐项业务迭代，或从 registry公开字典投影重新回建 typed 集合、按 value 恢复 string-key entry，也不要恢复旧的 quest-entry `Array<Dictionary>` 公开适配。
- 兼容约束：`RaceDef.damage_resistances` / `SubraceDef.damage_resistances` 的正式 schema 是 `StringName -> StringName`，identity registry / `ProgressionContentRegistry` / 身份摘要与战斗种族特性桥都不应再从 string key 或 string value 恢复。
- 兼容约束：`CombatEffectDef.params.slot_weight_map` 这类内容配置内嵌 map 的正式 slot key 是 `StringName`，`SkillContentRegistry` 校验和 `CombatEffectDef.GetStringNameIntMapParamTyped()` 不应再从 string key 恢复。
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
  - `scripts/systems/attributes/AttributeService.cs`
  - `scripts/systems/attributes/AttributePermanentChangeSource.cs`
- 负责：成长公式、职业规则、技能规则、建卡规则、属性计算；practice tier 与 attribute growth tier 预算现在直接走 enum 规则，`PracticeGrowthService` / `ProgressionService` 不再维护 tier dictionary；promotion selection 规则输入由 `PromotionSelectionData` 承载，`ProgressionService.PromoteProfession(...)` 不应暴露 `GDictionary selection` 或从任意 selection 字段读取 HP 掷骰/规则 override；permanent attribute change 的授权输入由 `AttributePermanentChangeSource` 承载，`AttributeService.ApplyPermanentAttributeChange(...)` 不应暴露 `GDictionary source_context`，protected custom stat 只允许 CharacterCreation 或显式授权的 StoryScript 来源写入；BAB 计算直接消费 `ProfessionBaseAttackProgression`，未知 progression 不做 half fallback；`AttributeService` / `ProgressionService` / `CharacterCreationService` 是 plain C# 规则服务，`DerivedAttributeRule` 是 plain C# rule value object，它们都不参与 Godot object 生命周期，也不要恢复 `GlobalClass` / `RefCounted`；`IdentityPayloadValidator` / `CharacterCreationService` 的正式 identity content-source 现在走 `ProgressionIdentityCatalogData`，不再保留 `GDictionary` content-source 重载，不要把建卡前 identity 校验、体型派生、或 save identity repair 退回各类公开 defs projection / bundle / `GDictionary` 业务态；`ProgressionService` / `SkillMergeService` / `PracticeGrowthService` 的 skill/profession catalog setup 现在优先走 typed `IReadOnlyDictionary<StringName, SkillDef/ProfessionDef>`，不要把新的 runtime caller 退回 `GDictionary` setup 或把 typed catalog 再投影回字典后回读；`AttributeService` 只消费 `AttributeSourceContext` 中已经解析好的 modifier 列表，generic trait 属性入口是 `trait_attribute_modifiers`，该入口必须保留 trait modifier 自身的 `source_type/source_id`，不要把它并入 equipment/passive/temporary 默认来源覆盖路径；`AttributeService.Setup(..., GDictionary skill_defs/profession_defs, ...)`、`LevelGrowthEvaluationService.Setup(GDictionary skillDefs)` 和 `CharacterManagementModule.setup(..., GDictionary skill/profession/achievement/item defs, ...)` 这类 Godot 边界只从正式 `StringName` key 建索引，不要再按 string key 或 resource 内 id 补索引。
- 适合：成长规则、属性公式、建卡候选、职业/技能规则。
- 邻接单元：CU-01、CU-09、CU-11、CU-12、CU-13、CU-15、CU-19。

### CU-15 战斗运行时总编排

- 文件：
  - `scripts/systems/battle/runtime/*.cs`
  - `scripts/systems/battle/fate/*.gd`
  - `scripts/systems/battle/fate/*.cs`
  - `scripts/systems/battle/core/BattleCommand.cs`
  - `scripts/systems/battle/core/BattlePreview.cs`
  - `scripts/systems/battle/core/AutoCastRequest.cs`
  - `scripts/systems/battle/core/Contingency*.cs`
  - `scripts/systems/battle/core/BattleEventBatch.cs`
  - `scripts/systems/battle/rules/Battle*.cs`
  - `scripts/systems/battle/runtime/BattleContingencySystem.cs`
  - `scripts/systems/battle/runtime/ContingencyTargetResolverService.cs`
  - `scripts/systems/game_runtime/BattleSessionFacade.cs`
  - `scripts/systems/battle/sim/*.gd`
  - `scripts/systems/battle/sim/*.cs`
- 负责：开战、时间轴、命令 preview/issue、技能执行、战斗结算、战斗内运行时编排；`BattleSimProfileDef.ai_score_profile` 现在直接持有 typed `BattleAiScoreProfile`，formal sim / override applier / benchmark C# runner 不要再把 score profile 当 `GodotObject` 传递；`BattleSimOverrideApplier` 给 C# caller 的正式结果面现在优先走 typed `BattleSimOverrideApplyResult`，`BattleSimRunner` / benchmark C# runner 不要再回读 overrides `GDictionary` 取 skill/brain/profile；`BattleSimRunner` / `BattleSimReportBuilder` 这条 battle-sim report 聚合边界现在也优先走 typed `BattleSimScenarioReport` / `BattleSimProfileReportEntry` / `BattleSimProfileSummary` / `BattleSimProfileComparison` / `BattleSimRunReport` / `BattleSimOutputFiles`，不要把 direct C# caller、runner owner state、或 trace-summary runner path 退回顶层 report `GDictionary` 往返；`BattleSimOverrideApplier` / `BattleSimContentProvider` / `BattleSimRunner` / `BattleSimReportBuilder` / `BattleSimTraceSummaryBuilder` 这组 battle-sim helper 现在也应保持 plain C# typed/helper surface，不要恢复 `RefCounted` / `GlobalClass`、runner 的 Godot `Array` profile 输入，或 report/trace builder 的 snake_case GDScript helper 边界。
- 补充约束：玩家已选技能的 HUD/hover preview 命令组装属于 `GameRuntimeBattleSelection` / `BattleSessionFacade` / `GameRuntimeFacade` 这条 runtime selection 边界，并继续交给 `BattleRuntimeModule.PreviewCommand(...)` 生成 `BattlePreview`；展示层不应自行重算命中、伤害、射程或目标合法性。
- 补充约束：战斗 preview 读侧应通过 `BattleStateReadView` / `BattleUnitReadView` 这类只读视图进入正式 `BattleState` / `BattleUnitState` 源；preview 链不得 clone 单位来规避写权限，也不得接收可变 `BattleUnitState` 作为只读预览参数。正式 battle state 一次战斗内只维护一个 `BattleUnitState` 源，preview 只能从该源读取并在最外层 payload/HUD/AI 边界投影。
- 迁移约束：`BattleSimFormalCombatFixture` 这条 formal sim owner 现在在 `setup_content()` 边界优先走 `ProgressionContentRegistry` / `ItemContentRegistry` typed catalog，内部 skill/profession/achievement/item lookup 与 `ProgressionService.setup(...)` 都直接用 typed index；`build_roster()` 的 roster options 也优先走 `BattleSimFormalRosterOptionsData`，成员 AI metadata 改用 typed `Dictionary<StringName, StringName>`。`GameRuntimeFacade -> BattleRuntimeModule.setup(...)` 的正式 runtime 接线只走 typed skill/enemy-template/enemy-brain/item catalog；`BattleRuntimeModule.SubmitPromotionChoice(...)` 与 `IBattleRuntimeCharacterGateway.PromoteProfession(...)` 的 selection 输入也必须保持 `PromotionSelectionData`，不要把战斗内晋升提交退回 `GDictionary selection`；`BattleRuntimeModule` 不再保留 `GDictionary` setup 边界或 `_skill_defs` 投影字段，不要把 string-key-only skill/enemy-template/enemy-brain/item 恢复进正式 typed index；runtime 内部 item catalog 现在也应继续以 typed `_itemDefIndex` 为正式业务态，`BuildItemDefIndexSnapshotTyped()` 只从 typed index 拷贝快照，不要再回扫 `_item_defs` public projection；`BattleDamageResolver` 的 skill catalog 也应继续停留在 typed `Dictionary<StringName, SkillDef>`，`BattleRuntimeModule.BindDamageResolver()` 直接绑定 typed skill index 与 typed hit resolver，不要再靠 `HasMethod("set_skill_defs"/"set_hit_resolver")` 或 runtime skill-def Godot 投影回绑；`BattleMovementService` 现在也应保持 plain C# helper，正式移动解析/执行链优先走 PascalCase `Setup/Dispose/GetUnitReachableMoveCoords/GetMovePathCost/ResolveMovePathResultTyped/MoveUnitAlongValidatedPathTyped` 与 typed `IReadOnlyList<Vector2I>` path/coord 集合，不要恢复 `RefCounted` / `GlobalClass`、service 内部 `GArray/GDictionary` request-result 入口，或旧 snake_case helper surface；`BattleGroundEffectService` 现在也应保持 plain C# helper，至少生命周期边界继续走 PascalCase `Setup/Dispose`，ground/unit spell-control 正式链优先走 typed `ResolveGroundSpellControlAfterCostResult/ResolveUnitSpellControlAfterCostResult`，shield 正式链优先走 typed `ApplyUnitShieldEffectsResult`，ground forced-move context 正式链优先走 typed `BuildGroundForcedMoveContextResult`，ground effect-coord 构建正式链优先走 typed `BuildGroundEffectCoords`，ground unit-effect resolve 正式链优先走 typed `ResolveGroundUnitEffectResult` 与 `AttackEffectResolutionResult`，`BattleRuntimeModule` 外层再投影 Godot payload，不要在 helper 内保留 `_resolve_ground_unit_effect_result` 这类 `Dictionary` wrapper；`BattleSkillExecutionOrchestrator` 的 unit-skill resolve 正式链现在也应继续优先走 typed `ResolveUnitSkillEffectResult`、typed `AttackEffectResolutionResult` 和 typed custom log lines/dispel events，再由 `BattleRuntimeModule._resolve_unit_skill_effect_result(...)` 做最外层 payload 投影，不要在 orchestrator 内恢复 `_resolve_unit_skill_effect_result`、`_apply_chain_damage_effects` 这类 public payload wrapper 或 `UnitSkillEffectResolution.Payload` 业务态；`AttackEffectResolutionResultReader` 在 legacy payload -> typed result 边界必须保留 payload 显式给出的 `crit_locked` 语义，不要再只信外部 `AttackCheckInput` 默认值；ground effect-def / preview-unit-id 正式链优先走 typed `CollectGroundUnitEffectDefs/CollectGroundTerrainEffectDefs/CollectGroundEffectDefs/CollectGroundPreviewUnitIds`，ground effect-def dedupe 正式链优先走 typed `DedupeEffectDefsByInstance`，ground wind-push / unit / terrain application 正式链优先走 typed `BattleGroundWindPushResult/BattleGroundUnitEffectsResult/BattleGroundTerrainEffectsResult`，ground skill validation 正式链优先走 typed `BattleGroundSkillValidationResult`，并只在 `BattleRuntimeModule` 外层 public wrapper 投影成 `GDictionary/GCombatEffectArray/GStringNameArray`，wind-push 正式链里的 target-unit collect / step recursion / sort 也应继续停留在内部 typed `List<BattleUnitState>` / `HashSet<StringName>`，不要在 helper 内恢复 `_resolve_*_spell_control_after_cost`、`_apply_unit_shield_effects`、`_build_ground_forced_move_context`、`_build_ground_effect_coords`、`_resolve_ground_unit_effect_result`、`_collect_ground_*`、`_apply_ground_*`、`_validate_ground_skill_command`、`_dedupe_effect_defs_by_instance`、`_collect_wind_push_target_units`、`_try_wind_push_unit_one_step`、`_sort_wind_push_units_near_to_far` 这类 `Dictionary/Array` wrapper；`BattleSpecialSkillResolver` 现在也应保持 plain C# helper，正式特殊技能链优先走 PascalCase `Setup/Dispose/ApplyUnitSkillSpecialEffectsResult/ApplyDoomShiftEffectResult/ApplyBlackStarBrandEffectResult/ApplyForcedMoveEffect/SetRuntimeStatusEffect/RecordVajraBodyMasteryFromIncomingDamage/PickForcedMoveCoord/ScoreForcedMoveCoord/HandleAdjacentAllyDefeat/HandleLowLuckRelicAllyDefeat/AreUnitsAdjacent`，不要恢复 `RefCounted` / `GlobalClass`、resolver 内部的 snake_case helper surface，或把 changed-coords / adjacent-allies / hostile-unit 集合重新退回 resolver 内部 `GArray` 业务态；`BattleChangeEquipmentResolver` 现在也应保持 plain C# helper，battle-local 换装正式链优先走 PascalCase `Setup/Dispose/PreviewCommand/HandleCommand/GetUnitHpMax/GetUnitStaminaMax` 与 typed `List<StringName>` occupied-slot 业务态，不要恢复 `RefCounted` / `GlobalClass` 或 snake_case helper 入口；`TraitTriggerHooks` 现在也应保持 plain C# helper，battle-start / turn-start 正式链优先走 typed `OnBattleStartResult()` / `OnTurnStartResult()`，不要再把 `battle_state` 包进 `GDictionary` context 回传给 runtime helper；`BattleCommonSkillOutcome` 与 `BattleSkillOutcomeCommitter` 也应继续保持 plain C# DTO/helper，special-profile commit 正式链优先走 PascalCase typed mutator / commit API，不要把 common outcome 或 committer 重新注册成 `RefCounted` / `GlobalClass`，也不要恢复 snake_case mutator / commit 入口；`BattleRuntimeLootResolver` 现在是 plain C# helper，并通过 typed enemy template index 取模板，不要再把 defeated-unit loot 正式链退回 `_enemy_templates` string fallback。不要把 formal fixture 的 progression 身份内容、roster options、或成员 AI metadata 退回 `GDictionary` / `Variant` 业务态。Battle runtime 这条 strict setup / plain-helper 约束当前由 `tests/world_map/schema/run_world_map_low_level_defensive_regression.cs` 覆盖；direct move-path DTO 与 movement-helper boundary 当前由 `tests/battle_runtime/runtime/run_battle_move_path_result_projection_regression.cs` 覆盖；special-skill helper boundary 当前由 `tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs` 覆盖；magic-backlash / spell-control typed boundary 当前由 `tests/battle_runtime/skills/run_magic_backlash_regression.cs` 覆盖；shield helper boundary 当前由 `tests/battle_runtime/runtime/run_battle_shield_service_typed_context_regression.cs` 覆盖；ground validation result / runtime wrapper 投影当前由 `tests/battle_runtime/runtime/run_battle_validation_result_projection_regression.cs` 覆盖；direct special-profile commit 边界当前由 `tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.cs` 覆盖；直接受影响的 formal fixture regression 已迁到 `tests/battle_runtime/simulation/run_battlesim_formal_fixture_regression.cs`，不要恢复旧的 `.gd` runner。
- Battle sim typed-boundary 约束：`BattleSimScenarioDef` 应先把 ally/enemy entries 解析成 `BattleSimScenarioUnitEntry` 再投影 start context/spawn coords，畸形条目必须在 spawn 抽取前失败；`BattleSimRunReport` 的 metrics 正式 owner 是 `BattleSimMetricsSnapshot` / `BattleSimUnitMetricsSnapshot`，`BattleSimReportBuilder` 与 `BattleSimTraceSummaryBuilder` 直接消费 snapshot，只有 `BattleSimReportProjection` 负责向报告 payload 输出字典；`BattleSimUnitSpec.skill_level_map` 的正式 key 是 `StringName`，不要恢复 string-key fallback。
- 补充约束：`BattleTerrainEffectSystem.ApplyTimedTerrainEffectTick(...)` 现在应直接消费 typed `AttackEffectResolutionResult`；`BattleRuntimeModule` / `BattleSkillExecutionOrchestrator` 不要恢复 `summarize_damage_result`、`build_damage_absorb_reason_text` 或 `_apply_equipment_durability_result(..., GDictionary, ...)` 这类仅为 payload 往返服务的 wrapper。
- 补充约束：`BattleContingencySystem` 属于 battle runtime orchestration，负责从 persistent setup 初始化战斗内状态、收集 hook fact、经 `ContingencyTargetResolverService` 解析目标并排队/执行 `AutoCastRequest`；battle finalization 只在结算提交成功后通过 character gateway 写回 consumed setup，失败时必须随 finalization rollback 回到战前 party/setup 状态。
- 补充约束：`BattleDamageResolver` 的正式伤害提交链现在通过 typed `DamageApplicationInput` / `DamageApplicationProjection` 计算 shield/HP 写回，并可绑定 battle-local `IBattleDamageApplicationHook`；`BattleRuntimeModule` 只把当前战斗的 `BattleContingencySystem` 作为 `BeforeDamageResolved` hook 注入，hook/projection/release context 都是 battle-local 运行时状态，不进入 save/payload 兼容层。
- 生命周期约束：`BattleTerrainEffectSystem`、`BattleRatingSystem`、`BattleUnitFactory`、`BattleChargeResolver`、`BattleRepeatAttackResolver`、`BattleSkillMasteryService`、`BattleRuntimeSkillTurnResolver`、`BattleShieldService`、`BattleSkillExecutionOrchestrator`、`FateRuntimeModule`、`MisfortuneService` 与 dev/profiling helper `AiTraceRecorder` 是 plain C# runtime helper，`BattleRuntimeModule` 只需通过普通 `DisposeRuntime()` / `IDisposable` 清 runtime weak ref、event subscription 与缓存，不要恢复成 `RefCounted` 或纳入 GodotObject owned-dispose 路径；`BattleFateEventBus` 仍声明 Godot Signal，可继续保留 `RefCounted`。
- 补充约束：`BattleSkillExecutionOrchestrator` / `BattleGroundEffectService` 也不要恢复 `_append_result_report_entry`、`_append_report_entry_to_batch` 这类仅做 runtime passthrough 的 report-entry payload wrapper；正式链继续直接走 typed `AttackEffectResolutionResult` 或直接调用 runtime outer surface。
- 补充约束：同样不要在 `BattleSkillExecutionOrchestrator` / `BattleGroundEffectService` 恢复 `append_result_source_status_effects(..., GDictionary)` 或 `_record_vajra_body_mastery_from_incoming_damage(..., GDictionary, ...)` 这类仅做 runtime passthrough 的 payload wrapper；正式链继续直接走 typed `AttackEffectResolutionResult`。
- 补充约束：同样不要在 `BattleSkillExecutionOrchestrator` / `BattleGroundEffectService` 恢复 `append_damage_result_log_lines(..., GDictionary)` 这类仅做 runtime passthrough 的 log wrapper；helper 内正式链继续直接走 typed `AttackEffectResolutionResult`。
- 补充约束：战斗结算结果 `BattleResolutionResult` 只承载战斗身份、胜负与 typed `BattleLootEntry` loot/overflow；pending character rewards 由 `BattleRuntimeModule` 的 typed 列表暂存，厄运指引应直接从结算结果的 calamity-conversion loot entry 判断碎片产出，不要恢复结算结果上的进度、世界变更、资源提交字段或三字段中转 DTO。
- 补充约束：`BattleGroundEffectService` 内部的 coord/effect/topology helper 应继续停留在 typed `IReadOnlyList<Vector2I>` / `IReadOnlyList<CombatEffectDef>` / `List<BattleUnitState>` 业务态，不要恢复 `_collect_units_in_coords`、`_append_changed_coords`、`_sort_coords`、`_collect_wind_push_effects`、`_should_resolve_ground_effects_as_attack`、`_reconcile_water_topology` 这类 `GArray` wrapper。
- 补充约束：`BattleGroundEffectService` 的 ground wind-push / unit-effect / terrain-effect / ground unit effect resolve / forced-move context / special-effect validation 这些 helper 入口也应继续直接接收 typed `IReadOnlyList<CombatEffectDef>` / `IReadOnlyList<Vector2I>`，不要在 helper owner 内恢复 `GArray` result 入参再回投 typed。
- 补充约束：`BattleMovementQueryService` 的正式查询结果应使用 `MovementReachabilityResult` / `MovementDistanceBandResult` / `MovementPathTargetResult`，AI move-to-range 候选也直接消费 `MovementPathTargetCandidate`；`ok/coords/path/cost` 和 distance-band path-target arrays 只允许通过 `MovementQueryResults.ToDictionary()` 这类显式投影进入 HUD/test/外层 payload，不要在 query service 或 AI candidate evaluator 内恢复 `Dictionary` 结果契约。
- 补充约束：ground relocation / jump relocation / precast-special-effect 这条正式链也应继续停留在 helper owner 内部 typed `IReadOnlyList<Vector2I>`，不要恢复 `_apply_ground_precast_special_effects`、`_apply_ground_relocation`、`_apply_ground_relocation_with_mode`、`_apply_ground_jump_relocation` 这类 `GArray` helper surface。
- 补充约束：`BattleSkillExecutionOrchestrator` 内部 ground-skill preview / execute 正式链也应继续直接走 typed effect/coord/unit-id 集合，不要恢复 `_build_ground_effect_coords`、`_collect_ground_unit_effect_defs`、`_collect_ground_terrain_effect_defs`、`_collect_ground_preview_unit_ids`、`_apply_ground_unit_effects`、`_apply_ground_unit_effects_result`、`_apply_ground_terrain_effects`、`_apply_ground_terrain_effects_result`、`_get_ground_special_effect_validation_message` 这类 public Godot wrapper surface；同样 `BattleRuntimeModule` 也不应继续保留 `_build_ground_effect_coords`、`_collect_ground_unit_effect_defs`、`_collect_ground_terrain_effect_defs`、`_collect_ground_effect_defs`、`_collect_ground_preview_unit_ids` 这组仅为内部链服务的 collect/build Godot wrapper。
- 补充约束：`BattleSkillExecutionOrchestrator` 也不应继续保留 `_apply_ground_precast_special_effects`、`_validate_ground_skill_command`、`_validate_ground_skill_command_result`、`_resolve_ground_cast_variant` 这组仅为内部 ground preview / validation / meteor 结算链服务的 Godot wrapper；`BattleRuntimeModule` 同样不应继续保留 `_apply_ground_precast_special_effects`、`_resolve_ground_cast_variant` 这类仅为内部链服务的 Godot wrapper。
- 补充约束：ground skill validation / meteor 这条内部正式链应继续直接走 `BattleRuntimeModule.ValidateGroundSkillCommandResultTyped(...)` 之类 typed internal surface，不要让 orchestrator / meteor resolver 再回调 runtime public `_validate_ground_skill_command_result(...)` adapter。
- 补充约束：`BattleMeteorSwarmResolver` 现在也应保持 plain C# helper，正式 meteor preview / commit 链优先走 `Setup/Dispose`、internal `PopulatePreview/BuildCastContextTyped/BuildPreviewFacts/BuildTargetPlanTyped/ResolveTyped`，不要恢复 `RefCounted` / `GlobalClass`，或 `populate_preview`、`build_cast_context`、`build_preview_facts`、`build_target_plan`、`resolve`、`_build_hostile_terrain_consequence`、`_build_component_damage_preview`、`_build_terrain_summary`、`_collect_component_save_profile_ids`、`_apply_save_profile_to_damage_effect`、`_populate_unit_distances`、`_build_plan_signature_for_anchor`、`_build_plan_signature`、`_extract_target_coords`、`_resolve_profile`、`_unit_covers_coord`、`_get_unit_max_hp`、`_terrain_profile_display_name` 这类仅供内部链或旧测试 surface 使用的 snake_case wrapper。
- 补充约束：`BattleMagicBacklashResolver` 现在也应保持 plain C# helper，正式 spell-control / ground-drift 链优先走 PascalCase `ShouldResolveSpellControl/ApplySpellControlAfterCostResult/BuildGroundBacklashTargetCoordsResult/AppendGroundBacklashLog` 与 typed `BattleSpellControlMetadata/BattleSpellControlResult/BattleGroundBacklashTargetResult`，不要恢复 `RefCounted` / `GlobalClass`、snake_case public helper，或 `GDictionary` wrapper 往返。
- 补充约束：读条 / pending cast 属于 `BattleRuntimeModule` 的 runtime sidecar 职责，正式链应继续通过 typed `BattleCastingTimeService`、`BattleTimelineDriver`、`BattleRuntimeSkillTurnResolver`、`BattleSkillExecutionOrchestrator` 和 core `BattlePendingCastState`/`SkillCostTransaction` 协作；manual cancel 的玩家/自动化入口应继续走 `BattleSessionFacade.CommandBattleCancelCastTyped(...)` / `GameRuntimeFacade.CommandBattleCancelCastTyped(...)` / `GameTextCommandRunner` 的 typed command path，不要绕回 `GodotObject.Call(...)` 或手写 payload；pending cast 与 casting-exhausted flag 是 runtime-only battle state，不能进入 save payload，但 clone / AI mutation guard 稳定快照必须保留，headless snapshot/text 只能在 `GameRuntimeSnapshotBuilder` / `GameTextSnapshotRenderer` 派生只读摘要，避免 AI 预演、缓存恢复或自动化检查漏掉读条状态。
- 补充约束：通用 trait 进入战斗的正式桥接是 `IBattleRuntimeCharacterGateway.BuildEffectiveTraitProjectionForEquipmentView(...)` -> `BattleUnitFactory` -> `BattleUnitState.effective_trait_instances` / `effective_trait_ids`。`BattleUnitFactory` 创建玩家单位、`RefreshBattleUnit`、`RefreshEquipmentProjection` 时都必须以 battle-local `EquipmentState` 作为输入重算 typed effective trait state；`effective_trait_instances` 的 live owner 是 `List<BattleEffectiveTraitInstanceState>`，不应恢复成 `Godot.Collections.Array<BattleEffectiveTraitInstanceState>`。战斗规则和 trigger helper 不应反查 `CharacterTraitService`、trait catalog、item catalog 或角色装备源状态。`PassiveStatusOrchestrator` 只保留身份静态投影与 racial skill charge 投影，不再把 identity `trait_ids` 写入 battle unit；不要恢复 `race_trait_ids/subrace_trait_ids/bloodline_trait_ids/ascension_trait_ids` 平行字段。
- 生命周期约束：`BattleRuntimeModule`、`BattleGridService`、`BattleTerrainGenerator`、`BattleDamageResolver`、`BattleHitResolver`、`BattleFateEventBus` 与 `BattleSimFormalCombatFixture` 都是 plain C# runtime helpers/owners，不参与 Godot native wrapper 生命周期；module owns 自己创建的默认 `BattleTerrainGenerator`，`setup(...)` 注入或测试直接替换的自定义 generator 仍由 caller 释放。module 在替换 generator 或 dispose 时必须释放自己创建且仍持有的旧默认实例，但不得释放 caller-owned generator，测试和 runtime 只能用 managed `IDisposable` / `BattleTerrainGenerator.IsDisposed` 验证所有权，不要恢复 `GodotObject.IsInstanceValid` / `RefCounted` 作为释放判据；fate event bus 使用 C# event 订阅/退订，不要恢复 Godot `[Signal]` / `EmitSignal`。
- 补充约束：temporal 状态族（`time_stasis` / `time_slow` / `time_reverberation`）的规则归属是 `BattleTemporalStatusService`（`scripts/systems/battle/rules/`，plain C# static helper，不持有 runtime callback、不维护格锁）；冻结/减速语义由 `BattleTimelineDriver`（step 开始静滞快照 + ready/进度/恢复跳过，到期当 step 仍按冻结处理）、`BattleCastingTimeService`（读条进度 rate 与静滞冻结）、`BattleRuntimeSkillTurnResolver`（stasis 冷却 anchor 冻结与移动阻断）、`BattleGridService` / `BattleSpecialSkillResolver`（位移与 forced movement 对静滞单位 fail closed）协作执行；temporal 业务态保持 typed `BattleStatusEffectState.status_tags` / `save_bonus_by_tag` 与 `CombatEffectDef.effect_tags`，资源层 `params` 导入只能在 `SkillContentRegistry` / status construction 边界投成 typed 字段，runtime owner 不回读 `params`；per-tag save bonus 只在 `BattleSaveResolver` 内部私有 `GetStatusSaveBonus(...)` 与 `save_bonus` / `control_save_bonus` 用同一套 `Math.Max` 合成；elite/boss 静滞降级统一走 `BattleExecutionRules.IsEliteOrBossTarget(...)`；不要恢复 `time_stasis_cell_locks` 平行格锁或 owner 撒网 hook。
- 补充约束：`BattleSpecialProfilePreviewFacts` / `MeteorSwarmPreviewFacts` 这组 special-profile preview facts 也应保持 plain C# DTO，preview 链里的 target/summary/breakdown/component 集合优先停留在 CLR `List<>` 业务态；其中 `terrain_summary` 应保持 `MeteorSwarmTerrainSummaryFact`、`friendly_fire_numeric_summary` 应保持 `List<MeteorSwarmNumericSummary>`，并且 `MeteorSwarmNumericSummary` 内部的 `ComponentBreakdown/SaveProfileIds/ResistanceTiersByDamageTag/StatusEffectIds` 也应保持 typed `List<MeteorSwarmComponentBreakdownEntry>/List<string>/Dictionary<StringName, StringName>/List<StringName>`，`MeteorSwarmComponentBreakdownEntry` 里的 `SaveEstimate/WorstSaveEstimate` 也应保持 `BattleDamagePreviewSaveEstimate`，mitigation source 也应继续保持 typed `HalfSourceLabels/DoubleSourceLabels/ImmuneSourceLabels/FixedMitigationSourceLabels` `List<string>`，不要恢复 `MitigationSources/FixedMitigationSources` 这类 `GArray` 业务态；meteor 友伤 preview 计算应继续直接走 `BattleDamageResolver.preview_damage_effect_typed(...)` / `BattleDamagePreviewResult`，不要恢复 `preview_damage_effect(...)` payload 往返；`attack_roll_modifier_breakdown` 应保持 `List<BattleAttackRollModifierSpec>`、`component_preview` 应保持 `List<MeteorSwarmComponentFact>`，只在 `ToDict()` 或 AI/HUD payload adapter 边界投影成 Godot array/dictionary，不要把 `BattleSpecialProfilePreviewFacts` 重新变成 payload/helper 工具宿主，也不要恢复 `ToDictionaryArray/ToDictionaryList/ToModifierSpecList` 这类与 DTO 本体无关的 static helper，或 `RefCounted` / `GlobalClass`、`to_dict()` / `get_friendly_fire_numeric_summary()` 这类 snake_case Godot API。
- 补充约束：`BattleAiScoreInput` 里的 AI trace 集合也应继续保持 typed 业务态，`runtime_action_metadata` 应直接持有 `BattleAiScoreRuntimeMetadata`，`special_profile_preview_facts` 应直接持有 `BattleSpecialProfilePreviewFacts`，`target_unit_ids` / `random_chain_candidate_unit_ids` / `estimated_*_target_ids` / `pre_action_threat_unit_ids` / `post_action_remaining_threat_target_ids` 应保持 `List<StringName>`，`target_coords` 应保持 `List<Vector2I>`，`target_numeric_summary` / `friendly_fire_numeric_summary` 应保持 `List<MeteorSwarmNumericSummary>`，`attack_roll_modifier_breakdown` 应保持 `List<BattleAttackRollModifierSpec>`，并且 `BattleSpecialProfilePreviewFacts.attack_roll_modifier_breakdown` 也应保持同一 typed spec 列表，只在 `ToDict()` / HUD / 最外层 payload 边界投影成 Godot payload；`high_priority_target_ids` 应保持 `List<StringName>`，`high_priority_reasons` 应保持 `Dictionary<StringName, List<string>>`，`path_step_hit_counts_by_unit_id` 应保持 `Dictionary<StringName, int>`，`save_estimates_by_target_id` 应保持 `Dictionary<StringName, List<BattleAiScoreService.DamageSaveEstimate>>`，`damage_estimates_by_target_id` 应保持 `Dictionary<StringName, List<BattleAiScoreService.DamageEstimateBreakdown>>`；同样 `Seal()/MatchesSealedFingerprint()` 这条 owner 内部完整性校验也应继续直接基于 typed 字段做 fingerprint，不要再回读 `ToDictionary()` payload 做 sealed snapshot，`BattleAiPayloadGuard.ScoreInputHasNoLiveState()` 对 special-profile preview facts / numeric summary / modifier breakdown 以及 `save_estimates_by_target_id` / `damage_estimates_by_target_id` / `high_priority_reasons` / `path_step_hit_counts_by_unit_id` 这组 typed dictionary 也应直接校验 typed DTO/集合，不要为了 no-live-state guard 再把这些 typed 业务态回投成 `GDictionary/GArray`；同样 `BattleAiPayloadGuard.CommandIsValueObject()` / `PreviewHasNoLiveState()` 也应继续直接覆盖 `BattleCommand.target_unit_ids/target_coords/equipment_occupied_slot_ids/equipment_instance`、`BattlePreview.log_lines/target_unit_ids/target_coords/random_chain_candidate_unit_ids/hit_preview/damage_preview`、`BattleEventBatch.changed_unit_ids/changed_coords/log_lines/report_entries` 和 `AttackPreviewData` 自己的 typed stage/source/modifier 状态，不要保留 `Array<StringName>` / `Array<Vector2I>` / `AttackPreviewData` 的 no-op guard；其中 `BattleCommand.target_unit_ids`、`BattleCommand.target_coords`、`BattleCommand.equipment_occupied_slot_ids` 以及 typed `BattleCommand.equipment_instance`、`BattlePreview.log_lines`、`BattlePreview.target_unit_ids`、`BattlePreview.target_coords`、`BattlePreview.random_chain_candidate_unit_ids`、typed `BattlePreview.damage_preview`、`BattleEventBatch.changed_unit_ids/changed_coords/log_lines/report_entries/progression_deltas`、以及 `UnitProgress` 自己的 `known_knowledge_ids/active_core_skill_ids/attribute_growth_progress/achievement_progress/pending_profession_choices/blocked_relearn_skill_ids/merged_skill_source_map/unlocked_combat_resource_ids/locked_level_trigger_skill_ids`、`CharacterProgressionDelta` 自己的 `changed_profession_ids/pending_profession_choices/mastery_changes/knowledge_changes/attribute_changes/unlocked_achievement_ids`、`PendingProfessionChoice` 自己的 `trigger_skill_ids/candidate_profession_ids/target_rank_map/qualifier_skill_pool_ids/assignable_skill_candidate_ids` 这类 runtime owner 内部字段也应继续停留在 internal typed backing state；其中 `mastery_changes` / `knowledge_changes` / `attribute_changes` 正式链现在应分别保持 `IReadOnlyList<CharacterMasteryChangeFact>` / `IReadOnlyList<CharacterKnowledgeChangeFact>` / `IReadOnlyList<CharacterAttributeChangeFact>`，`PendingProfessionChoice` 这五组字段则应继续保持 `IReadOnlyList<StringName>` / `IReadOnlyDictionary<StringName, int>` typed backing，`UnitProgress.pending_profession_choices` 应继续保持 `IReadOnlyList<PendingProfessionChoice>` typed backing，`attribute_growth_progress` 应继续保持 `IReadOnlyDictionary<StringName, int>` typed backing，`achievement_progress` 应继续保持 `IReadOnlyDictionary<StringName, AchievementProgressState>` typed backing，`merged_skill_source_map` 应继续保持 `IReadOnlyDictionary<StringName, List<StringName>>` typed backing，而 `known_knowledge_ids/active_core_skill_ids/blocked_relearn_skill_ids/unlocked_combat_resource_ids/locked_level_trigger_skill_ids` 这五组字段也应继续保持 `IReadOnlyList<StringName>` typed backing，不要恢复成 `IReadOnlyList<GDictionary>`、`GDictionary`、或在 progression/runtime helper 里回读 payload dictionary。对外只在 public Godot property 边界投影成 `GArray` / `Array<StringName>` / `Array<Vector2I>` / `GDictionary`，不要让 AI / runtime helper / promotion prompt builder 再直接把它们当 `Godot Array` / `GDictionary` 业务态读写；AI trace/export 链也应继续通过 `BattleAiScoreInput.ToTraceDictionary()` 这类 typed trace projection 直接导出 `AiCandidateSummary/AiActionTrace/BattleAiContext.build_turn_trace()` 所需的 C# trace map，其中 command trace / fingerprint 这条 owner 内部链也应继续复用 `AiCommandSummary.FromCommand(...)` 之类 typed command projection，不要在 `BattleAiContext.BuildCommandDictionary()` 或 `BattleAiScoreInput.AppendNamedCommandFingerprint()` 里重新直接 duplicate / fingerprint `BattleCommand.target_unit_ids/target_coords` Godot arrays；其余 `BattleAiScoreRuntimeMetadata` / `BattleSpecialProfilePreviewFacts` / `MeteorSwarmPreviewFacts` / `MeteorSwarmNumericSummary` / `BattleAttackRollModifierSpec` / `BattleAiScoreService.DamageSaveEstimate` / `BattleAiScoreService.DamageEstimateBreakdown` 这些 nested DTO 也应各自提供 typed trace projection，而不是再回到 `ToDictionary()` / `ToDict()` payload、`BuildAttackRollModifierBreakdownPayload(...)`、或 `TraceDictionaryProjection.FromDictionary(value.ToDictionary())` 这类 payload helper 后再被回读。不要把 AI score input 正式链退回 `GDictionary/GArray`。
- 补充约束：`BattlePreview.save_branch_preview` 是豁免分支命中率/结果预览的 typed backing state，与 `damage_preview` 同级由 runtime/AI/HUD 边界投影；`BattleAiPayloadGuard.PreviewHasNoLiveState()` 必须直接覆盖这组 typed 分支预览，不要让 PWK、豁免命中率或 HUD hover 展示链回退成 `GDictionary` 业务态。
- 补充约束：`MeteorSwarmCastContext`、`MeteorSwarmTargetPlan`、`MeteorSwarmCommitResult`、`MeteorSwarmTargetOutcome` 这组 meteor DTO 也应保持 plain C# 数据载体，不要为 runtime 内部 typed 传递再次挂回 `RefCounted` / `GlobalClass`。
- 补充约束：meteor DTO 的正式业务集合也应继续停留在 CLR `List/Dictionary` 业务态；`MeteorSwarmTargetPlan` 的 `affected_coords/ring_by_coord/target_unit_ids/unit_*`、`MeteorSwarmCommitResult` 的 changed/report/log/terrain/report-entry 集合、`MeteorSwarmTargetOutcome` 的 damage/status/report/component 集合只在最外层 `to_dict()` / report payload 投影时再转回 Godot array/dictionary；其中 `terrain_effects` 应保持 `List<MeteorSwarmTerrainEffectFact>`、`damage_events` 应保持 `List<DamageEventResult>`、`attack_roll_modifier_breakdown` 应保持 `List<BattleAttackRollModifierSpec>`、`report_component_breakdown` 应保持 `List<MeteorSwarmComponentFact>`，meteor 战报条目应通过 `MeteorSwarmReportEntry` 进入 `MeteorSwarmCommitResult` 并由 `MeteorSwarmProjection.Project(...)` 在 report/export 边界投影，不要把 owner 内正式链退回 `Godot.Collections.Array` / `GDictionary` 业务态。
- 补充约束：`MeteorSwarmCastContext` 的 `spell_control_context` / `drift_context` 也应继续保持 typed `BattleSpellControlResult` / `BattleGroundBacklashTargetResult`，不要在 orchestrator 或 resolver 内恢复 `ToDictionary()` -> `GDictionary` -> typed 的往返。
- 补充约束：ground special-effect validation message 这条内部正式链也应继续直接走 `BattleGroundEffectService.GetGroundSpecialEffectValidationMessage(...)` / `BattleRuntimeModule.GetGroundSpecialEffectValidationMessageTyped(...)`，不要恢复 service/runtime 的 `_get_ground_special_effect_validation_message(...)` Godot wrapper。
- 生命周期约束：`BattleRuntimeServices` 集中拥有和释放 movement / ground / special-skill / AI decision helper，包括 `BattleMovementService`、`BattleGroundEffectService`、`BattleSpecialSkillResolver`、plain C# `BattleMovementQueryService`、`BattleAiScoreContextAdapter`、`BattleAiQueryService`、`BattleAiCandidateEvaluationService` 与 reusable `BattleAiContext`；`BattleRuntimeModule` 只通过 services owner setup runtime sidecar、准备/绑定 AI context，不要重新保留平行字段或在 module dispose 中逐个清理这些 helper cache，也不要把 `BattleMovementQueryService` 恢复成 `RefCounted`。
- 适合：战斗流程、战斗结算、特殊技能流程、战斗内事务。
- 邻接单元：CU-02、CU-10、CU-11、CU-12、CU-13、CU-14、CU-16、CU-17、CU-18、CU-20、CU-21。

### CU-16 战斗状态模型、边规则、伤害、AI 规则层

- 文件：
  - `docs/design/battle_weapon_dice_and_equipment.md`
  - `scripts/systems/battle/core/*.cs`
  - `scripts/systems/battle/terrain/Battle*.cs`
  - `scripts/systems/battle/rules/*.cs`
  - `scripts/systems/battle/rules/DamageApplicationProjection.cs`
  - `scripts/systems/battle/rules/IBattleDamageApplicationHook.cs`
  - `scripts/systems/battle/ai/*.cs`
  - `scripts/enemies/actions/*.cs`
  - `scripts/player/progression/combat_effect_def.gd`
  - `scripts/player/warehouse/Weapon*.cs`
- 负责：BattleState、地形/边规则、伤害、命中、状态语义、AI 评分与决策规则；enemy brain 可携带正式 score profile 配置供 AI decision scope 读取，simulation/faction override 仍属于 battle-sim 调参边界；combat resource id、damage tag / mitigation tier、target-team filter、battle save tag/ability、weapon/item projection、fate attack formula、death-resolution priority/payload key、status modifier default multiplier 相关固定值都应继续通过 enum/typed utility 或内部规则常量解析，不要在 battle/core 或 rules 层恢复独立 public StringName HashSet / GD helper；`BattleGridDistanceService`、`BattleTerrainRules`、`BattleVirtualBoardOverlay`、`BattleEdgeService`、`BattleRatingSystem`、`BattleChargeResolver`、`BattleRepeatAttackResolver`、`BattleSkillMasteryService` 与 special-profile manifest validator 是 plain C# helper，不参与 Godot object 生命周期；`BattleAiBlackboard` 是 battle unit 的 plain C# runtime-only sidecar，只通过 typed field/snapshot/payload projection 使用，不参与 save payload 或 GodotObject disposal；`BattleTimelineState` 与 `BattleEdgeFeatureState` 也是 plain C# battle-state DTO，只在 owning `BattleState` / `BattleCellState` 边界以 typed field 保存并通过 `ToDictionary()` / `FromDictionary()` 投影，不要恢复 `RefCounted`、lifecycle partial constructor 或 edge feature 的手工 dispose；`BattleAiScoreProfile.action_base_scores` / `bucket_priorities` owner 现在是 internal typed `IReadOnlyDictionary<StringName, int>`，导入和投影都使用正式 `StringName` key，AI score service 不要再从公开 Godot dictionary projection 回读 profile 配置，也不要从 string-key-only score profile 资源恢复；`BattleAiContext` 不再保留 public `skill_defs` / `action_traces` / `mutation_guard_violations` Godot 投影，AI skill index、action trace 与 mutation guard violation 都应通过 internal typed 入口维护。
- 补充约束：`BattleUnitState.weapon_one_handed_dice` / `weapon_two_handed_dice` 的正式运行时字段应保持 typed `WeaponDice`，`WeaponProjection` 也应持有 typed `WeaponDice`；`Godot.Collections.Dictionary` 只允许出现在资源导入、save payload、`ToDictionary()` / `FromDictionary()` 等边界。不要恢复 `ApplyWeaponProjection(GDictionary)`、`CurrentWeaponDiceDictionary()`、`public GDictionary weapon_*_dice` 或在规则/AI/preview 内部回读武器骰子 payload。
- 补充约束：`BattleDamageResolver` 的伤害上下文正式链应使用 `DamageResolutionContext`，固定减伤正式链应使用 `FixedMitigationResult` / typed mitigation sources，装备耐久选择也应停留在 typed selection record；`GDictionary damage_context` 只能作为 public wrapper / 攻击 metadata 入站 / save-payload 适配边界，不要在伤害、预览、处决、装备耐久或减伤内部重新用字典传递 `critical_hit`、`attack_success`、`skill_id`、`equipment_slot_override`、`fixed_mitigation_sources` 等运行时事实。
- 补充约束：`DamageApplicationProjection` / `IBattleDamageApplicationHook` 是伤害写回前的 battle-local hook 边界；hook context 可携带当前伤害事件覆盖格用于 battle-local target resolver 判断 fatal escape 是否离开本次伤害范围；规则层只提供 projection 与 hook 调用点，hook suppression / release suppression 由战斗 runtime 的 contingency owner 管理，不要把 contingency release 状态塞回 damage payload、AI 评分或 save schema。
- 补充约束：`BattleFateEventBus` 的正式事件 surface 应保持 typed `BattleFateEventPayload`，`FateRuntimeModule` / `FortuneService` / `FortunaGuidanceService` / `LowLuckEventService` / `MisfortuneService` 只消费 typed fate payload 或 typed service input；misfortune 的直接触发应使用 `MisfortuneTriggerRequest`，不要恢复 `dispatch(StringName, GDictionary)`、`EventDispatched(StringName, Dictionary)`，或在 fate runtime 内用 `unit_state` / `attacker_member_id` / `status_effect_ids` 等字典 key 传递运行时事件事实。
- 补充约束：`BattleCommand` 是 plain C# runtime command DTO，目标单位、目标格、装备占用槽位等正式状态由 internal typed `List<T>` backing 承载，public Godot Array 属性只作为 UI/test/Godot 边界投影；命令根不参与 GodotObject lifecycle，不要恢复 `RefCounted`、`RuntimeStateLifecycle.MarkFinalizerless(...)` partial constructor 或对命令根调用 GodotObject disposal。`BattlePreview` 也是 plain C# runtime preview DTO，log/target/random-chain 集合由 typed backing list 承载；`AttackPreviewData` 是 plain C# hit-preview DTO，HUD/snapshot/hover 的 hit-preview public payload 只能投影为 `GDictionary`，不要把 live `AttackPreviewData` object 放进 Godot payload；当前 `save_branch_preview` 仍是 Godot Dictionary 边界 payload，必须在 constructor/setter 处通过 `RuntimeStateLifecycle.MarkValueGraphFinalizerless(...)` 注册为 runtime value graph，直到后续迁为 typed save-branch preview DTO。`BattleEventBatch` 是 plain C# runtime result DTO，changed ids/coords/log/progression delta 使用 typed backing；report entry 仍是 Godot Dictionary 边界 payload，入列时必须注册为 runtime value graph，不要恢复 batch 根的 `RefCounted` / GodotObject disposal。
- 补充约束：`BattleUnitState` 的技能等级、锁定命中加值、伤害抗性、冷却与次数状态应由 typed map owner 承载，`BattleState` 的地形列、runtime edge face 和 layered barrier payload cache 也应通过 `BattleState` owner API 读写；layered barrier runtime 真相源是 `BattleBarrierStore` / `BattleBarrierInstanceState`，`BattleBarrierService` 和 `BattleAiMutationGuard` 应直接读写 typed barrier entry / snapshot，规则、AI、地形和屏障服务不要恢复直接持有或写入这些 Godot dictionary 字段，只有 save/schema、terrain generation、HUD/test payload 断言等边界显式投影。
- 生命周期约束：battle runtime/save state 中仍保留的 Godot payload wrapper 只能在 owning 构建/导入边界通过 `RuntimeStateLifecycle.MarkValueGraphFinalizerless(...)` 注册为 runtime state graph；runtime `new` / `Duplicate(true)` / `DuplicateForRuntime(...)` Resource graph 必须进入当前 `GodotTransientResourceScope`，例如 damage/effect resolver、AI score service、skill execution、terrain factory 的临时 Resource 和 collection wrapper。consumer、score、preview、hook 读取点只能 assert/log ownership，不做 suppress、dispose、free 或 unknown wrapper 类型兜底。
- 补充约束：`BattleUnitState` 的状态效果正式源是 `BattleStatusEffectCollection`，读写只走 `GetStatusEffect()` / `GetStatusEffectsTyped()` / `GetSortedStatusEffectIdsTyped()` / `SetStatusEffect()` / `EraseStatusEffect()`；`status_effects` 只能作为 `ToDictionary()` / `FromDictionary()` save/schema payload key 出现，不要恢复 public/live `GDictionary status_effects` 或从投影字典回扫运行时状态。`BattleStatusEffectState` 的 runtime 语义字段必须保持显式 typed 字段，`BattleStatusEffectParams` 只在 content/save params 边界解析并剥离 residual save payload，不要让规则、AI、被动或特殊技能 resolver 直接读取 status `@params` 来决定效果。
- 补充约束：`BattleUnitState.effective_trait_instances` 是战斗内 trait 触发的正式 typed state，live owner 必须是 `List<BattleEffectiveTraitInstanceState>`，`BattleEffectiveTraitInstanceState` 本身是 plain C# DTO，不参与 GodotObject 生命周期，内部 `roll_values` 也必须保持 `List<TraitRollValueState>`；schema 校验/clone/save roundtrip 由 `BattleUnitState` 拥有，只有 `ToDictionary()`/`FromDictionary()` save 边界投影为 dictionary payload；`effective_trait_ids` 只能从 typed state 派生，作为 UI、trace、查询辅助，不作为叠加或触发事实源。`TraitTriggerHooks` 只消费 effective typed state 中的 `effect_type/trigger_type/charge_scope/charge_reset_timing/effective_instance_key`，charge key 必须来自 effective instance key，不再从旧身份 trait 数组 fallback。AI 决策、score、preview 链不得修改 effective trait payload 或派生 ids；`BattleAiMutationGuard` 必须把这两个字段纳入 stable diff 与 restore。
- 适合：战斗规则、伤害链、AI 行为、状态语义、目标过滤、射程规则。
- 邻接单元：CU-13、CU-15、CU-17、CU-18、CU-20。

### CU-17 战斗地形 profile、敌人 roster、prop 注入

- 文件：
  - `scripts/enemies/WildEncounterRoster*.cs`
  - `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`
  - `scripts/systems/world/EncounterRosterBuilder.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/BattleBoardPropCatalog.cs`
  - `data/configs/enemies/rosters/*.tres`
  - `assets/main/battle/terrain/canyon/*.png`
- 负责：战斗地形生成、wild encounter roster 装配、prop 注入；`EncounterRosterBuilder` 从 Godot build context 物化 skill/item/enemy-template/brain/roster index 时只接受正式 `StringName` key，不要再按 string key 或 resource 内部 id 补索引；`BattleBoardPropCatalog` 是 plain C# static catalog，展示/地形层只读其 typed prop 映射，不 owns 或注入 `RefCounted` catalog；`BattleCellState.BuildColumnsFromSurfaceCells(...)` 是程序集内部 terrain/grid/sim/runtime 构建 helper，不要恢复 public static `GDictionary` Godot API，C# 回归需要列数据时在测试本地构造。
- 适合：canyon 地形、spawn/roster、战斗 props、地形 profile。
- 邻接单元：CU-15、CU-16、CU-18、CU-20。

### CU-18 战斗展示主链

- 文件：
  - `scenes/ui/battle_map_panel.tscn`
  - `scripts/ui/BattleMapPanel.cs`
  - `scripts/systems/battle/presentation/BattleHudAdapter.cs`
  - `scenes/ui/battle_board_2d.tscn`
  - `scripts/ui/BattleBoard2D.cs`
  - `scripts/ui/BattleUiTheme.cs`
  - `scripts/ui/BattleBoardRenderProfile.cs`
  - `scripts/ui/BattleBoardController.cs`
  - `scenes/common/battle_board_prop.tscn`
  - `scripts/ui/BattleBoardProp.cs`
- 负责：battle HUD、棋盘绘制、单位/prop 渲染、相机、overlay、hover 展示；`BattleHudAdapter` 是 plain C# HUD projection helper，只消费 runtime 提供的 `BattlePreview` 投影 HUD/hover payload，不拥有技能命中、伤害、射程或目标合法性计算，也不要恢复 `RefCounted` / `GlobalClass`；`BattleUiTheme` 是 plain static C# theme helper，`BattleBoardRenderProfile` 是 plain C# render-profile 配置对象，`BattleBoardController` 是 `BattleBoard2D` 持有的 plain C# board helper，三者都不参与 Godot wrapper 生命周期；`BattleMapPanel` 动态创建的 viewport/board/equipment overlay 节点由场景树释放，panel `_ExitTree()` 只断信号、清 runtime/context 引用、suppress C# wrapper finalizer 并清字段，不手动释放 scene-owned child nodes。
- 适合：battle HUD、棋盘视觉、TileMap、相机、目标浮标。
- 邻接单元：CU-06、CU-15、CU-16、CU-17、CU-19、CU-20。

### CU-19 自动化回归与截图辅助

- 文件：
  - `tests/run_regression_suite.py`
  - `tests/shared/*`
  - `tests/equipment/*`
  - `tests/warehouse/*`
  - `tests/battle_runtime/**/*`
  - `tests/progression/**/*`
  - `tests/runtime/**/*`
  - `tests/text_runtime/**/*`
  - `tests/world_map/**/*`
  - `scripts/dev_tools/*.gd`
  - `tools/*.py`
  - `tools/*.gd`
- 负责：headless 回归、contract 验证、fixture、截图/签名辅助；`TestHarness.Finish(...)` 是测试退出前的集中生命周期闸口，应在返回退出码前执行 `TestResourceOwnership.Drain()` 和 `GodotSharpCleanup.CollectPendingFinalizers()`，保持 idempotent，并确保 `SceneTree.Quit(_test.Finish(...))` 这类调用先 drain 再 quit；普通 test runner / finally 不应直接调用 `GodotSharpCleanup.CollectPendingFinalizers()` 或 `GodotObjectLifecycle.CollectPendingFinalizers()`；normal/test forced GC 只处理 known owners（borrowed/derived static suppress+retain、runtime state known graph、owned transient scope drain/test quarantine），不要在普通测试或 forced GC drain 里新增未知 wrapper 全局 suppress，也不要清空 process-lifetime borrowed/derived static content keep-alive store。
- 适合：补回归、跑局部验证、定位改动影响面。
- 邻接单元：按业务域补 CU-10、CU-12、CU-15、CU-17、CU-18、CU-21。

### CU-20 敌方模板、AI brain、行动定义种子内容

- 文件：
  - `scripts/enemies/*.gd`
  - `scripts/enemies/*.cs`
  - `scripts/enemies/actions/*.cs`
  - `scripts/systems/world/EncounterRosterBuilder.cs`
  - `data/configs/enemies/enemy_content_seed.tres`
  - `data/configs/enemies/brains/*.tres`
  - `data/configs/enemies/templates/*.tres`
  - `data/configs/enemies/rosters/*.tres`
- 负责：敌方模板、AI brain/state/action、generation slot、transition rule、wild encounter roster 内容；`EnemyTemplateDef.base_attribute_overrides` / `attribute_overrides` / `skill_level_map` 的正式 key 是 `StringName`，schema 校验、属性投影和 `GetSkillLevelTyped()` 不再从 string key 恢复；`BuildBrainIndex` / `BuildSkillDefIndex` / `BuildItemDefIndex` / `WildEncounterRosterDef.BuildKnownTemplateIdSet()` 也只从正式 `StringName` key 物化 typed index，不要用 value 自带 id 或 string key 补索引。
- 适合：新敌人、敌方技能表、AI 状态与动作、roster 内容。
- 邻接单元：CU-02、CU-10、CU-15、CU-16、CU-17、CU-18。

### CU-21 Headless runtime、文本命令与快照渲染

- 文件：
  - `scripts/systems/game_runtime/headless/*.cs`
  - `scripts/systems/game_runtime/headless/*.gd`
  - `scripts/utils/GameTextSnapshotRenderer.cs`
  - `tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
  - `tests/text_runtime/commands/run_*.cs`
  - `tests/text_runtime/headless/run_*.cs`
  - `tests/text_runtime/tools/run_*.cs`
  - `tests/text_runtime/README.md`
- 负责：无 UI session、文本命令、expect 断言、文本/结构化快照；`HeadlessGameTestSession` 的 session/runtime 生命周期现在直接持有 typed `GameSession` / plain C# `GameRuntimeFacade`，`initialize/create_new_game/load_game/ensure_world_loaded/settle_frames/build_snapshot/dispose` 不要退回 `GodotObject.Call(...)` 或 `RefCounted` wrapper；owned `GameSession` teardown 必须走 `GameSession.Dispose()`，不要只调用 `DisposeOwnedRuntimeResources()` + `Free()` 绕开 log sink 注销；headless battle bootstrap / loot preview / snapshot augment 也继续直接走 typed `GameRuntimeFacade.get_battle_runtime()/get_battle_state()/get_party_state()/get_world_data()/get_player_coord()` 和 `GameSession.get_skill_defs_typed()/get_item_defs_typed()/get_enemy_templates_typed()/get_enemy_ai_brains_typed()/get_wild_encounter_rosters_typed()/set_battle_save_lock()`，不要再在这些 helper 里保留字符串方法名调用，也不要把 string-key-only enemy roster/template/brain/item 从 public `Dictionary` 恢复进正式 typed catalog；battle snapshot 的 contingency surface 包括 `battle.contingency` sidecar snapshot、unit overlay 的 `contingency_state` / `contingency_suppressed` / `contingency_release_queue_count` / `consumed_contingency_setup_ids` 字段，以及 `battle.report_entries` 中的结构化 contingency report entries；`HeadlessGameTestSession` 继续提供 typed `GetGameSessionTyped()` / `GetRuntimeFacadeTyped()`；`GameTextCommandRunner` 与 `GameTextCommandResult` 都是 plain C# automation helpers/result DTO，只通过 `IDisposable` 清理 session/snapshot/assertion 状态，不要恢复 `RefCounted`；`GameTextCommandRunner` 的 world/submap/party/quest/warehouse/reward/promotion/close/settlement/shop/battle 核心命令优先直接走 typed `GameRuntimeFacade` / `QuestProgressCommandPayloadData` / `PartyItemUseOptions`，`party contingency status/save/edit/charge/clear` 也应继续通过 typed `GameRuntimeFacade` gateway 并把稳定结果投影到 `party.contingency_last_result` / `party.contingency_status_by_member`，不要在这条命令链里继续组 `Dictionary options` 或依赖 `GodotObject.Call("command_world_move" / "command_world_select" / "command_open_settlement" / "command_world_inspect" / "select_world_cell" / "command_confirm_submap_entry" / "command_cancel_submap_entry" / "command_return_from_submap" / "command_open_party" / "command_select_party_member" / "command_set_party_leader" / "command_move_member_to_active" / "command_move_member_to_reserve" / "command_progress_quest" / "command_party_equip_item" / "command_party_unequip_item" / "command_warehouse_use_item" / "command_confirm_pending_reward" / "command_choose_promotion" / "command_close_active_modal" / "command_execute_settlement_action" / "command_shop_buy" / "command_shop_sell" / "command_confirm_battle_start" / "command_battle_tick" / "command_battle_select_skill" / "command_battle_cycle_variant" / "command_battle_move_to" / "command_battle_move_direction" / "command_battle_wait_or_resolve" / "command_battle_cancel_cast" / "command_battle_inspect" / "command_battle_clear_skill", ...)`；这条 headless regression 当前由 `tests/text_runtime/headless/run_headless_game_test_session_regression.cs` 覆盖 battle runtime facade setup 不得恢复 string-key-only enemy/item content 的约束。
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

### 只改角色成长、成就、奖励归并

- 必带：CU-11、CU-12、CU-13、CU-14
- 按需补：CU-09、CU-15、CU-19

### 只改敌方模板、敌方技能表、AI brain

- 必带：CU-20、CU-16
- 按需补：CU-10、CU-15、CU-17、CU-18

### 只改战斗规则、伤害、AI、terrain effect

- 必带：CU-15、CU-16
- 按需补：CU-13、CU-17、CU-18、CU-20

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

## 不推荐的切法

- 不要把这份文档写成实现迁移备忘录。
- 不要在单元描述里记录具体 typed 改造状态、字段级约束或 API 细节。
- 不要在这里维护具体回归脚本名单；测试入口留给对应 `tests/` 目录和 README。
- 不要把单个运行时 helper 当成独立架构层；优先按系统边界读图。
- 不要一次性装载 CU-02、CU-06、CU-12、CU-15、CU-18，除非任务确实跨越存档、世界、角色、战斗和展示整条链。
