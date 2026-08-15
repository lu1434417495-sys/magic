# 运行时 hub 解耦（GameRuntimeFacade / BattleRuntimeModule）

> 状态：**进行中**，分阶段落地。已完成部分是当前实现事实；未完成部分只是方向。
> 起始：2026-08-14。基线提交 `6545d837`（工作区，非提交态）。
> 背景审计：[`../../reviews/architecture_review_2026-07-19.html`](../../reviews/architecture_review_2026-07-19.html)
> 后续收敛计划：[`battle_runtime_architecture_convergence.md`](battle_runtime_architecture_convergence.md)。

## 0. 前提：先纠正两个会误导人的度量

**① C# 的 `namespace` 不提供任何访问控制。** 语言只有 `public` / `internal`（**程序集**范围）/ `private` / `protected` / `file`。不存在 namespace 级可见性——加了 `namespace` 之后，越层方写个 `using` 照样编译通过。因此"`namespace` 声明数 = 0"**不是**边界强度的证据，旧评审 P0-1 用它作证据是错的。真正的门禁是路径型 Roslyn analyzer（见 §1）。要让 `internal` 恢复语义只能拆程序集，当前是单个 Godot 游戏程序集，另案评估。

**② "internal 声明数"里绝大部分是方法，不是字段。** 直接数 `^\s*internal` 会得到 facade 344 / module 272 这种数字，据此说"字段大开门"是错的：

| | 字段 | 属性 | 方法 | 嵌套类型 |
|---|---|---|---|---|
| `GameRuntimeFacade`（整治前） | 52 | 4 | 287 | 1 |
| `BattleRuntimeModule` | 50 | 13 | 276 | 8 |

facade 字段数 52 比 2026-07-19 评审时的 ~75 反而是**下降**的。评估暴露面必须按种类拆开。

**③ 按字段名 grep 会严重误报。** `_party_state` / `_player_coord` / `_game_session` 在 `GameRuntimeFacade`、`GameSession`、`WorldMapSystem` 里**同名并存**。必须用限定式访问（`X._field`）判定，且要额外覆盖**对象初始化器**语法（`new T { _field = ... }` 没有 `.`）。这两个坑在本次整治中各踩过一次，都由编译器兜住。

## 1. 边界门禁（已落地，是后续所有工作的地基）

`magic.csproj` 以 `OutputItemType="Analyzer"` 挂载 `tools/architecture/Magic.ArchitectureAnalyzers`，配 `layer_rules.json` + `layer_baseline.json`：

- 11 层、6 条 denyRules，`pathMappings` 精确到单文件，`symbolOverrides` 可按符号纠正归属
- `MAGICARCH001`（越层依赖）/`002`（partial 跨层）/`003`（未分类源文件）/`900`（配置错误）全部 `DiagnosticSeverity.Error`
- **`layer_baseline.json` 的 `entries` 必须保持空数组**——当前零豁免，即旧评审图⑥ 的 6 条越层边已全部清零

**实现细节（改配置前必读）：`pathMappings` 是首个匹配生效**（`ArchitectureConfiguration` 里 `foreach … if (IsMatch) return`），所以更具体的映射必须排在通配之前。

## 2. 阶段一：GameRuntimeFacade 封装收敛（已完成）

结果：internal 字段 **52 → 23**，方法 **287 → 238**，声明合计 344 → 266。

1. **71 个零外部引用成员转 `private`**。partial 之间 `private` 完全互通（它们是同一个类），所以零调用点改动。
2. **`RuntimeTransaction` 是唯一读 facade 字段的生产代码**（共 6 处），改走 `CaptureRootWorldSnapshot()` / `BindRootWorldData()` / `ActiveGameSession`；它对 `GameSession` 的裸字段写收进 `GameSession.RestoreRuntimeStateForRollback()`。
   - ⚠ **该方法刻意不调用 `SetPartyState`/`SetPlayerCoord`**，因为那两个会 `MarkRuntimeStateDirty`，而回滚的目标正是回到已提交的干净态。不要"顺手改成用 setter"。
3. **测试对 9 个状态字段的 175 处读取改走 `IGameRuntimeSnapshotSource` 既有 getter**（`GetPartyState()` / `GetActiveModalKind()` / `GetStatusText()` 等，逐个核对过是 `=> _field` 的 1:1 直读）。
   - `GetResolvedSettlementId()` **不是** `_active_settlement_id` 的直读（它调 `ResolveCommandSettlementId()`），已排除。
4. **夹具构造收敛为 `SetupForTestFixture()`**。测试用对象初始化器绕开 `Setup(GameSession)`（后者会完整装配 content catalog / world data / encounter roster，测试只要隔离的最小 facade）。构造没有 port 对应物，硬造"构造 port"更糟，故用具名接缝；该方法**刻意保持裸赋值语义**以与它替换掉的初始化器逐字等价。

## 3. 阶段二：BattleRuntimeModule 按消费者解耦（已收口，6/7；第 7 项另案）

module 与 facade **问题形状完全不同**：50 个字段里 39 个被 production 外部读、分布在 61 个文件，且字段几乎全是**服务引用**（`_layered_barrier_service` 被 16 个文件读、`_skill_resolution_rules` 10 个）。这是真正的 service locator，**改可见性无效**。

**策略：按消费者切，不按字段切。** 修一个字段要跨 16 个文件，改完所有人手里还攥着 `_runtime`；切一个消费者则产出一个"依赖可声明、可隔离单测"的完整单元。

**模式**（沿用既有 `IBattleContingencyRuntimePort` / `BattleContingencyBridgeService`，非新发明）：
- `IXxxRuntimePort` —— 只暴露**行为**，不暴露服务对象，消费者因此不知道哪个服务负责哪件事
- `XxxBridgeService : BattleRuntimeModuleBorrower, IXxxRuntimePort` —— 全部成员为显式接口实现，穿透 hub 的行为关在这一个文件里
- 消费者持 `WeakReference<IXxxRuntimePort>`，**彻底不再引用 `BattleRuntimeModule`**

**棘轮（关键，不做则白干）：** 新增 `battle_runtime_isolated` 层 + `battle-isolated-to-hub` 禁则（→ `composition` / `content_authoring` / `presentation`）。已解耦的消费者再引用 `BattleRuntimeModule` **即编译失败**，已实测验证。后续每解耦一个，只需把文件加进该层的 `pathMappings`，不用再写规则。

> 针对的是本项目已被记录的真实失败模式：`BattleEquipmentAbilityRuntimeService` 2026-07-19 拆到 2,302 行后，一个月内回潮到 3,611 行。

### 已完成

- **`BattleTimelineDriver`**（2026-08-14）：原穿透 19 个成员（6 服务 + 12 方法 + `_state`）→ `IBattleTimelineRuntimePort`（22 个行为成员）+ `BattleTimelineBridgeService`。
  - 禁则当场逼出一处真实残留：`BattleDefeatHandlingOptions` 是纯数据值对象却声明在 `BattleRuntimeModule.cs` 内、因而被归为 composition，已下移到 `battle/core/`（domain_state），与其字段类型 `BattleKillProvenance` 同层。**搬类型，不给规则开豁免。**
  - 加固：`_timeline_driver.Setup(...)` 的 3 个调用点中，`_ensure_sidecars_ready()` 与 `ConfigureDamageResolverForTests` 前面没有 `_moduleBorrowers.Setup(this)`。改造前 driver 直接拿 module 无所谓；改造后会拿到未绑定 bridge，端口调用**静默变成 no-op**（最难查的失败）。已在 3 个调用点就地加 `TimelineBridge.Setup(this)`（经 `IsBoundTo` 幂等）。

- **`BattleChargeResolver`**（2026-08-15）：原穿透 8 个服务字段（`_state` / `_grid_service` / `_damage_resolver` / `_terrain_effect_system` / `_skill_resolution_rules` / `_layered_barrier_service` / `_equipment_ability_runtime_service` / `_skill_orchestrator`）+ 8 个 hub 方法 → `IBattleChargeRuntimePort`（21 个成员）+ `BattleChargeBridgeService`。
  - **端口分界线在这一步定下来，后续消费者照此办理**：默认一律包成**行为**；只有当某个 `domain_runtime` 层协作者会产生 **≥5 个纯转发方法**时才改用**访问器**（本例 `BattleGridService` 40 处调用/13 个方法、`BattleDamageResolver`、`BattleAttackCheckPolicyService`）。理由：`battle-isolated-to-hub` 只禁 composition/content_authoring/presentation，向下依赖 `domain_runtime` 本就合法，把它藏进十几个零信息量转发方法只是噪音；但调用点只有一两处时，行为反而更短也藏得更多。
  - ⚠ **访问器必须每次经 hub 现取，不能在 `Setup` 时缓存**——`_damage_resolver` 会被 `ConfigureDamageResolverForTests`（`BattleRuntimeModule.cs:1683`）整体换掉，teardown 时还会置 null，缓存会攥着旧对象。
  - 同样在 `_ensure_sidecars_ready()` 与 `FinishSetup` 两个 `_charge_resolver.Setup(...)` 调用点就地补 `_moduleBorrowers.ChargeBridge.Setup(this)`（幂等），避免端口静默变 no-op。
  - 顺带清掉一个死重载：`AppendResultSourceStatusEffects(…, GDictionary)` 无调用点，随之 `GArray`/`GDictionary` 两个 using 别名也一并删除。
  - 棘轮已实测：在 resolver 里放一个 `BattleRuntimeModule` 字段即报 `MAGICARCH001 battle-isolated-to-hub`。

- **`BattleCommandPreviewService`**（2026-08-15）：原穿透 10 个字段 + 5 个 hub 方法 → `IBattleCommandPreviewRuntimePort`（11 个成员，**全行为、零访问器**）+ `BattleCommandPreviewBridgeService`。
  - 收敛最明显的一处是 `ValidateSkillCommandEntryAccess`：hub 侧一次吃掉四个内容索引（`_skillCatalog` / `_skillDefinitionIndex` / `_equipmentAbilityBindingIndex` / `_itemDefIndex`）加 `_state` 加 `GetBattleWorldStep()`，连同 `BattleSkillAvailabilityService` 的构造一起搬进 bridge。10 个字段里有 4 个就此消失。
  - **本例与 charge 相反，一个访问器都没开**：预览路径对每个下层协作者只有一两处调用，包成行为反而更省。分界线的可操作判据是 **≥5 个转发才考虑改访问器**，不是"看到 `domain_runtime` 就开访问器"。
  - **结构变化**：该服务原本自己就是 `BattleRuntimeModuleBorrower`。改造后它降为普通类持端口，borrower 槽位换成新的 `CommandPreviewBridge`；`_commandPreviewService` 从 `=> _moduleBorrowers.CommandPreview` 的属性变回 module 上的真实字段，在构造函数里 `Setup(_moduleBorrowers.CommandPreviewBridge)` **绑一次即可**——bridge 与 module 同寿，重新装配只影响 bridge 到 hub 的那一段。因此**不要**给它加 teardown 里的 `DisposeRuntime`，否则 module 复用时端口会静默失联。
  - 搬走一个错层的方法：`BattleRuntimeModule.DisposeBattlePreview` 只把 `preview.hit_preview` 置空、根本不碰 hub，已下移为 `BattlePreview.ReleaseHitPreview()`（domain_state），hub 上那份唯一调用点消失后删除。测试侧的 `BattleTestFixture.DisposeBattlePreview` 是各自独立的副本，未受影响。

- **`BattleSkillPreviewService`**（2026-08-15）：原穿透 9 个字段 + 8 个 hub 方法 → `IBattleSkillPreviewRuntimePort`（4 访问器 + 13 行为）+ `BattleSkillPreviewBridgeService`。
  - 这一步把"何时开访问器"补全成三条可判定的规则，前两个消费者只覆盖到其中一条：
    1. **对象被原样外传**给另一个 API（`GetGridService` / `GetLayeredBarrierService` 要传进 `BattlePositionSwapRules` / `BattleAirbornePullRules` / `BattleWindPushRules`）——包成行为无从下手。
    2. **转发数 ≥5**（`GetSkillResolutionRules`，本服务用了它 7 个方法）。
    3. **同层 peer**（`GetChargeResolver`，`BattleChargeResolver` 自己就在 battle_runtime_isolated）——暴露它根本不泄露 hub 拓扑。
  - **本服务仍持有 `_owner`（`BattleSkillExecutionOrchestrator`）**，这是有意的：orchestrator 属 application 层，隔离层依赖它不违规；把这层关系也端口化属于 orchestrator 自己那一轮。禁则只保证它不再碰 hub。
  - 清掉一处无意义的 `var runtime = _runtime as BattleRuntimeModule;`——`_runtime` 本来就是该类型，这个 `as` 是纯噪音。
  - 绑定点在 `BattleSkillExecutionOrchestrator.Setup(runtime)` 里就地补 `SkillPreviewBridge.Setup(runtime)`（幂等），因为 orchestrator 的 Setup 可能早于 `_moduleBorrowers.Setup`。

- **地面效果服务族**（2026-08-15）：`BattleGroundEffectService` + `BattleGroundEffectCoordService` + `BattleGroundRelocationService` + `BattleGroundSkillValidationService` 四个文件（合计 3,777 行）→ `IBattleGroundEffectRuntimePort`（10 访问器 + 38 行为）+ `BattleGroundEffectBridgeService`。原穿透 15 个字段 + 约 30 个 hub 方法。
  - **四个文件必须一起解耦，不能只做根服务**：根服务把 hub 原样往下传给三个子服务（`_coordService.Setup(runtime, this)` 等），只改根服务的话子服务照旧攥着 hub，隔离是假的。它们共享同一个 `Runtime`，本来就是同一个可隔离单元，因此共用一个端口。
  - 这一步逼出两处真实的错层，都按"搬类型/搬方法"处理、没有开豁免：
    - `BattleRuntimeModule.RunTeardownStep` 是纯 try/catch 工具、不碰任何运行时状态，隔离层却要用它拆自己的子服务。已下移为 `BattleTeardown.RunStep`（`battle/core/`，domain_state），hub 上那份改为一行转发，**69 个既有调用点零改动**。
    - `_terrain_effect_nonce` 原先是消费者直接对 hub 字段做读-改-写（`nonce = Runtime._terrain_effect_nonce + 1; Runtime._terrain_effect_nonce = nonce;`）。已收成端口行为 `AllocateTerrainEffectNonce()`，递增语义关进 bridge。这类"消费者代管 hub 可变字段"是 service locator 最难查的一种，后续消费者要专门找。
  - 绑定点在 `BattleRuntimeServices.SetupRuntimeSidecars` 里就地补 `GroundEffectBridge.Setup(runtime)`（幂等）。

- **`BattleAiDecisionBindingService`**（2026-08-15）：原穿透 17 个字段 + 9 个 hub 方法 → `IBattleAiDecisionBindingRuntimePort`（**13 个成员，全行为、零访问器**）+ `BattleAiDecisionBindingBridgeService`。**收敛比是全阶段最高的一个。**
  - 收敛点是把两个"组装 + 交付"的整体动作整段挪进 bridge：`BindAiHelperServicesForDecision` 与 `PrepareAiContextForDecision` 原先要在消费者侧从 hub 上抓十几个字段（四个内容索引、**8 个 AI 回调**、trace 开关、score service）拼成上下文记录再交给 runtime services。现在消费者只交出"哪个单位、哪个上下文"。同理 `BuildUnitActionPlan` / `IsActionPlanStaleFor` 把四个索引和世界步数一并吃掉。
  - **这是本阶段最值得复用的一招**：当消费者从 hub 抓一堆字段只为拼一个参数对象时，别给每个字段开访问器——把"拼 + 交付"整体搬进 bridge，一次消掉十几个依赖。
  - 结构变化同 `BattleCommandPreviewService`：它原本自己就是 borrower，现降为普通类持端口，槽位换成 `AiDecisionBindingBridge`。
  - 去掉了 borrower 基类的 `DisposeRuntime()` override（原本用来清 action plan）。**这不丢清理**：`ClearAiActionPlans` 在 4 条重置/拆卸路径上都有显式调用（`BattleRuntimeModule.cs` 三处 + `ContentSync` 一处），原先等于清两次，现在清一次。
  - `BattleRuntimeModule.IsEmpty` 这个 StringName 空值判断改用本地 `private static`，与 `BattleChargeResolver` 里既有的同名私有工具一致——不值得为一行判断新开一个共享类型。

### 待做（按依赖面从小到大）

| 消费者 | 字段 | internal 方法 |
|---|---|---|
| `BattleSkillExecutionOrchestrator` | 19 | 62 |

### `BattleSkillExecutionOrchestrator`：实测结果与待定的判断

方案原本假设它"19 个依赖，有可能是合理宽度"。**2026-08-15 实测，这个假设不成立**：

| 项 | 方案假设 | 实测 |
|---|---|---|
| 文件 | 1 | **12 个 partial** |
| 行数 | — | **6,038** |
| hub 服务字段 | 19 | **17** |
| hub 方法 | 62 | **31 个 `_snake_case` + 57 个 PascalCase ≈ 88** |
| 端口成员总量 | — | **约 90** |

两个结论：

1. **它不能切小。** `MAGICARCH002` 禁止 partial 类型跨层，所以 12 个 partial 只能整体进 `battle_runtime_isolated`，没有"先切窄的几个"这条路（与 AutoCast 撤销的理由同源）。是 all-or-nothing。
2. **"合理宽度"说法站不住，但代价也是真的。** 17 个字段对一个中央执行器或许算合理，可加上 88 个方法后，端口会有约 90 个成员——那已经是**把 hub 改个名字**，作为边界的约束力很弱。

**决定（2026-08-15）：先不做，移入 §4。** 理由是给一个尚未拆分的 6,038 行执行器套端口，
锁住的只是它**当前**的形状；先降低那 88 个方法的耦合面、把 orchestrator 本身拆小，再谈端口，
是更优的顺序。阶段二收在 **6/7**。

（被否掉的另一面也记下来，免得下次重新推一遍：约 90 成员的端口虽宽，但它是**显式声明**的面，
往里加第 91 个成员是端口文件上一处可见的 diff，而不是随手打一个 `Runtime._whatever`。
如果 orchestrator 的拆分迟迟不启动，这个理由足以支持"先上棘轮再拆"。）

## 4. 未纳入本方案

- **`BattleSkillExecutionOrchestrator`**（见 §3 末实测）：12 个 partial / 6,038 行 / 约 90 个 hub 成员。`MAGICARCH002` 决定它只能整体解耦，而整体解耦得到的端口已接近 hub 的复制品。**前置动作是先拆小 orchestrator 本身**（拆成独立类而非 partial，之后各自可单独上棘轮），那是另一份方案。

- **facade 剩余 238 个 internal 方法**：其中 177 个被 `WorldMapRuntimeProxy`(69) / `GameRuntimeSettlementCommandHandler`(65) / `GameTextCommandRunner`(45) 等既定协作者调用，应逐步归入对应 port，但那是独立的一轮。
- **拆程序集**以恢复 `internal` 语义（见 §0①）。
- **`layer_rules.json` 把两个 hub 显式划入 `composition`**，其宽接触面按设计合法，门禁结构上不会对 hub 自身体量报警。是否给 composition 层加约束（如限制 internal 字段数、禁止 hub 互访）待议。

## 5. 验证

每一步都：`dotnet build magic.csproj` → 相关窄回归 → 全量套件。

`python tests/run_regression_suite.py --godot <path> --jobs auto`，当前 **470/470**。

> e2e 用例不能直接 `godot --headless --script` 跑：需要沙箱环境（`APPDATA`/`LOCALAPPDATA`/`XDG_*` 重定向 + `MAGIC_E2E_ISOLATED_USER_DATA=1` + `MAGIC_E2E_USER_DATA_ROOT`），且 `run_load_game_e2e` / `run_world_save_reload_e2e` 是**多进程**用例，需前置 create 步骤，单跑必失败。走套件运行器即可。
