# C# / Godot Object Lifecycle Audit

更新日期：`2026-06-20`

## 目标

这份文档用于逐项整理 C# 类型持有 Godot `Object` / `RefCounted` / `Resource`
时的生命周期问题。核心原则不是“见到 `RefCounted` 就释放”，而是把引用分成
owned / borrowed / scene-owned / test-owned，并让构造、替换和释放路径都显式表达所有权。

## 基本规则

- C# owner 持有的 `GodotObject` 引用必须是 `private` 或 `internal` 受控字段，不允许 public setter 作为正式 runtime 状态入口。
- 注入进来的 `RefCounted` / `Resource` 默认是 borrowed，owner 不释放；如果调用方要转交所有权，必须走 `SetOwned...`、`BindOwned...`、`owns...: true` 等显式 API。
- 构造函数或 owner 方法内部 `new` 出来的 Godot object 默认是 owned，owner 必须释放或显式转交所有权。
- 可被替换的 Godot object 字段必须同时维护 ownership 标记，例如 `_ownsRegistry` / `_ownsRng` / `_ownsTerrainGenerator`。
- 替换引用必须走表达 ownership 的方法，例如 `BindBorrowed...`、`SetOwned...`、`Replace...ForTests`，不能直接 public set。
- 内容修改必须走领域 owner/service API；外部不应拿到 mutable `RefCounted` 后直接改正式 runtime 状态。
- `Resource` 内容定义默认是 shared catalog content。runtime、UI、validator 和 service 只能借用，不能 dispose。
- 测试 fixture 可以深度 dispose 自己创建的状态图，但 helper 名称必须表达 test-owned，不应复用到生产路径。
- 不要为了规避 finalizer 问题在所有 `RefCounted` / `Resource` 构造函数里全局 `GC.SuppressFinalize(this)`；这会把未显式释放的 wrapper 变成 shutdown leak。
- 只有在明确 owned teardown 前才 suppress finalizer，顺序必须是 `GC.SuppressFinalize(obj)` 然后 `Dispose()` / `Dispose(true)`。

## 类型分组

### Shared Resource Content

代表类型：

- `SkillDef`
- `ItemDef`
- `QuestDef`
- `TraitDef`
- `ProfessionDef`
- `EnemyTemplateDef`
- `EnemyAiBrainDef`
- `WildEncounterRosterDef`
- `WorldMapGenerationConfig`
- `SettlementConfig`
- `FacilityConfig`

规则：

- 由 Godot resource loader、content registry 或 `.tres` 资源图拥有。
- catalog / runtime / UI 只保留 borrowed typed view。
- owner dispose 时只清引用、清快照或解绑，不递归 dispose resource。

可接受模式：

- `GameContentCatalog.ClearSessionBinding()` 清快照并递增 revision，而不是释放资源。
- `WorldMapSpawnSystem` 通过 `GD.Load<T>()` 读取默认 world resource 后只借用。

### Owned Content Registry

代表类型：

- `ProgressionContentRegistry`
- `SkillContentRegistry`
- `ItemContentRegistry`
- `RecipeContentRegistry`
- `EnemyContentRegistry`
- `BattleSpecialProfileRegistry`
- `BarrierContentRegistry`

规则：

- 如果由 session / service / fixture 自己 `new`，则该 owner 可以 dispose registry。
- registry dispose 只能清内部 typed index、validation errors、resource cache 引用，不应递归释放加载到的 resource。
- 如果 registry 从 `GameSession` 或外部注入，则注入方仍拥有，接收方只能 borrowed 使用。

当前可接受模式：

- `GameSession.DisposeOwnedRuntimeResources()` 释放 session 自己创建的 registry。
- `ProgressionContentRegistry.DisposeManagedRegistry()` 清缓存并释放自己创建的子 registry。
- `CharacterCreationWindow` 自己创建的 fallback progression registry 由窗口释放，`LoginScreen` 注入的 session registry 作为 borrowed 使用。
- `SettlementForgeService` 自己创建 `RecipeContentRegistry`，自己 dispose。
- `BattleBarrierService` 自己创建 `BarrierContentRegistry`，自己 dispose。
- `BattleSimContentProvider` 自己创建 progression/enemy registry，自己 dispose。
- `GameSession.SetItemContentRegistryForTests(...)` 默认绑定 borrowed test registry；`SetOwnedItemContentRegistryForTests(...)` 显式把 registry 所有权转回 session。
- 生产代码里的释放 helper 已收窄为 `DisposeOwnedRegistry` / `DisposeOwnedGodotService` / `DisposeOwnedRefCounted`，不再保留泛化的 `DisposeOwned<T where T : GodotObject>` 入口。

### Runtime State / DTO RefCounted

代表类型：

- `PartyState`
- `PartyMemberState`
- `UnitProgress`
- `EquipmentState`
- `WarehouseState`
- `BattleState`
- `BattleUnitState`
- `BattleCellState`
- `BattlePreview`
- `BattleCommand`
- `BattleEventBatch`
- `AttackPreviewData`
- `PendingCharacterReward`
- `EncounterAnchorData`

规则：

- 普通 service 持有这些对象时默认 borrowed，只能清绑定，不释放。
- session、runtime 或 fixture 如果创建了整棵状态图，才可以按 owner teardown 处理。
- `Dispose()` 不应从 `GameRuntimeFacade` / `BattleRuntimeModule` 递归释放 `_party_state` / `_battle_state`；这些对象需要按更高层生命周期失效或由测试 fixture 明确释放。

当前可接受模式：

- `PartyState.Dispose()` 负责释放 session/fixture-owned 队伍状态树中的 `PartyMemberState`、`UnitProgress`、`EquipmentState`、`WarehouseState`、`TraitInstanceState`、`PendingCharacterReward` 等子状态；shared catalog `Resource` 不在这条链里释放。
- `GameSession.Dispose()` 会释放 session-owned party state；`GameSession.SetPartyState(...)` 仍通过 normalize 产生 session snapshot，不直接释放入参或旧 runtime 引用，因为 `GameRuntimeFacade` 可能仍借用当前 party 对象直到下一次 setup/sync。
- `PartyWarehouseService` / `PartyEquipmentService` / `PartyItemUseService` / `QuestProgressService` / `CharacterManagementModule` 对外部 setup 的 `PartyState` 只 borrowed；只有自己创建的 fallback `PartyState` / backpack view 在替换或 dispose 时释放。
- `WarehouseState.ReplaceStacks(...)` / `ReplaceEquipmentInstances(...)` 只释放被替换且未被新集合保留的旧子对象；`EquipmentState.ClearEntrySlot(...)` 释放清槽旧实例，`PopEquippedInstance(...)` 则转交实例给调用方。
- `GameRuntimeFacade.Dispose()` 清 `_party_state` / `_battle_state` 引用，不递归 dispose。
- `BattleRuntimeModule.DisposeManagedRuntime()` 清 `_state` 拓扑和引用，不递归释放 shared content。
- `BattleSimFormalCombatFixture` 使用 `_ownsPartyState` 区分 fixture-built roster 与 borrowed party state；fixture-owned party 才能深度释放。
- `tests/shared/BattleTestFixture` 的 battle state deep dispose helper 只用于 test-owned battle graph，不作为生产释放策略。
- 嵌套 `Godot.Collections.Array<T RefCounted>` teardown 不应依赖 typed enumerator；已 disposed 的槽位可能退化成裸 `RefCounted`，释放 helper 必须按 untyped `Variant` 读取并跳过不匹配对象。

### Owned Godot Utility Objects

代表类型：

- `RandomNumberGenerator`
- 临时 `Godot.Collections.Dictionary` / `Array` 不是 Godot object，但持有 Variant/RefCounted 时同样要注意引用语义。

规则：

- 自己 `new RandomNumberGenerator()` 的 class 应实现 `IDisposable` 或确保生命周期很短且不缓存 wrapper。
- 外部注入 RNG 时默认 borrowed，不释放；需要转交所有权时必须使用 `SetOwnedRng(...)` 或 `ownsRng: true` 这类显式入口。
- 允许用 CLR `Random` 或 deterministic typed RNG 替换 Godot RNG，减少 Godot wrapper 生命周期面。

当前可接受模式：

- `EquipmentDropService` / `EquipmentTraitRollService` 自己创建 fallback RNG 时自己 dispose，普通外部注入 RNG 时只借用；调用方通过 `SetOwnedRng(...)` 或 `ownsRng: true` 显式转交的 RNG 由 service dispose。
- `BattleTerrainGenerator` 释放 self-owned RNG；`BattleRuntimeModule` 对 injected generator / drop service 默认按 caller-owned 处理，只释放 module-created 或通过 `owns_terrain_generator: true` / `owns_equipment_drop_service: true` 显式转交的对象。
- `CharacterCreationWindow` 退出 tree 时释放自己创建的 RNG。
- `WorldMapSpawnSystem` 是 `IDisposable` transient helper；调用点用 `using var`，dispose 释放 self-owned RNG 并清 borrowed resource 引用。
- `SettlementShopService` 释放 self-owned RNG。
- `FateAttackFormula` 默认投骰源改为 `TrueRandomSeedService`，只有外部显式传入 `RandomNumberGenerator` 时才建立 borrowed adapter；`FortuneService` 的 deterministic seeded roll source 改用 CLR `Random`。
- `TrueRandomSeedService` fallback 路径使用 CLR `Random.Shared`，不再创建临时 Godot RNG。

### Scene-Owned Nodes

代表类型：

- `WorldMapSystem`
- `BattleMapPanel`
- `CharacterCreationWindow`
- `SettlementWindow`
- `ShopWindow`
- `PartyManagementWindow`
- `BattleBoard2D`
- `BattleBoardController`

规则：

- scene tree owns child nodes。
- `_ExitTree()` 只断信号、清 runtime 引用、释放自己创建的非 scene sidecar；不要 dispose/Free scene-owned child，除非该 node 是代码动态创建且 owner 明确。
- `SuppressFinalize` 只解决 C# wrapper shutdown；不能替代 ownership 释放策略。

当前可接受模式：

- `WorldMapSystem._ExitTree()` 断 signal、清 runtime/proxy，并避免释放 scene child。
- `BattleMapPanel._ExitTree()` 清 runtime/context sidecar，不手动释放 scene-owned child。

需要整理：

- UI 节点中如果自己创建 content registry / RNG，需要和 scene-owned child 分开处理。

## 当前高优先级问题清单

### 1. CharacterCreationWindow registry ownership（已整理）

文件：

- `scripts/ui/CharacterCreationWindow.cs`
- `scripts/ui/LoginScreen.cs`

问题：

- `CharacterCreationWindow` 默认 `new ProgressionContentRegistry()`。
- `LoginScreen` 后续调用 `SetProgressionContentRegistry(gameSession.GetProgressionContentRegistry())` 注入 session-owned registry。
- 当前没有 `_ownsProgressionContentRegistry`，因此：
  - 不 dispose 会泄漏默认创建的 registry。
  - 退出时直接 dispose 当前字段会误杀 session registry。

目标：

- 引入 `BindBorrowedProgressionContentRegistry(...)` 或扩展 `SetProgressionContentRegistry(..., bool ownsRegistry)`。
- 替换前只 dispose owned fallback。
- `_ExitTree()` 只释放 owned registry，不释放 borrowed session registry。

建议测试：

- 新增或扩展 UI/headless schema 回归，验证注入 session registry 后窗口退出不会使 session registry invalid。

### 2. EquipmentTraitRollService RNG ownership（已整理）

文件：

- `scripts/systems/inventory/EquipmentTraitRollService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`

现状：

- `EquipmentTraitRollService` 是 `IDisposable` plain C# service。
- fallback RNG 为 service-owned；普通注入 RNG 为 borrowed；`SetOwnedRng(...)` / `ownsRng: true` 表示显式转移。
- `ConfigureRng(...)` 替换 RNG 前只释放旧 owned RNG。
- `GameRuntimeFacade` 在 catalog revision、session 绑定变化或 facade dispose 时释放自己创建的 trait roll service。

剩余关注：

- 测试应通过 service 的 managed ownership state 验证，不要用 disposed wrapper 的 `GodotObject.IsInstanceValid(...)` 或强制 finalizer drain 证明释放。

### 3. EquipmentDropService RNG ownership（已整理）

文件：

- `scripts/systems/inventory/EquipmentDropService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

现状：

- `EquipmentDropService` 是 `IDisposable` plain C# service。
- fallback RNG 为 service-owned；普通注入 RNG 为 borrowed；`SetOwnedRng(...)` / `ownsRng: true` 表示显式转移。
- `BattleRuntimeModule.setup(...)` 普通注入 drop service 为 caller-owned；module 自建或 `owns_equipment_drop_service: true` 注入的 service 为 module-owned。
- `GameRuntimeFacade` 对自己字段 `_equipment_drop_service = new()` 负责 dispose。

剩余关注：

- module/facade 测试应验证 managed disposed state 与 ownership flag，不要通过 native wrapper validity 判定释放。

### 4. BattleTerrainGenerator RNG cleanup（已整理）

文件：

- `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `tests/battle_runtime/runtime/run_battle_runtime_terrain_generator_ownership_regression.cs`

现状：

- `BattleRuntimeModule` 保留 terrain generator ownership flag，并支持 `owns_terrain_generator: true` 显式接管注入 generator。
- `BattleTerrainGenerator.Dispose()` 会释放 self-owned RNG 并清空引用。
- 普通注入 generator 仍为 caller-owned，module dispose 不释放。

剩余关注：

- 后续若将 generator RNG 外部化，也应默认 borrowed，显式 transfer 才释放。

### 5. WorldMapSpawnSystem transient Godot object fields（已整理）

文件：

- `scripts/systems/world/WorldMapSpawnSystem.cs`
- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/world/WorldMapDataContext.cs`

现状：

- `WorldMapSpawnSystem` 是 `IDisposable` plain C# transient helper，但持有 self-owned RNG 和 loaded resource 引用。
- loaded resources 是 borrowed/shared，不应 dispose。
- `GameSession.PrepareNewWorld(...)` 与 `WorldMapDataContext.EnsureSubmapGenerated(...)` 用 `using var` 创建 helper。
- `Dispose()` 只释放 RNG、清 borrowed resource 引用，不释放 loaded world resources。

### 6. Generic GodotObject dispose helpers（已整理）

文件：

- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `tests/shared/GodotSharpCleanup.cs`
- `tests/shared/BattleTestFixture.cs`

现状：

- 生产代码不再保留未使用的 `DisposeOwned<T where T : GodotObject>` helper。
- `GameSession` helper 收窄为 `DisposeOwnedRegistry` / `DisposeOwnedGodotService` / `DisposeOwnedRefCounted`。
- 新增 `GodotRefCountedDisposer` 只用于明确 owned `RefCounted` teardown，不作为 borrowed/shared resource 的通用清理入口。
- 测试 helper 仍可保留，但只用于 test-owned 对象图。

### 7. BattleSimFormalCombatFixture deep dispose boundary（已整理）

文件：

- `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`

现状：

- fixture 会递归 dispose `PartyState` 及其子状态。
- 这只在 fixture 自己构建 roster 状态图时安全。
- `_ownsPartyState` 区分 fixture-built party 与 borrowed party。
- `BindBorrowedPartyStateForTests(...)` 绑定外部 party 后，fixture dispose 只清引用，不释放外部 owner 的对象。
- deep dispose helper 命名为 `DisposeFixtureOwnedPartyState(...)`。

### 8. Test direct registry construction cleanup（已整理）

文件范围：

- `tests/**/*.cs`

现状：

- 直接构造 registry 的测试已改为 `using var` 或显式 finally dispose。
- 需要在 registry dispose 后继续使用 typed view 的测试先复制 `Dictionary<StringName, TResource>` 快照；resource 值本身仍是 shared/borrowed content，不递归释放。
- lifecycle regression 中故意创建 borrowed registry/RNG 的测试仍保留显式外部释放，以验证 owner 不误杀 borrowed object。
- 测试现场构造的嵌套 `Resource` / `RefCounted` 图如果是 test-owned，必须由 fixture 子到父显式释放；多个大型 fixture 在同一 runner 中连续创建/销毁时，case 间可以 `GodotSharpCleanup.CollectPendingFinalizers()`，避免上一段 finalizer 队列和下一段 owned graph teardown 交错。

### 9. Runtime state graph and plain service fallback ownership（已整理）

文件：

- `scripts/player/progression/PartyState.cs`
- `scripts/player/progression/PartyMemberState.cs`
- `scripts/player/progression/UnitProgress.cs`
- `scripts/player/equipment/EquipmentState.cs`
- `scripts/player/warehouse/WarehouseState.cs`
- `scripts/systems/inventory/PartyWarehouseService.cs`
- `scripts/systems/inventory/PartyEquipmentService.cs`
- `scripts/systems/inventory/PartyItemUseService.cs`
- `scripts/systems/progression/CharacterManagementModule.cs`
- `scripts/systems/progression/QuestProgressService.cs`

现状：

- session/fixture-owned `PartyState.Dispose()` 会释放队伍状态图中的 owned 子状态。
- plain inventory/progression services 对外部 `PartyState` 仍是 borrowed；只有自己创建的 fallback party/backpack view 在替换或 dispose 时释放。
- `WarehouseState` / `EquipmentState` 的替换、清槽、预览副本路径明确区分 discard 与 transfer。

## 当前可接受但需要保持的模式

- `GameContentCatalog` 持有 typed snapshot，但 root dispose 只 `ClearSessionBinding()`，不释放 shared content。
- `GameRuntimeFacade.Dispose()` 释放 plain C# helper 并清 `_party_state` / `_battle_state` 引用，不递归 dispose。
- `BattleRuntimeModule` 对 `BattleTerrainGenerator` 已经区分 module-owned 与 caller-owned。
- `SettlementForgeService` / `SettlementShopService` 自己创建 Godot helper 时自己释放。
- `PartyWarehouseService` / `PartyEquipmentService` / `PartyItemUseService` / `QuestProgressService` / `CharacterManagementModule` 只释放 self-created fallback party/backpack，不释放 setup 注入的 session party。
- `HeadlessGameTestSession` owns `GameSession` 时走 `GameSession.Dispose()`，不绕过 log sink cleanup。

## 建议修复顺序

1. 建立小型 ownership helper / 命名规范，不引入复杂框架。
2. 已整理：`CharacterCreationWindow` registry ownership。
3. 已整理：`EquipmentTraitRollService` RNG ownership，并调整 `GameRuntimeFacade` 替换路径。
4. 已整理：`EquipmentDropService` ownership，并调整 `GameRuntimeFacade` / `BattleRuntimeModule`。
5. 已整理：`BattleTerrainGenerator.Dispose()`。
6. 已整理：`WorldMapSpawnSystem` 采用 `IDisposable`，调用点使用 `using var`。
7. 已整理：删除或收窄生产代码通用 `DisposeOwned<T GodotObject>`。
8. 已整理：标注 formal fixture / shared test fixture 的 test-owned deep dispose 边界。
9. 已整理：运行时状态图与 plain service fallback ownership。

## 回归建议

- `dotnet build magic.csproj`
- `godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs`
- `godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_terrain_generator_ownership_regression.cs`
- `godot --headless --script tests/equipment/run_equipment_trait_roll_regression.cs`
- `godot --headless --script tests/equipment/run_equipment_drop_service_lifecycle_regression.cs`
- `godot --headless --script tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs`
- 针对新增 ownership helper 增加最小 lifecycle regression，验证 borrowed object 不被 dispose、owned object 会被 dispose。

## Project Context Units Impact

本次 lifecycle 修复改变了 `GameSession` session teardown、inventory/progression plain service fallback ownership、world spawn helper ownership 与测试 registry/RNG 清理关系；已同步更新 `docs/design/project_context_units.md` 中 CU-02、CU-04、CU-10、CU-11、CU-12、CU-15、CU-19、CU-21 的生命周期约束。后续如果继续修改 `GameRuntimeFacade`、`BattleRuntimeModule`、`GameSession`、`CharacterCreationWindow` 或 content registry 的主要生命周期关系，需要继续同步对应 context unit。
