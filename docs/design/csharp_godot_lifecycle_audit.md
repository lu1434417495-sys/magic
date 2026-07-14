# C# / Godot Object Lifecycle Audit

更新日期：`2026-06-20`

## 目标

这份文档用于逐项整理 C# 类型持有 Godot `Object` / `RefCounted` / `Resource`
时的生命周期问题。核心原则不是“见到 `RefCounted` 就释放”，而是把引用分成
owned / borrowed / scene-owned / test-owned，并让构造、替换和释放路径都显式表达所有权。

## 基本规则

- C# owner 持有的 `GodotObject` 引用必须是 `private` 或 `internal` 受控字段，不允许 public setter 作为正式 runtime 状态入口。
- 注入进来的 `RefCounted` / `Resource` 默认是 borrowed，owner 不释放。
- 构造函数或 owner 方法内部 `new` 出来的 Godot object 默认是 owned，owner 必须释放或显式转交所有权。
- 可被替换的 Godot object 字段必须同时维护 ownership 标记，例如 `_ownsRegistry` / `_ownsRng` / `_ownsTerrainGenerator`。
- 替换引用必须走表达 ownership 的方法，例如 `BindBorrowed...`、`SetOwned...`、`Replace...ForTests`，不能直接 public set。
- 内容修改必须走领域 owner/service API；外部不应拿到 mutable `RefCounted` 后直接改正式 runtime 状态。
- `Resource` 内容定义默认是 shared catalog content。runtime、UI、validator 和 service 只能借用，不能 dispose。
- 测试 fixture 可以深度 dispose 自己创建的状态图，但 helper 名称必须表达 test-owned，不应复用到生产路径。

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
- `SettlementForgeService` 自己创建 `RecipeContentRegistry`，自己 dispose。
- `BattleBarrierService` 自己创建 `BarrierContentRegistry`，自己 dispose。
- `BattleSimContentProvider` 自己创建 progression/enemy registry，自己 dispose。

需要整理：

- `CharacterCreationWindow` 默认创建 `ProgressionContentRegistry`，随后又可被 `LoginScreen` 注入 session registry；当前没有 `_ownsProgressionContentRegistry` 标记。
- `GameSession.SetItemContentRegistryForTests(...)` 替换 `_item_content_registry` 前没有处理旧 owned registry，测试路径 ownership 需要明确。
- 生产代码里残留通用 `DisposeOwned<T where T : GodotObject>` helper，语义过宽，后续容易误用于 borrowed object。

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

- `GameRuntimeFacade.Dispose()` 清 `_party_state` / `_battle_state` 引用，不递归 dispose。
- `BattleRuntimeModule.DisposeManagedRuntime()` 清 `_state` 拓扑和引用，不递归释放 shared content。

需要整理：

- `BattleSimFormalCombatFixture` 和 `tests/shared/BattleTestFixture` 有深度 dispose helper。这些 helper 只适合 test-owned 状态图，名称和注释需要明确，避免被生产路径复用。
- 所有 plain C# service 持有 `PartyState` / `BattleState` / `BattleUnitState` 字段时，应确认 dispose 只清绑定，不释放对象。

### Owned Godot Utility Objects

代表类型：

- `RandomNumberGenerator`
- 临时 `Godot.Collections.Dictionary` / `Array` 不是 Godot object，但持有 Variant/RefCounted 时同样要注意引用语义。

规则：

- 自己 `new RandomNumberGenerator()` 的 class 应实现 `IDisposable` 或确保生命周期很短且不缓存 wrapper。
- 外部注入 RNG 时默认 borrowed，不释放。
- 允许用 CLR `Random` 或 deterministic typed RNG 替换 Godot RNG，减少 Godot wrapper 生命周期面。

需要整理：

- `EquipmentTraitRollService` 自己创建 fallback RNG，但没有 `IDisposable` 和 `_ownsRng`。
- `EquipmentDropService` 自己创建 fallback RNG，但没有 `IDisposable` 和 `_ownsRng`。
- `BattleTerrainGenerator.Dispose()` 只设置 `IsDisposed`，未处理自建 RNG。
- `WorldMapSpawnSystem` 是 transient plain C# helper，但持有自建 RNG；需要改为 `IDisposable`、改用非 Godot RNG，或确认通过短生命周期局部使用规避。
- `CharacterCreationWindow` 自建 RNG；作为 scene node 字段，可以随 node 生命周期，但仍应在 `_ExitTree()` 明确处理或避免 Godot RNG 字段长期悬挂。
- `FateAttackFormula.GodotRandomRollSource` 和 `FortuneService.SeededGodotRollSource` 内部自建 RNG，但 roll source 没有 dispose；如果是短生命周期对象，应文档化为 ephemeral，或改用非 Godot RNG。

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

### 1. CharacterCreationWindow registry ownership

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

### 2. EquipmentTraitRollService RNG ownership

文件：

- `scripts/systems/inventory/EquipmentTraitRollService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`

问题：

- service 内部会 fallback 创建 `RandomNumberGenerator`。
- `GameRuntimeFacade.GetEquipmentTraitRollService()` 在 session/catalog revision 变化时直接替换旧 service。
- 旧 service 里的 self-owned RNG 没有确定释放路径。

目标：

- `EquipmentTraitRollService : IDisposable`。
- 字段：`private bool _ownsRng;`
- 注入 RNG 为 borrowed；fallback RNG 为 owned。
- `ConfigureRng(...)` 替换 RNG 前释放旧 owned RNG。
- `GameRuntimeFacade` 替换 `_equipment_trait_roll_service` 前 dispose 旧 service；facade dispose 时也 dispose。

建议测试：

- 新增 inventory service lifecycle regression，覆盖 fallback RNG、borrowed RNG、重复 Configure、facade catalog revision rebuild。

### 3. EquipmentDropService RNG ownership

文件：

- `scripts/systems/inventory/EquipmentDropService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

问题：

- service 可注入 RNG，也会 fallback 创建 RNG。
- 没有 `_ownsRng` 和 dispose。
- `BattleRuntimeModule.setup(...)` 外部注入 `EquipmentDropService` 时，应默认 caller-owned；module 自建时 module-owned。

目标：

- `EquipmentDropService : IDisposable`。
- 区分 injected RNG borrowed 与 fallback RNG owned。
- `BattleRuntimeModule` 增加 `_ownsEquipmentDropService` 或明确不 owns injected service，只 dispose self-created service。
- `GameRuntimeFacade` 对自己字段 `_equipment_drop_service = new()` 负责 dispose。

建议测试：

- battle loot/drop lifecycle regression，覆盖 module 自建与注入 service 两种路径。

### 4. BattleTerrainGenerator RNG cleanup

文件：

- `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `tests/battle_runtime/runtime/run_battle_runtime_terrain_generator_ownership_regression.cs`

问题：

- `BattleRuntimeModule` 已经有 terrain generator ownership flag，这是正确方向。
- `BattleTerrainGenerator.Dispose()` 目前只设置 `IsDisposed`，未释放 self-owned `RandomNumberGenerator`。

目标：

- `BattleTerrainGenerator.Dispose()` 释放 self-owned RNG 或改用非 Godot RNG。
- 保留现有 module-owned vs caller-owned generator 行为。

建议测试：

- 扩展现有 terrain generator ownership regression，验证 dispose 后 generator 标记和 owned RNG 清理行为。

### 5. WorldMapSpawnSystem transient Godot object fields

文件：

- `scripts/systems/world/WorldMapSpawnSystem.cs`
- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/world/WorldMapDataContext.cs`

问题：

- `WorldMapSpawnSystem` 是 plain C# transient helper，但持有 self-owned RNG 和 loaded resource 引用。
- loaded resources 是 borrowed/shared，不应 dispose。
- RNG 是 owned wrapper，当前没有显式 cleanup。

目标：

- 方案 A：实现 `IDisposable`，调用点改为 `using var spawnSystem = new WorldMapSpawnSystem();`，dispose 只释放 RNG / 清引用，不释放 loaded resources。
- 方案 B：改为 CLR RNG，消除 Godot RNG 字段。

建议：

- 优先方案 B，如果不影响 deterministic API。

### 6. Generic GodotObject dispose helpers

文件：

- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `tests/shared/GodotSharpCleanup.cs`
- `tests/shared/BattleTestFixture.cs`

问题：

- `DisposeOwned<T where T : GodotObject>` 语义过宽。
- 生产代码里的通用 helper 容易被误用于 borrowed `Resource` / runtime state。
- 测试 helper 可以存在，但必须明确 test-owned。

目标：

- 生产代码中删除未使用的通用 helper。
- `GameSession` helper 改名为 `DisposeOwnedRegistry` / `DisposeOwnedGodotService` 等窄语义方法。
- 测试 helper 改名或注释为 `DisposeTestOwned...`。

### 7. BattleSimFormalCombatFixture deep dispose boundary

文件：

- `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`

问题：

- fixture 会递归 dispose `PartyState` 及其子状态。
- 这只在 fixture 自己构建 roster 状态图时安全。
- 如果未来允许注入 external party state，这条 dispose 会误杀外部 owner 的对象。

目标：

- 添加 `_ownsPartyState` 或让 fixture 不支持外部注入。
- 将 deep dispose helper 命名为 test/formal-fixture-owned。

### 8. Test direct registry construction cleanup

文件范围：

- `tests/**/*.cs`

问题：

- 多处测试 `new ProgressionContentRegistry()` / `new ItemContentRegistry()` / `new BattleSpecialProfileRegistry()` 后没有统一 dispose。
- 有些是短命令 runner 退出时靠进程清理，有些会在长测试中积累 Godot wrapper。

目标：

- 测试中 registry 优先 `using var`。
- 若返回 typed dictionary 不能马上 dispose registry，应明确 copied snapshot vs borrowed resource 引用。

## 当前可接受但需要保持的模式

- `GameContentCatalog` 持有 typed snapshot，但 root dispose 只 `ClearSessionBinding()`，不释放 shared content。
- `GameRuntimeFacade.Dispose()` 释放 plain C# helper 并清 `_party_state` / `_battle_state` 引用，不递归 dispose。
- `BattleRuntimeModule` 对 `BattleTerrainGenerator` 已经区分 module-owned 与 caller-owned。
- `SettlementForgeService` / `SettlementShopService` 自己创建 Godot helper 时自己释放。
- `HeadlessGameTestSession` owns `GameSession` 时走 `GameSession.Dispose()`，不绕过 log sink cleanup。

## 建议修复顺序

1. 建立小型 ownership helper / 命名规范，不引入复杂框架。
2. 修 `CharacterCreationWindow` registry ownership。
3. 修 `EquipmentTraitRollService` RNG ownership，并调整 `GameRuntimeFacade` 替换路径。
4. 修 `EquipmentDropService` ownership，并调整 `GameRuntimeFacade` / `BattleRuntimeModule`。
5. 修 `BattleTerrainGenerator.Dispose()`。
6. 决定 `WorldMapSpawnSystem` 使用 `IDisposable` 还是 CLR RNG。
7. 删除或收窄生产代码通用 `DisposeOwned<T GodotObject>`。
8. 标注 formal fixture / shared test fixture 的 test-owned deep dispose 边界。

## 回归建议

- `dotnet build magic.csproj`
- `godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs`
- `godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs`
- `godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_terrain_generator_ownership_regression.cs`
- 针对新增 ownership helper 增加最小 lifecycle regression，验证 borrowed object 不被 dispose、owned object 会被 dispose。

## Project Context Units Impact

当前文档是 lifecycle audit，不改变 runtime ownership 边界本身。后续如果实际修改 `GameRuntimeFacade`、`BattleRuntimeModule`、`GameSession`、`CharacterCreationWindow` 或 content registry 的主要生命周期关系，需要同步更新 `docs/design/project_context_units.md` 中 CU-01、CU-02、CU-06、CU-10、CU-15 或 CU-21 的生命周期约束。
