# 架构问题修复详细方案

日期：2026-06-17

## 背景

本方案来自 2026-06-17 的只读架构审查，覆盖 `GameSession`、`GameRuntimeFacade`、`BattleRuntimeModule`、`CharacterManagementModule`、settlement command、测试基础设施和 typed 迁移边界。

审查结论集中在五类结构债：

1. `GameSession`、`GameRuntimeFacade`、`BattleRuntimeModule` 仍是超级 owner，生命周期和依赖顺序过度集中。
2. `CharacterManagementModule` 同时承担身份、奖励、成长、属性、任务和战斗写回，文件过大且热路径重复创建服务。
3. typed 迁移目标与实际实现存在回流，部分核心结果仍以 `Godot.Collections.Dictionary` 承载并被核心逻辑回读。
4. 测试大量直接写 `_state`、`_party_state`、`_skill_defs` 等内部字段，缺少稳定 public fixture 和装配 API。
5. battle sidecar 由 `BattleRuntimeModule` 手工装配，部分 helper 生命周期不完整，扩展和独立测试成本高。

本方案不是一次性大重写计划，而是把这些问题拆成可独立合入、可回归、可停止的修复工作包。

## 关联文档

- `docs/design/project_context_units.md`
- `docs/design/csharp_migration.md`
- `docs/design/2026-06-17-type-field-ownership-refactor-plan.md`
- `docs/discussions/test_infrastructure_and_character_management_review.md`
- `docs/discussions/scripts_cs_full_review_2026_06_17.md`

其中 `project_context_units.md` 继续只作为读图索引，不承载本方案的迁移细节。本方案落地时，只有实际改变 CU 职责、owner 边界或推荐读集，才需要同步更新 `project_context_units.md`。

## 总目标

把项目主干从“Godot autoload + 大 facade + 大 runtime + dictionary payload”逐步收敛为：

```text
GameSession(Node autoload)
  -> Session/Save adapter
  -> GameRoot
  -> GameContentCatalog typed snapshot

GameRuntimeFacade
  -> Runtime coordinator
  -> typed command handlers
  -> RuntimeTransaction
  -> domain services

BattleRuntimeModule
  -> battle command / advance / orchestration
  -> BattleRuntimeServices factory-owned sidecars
  -> typed state/result DTOs

UI
  -> view model / snapshot projection
  -> typed commands
  -> no direct persistence ownership
```

## 非目标

1. 不在本方案中实现新玩法、新 UI 体验或数值调参。
2. 不新增旧 schema 兼容、fallback、alias、string-key 宽容读取；涉及兼容策略必须单独确认。
3. 不把所有 Godot `Dictionary` 一次性清空。Godot payload 可保留在 scene、UI、serializer、public Godot API 和导出边界。
4. 不为了减少测试文件数量做纯测试迁移。测试改动必须服务于生产边界收紧。
5. 不在单个 PR 同时改 save schema、battle runtime、UI 和测试基础设施。

## 硬约束

### Typed 迁移约束

生产核心逻辑不得新增：

- `GodotObject.Call()` / `.Set()` 作为业务模型访问方式
- typed -> `GDictionary` -> typed 的往返
- 为了核心逻辑回读而新增的 `ToDictionary()`
- `Variant` / `GDictionary` request-result 业务状态
- `RefCounted` / `[GlobalClass]` DTO

允许保留：

- `.tres` 内容资源字段
- save payload
- UI/window payload
- public Godot API 投影
- headless snapshot 输出
- trace/report export

### 测试约束

新测试和迁移后的测试不得直接写：

- `BattleRuntimeModule._state`
- `GameRuntimeFacade._party_state`
- `GameSession._active_save_id` / `_world_data` / `_quest_defs`
- content registry 内部缓存字段
- battle runtime 私有/内部 resolver 状态

例外只能是“正在验证当前 owner 自身的 lifecycle / serialization restore API”，并且需要在测试名或注释中说明为什么不能走 public fixture。

### 工作区约束

当前工作树已有未提交修改。本方案落地时，各工作包必须先确认目标文件是否有 unrelated dirty changes；如果有，避开或先协调，不覆盖已有改动。

## 修复工作包总览

| 工作包 | 名称 | 主要 CU | 风险 | 收益 |
| --- | --- | --- | --- | --- |
| WP0 | 公共测试 fixture 和装配 API | CU-19、CU-21、CU-15 | 低 | 解锁后续拆分，减少内部字段依赖 |
| WP1 | Payload reader 与 typed command result | CU-06、CU-15、CU-21 | 中 | 阻断 dictionary 往返扩散 |
| WP2 | Settlement result typed 化 | CU-06、CU-08、CU-12 | 中 | 清掉一个典型 typed 回流点 |
| WP3 | `CharacterManagementModule` 轻量拆分 | CU-11、CU-12、CU-14、CU-15 | 中 | 降低最大成长 owner 复杂度 |
| WP4 | `RuntimeTransaction` 写回收口 | CU-02、CU-06、CU-12、CU-15 | 中高 | 降低漏保存和部分提交风险 |
| WP5 | Battle sidecar lifecycle/factory | CU-15、CU-16、CU-19 | 中 | 明确 battle runtime 服务图和释放规则 |
| WP6 | GameSession 收缩第一阶段 | CU-02、CU-13、CU-20 | 中高 | 降低全局 session 职责和内容 IO 耦合 |
| WP7 | UI/runtime view model 边界 | CU-06、CU-07、CU-18、CU-21 | 高 | 让 UI 不再拥有 persistence/runtime 真相源 |
| WP8 | 工具链与架构检查 | CU-19 | 低 | 让迁移边界可持续检查 |

推荐顺序：`WP0 -> WP1 -> WP2 -> WP3 -> WP5 -> WP4 -> WP6 -> WP8 -> WP7`。

`WP7` 收益很高，但触碰 UI 和场景行为，应该等 command/result 和 fixture 基础稳定后再做。

## WP0：公共测试 fixture 和装配 API

### 目标

建立 public behavior 驱动的测试基础设施，先停止新增测试直接写内部字段，再逐步迁移最常用的旧测试模式。

### 文件范围

- `tests/shared/*`
- `tests/battle_runtime/**/*`
- `tests/runtime/**/*`
- `tests/world_map/runtime/**/*`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
- `scripts/systems/persistence/GameSession.cs`

### 设计

新增共享测试工具：

```text
tests/shared/
  BattleTestFixture.cs
  RuntimeTestFixture.cs
  SessionTestFixture.cs
  StubRng.cs
  BattleTestAssertions.cs
```

建议 API：

```csharp
public sealed class BattleTestFixture
{
    public BattleRuntimeModule Runtime { get; }
    public BattleState State { get; }

    public static BattleTestFixture CreateFlatBattle(
        Vector2I mapSize,
        IEnumerable<BattleUnitState> allies,
        IEnumerable<BattleUnitState> enemies
    );

    public BattleTestFixture WithSkillDefs(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    );

    public BattlePreview Preview(BattleCommand command);
    public BattleEventBatch Issue(BattleCommand command);
}
```

生产侧需要提供窄的测试可用 public/internal API，而不是让测试写字段：

```csharp
internal void SetupStateForTests(BattleState state);
internal void ReplaceContentForTests(BattleRuntimeContent content);
```

如果项目不希望生产类型暴露 `ForTests` 方法，则放到 `tests/shared` 的 fixture builder 里，通过正式 `setup(...)` / `StartBattle...` API 构造。只有当正式 API 不足以构造必要状态时，才补 production API。

### 迁移优先级

第一批迁移：

- `tests/shared/run_shared_test_fixture_regression.cs`
- 直接出现 `runtime._state = state` 的 battle runtime 测试
- 直接填充 `GameSession._active_save_id` / `_world_data` 的 runtime fixture

### 验收

1. 新增 fixture 自身回归通过。
2. `tests/shared/run_shared_test_fixture_regression.cs` 不再直接写 `runtime._state`。
3. 新增检查脚本能列出仍直接写内部字段的测试，并输出白名单/待迁移清单。
4. 后续工作包新增测试默认使用 shared fixture。

### 回归建议

- `godot --headless -s res://tests/shared/run_shared_test_fixture_regression.cs`
- 选取 3 到 5 个迁移后的 battle/runtime 测试单跑。

## WP1：Payload reader 与 typed command result

### 目标

把重复的 `ReadString` / `GetDict` / `ReadDictionaryItems` 收敛到统一边界，避免各层对同一 payload 字段有不同容错语义。

### 文件范围

- `scripts/utils/GodotVariantReadExtensions.cs`
- 新增 `scripts/utils/VariantReader.cs` 或 `scripts/utils/PayloadReader.cs`
- `scripts/systems/game_runtime/*CommandHandler.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/persistence/GameSession.cs`
- `scripts/ui/BattleMapPanel.cs`
- `scripts/utils/GameTextSnapshotRenderer.cs`

### 设计

新增 reader：

```csharp
internal readonly struct PayloadReadResult<T>
{
    public bool Ok { get; }
    public T Value { get; }
    public string Error { get; }
}

internal static class PayloadReader
{
    public static string String(GDictionary source, string key, string fallback = "");
    public static StringName StringNameStrict(GDictionary source, string key);
    public static int Int(GDictionary source, string key, int fallback = 0);
    public static bool Bool(GDictionary source, string key, bool fallback = false);
    public static Vector2I Vector2I(GDictionary source, string key, Vector2I fallback);
    public static IEnumerable<GDictionary> Dictionaries(GArray source);
}
```

读取策略要分两类：

1. `Strict`：正式 command/input 边界，只接受正式类型，不做 string/StringName 双向宽容。
2. `LenientProjection`：UI/snapshot 展示边界可容错显示，但不能回流到核心业务。

### 禁止事项

不新增通用 `ReadAnyId(...)` 来恢复 string-key fallback。正式内容 ID 仍以 `StringName` 为 owner。

### 验收

1. settlement command、battle start options、snapshot renderer 至少各迁一个调用族。
2. `scripts/` 中重复 reader 不再增长。
3. 新增 `PayloadReader` regression：strict StringName 拒绝 string，projection reader 可以显示 string。

### 回归建议

- `dotnet build magic.csproj`
- settlement command 相关 runner
- battle start/options 相关 runner
- text runtime snapshot runner

## WP2：Settlement result typed 化

### 目标

把 `SettlementServiceResult` 从“半 typed + 半 `GDictionary`”改为正式 typed result，只在 UI/snapshot 边界投影 dictionary。

### 当前问题

`SettlementServiceResult` 当前仍包含：

- `SetInventoryDelta(GDictionary)`
- `SetServiceSideEffects(GDictionary)`
- `ToDictionary()`

`GameRuntimeSettlementCommandHandler` 当前仍有 `BuildRuntimeCommandResult(GDictionary)`，并且 `_dispatch_settlement_action(...)` 返回 dictionary command result。

### 文件范围

- `scripts/systems/settlement/SettlementServiceResult.cs`
- `scripts/systems/settlement/SettlementShopTradeResult.cs`
- `scripts/systems/settlement/SettlementForgeService.cs`
- `scripts/systems/settlement/SettlementResearchService.cs`
- `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`
- `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs`

### 设计

新增 typed DTO：

```csharp
public sealed class SettlementInventoryDelta
{
    public StringName RecipeId { get; init; } = "";
    public IReadOnlyList<WarehouseInventoryEntry> AddedItems { get; init; }
    public IReadOnlyList<WarehouseInventoryEntry> RemovedItems { get; init; }
    public IReadOnlyList<EquipmentInstanceState> AddedEquipment { get; init; }
    public IReadOnlyList<StringName> RemovedEquipmentInstanceIds { get; init; }
}

public sealed class SettlementServiceSideEffects
{
    public IReadOnlyList<StringName> RevealedWorldEventIds { get; init; }
    public IReadOnlyList<Vector2I> RevealedCoords { get; init; }
    public IReadOnlyDictionary<string, string> DisplayFacts { get; init; }
}
```

`SettlementServiceResult` 变为：

```csharp
public sealed class SettlementServiceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool PersistPartyState { get; init; }
    public bool PersistWorldData { get; init; }
    public bool PersistPlayerCoord { get; init; }
    public int GoldDelta { get; init; }
    public SettlementInventoryDelta InventoryDelta { get; init; }
    public IReadOnlyList<PendingCharacterReward> PendingCharacterRewards { get; init; }
    public IReadOnlyList<QuestProgressService.QuestProgressEventData> QuestProgressEvents { get; init; }
    public SettlementServiceSideEffects SideEffects { get; init; }
}
```

`ToDictionary()` 移到投影类：

```csharp
internal static class SettlementServiceResultProjection
{
    internal static GDictionary ToDictionary(SettlementServiceResult result);
}
```

`BuildRuntimeCommandResult(GDictionary)` 删除或只保留在 legacy adapter 白名单中，不再被 typed command path 调用。

### 分步

1. 添加 typed DTO 和 projection，不改行为。
2. 修改 forge/research/rest/fog reveal 构造 typed result。
3. 修改 `FinalizeSuccessfulActionTyped(...)` 直接读 typed fields。
4. 删除 handler 内部 result dictionary 回读路径。
5. 更新测试断言从 dictionary payload 改为 typed command/result 或 public state。

### 验收

1. `SettlementServiceResult` 不再持有 `GDictionary` 字段。
2. `GameRuntimeSettlementCommandHandler` typed path 不再调用 `BuildRuntimeCommandResult(GDictionary)`。
3. settlement action、forge、research、contract board 回归通过。
4. `ToDictionary()` 只存在 projection/adapter。

## WP3：`CharacterManagementModule` 轻量拆分

### 目标

先做低风险拆分和 service 复用，减少 4296 行大 owner 的压力，同时不改变成长写入唯一入口的架构定位。

### 文件范围

- `scripts/systems/progression/CharacterManagementModule.cs`
- `scripts/systems/progression/ProgressionService.cs`
- `scripts/systems/progression/ProfessionAssignmentService.cs`
- `scripts/systems/progression/SkillMergeService.cs`
- `scripts/systems/progression/ProfessionRuleService.cs`
- 新增 `scripts/systems/progression/CharacterRewardProcessor.cs`
- 新增 `scripts/systems/progression/CharacterIdentityManager.cs`
- 新增或迁移 `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- `tests/progression/**/*`
- `tests/runtime/facade/**/*`
- `tests/battle_runtime/runtime/**/*`

### 子目标 A：消除 transient service 创建

当前 `BuildProgressionService(UnitProgress)` 每次 new：

- `ProfessionAssignmentService`
- `SkillMergeService`
- `ProfessionRuleService`
- `ProgressionService`

第一步不要直接复用同一个 `ProgressionService` 实例处理不同 `UnitProgress`，先引入 factory/cache：

```csharp
internal sealed class ProgressionServiceFactory
{
    public ProgressionService Build(
        UnitProgress progression,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs
    );
}
```

如果 service 内部无 per-call mutable state，再改为 owner 级实例 + `SetupProgression(progression)`。这个判断必须通过回归证明。

### 子目标 B：提取战斗写回

从 `CharacterManagementModule` 移出：

- `CommitBattleResources`
- `CommitBattleDeath`
- `CommitBattleKo`
- `FlushAfterBattle`
- `_salvage_member_equipment`

目标类：

```csharp
internal sealed class CharacterBattleWritebackService
{
    public void Setup(
        PartyState partyState,
        PartyWarehouseService warehouseService,
        Func<StringName, AttributeSnapshot> attributeSnapshotProvider
    );

    public void CommitResources(StringName memberId, int hp, int mp, int aura);
    public void CommitDeath(StringName memberId);
    public int FlushAfterBattle();
}
```

`CharacterManagementModule` 继续暴露原 public 方法作为薄转发，避免一次性改所有调用点。

### 子目标 C：提取奖励处理

把 `ApplyPendingCharacterReward(...)` 和 pending reward normalize/sort/apply 相关方法迁到 `CharacterRewardProcessor`。

约束：

- Processor 不直接拥有 `PartyState` 全局可变字段。
- Processor 通过窄接口调用 skill/progression/attribute 服务。
- 输出 `CharacterProgressionDelta`，由 `CharacterManagementModule` 负责最终队列移除和成就事件归并。

### 子目标 D：提取身份管理

把 race/subrace/bloodline/ascension/stage advancement/body size/age projection 相关 getter、apply、revoke 汇总到 `CharacterIdentityManager`。

约束：

- 不改变 identity content catalog 输入。
- 不恢复 `GDictionary` content-source。
- 所有成员状态修改走 `PartyMemberState` owner 接口，配合 `2026-06-17-type-field-ownership-refactor-plan.md`。

### 验收

1. `CharacterManagementModule.cs` 行数下降，且 public surface 不新增大批 dictionary 方法。
2. `BuildProgressionService` 不再在热路径重复 new 四个 service，或明确封装到 factory 并有测试覆盖。
3. battle writeback、pending reward、identity apply 都有独立 regression。
4. 现有 headless/text runtime 奖励链通过。

## WP4：`RuntimeTransaction` 写回收口

### 目标

把 `GameRuntimeFacade` 和 command handler 中分散的 `SetPartyState` / `SetWorldData` / `SetPlayerCoord` / flush 统一成事务提交入口，降低漏持久化和部分提交风险。

### 文件范围

- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`
- `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`
- `scripts/systems/persistence/GameSession.cs`
- `tests/runtime/facade/**/*`
- `tests/world_map/runtime/**/*`
- `tests/text_runtime/**/*`

### 设计

新增：

```csharp
internal sealed class RuntimeTransaction
{
    public bool PersistPartyState { get; private set; }
    public bool PersistWorldData { get; private set; }
    public bool PersistPlayerCoord { get; private set; }

    public RuntimeTransaction MarkPartyChanged();
    public RuntimeTransaction MarkWorldChanged();
    public RuntimeTransaction MarkPlayerCoordChanged();

    public RuntimeCommitResult Commit(GameSession session, RuntimeStateSource source);
}

internal sealed class RuntimeCommitResult
{
    public bool Ok { get; init; }
    public int PartyError { get; init; }
    public int WorldError { get; init; }
    public int PlayerError { get; init; }
    public string Message { get; init; }
}
```

`RuntimeStateSource` 只暴露提交所需状态：

```csharp
internal interface RuntimeStateSource
{
    PartyState GetPartyStateForCommit();
    GDictionary GetWorldDataForCommit();
    Vector2I GetPlayerCoordForCommit();
}
```

### 分步

1. 只添加 transaction，并让现有 `PersistChangesTyped(...)` 调用它。
2. settlement finalize 改为生成 transaction flags。
3. battle loot/writeback 改为生成 transaction flags。
4. facade command 中的直接 session 写回逐步替换。

### 验收

1. `GameRuntimeFacade` 中直接调用 session set/flush 的位置减少。
2. 所有 command result 能区分业务失败和 persistence failure。
3. settlement、battle end、quest、warehouse command 在 persistence failure 下行为一致。

## WP5：Battle sidecar lifecycle/factory

### 目标

让 `BattleRuntimeModule` 不再手工承担所有 sidecar 的创建、setup、dispose 细节；先解决生命周期不完整和测试装配困难，再考虑进一步拆 orchestration。

### 文件范围

- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleMovementQueryService.cs`
- `scripts/systems/battle/runtime/*Service.cs`
- `scripts/systems/battle/rules/*.cs`
- `scripts/systems/battle/ai/*.cs`
- `tests/battle_runtime/runtime/**/*`
- `tests/battle_runtime/ai/**/*`

### 设计

新增：

```csharp
internal sealed class BattleRuntimeServices : IDisposable
{
    public BattleMovementService Movement { get; }
    public BattleGroundEffectService GroundEffects { get; }
    public BattleSpecialSkillResolver SpecialSkills { get; }
    public BattleMovementQueryService AiMovementQuery { get; }
    public BattleAiQueryService AiQuery { get; }
    public BattleAiCandidateEvaluationService AiCandidateEvaluation { get; }

    public void Setup(BattleRuntimeServiceContext context);
    public void ClearRuntimeBindings();
    public void Dispose();
}
```

`BattleRuntimeServiceContext` 包含：

- `BattleState`
- `BattleGridService`
- typed skill/item/enemy indexes
- character gateway
- equipment drop service
- callbacks

### 立即修复项

`BattleMovementQueryService` 当前保存 `_state`、`_gridService`、`_moveCostProvider` 和多组 cache，应增加：

```csharp
internal void ClearRuntimeBindings()
{
    _state = null;
    _gridService = null;
    _moveCostProvider = null;
    _mapSize = Vector2I.Zero;
    _cells = Array.Empty<CellInfo>();
    _units.Clear();
    _edges.Clear();
    _distanceFromAnchorToTargetCache.Clear();
    _pathTargetQueryCache.Clear();
    _moveCostSignatureCache.Clear();
    _snapshotRevision = long.MinValue;
}

protected override void Dispose(bool disposing)
{
    if (disposing)
        ClearRuntimeBindings();
    base.Dispose(disposing);
}
```

并由 `BattleRuntimeModule.DisposeManagedRuntime()` 调用。

### 验收

1. battle runtime dispose 后，AI movement query 不保留旧 state/grid/delegate/cache。
2. `BattleRuntimeModule` 构造函数和 dispose 代码变短，sidecar ownership 集中在 `BattleRuntimeServices`。
3. 至少一个 AI decision fixture 可以通过 services factory 单独装配。

## WP6：GameSession 收缩第一阶段

### 目标

不立刻拆掉 autoload，而是把内容 bootstrap 和 save repository 先从 `GameSession` 中分离，让 `GameSession` 变成生命周期和门面适配器。

### 文件范围

- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/persistence/SaveSerializer.cs`
- `scripts/systems/content/GameRoot.cs`
- `scripts/systems/content/GameContentCatalog.cs`
- `scripts/player/progression/*ContentRegistry.cs`
- `scripts/player/warehouse/*ContentRegistry.cs`
- `scripts/enemies/EnemyContentRegistry.cs`
- `tests/runtime/persistence/**/*`
- `tests/runtime/validation/**/*`

### 设计

新增：

```csharp
internal sealed class SaveRepository
{
    public SaveLoadResult LoadSlot(string saveId);
    public SaveWriteResult WritePartyState(string saveId, PartyState partyState);
    public SaveWriteResult WriteWorldData(string saveId, GDictionary worldData);
    public SaveWriteResult WritePlayerCoord(string saveId, Vector2I playerCoord);
}

internal sealed class ContentBootstrapper
{
    public ContentBootstrapResult BuildCatalog();
}

internal sealed class RuntimeSessionState
{
    public string ActiveSaveId { get; }
    public PartyState PartyState { get; }
    public GDictionary WorldData { get; }
    public Vector2I PlayerCoord { get; }
}
```

第一阶段不改变 save payload schema，只改变内部 owner：

- `GameSession` 仍是 autoload。
- `GameSession` 仍暴露现有 public methods。
- `SaveRepository` 负责文件读写。
- `ContentBootstrapper` 负责 registry refresh 和 catalog rebuild。

### 验收

1. `GameSession` 构造函数不直接负责所有内容刷新细节。
2. save/load regression 无行为变化。
3. content catalog regression 无行为变化。
4. 测试不再通过直接写 `GameSession._...` 构造 active world；改用 `SessionTestFixture` 或 public setup helper。

## WP7：UI/runtime view model 边界

### 目标

让 UI 消费 view model 和发 typed command，不直接拥有 `GameSession` 或操作 runtime 真相源。

### 文件范围

- `scripts/systems/game_runtime/WorldMapSystem.cs`
- `scripts/ui/WorldMapView.cs`
- `scripts/ui/BattleMapPanel.cs`
- `scripts/ui/BattleHudAdapter.cs`
- `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`
- `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- `tests/world_map/ui/**/*`
- `tests/battle_runtime/rendering/**/*`
- `tests/text_runtime/**/*`

### 设计

新增 view model：

```csharp
internal sealed class WorldRuntimeViewModel
{
    public Vector2I PlayerCoord { get; init; }
    public Vector2I SelectedCoord { get; init; }
    public RuntimeModalKind ActiveModalKind { get; init; }
    public IReadOnlyList<WorldEventViewModel> NearbyEvents { get; init; }
}

internal sealed class BattleRuntimeViewModel
{
    public BattleHudViewModel Hud { get; init; }
    public BattleBoardViewModel Board { get; init; }
    public BattlePreviewViewModel Preview { get; init; }
}
```

`WorldMapSystem` 只负责：

- scene node 获取
- signal 连接
- view model 渲染
- command 转发

禁止：

- UI 直接写 party/world/battle state
- UI 直接读取 `GameSession`
- UI 自行计算命中、伤害、射程、目标合法性

### 分步

1. `BattleHudAdapter` 保持只读 adapter，明确输入为 `BattlePreview` / `BattleRuntimeViewModel`。
2. `WorldMapRuntimeProxy` 增加 typed command + typed view query。
3. `BattleMapPanel` 移除对 `GameSession` 的直接依赖。
4. `WorldMapSystem` 逐步只持有 proxy，不持有 facade/session。

### 验收

1. UI regression 可以用 fake proxy/view model 测主要展示逻辑。
2. `BattleMapPanel` 不再通过 session/runtime 重算战斗规则。
3. headless snapshot 与 UI view model 的字段来源一致。

## WP8：工具链与架构检查

### 目标

让架构修复成果持续可检查，避免后续改动重新引入 dictionary 回读、内部字段测试写入和工具脚本漂移。

### 文件范围

- `tools/*`
- `tests/run_regression_suite.py`
- 新增 `tools/architecture_checks.py`
- `AGENTS.md`
- 可选新增 `scripts/check`

### 检查项

`tools/architecture_checks.py` 输出：

1. `scripts/` 中新增 `.Set(`、`.Call(`。
2. `scripts/` 中新增 `ToDictionary()`，按白名单分类。
3. `scripts/` 核心 runtime 中新增 `GDictionary` 字段。
4. `tests/` 中直接写 `_state`、`_party_state`、`_skill_defs`、`_active_save_id`。
5. `scripts/` 中重复 payload reader helper。

建议白名单文件：

- serializer
- projection
- UI/window payload
- snapshot renderer
- trace export
- content Resource import

### 验收

1. `python tools/architecture_checks.py` 可运行并输出当前 baseline。
2. CI 或本地 `scripts/check` 能串联：
   - `dotnet build magic.csproj`
   - `python tests/run_regression_suite.py`
   - `python tools/architecture_checks.py`
3. 文档记录如何更新白名单。

## 里程碑与合入门

### Gate 1：测试基础稳定

必须满足：

- WP0 完成。
- 新增 fixture 能构造 battle/runtime/session 常用状态。
- 新增测试默认不写内部字段。

解锁：WP2、WP3、WP5 的安全拆分。

### Gate 2：dictionary 回流受控

必须满足：

- WP1 完成核心 reader。
- WP2 settlement typed result 完成。
- `SettlementServiceResult` 不再持有 `GDictionary` 字段。

解锁：RuntimeTransaction 和更多 command handler typed 化。

### Gate 3：大 owner 初步收缩

必须满足：

- WP3 的 battle writeback 和 reward processor 至少完成一个。
- WP5 的 sidecar lifecycle 修复完成。
- `CharacterManagementModule` 和 `BattleRuntimeModule` 的新增功能不再直接扩大核心文件。

解锁：WP4、WP6。

### Gate 4：事务和 session 收缩

必须满足：

- WP4 的 transaction 统一提交路径覆盖 settlement / battle / quest / warehouse 至少两个域。
- WP6 第一阶段完成，不改变 save schema。

解锁：WP7 UI/runtime 分离。

## 推荐首批 PR

### PR 1：Shared fixture baseline

范围：

- 新增 `BattleTestFixture`
- 迁移 `tests/shared/run_shared_test_fixture_regression.cs`
- 增加内部字段写入扫描脚本的只读报告模式

验收：

- shared regression 通过
- 脚本能列出待迁移测试

### PR 2：BattleMovementQuery lifecycle

范围：

- `BattleMovementQueryService.ClearRuntimeBindings()`
- `BattleRuntimeModule.DisposeManagedRuntime()` 调用清理
- lifecycle regression

验收：

- dispose 后不保留旧 state/grid/delegate/cache
- battle runtime lifecycle 测试通过

### PR 3：SettlementServiceResult typed inventory delta

范围：

- 新增 `SettlementInventoryDelta`
- forge/research/rest result 改 typed field
- `FinalizeSuccessfulActionTyped` 直接读 typed result

验收：

- settlement command handler regression 通过
- `SettlementServiceResult` 不再新增 dictionary 回读

### PR 4：Character battle writeback extraction

范围：

- 新增 `CharacterBattleWritebackService`
- `CharacterManagementModule.CommitBattle*` 改薄转发
- battle writeback regression

验收：

- 原 public API 行为不变
- `CharacterManagementModule` 行数下降

## 风险与应对

### 风险：过早拆 UI/runtime 导致大量场景回归

应对：WP7 放到后期，只在 typed command/result 和 fixture 成熟后开始。

### 风险：Transaction 引入后保存行为细节漂移

应对：WP4 第一阶段只让旧 `PersistChangesTyped` 走 transaction，不改变调用点语义；之后按域迁移。

### 风险：CharacterManagement 拆分破坏成长写入唯一入口

应对：拆出的 processor/manager 不作为外部 public owner 暴露；外部仍只通过 `CharacterManagementModule` 进入。

### 风险：Content bootstrapper 改动触发 save/schema 兼容问题

应对：WP6 第一阶段不改 payload schema，不添加旧内容 fallback，只改变内部 owner 和测试 fixture。

### 风险：检查脚本误伤合法 Godot 边界

应对：检查脚本先报告不失败，白名单稳定后再作为 gate。

## 完成定义

本方案完成时应满足：

1. 新测试不再依赖核心内部字段写入。
2. settlement command/result、battle helper、runtime command 的核心链不再 dictionary 往返。
3. `CharacterManagementModule` 至少拆出战斗写回和奖励处理之一，且 transient service 创建受控。
4. `BattleRuntimeModule` sidecar lifecycle 有集中 owner，dispose 后不保留旧 battle state。
5. `GameRuntimeFacade` 至少两个 command 域通过 `RuntimeTransaction` 提交。
6. `GameSession` 的内容 bootstrap 或 save repository 至少拆出一层，autoload 职责下降。
7. 工具链能检查 `.Call` / `.Set` / `ToDictionary` / 内部字段测试写入的新增风险。

## 2026-06-17 首批执行记录

本轮先落地低风险、可验证的架构治理切片，避免在同一批改动中同时拆大型 owner、save schema 和 UI 行为。

已完成：

1. 新增 `tools/architecture_checks.py`，默认只报告不失败，覆盖 `.Call` / `.Set`、核心 `ToDictionary()`、`GDictionary` 字段和测试内部字段写入。
2. 更新 `tools/README.md`，把架构扫描工具纳入 repo-level 开发工具索引。
3. 新增 `tests/battle_runtime/runtime/run_battle_movement_query_service_lifecycle_regression.cs`，覆盖 `BattleMovementQueryService.DisposeRuntime()` 后旧 `BattleState` 不再可读，并允许重新 `Setup()` 新状态。
4. 在 `AttackEffectResolutionResult` 增加到 Godot payload 的集中式过渡投影，复用 `AttackEffectResolutionResultReader.BuildGodotPayload()`，用于保持已迁移 typed resolver 与尚未迁移的 legacy `GDictionary` 调用点可编译。

验证结果：

```text
dotnet build magic.csproj
Build succeeded. 0 Warning(s), 0 Error(s).

godot --headless -s res://tests/battle_runtime/runtime/run_battle_movement_query_service_lifecycle_regression.cs
Battle movement query service lifecycle regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 863
```

后续注意：

- `AttackEffectResolutionResult` 的 implicit payload projection 只是过渡桥，不能作为新增核心逻辑的使用模式。后续 WP1/WP2 应继续把调用点改为直接读 typed field。
- 当前架构扫描基线仍然很高，工具应先用于 review 观察；启用 `--fail-on-findings` 前需要按域建立白名单或增量模式。

## 2026-06-17 WP0 继续执行记录

本轮继续推进公共测试 fixture 第一阶段。

已完成：

1. 新增 `tests/shared/BattleTestFixture.cs`，提供平面战场、unit 安装和 runtime 测试装配入口。
2. 新增 `tests/shared/StubRng.cs`，把 shared fixture 回归里的本地 RNG helper 抽成共享测试工具。
3. 在 `BattleRuntimeModule` 新增 `internal SetupStateForTests(BattleState state)`，作为测试 fixture 的唯一 state 安装入口，避免新测试继续直接写 `_state`。
4. 迁移 `tests/shared/run_shared_test_fixture_regression.cs`，不再直接写 `runtime._state`。
5. 迁移 `tests/battle_runtime/runtime/run_battle_spawn_side_regression.cs` 的 4 处直接 `_state` 写入，改走 `BattleTestFixture.CreateFlatBattle(...)`。

验证结果：

```text
dotnet build magic.csproj
Build succeeded. 0 Warning(s), 0 Error(s).

godot --headless -s res://tests/shared/run_shared_test_fixture_regression.cs
Shared test fixture regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_battle_spawn_side_regression.cs
Battle spawn side regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 858
tests internal field writes: 187
```

备注：

- `run_battle_spawn_side_regression.cs` 通过但 Godot headless 退出时仍输出 RefCounted leak warning；命令退出码为 0，后续可单独清理 headless Godot 生命周期噪声。
- 下一批 WP0 建议继续迁移 `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs` 或 AI runtime 测试族，但这两类测试更依赖 runtime 内部 sidecar，需要按子域拆分。

## 2026-06-17 WP0 第三批执行记录

本轮继续迁移单点 `_state` 安装测试，优先选择不改变领域 fixture 构造、只替换 runtime state 安装入口的用例。

已完成：

1. 迁移 `tests/battle_runtime/runtime/run_battle_metrics_collector_regression.cs`，改用 `BattleTestFixture.CreateFlatBattle(...)` 安装 metrics state。
2. 迁移 `tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.cs`，改用 `BattleTestFixture.CreateFlatBattle(...)` 安装 commit state。
3. 迁移 `tests/battle_runtime/runtime/run_battle_move_path_result_projection_regression.cs`，把 `runtime._state = state` 改为 `runtime.SetupStateForTests(state)`。
4. 迁移 `tests/battle_runtime/runtime/run_control_status_contract_regression.cs`，先构造 state/unit，再通过 `SetupStateForTests` 安装。
5. 迁移 `tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs`，改用 `SetupStateForTests` 安装。

验证结果：

```text
dotnet build magic.csproj
Build succeeded. 0 Warning(s), 0 Error(s).

godot --headless -s res://tests/battle_runtime/runtime/run_battle_metrics_collector_regression.cs
Battle metrics collector regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.cs
Meteor swarm commit payload boundary regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_battle_move_path_result_projection_regression.cs
Battle move path result projection regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_control_status_contract_regression.cs
Control status contract regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs
Temporal status semantics regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 853
tests internal field writes: 182
```

下一步：

- 继续 WP0 时，应优先迁移单文件多处 `_state` 写入的测试族，例如 fate/crown/doom 或 AI behavior。它们更可能需要扩展 `BattleTestFixture` 的 skill/fate/AI 内容装配能力。
- `SetupStateForTests` 是过渡测试入口，不应被生产代码调用；后续可用 analyzer 或扫描规则限制调用目录。

## 2026-06-17 WP0 第四批执行记录

本轮继续迁移 runtime 测试中的直接 battle state 安装点，仍然限定为不改变测试领域数据构造和断言语义。

已完成：

1. 迁移 `tests/battle_runtime/runtime/run_battle_change_equipment_requirement_regression.cs` 的 3 处 `runtime._state = state`，改为 `runtime.SetupStateForTests(state)`。
2. 迁移 `tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs` 的 3 处 state 安装，改为 `SetupStateForTests(state)`。
3. 同步迁移 `run_battle_ground_effect_typed_sets_regression.cs` cleanup 中的 `runtime._state = null`，改为 `SetupStateForTests(null)`。

验证结果：

```text
dotnet build magic.csproj
Build succeeded. 0 Warning(s), 0 Error(s).

godot --headless -s res://tests/battle_runtime/runtime/run_battle_change_equipment_requirement_regression.cs
Battle change equipment requirement regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs
Battle ground effect typed sets regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 848
tests internal field writes: 175
```

备注：

- 本轮只减少测试内部字段写入。`scripts GDictionary fields` 当前报告为 113，仍属于后续 WP1/WP2/WP8 处理面。
- runtime 测试里剩余 `_state` 直写主要集中在 validation projection、meteor/prismatic preview 和更高层的 fate/AI 测试，迁移前需要判断是否扩展 fixture 的 skill catalog、fate runtime 或 AI sidecar 装配能力。

## 2026-06-17 WP0 第五批执行记录

本轮在提交 `a70025a1 Add architecture remediation fixture groundwork` 后继续迁移 runtime 目录剩余的干净测试文件。

已完成：

1. 迁移 `tests/battle_runtime/runtime/run_battle_validation_result_projection_regression.cs`，把 2 处 state 安装和 2 处清空改为 `SetupStateForTests(...)`。
2. 迁移 `tests/battle_runtime/runtime/run_meteor_swarm_preview_surface_contract_regression.cs`，把 preview fixture 的 state 安装改为 `SetupStateForTests(state)`。

验证结果：

```text
dotnet build magic.csproj
Build succeeded. 0 Warning(s), 0 Error(s).

godot --headless -s res://tests/battle_runtime/runtime/run_battle_validation_result_projection_regression.cs
Battle validation result projection regression: PASS

godot --headless -s res://tests/battle_runtime/runtime/run_meteor_swarm_preview_surface_contract_regression.cs
Meteor swarm preview surface contract regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 843
tests internal field writes: 170
```

备注：

- `tests/battle_runtime/runtime/` 当前只剩 `run_prismatic_sphere_regression.cs` 仍直接写 `_state`；该文件已有并行脏改，暂不在本批处理。

## 2026-06-17 WP0 第六批执行记录

本轮从 runtime 目录转向当前干净的 rules/skills 测试文件，继续替换直接 battle state 安装点。

已完成：

1. 迁移 `tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs` 的 2 处 `runtime._state = state`，改为 `runtime.SetupStateForTests(state)`。
2. 迁移 `tests/battle_runtime/skills/run_titan_colossus_form_regression.cs` 的 2 处 `runtime._state = state`，改为 `runtime.SetupStateForTests(state)`。

验证结果：

```text
dotnet build magic.csproj
Build succeeded. 0 Warning(s), 0 Error(s).

godot --headless -s res://tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs
Battle hit preview contract regression: PASS

godot --headless -s res://tests/battle_runtime/skills/run_titan_colossus_form_regression.cs
Titan colossus form regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 839
tests internal field writes: 166
```

下一步：

- 剩余高收益区集中在 AI behavior、fate 技能族和 `run_status_effect_semantics_regression.cs`。这些文件通常有 3 到 5 处以上 `_state` 写入，但更依赖专用 sidecar/content 装配，迁移前应先确认文件是否已有并行改动。

## 2026-06-17 WP0 第七批执行记录

本轮继续处理当前没有并行 diff 的 terrain 测试。尝试迁移 `run_battle_ai_wait_behavior_regression.cs` 时发现 `SetupStateForTests` 的 sidecar refresh 会改变该 AI 决策测试的 action plan 选择，因此已撤回该文件改动，保留给后续单独设计 fixture 参数。

已完成：

1. 迁移 `tests/battle_runtime/terrain/run_battle_terrain_lifetime_regression.cs` 的 2 处 `runtime._state = state`，改为 `runtime.SetupStateForTests(state)`。
2. 迁移 `tests/battle_runtime/terrain/run_battle_terrain_topology_service_regression.cs` 的直接 state 安装，改为局部 `BattleState state` + `SetupStateForTests(state)`。
3. 迁移 `tests/battle_runtime/terrain/run_meteor_swarm_terrain_modifier_regression.cs` 的 state 安装，改为 `SetupStateForTests(state)`。

验证结果：

```text
godot --headless -s res://tests/battle_runtime/terrain/run_battle_terrain_lifetime_regression.cs
Battle terrain lifetime regression: PASS

godot --headless -s res://tests/battle_runtime/terrain/run_battle_terrain_topology_service_regression.cs
Battle terrain topology service regression: PASS

godot --headless -s res://tests/battle_runtime/terrain/run_meteor_swarm_terrain_modifier_regression.cs
Meteor swarm terrain modifier regression: PASS

python3 tools/architecture_checks.py --max-results 5
Total review findings: 834
tests internal field writes: 162
```

阻塞：

```text
dotnet build magic.csproj
Build failed in scripts/systems/battle/core/AttackEffectResolutionResult.cs:
missing ReadTraitTriggerResults, ReadDouble, ReadDiceRollDetail, EquipmentDurabilityEventResult.Payload,
plus tests/shared/run_shared_test_fixture_regression.cs cannot see the committed implicit projection while the file is dirty.
```

这些 build errors 来自当前工作树中未提交的 `AttackEffectResolutionResult.cs` 并行改动；本批 terrain 测试已通过定向 Godot 回归。
