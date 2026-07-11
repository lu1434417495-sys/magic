# 当前 Godot 项目的上下文装载单元

更新日期：`2026-07-11`

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

## 全局架构纪律

以下是贯穿全项目的通用纪律，各单元不再逐条重复；字段级 owner、方法名、迁移状态与回归入口一律放源码、对应设计文档与 `tests/`。

- 运行时业务态由 plain C# typed owner（DTO / 服务 / typed 集合）承载；`Godot.Collections.Dictionary` / `Array` 只在 save/schema、UI/window、资源导入、Godot API 这些边界短暂投影，不作为长期真相源。
- runtime helper / service 默认是 plain C# `IDisposable`，不用 `RefCounted` / `GlobalClass` 或 GodotObject validity/dispose 生命周期（少数声明 Godot Signal 的除外）。
- fixed schema 名称（枚举式固定值）优先由 enum/typed 规则拥有，不恢复 public GD helper 或字符串白名单；正式内容 key 是 `StringName`，不从 string key 或 value 内 id 回建索引。
- 静态内容（`.tres`）经各 `*ContentRegistry` 载入并投影为 typed 定义；runtime 只消费 `GameContentCatalog` / session 的 typed 快照，不回读弱类型 content payload。

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
- 负责：启动入口、世界预设入口、存档选择、显示设置、建卡入口。
- 适合：开始菜单、建卡 UI、预设入口、存档列表、显示设置。
- 邻接单元：CU-02、CU-03、CU-14。

### CU-02 GameSession、存档、序列化、全局内容缓存

- 文件：
  - `docs/superpowers/specs/2026-07-10-godotsharp-lifecycle-architecture-design.md`
  - `project.godot`
  - `scripts/systems/lifecycle/*.cs`
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
  - `scripts/utils/GodotObjectLifecycle.cs`
  - `scripts/utils/RuntimeStateLifecycle.cs`
  - `scripts/utils/GodotTypedResourceGraphWalker.cs`
  - `tests/runtime/lifecycle/*.cs`
- 负责：application shutdown、active save、slot meta、save payload/index、内容注册表、全局会话边界。
- 边界：`ApplicationLifetimeCoordinator` 是进程内 shutdown state、owner drain、finalizer barrier 与最终 `SceneTree.Quit` 的唯一 owner，按 Runtime、Session 阶段关闭顶层 participant；退出后的 stderr、进程返回码与 GodotSharp fatal marker 由 CU-19 的外层 runner 判定，不回写进程内 report。`WorldMapSystem` / `HeadlessGameTestSession` 关闭各自持有的 runtime graph，`GameSession` 关闭 session graph，子服务由这些顶层 owner 递归释放而不独立注册。`GameSession` 是会话根、持有 `GameRoot`；`GameContentCatalog` 是正式内容类型的组合根读入口，持有 typed 内容快照缓存并带 revision，生命周期绑定 owning `GameRoot`（root dispose 后 catalog 失效）。`SaveRepository` 拥有底层 save 文件 IO，`GameSession` 拥有 active save / schema / meta / index 归并；`world_data` 的 runtime owner 是 `WorldRuntimeData`，只在 save payload 入口/出口投影。子 payload 破坏性 schema 变化时同步升级 owning save version，且只接受当前版本、不做 legacy 兼容迁移。
- 适合：save schema、序列化、内容接入、全局注册表问题。
- 邻接单元：CU-01、CU-03、CU-04、CU-10、CU-11、CU-13、CU-20、CU-21。

#### Quest Content（任务内容）

- Source: `data/configs/quests/*.tres`
- Loader: `QuestContentRegistry` (called from `ProgressionContentRegistry.Build`)
- Schema owner: `QuestDef`
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
  - `scripts/utils/WorldMapContentValidator.cs`
  - `data/configs/world_map/*.tres`
  - `data/configs/world_map/shared/*.tres`
- 负责：world preset、世界生成配置、据点/设施/野外遭遇的静态资源与内容校验。
- 适合：世界预设、设施分布、遭遇配置、世界内容校验。
- 邻接单元：CU-01、CU-02、CU-04。

### CU-04 世界生成、据点服务注入、遭遇锚点

- 文件：
  - `docs/design/settlement.md`
  - `scripts/systems/world/WorldMapSpawnSystem.cs`
  - `scripts/systems/world/EncounterAnchorData.cs`
  - `scripts/systems/world/WorldMapResourceNodeData.cs`
  - `scripts/systems/world/WildEncounterGrowthSystem.cs`
  - `scripts/utils/WorldEventConfig.cs`
  - `scripts/utils/MountedSubmapConfig.cs`
- 负责：世界生成、据点注入、挂载子地图事件、遭遇锚点、野外资源点、世界初始状态。
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
  - `scripts/ui/NpcQuestOfferDialog.cs`
  - `scenes/ui/npc_quest_offer_dialog.tscn`
- 负责：world/battle 切换、窗口互斥、命令编排、场景同步、战后回写、运行时总入口。
- 边界：`GameRuntimeFacade` 通过当前 root/session 解析 `GameContentCatalog`，只在仍绑定当前 session（`IsBoundToSession`）时复用，用 revision 校验有效性。世界运行态 owner 是 `WorldRuntimeData`（settlement / submap / encounter anchors / world events / fog 等先进 typed owner），经 `WorldMapDataProjection` 投影 save/window/proxy。跨 party/world/coord 的提交统一走 `RuntimeTransaction` stage + `CommitRuntimeState`；`PartyState` 替换必须保持 session/runtime/services canonical root 一致，root 替换走 `RuntimeTransaction` / facade rebind。命令输入以 typed request（`SettlementActionRequest` / `PromotionSelectionData` / `PartyItemUseOptions` 等）为正式边界。
- 适合：runtime 接线、模式切换、世界场景同步、据点/仓库/奖励/任务命令入口。
- 邻接单元：CU-02、CU-04、CU-05、CU-07、CU-08、CU-09、CU-10、CU-12、CU-15、CU-18、CU-21。

#### Settlement Runtime Commands（据点运行时命令）

- Contract board modal: `GameRuntimeSettlementCommandHandler` + `ShopWindow`
- NPC quest offer modal: `GameRuntimeSettlementCommandHandler` -> `NpcQuestOfferWindowData` -> `NpcQuestOfferDialog`, wired by `WorldMapSystem`
- Accept availability: `QuestAcceptRequirementEvaluator` invoked by handler (contract board and NPC quest offer)
- Confirmation state: modal context `pending_confirmation_quest_id/text/source` (contract board and NPC quest offer)
- Recommended reads before changes:
  - `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
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
- 边界：窗口只渲染当前状态并通过 signal 提交意图，不直接改 `PartyMemberState`；`PromotionChoiceWindow` 的 selection 在 scene/runtime adapter 处转成 `PromotionSelectionData` 才进入 runtime/progression。
- 适合：队伍窗口、转职 UI、触发术 setup UI、角色奖励弹窗。
- 邻接单元：CU-06、CU-10、CU-11、CU-12、CU-14。

### CU-10 队伍共享背包、物品定义与装备基础流转

- 文件：
  - `docs/design/battle_weapon_dice_and_equipment.md`
  - `docs/design/weapons/equipment_ability_system.md`
  - `docs/design/weapons/equipment_ability/README.md`
  - `docs/design/weapons/equipment_ability/equipment_durability_selector_commit.md`
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
- 负责：共享背包、堆叠/容量、物品与配方定义、装备实例、装备/卸装、物品使用。
- 边界：`WarehouseState` / `EquipmentState` / `EquipmentInstanceState` 家族是 plain C# runtime/save owner。物品内容（category / equipment type / 伤害标签 / 固定 trait ids / trait roll groups）跨 trait 规则由 `ItemTraitContentValidator` 校验；装备 trait mint 时机由 `EquipmentRules` / `EquipmentTraitRollService` 拥有——仓库分配稳定装备 instance id 后才生成 `equipment_roll` trait，不在 transient drop 阶段或已入库实例上重 roll。
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
- 负责：队伍状态、成员状态、成长状态、任务状态、角色奖励载体。
- 边界：`PartyState.member_states` owner 是 `PartyMemberStateCollection`；`PartyMemberState` 持有 `ContingencyMatrixSetupState`（只承载战斗外 setup / 充能 / 消耗回写事实，不承载战斗内 release queue / hook）。其余成长、任务、奖励状态都是 plain C# runtime/save DTO。角色 trait 与装备 roll trait 的持久实例边界是 `TraitInstanceState` / `TraitInstanceCollection`：成员实例只承载 `character` source、装备实例只承载 `equipment_roll` source，反序列化按严格字段集与 source kind 校验。
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
- 负责：角色管理门面、奖励归并、成就/任务推进、身份与成长桥接。
- 边界：`PartyContingencySetupService` 是世界侧 contingency setup save/charge/clear/status mutation owner；setup 模板是内容，authored 在 `data/configs/contingency_templates/*.tres`（`ContingencySetupTemplateDef`，经 `ContingencyTemplateContentRegistry` 载入），充能材料与预留 MP 公式的单一出处是 `ContingencyContentRules`。`CharacterBattleWritebackService` 拥有战斗结束后 consumed setup 与战后 HP/MP/death/装备回收/roster 移除的写回。`CharacterManagementModule` 经 `ProgressionServiceFactory` 构建 transient `ProgressionService` 图，是门面而非规则宿主。
- 适合：奖励入账、任务推进、成就记录、跨系统成长接线。
- 邻接单元：CU-06、CU-08、CU-09、CU-10、CU-11、CU-13、CU-14、CU-15、CU-19。

### CU-13 progression 内容定义、条件模型、seed 内容

- 文件：
  - `docs/design/weapons/equipment_ability_system.md`
  - `docs/design/weapons/equipment_ability/README.md`
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
  - `data/configs/quests/*.tres`
- 负责：技能、职业、种族、通用 trait、任务、血脉、升华、信仰等静态内容与内容校验。
- 边界：`TraitDef` / `TraitContentRegistry` / `data/configs/traits/*.tres` 是通用 trait 内容定义边界，source scope、effect/stack/charge/roll schema、typed passive 投影字段（如 save advantage tags、damage resistance entries、`passive_status_effects`）等固定值由 `TraitContentRules` / registry 校验（trait effect 配置须显式 typed 字段）。跨表校验消费已经投影出的 `SkillDefinition` 索引；raw `SkillDef` 只在资源加载、字段校验、`SkillDefinition.FromResource(...)` 这类明确 Resource-boundary 触达。只供通用运行时内部结算引用、不得进入任何学习路径的技能使用 `learn_source = internal`，不能借用 `innate` 表达隐藏性。
- CombatEffect 内容边界：多伤害段与目标分类倍率必须走 `CombatDamageSegmentDef` / `CombatTargetDamageMultiplierRuleDef` → `CombatEffectDefinition.ExtraDamageSegments` / `TargetDamageMultiplierRules` typed 投影；来源绑定的武器额外骰走 `CombatEffectDef.source_bound_weapon_bonus_damage_dice_*` → `BattleStatusEffectState`，由 `BattleDamageResolver` 按状态 `source_unit_id` 与真实武器伤害结算，不在 `params` 或技能 id 文本里塞 ad-hoc 结构；资源校验由 `SkillContentRegistry` 负责。
- CombatSkill 边界：`CombatSkillDef.attack_resolution_mode`（authoring）与 `SkillDefinition.AttackResolutionModeKind`（runtime DTO）唯一由 `BattleSkillResolutionRules` 解释为 direct effect / fate attack / force-hit-no-crit。装备必中主动技能应配 `attack_resolution_mode = &"direct_effect"`，再由 combat effect 的 target/status requirement 与 `BattleDamageResolver` 决定是否生效，不在装备能力 service、技能 id、状态 params 里硬编码“必中”。
- 装备能力内容边界：召唤、消费召唤物、按召唤物数量/距离产生攻击检定修正必须走 `SummonUnitsActionPayloadDef` / `ConsumeSummonedUnitsActionPayloadDef` / `SummonedUnitAttackRollModifierActionPayloadDef` 与 `summoned_unit_count` fact；附近生物/友军计数走 `nearby_unit_count` / `nearby_ally_count` fact；击杀后的额外武器攻击走 `ImmediateWeaponAttackActionPayloadDef`；命中后升级为暴击走 `CriticalHitOverrideActionPayloadDef`；不进入技能可用性列表的装备内部结算走 `TriggerSkillActionPayloadDef`，仍引用正式 `SkillDef` 并复用技能效果、豁免、范围和死亡规则；AP 变动与下一行动回合 AP 清零走 `ModifyActionPointsActionPayloadDef`；装备期间影响行动/读条进度倍率的被动走 binding 级 `EquipmentTemporalProgressModifierDef`；装备触发的固定伤害减免走 `DamageReductionActionPayloadDef`；按实际伤害等 fact 计算治疗走 `HealFromFactActionPayloadDef`；状态栈消费走 `ConsumeStatusStacksActionPayloadDef`；随机分支走 `EquipmentOutcomeTableDef` / `EquipmentOutcomeEntryDef`，仍由分支内 typed action payload 表达效果；持久成长计数由 `ModifyAbilityStateActionPayloadDef` 写入，计数同步出的保存状态由 `EquipmentAbilityStateSchemaDef` 的同步字段声明；临时战场边特征/裂隙走 `ApplyEdgeFeatureActionPayloadDef` 的 from/to selector、`duration_tu`、edge feature 字段与 `max_active_edges`；武器近/远程分类条件读 `weapon_range_type` fact。资源只声明 binding/state/source/target 关系，不在技能 id 或武器 id 文本中推导召唤物、附近单位、追击目标、时间进度、伤害减免、伤害转治疗、状态栈消费、随机分支、临时边或成长状态键。
- 装备能力状态/后置动作边界：状态锁定（如 counterattack/guard/dodge bonus）、强制位移免疫等由 `ApplyStatusActionPayloadDef` typed 字段投影到 runtime status；`after_skill` / `after_damage` 的直接伤害、治疗、清状态、按 fact 治疗、状态栈消费等通用动作继续消费 typed action payload；有时限的 typed target mark 自己持有剩余 TU，并随目标的个人状态时间线推进，因此多个装备来源标记同一目标时不会互相刷新到期时钟；同一 `mirror_status_id` 只做聚合展示，选择剩余时间最长的 mark，当前镜像来源被消费、替换、到期或移除后从剩余 mark 重建，最后一份 mark 消失时才删除；到期由 `on_target_mark_expired/after_status_expired` 分发一次反应，`expired_target_mark_matches` fact 校验来源装备实例、binding、state key 与目标，随后统一清理 mark；授予技能可用性条件消费同一 fact query，生命百分比用 `hp_percent_bp`（basis points，0-10000）；授予技能结果条件走 `skill_damaged_target_count` / `skill_killed_target_count` / `skill_hp_damage_dealt` / `skill_moved_target_count` / `skill_unmoved_target_count` fact，伤害后动作可读 `hp_damage` fact，on-kill 条件可读 `kill_source_is_attack` / `kill_source_equipment_instance_matches` / `kill_source_binding_matches` 这类通用击杀来源 fact，不从授予技能 id 或武器 id 推导特例。
- 装备能力来源生命周期边界：`BattleUnitFactory.RefreshEquipmentProjection(...)` 重建 `equipment_ability_sources` 后，由 `BattleEquipmentAbilityRuntimeService` 按来源单位、装备实例和 binding 清理已经失去投影来源且声明 `remove_on_source_missing` 的 typed target mark；这类来源移除只重建或删除镜像状态，不分发自然到期反应。正式换装直接消费刷新结果；装备耐久摧毁则由 `BattleDamageResolver.ResolveEffects/ResolveAttackEffects` 在完整效果结算后统一触发刷新，因此普通技能、冲锋/连击、ground effect、内部技能与装备立即攻击共享同一清理边界；可用的 `BattleEventBatch` 同步记录受影响的来源/目标单位。
- 装备能力 fact 来源隔离边界：`status_stacks` 可通过 `EquipmentAbilityFactQueryDef.require_source_unit_match` 要求目标状态的 `source_unit_id` 与当前装备能力来源单位一致；不匹配时返回 0。需要来源私有的命中、追加骰、消费或击杀条件时复用该 fact，不按具体状态或武器 id 增加运行时分支。
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
- 负责：成长公式、职业规则、技能规则、建卡规则、属性计算。
- 边界：promotion 规则输入由 `PromotionSelectionData` 承载，永久属性写入授权由 `AttributePermanentChangeSource` 承载（protected custom stat 仅 CharacterCreation 或显式授权 StoryScript 可写）。`ProgressionService` 的手动学习入口拒绝 `internal` 以及职业/身份授予来源；内部 SkillDef 只能由明确持有其定义的运行时服务直接结算。`AttributeService` 只消费 `AttributeSourceContext` 中已解析好的 modifier，generic trait 属性入口是 `trait_attribute_modifiers`（保留 modifier 自身 source_type/source_id，不并入 equipment/passive 默认来源）；基础六维派生的 `*_modifier` 可被 modifier overlay 叠加但不回写基础属性或成长事实；BAB 直接消费 `ProfessionBaseAttackProgression`。
- 适合：成长规则、属性公式、建卡候选、职业/技能规则。
- 邻接单元：CU-01、CU-09、CU-11、CU-12、CU-13、CU-15、CU-19。

### CU-15 战斗运行时总编排

- 文件：
  - `docs/design/weapons/equipment_ability_system.md`
  - `docs/design/weapons/equipment_ability/README.md`
  - `docs/design/weapons/equipment_ability/battle_skill_availability_migration.md`
  - `docs/design/weapons/equipment_ability/equipment_durability_selector_commit.md`
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
- 负责：开战、时间轴、命令 preview/issue、技能执行、战斗结算、战斗内运行时编排。
- 桥接：通用 trait 进战斗为 `IBattleRuntimeCharacterGateway.BuildEffectiveTraitProjectionForEquipmentView(...)` → `BattleUnitFactory` → `BattleUnitState.effective_trait_instances/effective_trait_ids`（以 battle-local `EquipmentState` 重算，战斗规则不反查 trait/item catalog 或角色装备源），再由 `BattleTraitPassiveProjectionService` 将 trait typed passive 投影到 `BattleUnitState.save_advantage_tags/damage_resistances/save_bonus_by_ability/status_effects`，其中 trait 被动状态使用 `trait_passive_status` source layer 以便刷新装备投影时清理重建。装备能力进战斗为 `GameContentCatalog.GetEquipmentAbilityBindingDefinitionsTyped/GetTraitDefsTyped` → `BattleRuntimeModule` typed 索引 → `BattleEquipmentAbilityProjectionService` → `BattleUnitState.equipment_ability_sources`；binding 级时间进度倍率配置同时投影为 `BattleUnitState.temporal_progress_modifiers` 供 timeline/casting runtime 读取；角色装备武器的 `range_type` 通过 `WeaponProjection` 进入 `BattleUnitState.weapon_range_type` 供装备能力 fact 读取；敌人经 `EncounterRosterBuilder` 投 battle-only source；战斗规则只从 `BattleUnitState` 读装备能力源、时间进度 modifier、武器 range type 与 `creature_type_tags`。装备能力召唤的战斗内单位由 `BattleEquipmentAbilityRuntimeService` 创建为 battle-only `BattleUnitState`，可从 `SummonUnitsActionPayloadDef` 投影基础技能与天生武器，占用 `BattleState`/grid 并通过 `BattleAiBlackboard` 记录来源单位、装备实例、binding、state key 与过期 TU。
- 装备能力运行时动作边界：`BattleEquipmentAbilityRuntimeService` 解析 before-hit / after-hit / after-skill / after-kill / after-damage / after-attack-check / after-status-expired 的 typed action payload 并写入 `BattleEventBatch` / `BattleState`，包括强制暴击来源、内部 SkillDef 结算、直接伤害、治疗、按 fact 治疗、清状态/target mark、状态栈消费、授予技能、召唤单位、召唤物消费、临时边特征、持久装备 counter 修改、持久状态同步与立即武器追击；有时限 target mark 的剩余 TU 存在 `BattleEquipmentTargetMarkState`，由 `BattleRuntimeSkillTurnResolver` 与目标状态时钟同步推进。`BattleAttackCheckPolicyService` 把强制暴击及来源装备实例/binding/action 写入本次 `AttackCheckInput`，未命中不升级；预览只有在 `CritLocked`/force-hit-no-crit 均未生效时才公开“命中后必定暴击”，命中后的击杀归因沿该来源进入 on-kill；`BattleDamageResolver` 在真实武器攻击检定提交后分发 after-attack-check，并把声明合并的内部技能结果并回父攻击；`BattleSkillExecutionOrchestrator` 只向装备技能提交通用目标结果摘要和 `BattleKillProvenance`，装备授予技能入口取 binding 固定 `skill_level` 与角色已学同名技能等级的较高值；来源绑定武器追加伤害通过伤害事件携带来源技能 id，只有角色已学该技能时才向该技能熟练度写入，不按具体武器分支。
- 强制暴击击杀归因边界：`BattleKillProvenance` 只在最终 `AttackEffectResolutionResult.CriticalHit` 为真时保留 forced-critical 的装备实例/binding/action；禁暴击或其他后续规则把结果降为普通命中时，击杀按实际主手攻击归因，不得误触发依赖 forced-critical binding 的 on-kill 反应。装备能力发起的立即武器攻击可以提供外层 provenance 作为 fallback：最终强制暴击来源优先覆盖它，未形成强制暴击时仍保留外层 binding/action 的 on-kill 语义。
- 装备能力 AP 恢复边界：`ModifyActionPointsActionPayloadDef.mode = restore_current_action_points_capped` 可在 after-kill 等通用 action 阶段恢复指定单位的当前 AP，并以该单位 `attribute_snapshot[action_points]` 为正常上限；已达到或超过上限时不增加也不反向降低。触发次数限制若需要必须由内容显式声明，运行时不默认附加 once-per-turn。
- 边界：读条 / pending cast 是 runtime-only battle state（不进 save），由 `BattleCastingTimeService` / `BattleTimelineDriver` / `BattleRuntimeSkillTurnResolver` / `BattleSkillExecutionOrchestrator` 协作，manual cancel 走 typed command path。contingency 战斗侧由 `BattleContingencySystem` 从 persistent setup 初始化、经 `ContingencyTargetResolverService` 解析目标、排队执行 `AutoCastRequest`；consumed 单一真相是 `BattleUnitState.MarkContingencySetupConsumed`，写回契约是 `IBattleRuntimeCharacterGateway.Validate/CommitContingencyConsumedSetups`，失败随 finalization rollback。temporal 状态族（`time_stasis` / `time_slow` / `time_reverberation`）与装备投影出的时间进度倍率规则归属 `BattleTemporalStatusService`，由 timeline / casting / turn-resolver / grid 协作执行；下一回合 AP 清零这类 turn-start AP 规则归属 `BattleStatusSemanticTable` + `BattleRuntimeSkillTurnResolver`，不在具体武器技能里硬编码。
- 适合：战斗流程、战斗结算、特殊技能流程、战斗内事务。
- 邻接单元：CU-02、CU-10、CU-11、CU-12、CU-13、CU-14、CU-16、CU-17、CU-18、CU-20、CU-21。

### CU-16 战斗状态模型、边规则、伤害、AI 规则层

- 文件：
  - `docs/design/battle_weapon_dice_and_equipment.md`
  - `docs/design/weapons/equipment_ability_system.md`
  - `docs/design/weapons/equipment_ability/battle_skill_availability_migration.md`
  - `docs/design/weapons/equipment_ability/equipment_durability_selector_commit.md`
  - `scripts/systems/battle/core/*.cs`
  - `scripts/systems/battle/terrain/Battle*.cs`
  - `scripts/systems/battle/rules/*.cs`
  - `scripts/systems/battle/rules/DamageApplicationProjection.cs`
  - `scripts/systems/battle/rules/IBattleDamageApplicationHook.cs`
  - `scripts/systems/battle/ai/*.cs`
  - `scripts/enemies/actions/*.cs`
  - `scripts/player/progression/combat_effect_def.gd`
  - `scripts/player/warehouse/Weapon*.cs`
- 负责：BattleState、地形/边规则、伤害、命中、状态语义、AI 评分与决策规则。
- 边界：伤害上下文正式链是 `DamageResolutionContext`，写回前 hook 边界是 `DamageApplicationProjection` / `IBattleDamageApplicationHook`（hook / release suppression 由战斗 runtime 的 contingency owner 管理，不入 damage payload / AI 评分 / save schema）。`BattleUnitState` 状态效果正式源是 `BattleStatusEffectCollection`，状态上的一次性攻击优势、攻击/豁免加值由 `BattleStatusEffectState` typed 字段声明，真实攻击检定消耗归 `BattleDamageResolver`，真实豁免消耗归 `BattleSaveResolver`，preview/probability 不消费；execute effect 的 `soul_fracture_duration_tu = 0` 明确表示不施加灵魂裂隙，正数才生成该状态；typed save / damage mitigation 真相源是 `save_advantage_tags` / `damage_resistances`；layered barrier 真相源是 `BattleBarrierStore` / `BattleBarrierInstanceState`；临时边特征真相源是 `BattleTemporaryEdgeFeatureState`，由 `BattleEdgeService` 叠加进 runtime edge face，移动/占位/寻路仍消费统一 edge face。`BattleStatusSemanticTable` 拥有通用状态语义（如 `paralyzed` 的行动、移动与 pending cast 阻断），不在具体武器能力中硬编码。`BattleFateEventBus` 事件 surface 是 typed `BattleFateEventPayload`，misfortune 直接触发用 `MisfortuneTriggerRequest`。fixed schema 名称（combat resource id / damage tag / mitigation tier / target-team filter / save tag/ability / forced-move mode 等）由 enum/typed utility 解析。
- 伤害段边界：`BattleDamageResolver` 负责把 `CombatEffectDefinition.ExtraDamageSegments` 结算为同一次 damage effect 下的额外 `DamageEventResult`；额外段复用该 effect 的 save result 与目标倍率规则，但不继承武器骰、暴击额外骰或装备追加骰。目标分类倍率只读 `BattleUnitState.creature_type_tags`，不回查敌人模板或物品/trait catalog。
- 装备伤害骰边界：装备能力 `add_damage_dice` 与状态来源绑定的武器额外骰由 `BattleDamageResolver` 统一结算；来源绑定骰只在状态 `source_unit_id` 匹配攻击者且本次 effect 实际包含武器伤害时触发，并入 bonus damage dice 以复用暴击额外骰路径；装备能力骰数可由通用 fact（如装备能力状态 fact）按 authoring 公式放大，具体武器的成长键仍只在 `.tres`；`subtract=true` 的骰只扣减匹配主伤害标签的本次基础伤害，不生成负数额外伤害段。
- 桥接：装备能力的命中检定加值、优势、防御组件调整、命中后强制暴击与召唤物数量/距离修正由 `BattleEquipmentAbilityRuntimeService` 从 `BattleUnitState.equipment_ability_sources`、`BattleUnitState.attribute_snapshot`、typed target mark 和 battle-only 召唤单位 blackboard 收集，经 `BattleAttackCheckPolicyService` 汇总后交给 `BattleHitResolver` 生成本次 `AttackCheckInput`；命中检定加值与强制暴击可用 `require_weapon_damage` 限定只作用于含武器伤害的攻击定义，也可通过 `attribute_modifier_id` 从使用者属性快照读取动态调整值。装备能力固定伤害减免由同一 runtime service 收集后进入 `BattleDamageResolver` fixed mitigation 汇总。忽略 AC component 等规则只调整本次目标 AC，不改写目标 `attribute_snapshot[armor_class]`。召唤单位是否为 summoned 的规则只读 `BattleAiBlackboard` / 状态效果，不从武器内容表反查。
- 目标过滤边界：死者单位默认不可被 unit skill 选中；只有 ally/self 且 effect 列表含 `Heal` / `HealFatal` 这类复活治疗语义时，由 `BattleSkillExecutionOrchestrator` 显式放开 dead target，不为具体装备技能 id 建特例。
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
- 负责：战斗地形生成、wild encounter roster 装配、prop 注入。
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
- 负责：battle HUD、棋盘绘制、单位/prop 渲染、相机、overlay、hover 展示。
- 边界：`BattleHudAdapter` 只消费 runtime 提供的 `BattlePreview` 投影 HUD/hover，不拥有命中、伤害、射程或目标合法性计算。
- 适合：battle HUD、棋盘视觉、TileMap、相机、目标浮标。
- 邻接单元：CU-06、CU-15、CU-16、CU-17、CU-19、CU-20。

### CU-19 自动化回归与截图辅助

- 文件：
  - `docs/superpowers/specs/2026-07-10-godotsharp-lifecycle-architecture-design.md`
  - `.github/workflows/ci.yml`
  - `tests/run_regression_suite.py`
  - `tests/tooling/test_run_regression_suite.py`
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
- 负责：headless 回归、contract 验证、fixture、截图/签名辅助。
- 边界：`TestHarness.Finish(...)` 只冻结断言并生成 `TestResult`；C# runner 统一经 `LifecycleTestSceneTree` / `TestExitCoordinator` 把结果提交给 `ApplicationLifetimeCoordinator`，owner teardown、finalizer barrier 与最终退出均由 production shutdown pipeline 负责。外层 `run_regression_suite.py --lifecycle-correctness` 拥有 post-exit correctness 判定：保留调用者的发现、筛选、并发与超时设置，为每个子进程强制 strict/trace、禁用 finalizer retry，并把 GodotSharp fatal marker 独立于普通输出错误判为失败；阶段性 unsafe/resource 输出保留为可见 baseline。CI 在完整回归前以相同 profile 运行专用生命周期门。GodotSharp 生命周期或退出顺序改动必须同时读取 lifecycle architecture spec、runner tooling regression 与 CI 接线。功能测试读取正式 `SkillDef` `.tres` 经 `TestSkillDefinitionProjection.LoadSkillDefinition(...)` 在加载边界投影为 `SkillDefinition`，不把 `SkillDef` 传入服务。
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
- 负责：敌方模板、AI brain/state/action、generation slot、transition rule、wild encounter roster 内容。
- 边界：敌方内容校验需要 skill catalog 时消费加载边界已投影好的 `SkillDefinition` 索引，不由 `EnemyTemplateDef` 提供 `SkillDef` resource 到 DTO 的投影。
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
- 负责：无 UI session、文本命令、expect 断言、文本/结构化快照。
- 边界：`HeadlessGameTestSession` 持有 typed `GameSession` / plain C# `GameRuntimeFacade`，owned session teardown 走 `GameSession.Dispose()`；headless C# runner 与其他回归一样通过共享 lifecycle exit adapter 委托 application shutdown pipeline，外层进程结果再由 CU-19 lifecycle correctness profile 判定，headless session 不自行执行 GC、retry 或 post-exit 日志过滤。`GameTextCommandRunner` 的核心命令直接走 typed `GameRuntimeFacade` gateway；battle snapshot 的 contingency surface 包括 `battle.contingency` sidecar snapshot、unit overlay 的 contingency 字段与 `battle.report_entries` 结构化条目。
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
- 设计必读：`docs/design/weapons/equipment_ability_system.md`、`docs/design/weapons/equipment_ability/README.md`、`docs/design/weapons/equipment_ability/battle_skill_availability_migration.md`、`docs/design/weapons/equipment_ability/equipment_durability_selector_commit.md`
- 按需补：CU-06、CU-11、CU-12、CU-18、CU-20、CU-21

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

### 只改 GodotSharp 生命周期、内容 owner、projection lease 或退出屏障

- 必带：CU-02、CU-19
- 设计必读：`docs/superpowers/specs/2026-07-10-godotsharp-lifecycle-architecture-design.md`
- 启动必读：`project.godot`
- 按需补：CU-06、CU-15、CU-16、CU-18、CU-21

## 不推荐的切法

- 不要把这份文档写成实现迁移备忘录。
- 不要在单元描述里记录具体 typed 改造状态、字段级约束或 API 细节。
- 不要在这里维护具体回归脚本名单；测试入口留给对应 `tests/` 目录和 README。
- 不要把单个运行时 helper 当成独立架构层；优先按系统边界读图。
- 不要一次性装载 CU-02、CU-06、CU-12、CU-15、CU-18，除非任务确实跨越存档、世界、角色、战斗和展示整条链。
