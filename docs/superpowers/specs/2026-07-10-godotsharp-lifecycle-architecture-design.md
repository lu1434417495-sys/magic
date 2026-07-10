# GodotSharp 生命周期架构设计

日期：2026-07-10
状态：已批准，待分阶段实施
决策：采用方案 B——进程内容根、plain C# 内容快照、短生命周期投影租约与显式退出屏障

## 问题

当前项目已经把大部分核心 runtime/save 状态从 `RefCounted` 迁为 plain C#，并建立了
Godot wrapper 分类、静态内容强引用池、transient scope、test quarantine 和退出前 GC drain。
这些机制已显著降低 `gchandle.is_released()` / `GodotObject.Finalize()` 崩溃概率，但当前
稳定性主要依赖两类止血行为：

1. 对 Godot wrapper 调用 `GC.SuppressFinalize`；
2. 把大量 static、derived 或 quarantine wrapper 强引用到进程结束。

这两类行为都不是释放。前者可能让 wrapper 跳过 GodotSharp 的正常 native 解绑路径，后者则
让旧 catalog generation、临时 projection 和生产 UI wrapper 无法回收。现有 `TestHarness.Finish`
又在 SceneTree/autoload owner 真正离树之前执行 GC，因此不能证明“所有 owner 已关闭后才进入
finalizer drain”。

2026-07-10 的当前快照基线如下：

- `dotnet build magic.csproj` 为 0 warning、0 error；
- AI 子集 35/35 通过，`finalizer-crash-retries=0`；
- strict lifecycle regression 登记约 60,188 个 static wrapper，并报告 ObjectDB/resource-in-use；
- headless session regression 首轮登记约 18,938 个 runtime wrapper，最终 static wrapper 达
  75,258，并报告 3 个 unsafe Resource reference。

因此当前状态定义为“已稳定已知崩溃路径，但生命周期尚未闭环”，不能以测试重试、日志豁免、
全局 quarantine 或 wrapper 数持续增长作为最终成功条件。

## 技术依据

- 仓库内原始问题、taxonomy 与“创建点声明 ownership、consumer 只审计”的讨论：`life.md`；
- Godot 4.6.2 `GodotObject` finalizer/Dispose 实现：
  <https://github.com/godotengine/godot/blob/4.6.2-stable/modules/mono/glue/GodotSharp/GodotSharp/Core/GodotObject.base.cs>；
- Godot shutdown tracker 使用弱引用并在引擎卸载时 Dispose 仍存活 wrapper：
  <https://github.com/godotengine/godot/blob/4.6.2-stable/modules/mono/glue/GodotSharp/GodotSharp/Core/DisposablesTracker.cs>；
- Godot 官方 C# collection 边界建议：
  <https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_collections.html>；
- Godot 官方自定义退出顺序：
  <https://docs.godotengine.org/en/4.6/tutorials/inputs/handling_quit_requests.html>。

据上述源码作出的工程推论是：`GC.SuppressFinalize` 只取消 managed finalizer，不执行 native
解绑；如果随后丢掉唯一强引用，shutdown tracker 的弱引用也可能取不到该 wrapper。当前方案必须
从“阻止 finalizer”转为“在引擎仍存活时让 owner 结束并完成正常 Dispose/finalizer”。

## 设计目标

1. 在 Godot native runtime 仍存活时，按确定顺序关闭所有项目 owner，再执行唯一一次最终 GC
   barrier，最后请求 `SceneTree.Quit`。
2. `.tres` 继续作为 Godot 编辑器 authoring 格式；runtime 只消费 immutable typed C# content
   snapshot，不长期持有业务 Resource。
3. runtime/save state 和 runtime service 由 plain C# class/collection owner 承载；Godot collections
   只存在于 save、UI、trace、资源导入和 Godot API 的短期投影边界。
4. 每个 runtime-created Resource、Godot collection 和 native utility 都能追溯到一个显式 owner
   或 lease；consumer 不改变生命周期。
5. 普通 session 关闭、battle 结束和 process 退出是不同生命周期事件，不能再从 `_ExitTree()`
   或 `IsInsideTree()` 推断 process shutdown。
6. 保持现有玩家可见行为、战斗结果、headless snapshot 与 save version 12 不变。
7. 以 `retries=0`、无 unsafe-reference/finalizer fatal、owner 计数归零和重复循环不增长作为完成门。

## 硬约束

- Godot 版本保持 4.6.2 Mono，目标框架保持 .NET 8。
- 不新增 legacy alias、旧 payload fallback 或 save migration；若实现迫使 save schema 变化，必须
  暂停并重新确认。
- V1 不支持进程内内容热重载。内容发生变化后重启进程；测试需要不同内容时使用隔离 host。
- 不建设完整 addressable/refcount 内容系统，也不建设离线 `.tres` 编译器。
- 不把 lifecycle 规则塞入 `GameRuntimeFacade`、`WorldMapSystem` 或具体 AI scorer；生命周期由
  独立 owner/coordinator 管理。
- Node 只由 SceneTree 管理，不进入 Resource/transient/projection scope。
- `ResourcePath` 只能用于诊断和 invariant，不能据此猜测 ownership。
- reflection graph walker 不能承担 production cleanup；production 只释放 factory 明确创建的对象。

## 非目标

- 不把 Texture、PackedScene、AudioStream、Material、Theme、TileSet 等引擎资产转成 typed C#
  definition；它们仍遵守下文的 process/scene asset owner 规则。
- 不为了生命周期重构游戏规则、数值、AI 评分或 UI 布局。
- 不要求普通 session 关闭时执行全进程 GC；强制 GC 只属于 process shutdown 和专用 correctness
  regression。
- 不要求清除 Godot/CLR 自身的全部 engine-internal tracker 项；验收针对项目拥有或项目创建的
  wrapper，并禁止用宽泛日志字符串豁免隐藏来源。

## 现有 ownership

### 已经正确的边界

- `PartyState`、`PartyMemberState`、`BattleState`、`BattleUnitState`、`BattleCommand`、
  `BattlePreview` 等核心状态已经是 plain C#。
- `GameRuntimeFacade`、`BattleRuntimeModule` 和 AI service 已有幂等 `Dispose` 主链。
- `BattleAiRuntimeActionPlan` 已拥有自己的 transient scope。
- `GameRoot.Dispose()` 会使当前 session 的 catalog view 失效。
- `SkillDefinition` 和 equipment ability definition 已经有 plain typed projection。

### 必须替换的边界

- `GodotContentOwnership.StaticStrongWrappers` 同时持有 borrowed、derived 和 projection wrapper，
  不能按 session/catalog generation 释放。
- `GodotTransientResourceScope` 在登记和 drain 时 suppress wrapper，而不是执行可证明的解绑。
- `RuntimeStateLifecycle` 在 30 个文件、62 个调用点为临时 Godot payload suppress finalizer。
- `GameSession._ExitTree()` 会把普通 session teardown 当成 process shutdown。
- `BattleBoardController` 在生产路径启用了 test quarantine。
- `GameContentCatalog` 仍暴露 `TraitDef`、`ProfessionDef`、`AchievementDef`、`QuestDef`、`ItemDef`、
  `RecipeDef`、`EnemyTemplateDef`、`EnemyAiBrainDef`、`WildEncounterRosterDef` 和 Godot Dictionary。
- 正式运行路径仍动态创建 `AttributeModifier : Resource`，部分 attribute rule 和 projection 仍长期
  保存 Godot collections。
- `BattleRuntimeModule.Dispose` 尚未清空全部 borrowed catalog index；复用型 AI context 可在一次
  decision 结束后继续引用旧 action plan/state。

## 方案比较与决策

### 方案 A：继续补 suppress 和 quarantine

修改范围小，也能继续降低退出崩溃概率；但无法证明 native association 已释放，不能支持可靠
session recreate，并且 wrapper 与内存会随 catalog/query/battle 次数增长。该方案只描述现有止血
状态，不作为目标架构。

### 方案 B：进程内容根 + plain snapshot + lease + pre-quit barrier

保留 `.tres` authoring，由一个进程 owner 持有 canonical raw content root；加载后立即投影成
immutable plain snapshot。runtime-created native wrapper 进入短期 lease，process shutdown 在
Godot 存活时完成 owner teardown 和 GC barrier。该方案能分阶段落地，并复用当前 typed migration。

### 方案 C：离线编译全部业务 `.tres`

运行时最干净，但需要内容编译器、增量缓存、export 接线、source mapping、schema 版本与编辑器
一致性工具。当前收益不足以承担双重 schema 和工具链成本。

选择方案 B。现有 suppress/quarantine 只允许作为迁移中的已有安全网，不新增同类 fallback；每个
阶段通过 correctness gate 后删除其覆盖范围，最终全部退出 production 路径。

迁移期允许旧机制与新机制并存，但同一 wrapper 不能同时由旧 static/quarantine pool 和新 lease
拥有。每次迁移以完整 owner domain 为单位切换，先在 strict audit 中证明该 domain 的新 owner
闭环，再删除该 domain 的 suppress/quarantine 注册。

## 核心不变量

1. 创建点或加载点声明 owner；consumer 只能 borrow、transfer 或 assert。
2. 一个对象同一时刻只有一个实际 owner；registry 是弱引用诊断表，不是 owner。
3. owner 先清空所有 borrower 引用，再释放 owned wrapper。
4. session/battle/request/projection scope 不得吞入 path-backed process content。
5. raw `.tres` immutable；默认值归一化、merge 和 derived calculation 发生在 plain projection。
6. runtime state graph 不含 `Resource`、Godot Array/Dictionary 或 `Variant.Type.Object`。plain C#
   owner 可以保存下文明确允许的 Godot value type，但不能因此变成 GodotObject owner。
7. 只有 `ApplicationLifetimeCoordinator` 可以执行 process shutdown 和最终 `SceneTree.Quit`。
8. 普通 `_ExitTree`、普通 `Dispose`、session recreate 和 battle end 的 process-content suppress 次数
   必须为 0。
9. 项目代码不直接对 GodotObject/Godot collection 调用 `GC.SuppressFinalize`；独占对象走
   `Dispose()`，borrowed 对象只断开引用。
10. GC barrier 只能在 non-terminal runtime owner、content borrower、scope、lease、job 均为 0，
    process content root map 已清空，且只剩 coordinator 这个 terminal SceneTree owner 后执行。

## Ownership descriptor

现有 `GodotWrapperOwnershipKind` 把对象类型、生命周期域和引用角色混为一个枚举。最终诊断模型
拆为三个正交维度：

```text
StorageKind:
  PlainManaged | GodotValue | GodotResource | GodotCollection | NativeUtility | SceneNode

LifetimeDomain:
  ProcessContent | Session | Battle | Decision | Request | SceneTree | External

ReferenceRole:
  Owned | Borrowed | Transferred
```

每条诊断记录还包含 `owner_id`、可选 `borrow_anchor_id`、`epoch`、`creation_reason` 和可选
canonical path。path-backed cached Resource 的 `owner_id` 是 Godot resource cache，
`borrow_anchor_id` 是 `ProcessContentHost`；ownership registry 继续使用弱引用。strict 模式下
unknown、重复 owner、跨域 retain 和关闭后注册直接失败。

这里的“plain C# owner”描述 class/collection 的 ownership，不表示图中所有值都来自纯 CLR。
项目继续使用以下明确允许的 Godot value type：

- `StringName` 作为正式内容 key/id；
- `Vector2I`、`Vector2`、`Vector3`、`Color` 等不引用 GodotObject 的数学值。

其中 `StringName` 含 native handle，登记为 `StorageKind.GodotValue`，但不按每个 struct copy 调用
`Dispose()`。它只能存在于有明确生命周期的 typed owner 或声明过的 process-static ID 集中；关闭
owner 时先清包含它的集合和字段，Quiescing 后禁止创建新动态 ID。退出前 audit 分别报告动态
GodotObject/collection owner 和 process-static GodotValue 数；后者不计入 non-terminal owner=0，
最终由 Godot 的 disposable tracker 在引擎 shutdown 阶段处理。GodotValue audit 登记声明它的
owner field/static ID source，不尝试跟踪每一次 struct copy。

## 目标架构

```text
ApplicationLifetimeCoordinator (autoload, last owner to leave)
└── ProcessContentHost
    ├── canonical ResourcePath -> raw immutable content/engine-asset root Resource
    ├── EngineAssetResolver -> process-shared asset borrow
    └── ContentSnapshot(epoch, typed immutable definitions)
        └── GameSession / GameRoot catalog view
            └── GameRuntimeFacade
                └── BattleLifetime
                    ├── AiDecisionLease
                    ├── NativeLeaseScope
                    └── GodotProjectionLease
```

### ApplicationLifetimeCoordinator

新增排在 `GameSession` 之前的 autoload。它在 `_Ready()` 设置
`SceneTree.AutoAcceptQuit = false`，处理 `NotificationWMCloseRequest`，并拥有进程级 shutdown
state machine 与 `ProcessContentHost`。

对外只有一个幂等入口；production 和 test 都通过同一个 request DTO：

```csharp
public sealed record ShutdownRequest(
    int RequestedExitCode,
    ShutdownReason Reason,
    ShutdownCallerResult CallerResult = null
);

public sealed record ShutdownCallerResult(string Label, bool Passed);

ValueTask<ShutdownReport> RequestShutdownAsync(ShutdownRequest request);
```

第一次调用创建并缓存 shutdown completion；并发或重复调用等待同一个结果，不能读取尚未写入的
默认 exit code。`ShutdownCallerResult` 是 production-owned 通用 DTO；`TestExitCoordinator` 只负责
把 `TestResult` 映射成它。caller failed、任一 shutdown violation 或 requested code 非零都会令
effective exit code 非零。协调器在 Quit 前输出一次最终结果。协调器记录每一步异常，继续尝试后续
teardown；测试/CI 只要任何步骤失败就返回非零退出码。

`AppDomain.ProcessExit` 只检查 state 是否已经到达 `FinalizersDrained` 或带失败标记的
`FinalizerBarrierSkipped`，并输出诊断。它不遍历 SceneTree、不调用 Godot API，也不执行补救
suppress；看到 skipped phase 必须报告 lifecycle failure。

### ProcessContentHost 与引擎资产

Godot 的 ResourceLoader/cache 与 RefCounted 规则拥有 path-backed native Resource 的实际 native
生命周期；项目不得把 cached Resource 当作 exclusively-owned wrapper 调用 `Dispose()`。
`ProcessContentHost` 是唯一的项目侧 managed borrow anchor：它决定 raw authored content wrapper 在
load/validate/project 和进程运行期何时必须保持可达，但不声称拥有 native Resource 本体。

`ProcessContentHost` 的规则：

- 以规范化 `ResourcePath` 为 key，只强持有每个已加载 graph 的根 Resource wrapper；
- 不递归强持有 subresource、Array、Dictionary 或 getter 临时创建的 wrapper；
- 同一路径重复请求返回同一个 canonical root；
- root 只能在 host 的 load/validate/project 阶段访问，不能进入 runtime service/state；
- raw Resource 不允许被 normalization 或 merge 修改；
- host 初始化完成后进入 sealed 状态，V1 禁止在 active session 中 reload/register；
- process shutdown 时先释放 content snapshot/session borrower，再清 canonical root map，让 managed
  wrapper 在 Godot 仍存活时通过正常 finalizer/engine tracker 解除绑定；不直接 Dispose cached root。

业务 definition 只保存内容 id、canonical path 或 UID，不保存 Texture、PackedScene 等 wrapper。
引擎资产按来源分为三类：

1. 通过 process service 显式加载并跨 scene 复用的 path-backed Texture、PackedScene、AudioStream、
   Material 等，由 `ProcessContentHost.EngineAssetResolver` 以 canonical path 建立 managed borrow
   anchor；native owner 仍是 Godot ResourceLoader/cache；
2. 由 PackedScene/exported property 随 Node graph 注入的资产由 SceneTree/native scene graph 拥有，
   项目字段只登记 `LifetimeDomain.SceneTree + ReferenceRole.Borrowed`，不 Dispose；
3. runtime factory 创建的 pathless Image、Texture、Mesh、TileSet 等进入 `NativeLeaseScope`。

任意运行时资产加载都必须落入上述三类之一。`External` 只用于引擎或第三方明确拥有且项目无法
关闭的 wrapper，并要求记录来源 API；不能把 ownership unknown 标成 External 来绕过 strict gate。

### ContentSnapshot 与 GameContentCatalog

`ContentSnapshot` 是 immutable typed C# graph，拥有单调递增 `epoch`。同一进程只能有一个 raw
`ProcessContentHost`，因为 Godot ResourceLoader cache 可能为相同路径返回同一 wrapper。需要内容
隔离的测试只能选择：

- 启动独立 Godot 进程并创建自己的 raw host；或
- 在同一进程注入不加载 Resource 的 pure CLR synthetic snapshot。

V1 正常进程只有一个 active snapshot。`GameSession` 不再重建 raw registry graph，而是从 host
借用 snapshot；`GameRoot` 仍负责 catalog view 的 session binding、revision 和失效语义。

迁移后的 catalog 内容包括：

- `SkillDefinition`、`TraitDefinition`、`ProfessionDefinition`、`AchievementDefinition`、
  `QuestDefinition`；
- `ItemDefinition`、`RecipeDefinition`、equipment ability pack/binding definition；
- `EnemyTemplateDefinition`、`EnemyAiBrainDefinition`、`EnemyAiActionDefinition`、
  `WildEncounterRosterDefinition`；
- world generation/content definition；
- battle special profile 的 plain typed view。

derived item、skill effective profile、cast variant 和 simulation override 生成 plain definition，不再
`Duplicate()` Resource。skill/effect 参数使用受限 plain value graph：primitive、enum、`StringName`、
Godot 数学值类型、plain list、plain map 和明确 typed value object；禁止 `Variant.Type.Object` 穿过
catalog boundary。

### Session 与 Battle owner

`GameSession` 提供显式、幂等的 normal close，不再从 tree 状态推断 process exit。`Dispose()` 委托
normal close；`_ExitTree()` 只作为 normal close fallback 和“是否已由 coordinator 关闭”的审计点。

normal session close 顺序：

1. 禁止新 session command；
2. 关闭当前 battle、world runtime 和 facade；
3. 清 session service、transaction、pending prompt 和 save lock；
4. 使 GameRoot/catalog view 失效；
5. drain session-owned native/projection lease；
6. 清 registry index 和 session 字段；
7. 从 SceneTree 释放 session Node。

它不清 `ProcessContentHost`、不 suppress process content、不调用 `SceneTree.Quit`。关闭后可创建
session B，并重新消费同一 immutable snapshot。

`BattleRuntimeModule.Dispose` 必须先清 borrower，再释放 owner：

1. 停止 command/AI decision；
2. 清 active AI decision context；
3. dispose action plans、AI service 和 score service；
4. dispose battle services/sidecars；
5. 清 `_skillCatalog`、trait/equipment binding/item/enemy 等全部 catalog index；
6. 清 battle topology/state；
7. drain battle native/projection scope。

AI context 改为 decision-scoped lease，`ChooseCommand` 的 `finally` 必须断开 plan、state、score profile
和 content borrower；不能等下一次 decision reset。

### NativeLeaseScope

`NativeLeaseScope` 只拥有 factory 明确创建或 duplicate 的 pathless Resource、Godot collection 和
native utility。它不通过 graph reflection 猜 child，不因 `ResourcePath == ""` 自动取得 ownership。

规则：

- factory 在创建时登记每个 wrapper 和显式 disposal order；
- borrowed child 只登记为 borrowed，不进入 dispose list；
- owner drain 前先清所有 consumer 字段；
- 每个 exclusively-owned wrapper 最多调用一次 `Dispose()`；
- scope 关闭后创建、转移或访问 owned wrapper 在 strict 模式失败；
- Node 和 process content 进入 scope 时原子拒绝，不能部分 retain 后再报错；
- `GC.SuppressFinalize` 不属于 scope API。

运行时的动态 `AttributeModifier Resource` 必须迁为 plain `AttributeModifierDefinition/Value`，
`DerivedAttributeRule` 改用 `IReadOnlyDictionary<StringName, int>`。authored `AttributeModifier Resource`
只在 content projection 输入端存在。

### GodotProjectionLease

save、UI、trace、preview 和 Godot API 投影由短期 lease 拥有：

```csharp
using GodotProjectionLease<Godot.Collections.Dictionary> payload =
    SavePayloadProjection.Build(state);

file.StoreVar(payload.Value);
```

projection builder 逐个登记自己创建的 container，lease 按显式顺序释放。projection 不能缓存进
runtime state；同步 consumer 返回后立即关闭。跨帧 UI 优先持有 plain view model并按帧投影；确实
需要 wrapper 时，由对应 SceneTree Node 持有并在 replace/exit 时关闭 lease。

优先覆盖：

- save payload 与 FileAccess transaction；
- `BattleEventBatch`、battle preview、mutation guard 和 rollback projection；
- AI trace、text/headless snapshot；
- `BattleHudAdapter`、window payload 和 `BattleBoardController` collection；
- battle special profile query snapshot。

`RuntimeStateLifecycle.MarkValueGraphFinalizerless` 随这些边界迁移逐步删除，最终整个 helper 和两套
reflection suppress walker 一并移除。

### LifecycleAuditRegistry 与 ShutdownReport

registry 只保留弱引用诊断，不持有生命周期。必须暴露：

- process content root count，按 canonical path/type；
- active content snapshot epoch；
- active owner/lease/scope/job 数；
- created、disposed、transferred、escaped、unknown 数；
- normal-phase suppress count；
- quarantine count；
- shutdown 各阶段耗时与异常。

`ShutdownReport` 只保存 Quit 前可以确定的结果：最终 pre-quit phase、requested/effective exit code、
每个 owner 的关闭结果和所有 invariant violation。production 记录结构化错误并尽量完成退出；
strict test 发现 violation 时退出码必须非零。`SceneTree.Quit` 之后才出现的 stderr、ObjectDB 和
resource-in-use 总结由外部 Python runner 收集并判定，不能假装已经写入进程内 report。

audit issue 分成 `Violation` 与 `LegacyDebt`。`Violation` 在当前阶段 strict gate 中立即失败；
`LegacyDebt` 只能是阶段矩阵中精确列出的既有调用点，并记录 source file、owner domain 和删除阶段。
新增 debt、扩大已有 debt 覆盖或越过声明删除阶段都按 `Violation` 处理。最终阶段不允许任何
`LegacyDebt`。

## 生命周期流程

### 启动

```text
ApplicationLifetimeCoordinator._Ready
→ 禁用 AutoAcceptQuit
→ ProcessContentHost.Load/Validate/Project
→ seal host
→ 创建 ContentSnapshot(epoch=1)
→ GameSession 借用 snapshot 并创建 GameRoot/catalog view
→ Running
```

内容加载必须从 `GameSession` constructor 移到 coordinator 可控的初始化阶段，避免 GameSession 在
process owner 尚未就绪时自行建立 raw registry graph。

### 普通 session recreate

```text
Session A Running
→ CloseNormal
→ runtime/battle/service/lease drained
→ GameRoot A invalidated
→ Session A leaves tree
→ ProcessContentHost 与 snapshot 保持存活
→ Session B binds same snapshot
```

production 不强制 GC；correctness regression 在 A 关闭后执行 GC barrier，再创建 B，以主动暴露
错误 suppress、stale catalog 和 borrowed wrapper 失效。

### Battle / AI decision

```text
BattleLifetime opens
→ borrow ContentSnapshot epoch
→ create plain BattleState/services
→ each AI decision opens AiDecisionLease
→ each preview/trace opens ProjectionLease
→ decision finally clears all borrowers and closes leases
→ battle end disposes module and closes BattleLifetime
```

AI mutation guard 和 preview clone 必须使用 typed CLR view；只有真正进入 Godot API、save 或展示
边界时才创建 Godot collection。

### Process shutdown

state machine：

```text
Running
→ Quiescing
→ RuntimeDrained
→ SceneDrained
→ ContentReleased
→ FinalizersDrained | FinalizerBarrierSkipped
→ QuitRequested
```

`ContentReleased → FinalizersDrained` 只在 barrier gate 通过且 GC barrier 成功时发生；gate 失败或
barrier 抛错时进入 `FinalizerBarrierSkipped`，effective exit code 强制非零，然后才允许进入
`QuitRequested`。两个 phase 不能共用一个成功状态。

固定顺序：

1. 原子进入 `Quiescing`，拒绝新 command、session、owner、lease 和 content load；
2. cancel/join worker。当前生产实现没有实际后台 worker，V1 断言 active job 为 0；
3. 关闭 decision/request/projection lease；
4. 关闭 battle、world runtime、facade、session service 和 GameRoot；
5. Free 当前场景与 GameSession，等待 process frame，直到项目 SceneTree owner 已离树；
6. 断言 non-terminal runtime owner、content borrower、scope、lease、job 为 0；此时只允许
   coordinator terminal Node 与尚未清空的 ProcessContentHost 存活；
7. 释放 ContentSnapshot borrower，再清 `ProcessContentHost` canonical roots；
8. barrier gate 通过时，在 coordinator 仍在 SceneTree、Godot native runtime 仍存活时执行一次
   `GC.Collect → GC.WaitForPendingFinalizers → GC.Collect`；gate 失败则明确跳过并标记失败；
9. 读取 pre-quit audit，确认 content root map 已清空并完成 `ShutdownReport`；
10. 调用唯一的 `SceneTree.Quit(effectiveExitCode)`。

进程退出后，外部 runner 再根据 stderr、exit code、ObjectDB/resource-in-use 和 fatal marker 形成
post-exit 结果；post-exit 失败不能回写已经结束的 `ShutdownReport`，但必须让 CI task 失败。

如果某一步失败，记录异常并继续其他仍可安全执行的 teardown/content release。只有 barrier gate
确认不变量满足时才运行强制 GC；仍有 active owner 时必须跳过 barrier、记录
`FinalizerBarrierSkipped` 并以非零码请求退出。barrier 自身抛错时同样记录 fatal diagnostic 并请求
退出，防止应用永久挂起。以上两种情况都属于生命周期失败，不能表述为正常完成。

## Test exit

`TestHarness.Finish` 不再执行 owner cleanup 或 GC。它只生成幂等 `TestResult`。新增共享
`TestExitCoordinator` 作为适配器：它把 `TestResult` 映射为 `ShutdownCallerResult`，连同 requested
exit code 交给 production
`ApplicationLifetimeCoordinator`，但不自行执行 barrier 或 Quit。所有测试采用同一顺序：

```text
test body completes
→ dispose test-owned runner/facade/session
→ TestExitCoordinator submits ShutdownRequest/ShutdownCallerResult
→ ApplicationLifetimeCoordinator closes SceneTree owners
→ coordinator asserts owner counters
→ coordinator runs GC barrier while Godot alive
→ coordinator emits PASS/FAIL once
→ coordinator calls SceneTree.Quit
```

Test mode 只改变 report 标签和 effective exit code 合并规则，不改变 owner teardown/barrier/Quit 的
唯一执行者。

最终 lifecycle correctness lane 固定配置：

- `MAGIC_LIFECYCLE_STRICT=1`；
- `finalizer-crash-retries=0`；
- quarantine disabled；
- `NoGCRegion` disabled；
- unsafe reference、ObjectDB/resource-in-use、gchandle/finalizer fatal 不做宽泛豁免；
- 首次失败就是任务失败。

兼容性 smoke lane 可以在迁移早期暂时保留当前 runner 行为，但不能作为 lifecycle correctness gate；
最终在全量门通过后删除 retry 和 quarantine 支持。

阶段 1 先增加一个范围受控的 lifecycle-order lane：它同样使用 strict 与 retries=0，并禁止被测
domain 使用 quarantine，但只要求退出 state 顺序、owner close 幂等和零 finalizer fatal；尚未迁移
domain 的 wrapper/resource-in-use 计数必须精确记录为 baseline，不能用该阶段测试宣称最终闭环。

## Error handling 与幂等性

- coordinator、session、battle、scope 和 lease 都有单向状态机，关闭后不允许回到 Running/Open。
- 重复 close 返回同一 report 或 no-op，不重复递增 catalog revision，不重复 Dispose wrapper。
- owner registration 在 Quiescing/Closed 阶段原子失败。
- transfer 必须同时从旧 owner 注销并向新 owner 注册；任一步失败都回滚，不允许双 owner。
- graph validation 在任何 retain/mutation 前完成；混入 Node、process content 或另一 scope owner 时原子拒绝。
- shutdown callback 不吞异常；异常进入 report，并包含 owner id、domain、epoch 和 creation reason。
- 某个 owner close 失败后仍继续关闭其他 owner；到 barrier gate 时若 non-terminal owner/scope/lease
  仍非零，则设置 `FinalizerBarrierSkipped = true`、effective exit code 非零并跳过强制 GC，随后请求
  Quit。不能一边保留 active owner，一边违反不变量强行运行 finalizer barrier。
- `_ExitTree` 仅做 idempotent normal cleanup fallback，不能执行 process-only 行为。

## Save、snapshot 与兼容边界

- `GameSession.SaveVersion` 和 `SaveSerializer` 继续使用 version 12。
- 内部 state/definition 迁移不改变 serialized key、value 类型和 required field。
- save adapter 在边界把 plain state 投影为当前 Godot Dictionary shape；load adapter立即归一化为 plain
  state，不把 payload wrapper 保存进 session/runtime。
- golden save、text snapshot、AI fingerprint 和 battle result 必须在迁移前后相同。
- 若发现不能在 version 12 shape 内完成迁移，停止该任务并请求用户决定是否升级 schema；不自动
  增加旧版 migration。

## 分阶段实施边界

该架构按依赖拆成五个可独立验收的阶段；后续每个阶段使用单独实施计划和提交序列。

### 阶段 1：correctness gate 与显式退出屏障

- 增加 audit counters、shutdown report 和 coordinator state machine；
- 接管 window close 与 `SceneTree.Quit`；
- 修正 `TestHarness` 顺序、NoGCRegion 和 runner correctness lane；
- 区分 normal session close 与 process shutdown；
- 将现有 production quarantine 登记为精确 `LegacyDebt`，禁止新增或扩大调用点，并指定阶段 2
  为删除期限；
- 保持现有内容/runtime representation，先证明正确 teardown 顺序、幂等 shutdown 和专用
  lifecycle regression 首次运行无 finalizer fatal。该阶段不宣称 wrapper leak 已清零。

### 阶段 2：ProjectionLease 与 runtime native owner

- 增加 `NativeLeaseScope` 和 `GodotProjectionLease`；
- 迁 save、event、preview、trace、HUD/window projection；
- 消除动态 `AttributeModifier Resource` 和静态 Godot collections；
- 修复 BattleRuntimeModule 和 AI decision borrower 清理；
- 把 `BattleBoardController` 纳入 scene-owned lease，删除 production quarantine。

### 阶段 3：ProcessContentHost 与非 AI typed ContentSnapshot

- 把 raw registry load 移到 process host；
- 禁止修改 loaded `.tres`；
- 迁 Trait、Profession、Achievement、Quest、Item、Recipe、world config；
- 迁 derived Resource 和 special-profile Dictionary 为 plain definition；
- 递归移除 typed definition 内的 `Variant.Type.Object`。

阶段 3 期间，尚未迁移的 EnemyTemplate/Brain/Action/Roster 保持在现有 raw registry 路径并登记为
`LegacyDebt`；它们不进入已经迁移的 `ContentSnapshot` domain，也不能扩散到新的 runtime API。
阶段 4 必须整体迁移并删除这条现有边界，不能长期形成双 catalog。

### 阶段 4：Enemy/AI authored content 与剩余边界

- 把 EnemyTemplate、EnemyAiBrain、EnemyAiAction、WildEncounterRoster 整体投影为 immutable
  definitions；
- 把 EnemyAiAction 的条件、参数和 transition payload 全量归一化为 typed action definition；
- runtime plan 不再按 Resource instance id 建 metadata；
- 清除 mutation guard、preview clone 和 simulation fixture 中不必要的 Godot collection 中转。

### 阶段 5：删除止血基础设施并收紧 CI

- 删除 `RuntimeStateLifecycle`；
- 删除 production reflection suppress walker；
- 删除 `StaticStrongWrappers`、test quarantine 和手写 Godot wrapper suppress；
- 全量 runner 固定 `retries=0`；
- 运行重复 session/battle/content soak 与完整回归。

在阶段 1 完成前不开始阶段 2；在 projection lease 稳定前不删除现有 suppress 覆盖；在阶段 3 的
plain snapshot 覆盖一个内容域并通过同域回归前，不批量迁移下一个内容域。

阶段性验收是累积的：阶段 1 只证明 shutdown 顺序与专用 fatal gate；阶段 2 证明 runtime lease
回到基线；阶段 3 证明 content root/snapshot 不增长；阶段 4 证明 AI/runtime 不再借用 authored
Resource；阶段 5 才执行本 spec 的全部静态、行为和稳定性合同。

| 阶段 | 当阶段必须通过 | 暂时允许 | 当阶段删除 |
|---|---|---|---|
| 1 | lifecycle-order strict、retries=0、shutdown phase/幂等、被测域零 fatal | 阶段矩阵精确登记的 `LegacyDebt` 与未迁移域 baseline | 普通 session/process shutdown 混用、TestHarness 提前 GC、NoGCRegion |
| 2 | runtime lease/scope 回基线、Battle/AI borrower 清空、被迁移 projection 零 escape | process content static pool | production BattleBoard quarantine、被迁移域 RuntimeStateLifecycle 调用 |
| 3 | canonical path 不增长、session A/B 复用、已迁移 snapshot domain 无 Resource/Object Variant | 精确登记的现有 Enemy/AI raw registry `LegacyDebt` | 已迁移 content domain 的全 wrapper 强持有与 reflection cleanup |
| 4 | Enemy/AI catalog 与 plan/decision 全 typed、热路径无 authored Resource/Godot collection 中转 | 仅最终兼容性 smoke runner | Enemy/AI raw registry debt、Resource action fallback、instance-id metadata |
| 5 | 本 spec 全部静态/行为/稳定性合同 | 无 | 剩余 suppress、reflection walker、quarantine、retry 与宽泛日志豁免 |

## 验收合同

### 静态检查

1. production 中 `quarantineOnDrain: true` 为 0。
2. `SceneTree.Quit` 的 production 调用者只有 `ApplicationLifetimeCoordinator`。
3. runtime state 字段中的 `Resource`、Godot Array/Dictionary 和 `Variant.Type.Object` 为 0。
4. 正式运行路径中的 `new AttributeModifier` 为 0。
5. 每个 runtime-created Resource/Duplicate 都能静态追溯到 native factory/scope。
6. 每个返回 Godot wrapper graph 的 projection API 都返回 lease 或接收显式 owner。
7. project code 对 Godot wrapper 的直接 `GC.SuppressFinalize` 为 0。
8. reflection walker 不在 production cleanup 调用图中。

### 行为检查

1. 同一路径重复加载、重复 session bind 时 process content root count 不增长。
2. catalog/query 调用 10,000 次后 process-owned wrapper count 不增长。
3. normal session close 的 process-content suppress count 恒为 0。
4. session A close 后强制 GC，再创建 session B，能够读取并实际使用同一 content snapshot。
5. battle start/end、AI decision、preview、save/load 后 active battle/request/projection owner 回到基线。
6. Dispose/Close 重复调用不改变结果、不重复 revision、不重复释放。
7. strict 模式 unknown ownership、owner conflict、escaped lease 和 close-after-use 均为 0。
8. shutdown barrier 前 non-terminal runtime owner、content borrower、scope、lease、job 为 0，
   ProcessContentHost root map 已清空；只允许 coordinator terminal Node 存活。

### 稳定性检查

1. stderr 中零 `gchandle.is_released`、`GodotObject.Finalize`、`Handle is not initialized`、
   `godotsharp_variant_destroy` 和 project-owned unsafe reference。
2. project-owned ObjectDB/resource-in-use count 为 0；任何 engine-originated baseline 必须按精确
   type/owner 解释，不能按整行字符串豁免。
3. lifecycle soak 使用固定 seed 和相同场景运行 110 次单进程
   session → battle → AI → preview → save/load → teardown：前 10 次 warm-up，后 100 次计量。
   每轮 teardown 后等待 2 个 process frame，执行
   `GC.Collect → WaitForPendingFinalizers → GC.Collect`，再等待 1 帧并采样
   `GC.GetTotalMemory(false)`、`Process.GetCurrentProcess().PrivateMemorySize64` 和 lifecycle owner
   counters。owner/root/lease 的 warm-up 基线定义为第 10 轮 GC 后的完整 counter vector，后续每轮
   必须与该 vector 精确相等。内存 baseline 定义为第 11–20 轮样本中位数；第 101–110 轮中位数
   相对该 baseline，managed heap 增量不得超过 `max(8 MiB, baseline × 5%)`，process-private 增量
   不得超过 `max(32 MiB, baseline × 10%)`；后 100 轮最小二乘趋势斜率分别不得高于 64 KiB/轮
   和 256 KiB/轮。
4. AI 子集与完整回归各运行 10 轮、`jobs=16`、`finalizer-crash-retries=0`；`jobs=16` 指 16 个
   独立 Godot 进程，不代表单进程 AI worker。
5. save version 12 golden payload、text/headless snapshot、AI fingerprint 和战斗结果保持一致。

## 预计文件边界

新增的 owner 文件应保持职责单一：

- `scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs`：autoload adapter、close notification、
  process shutdown state machine；
- `scripts/systems/lifecycle/ApplicationShutdownPhase.cs`：关闭 phase enum；
- `scripts/systems/lifecycle/ShutdownRequest.cs`：production/test 共用的退出请求与 caller result DTO；
- `scripts/systems/lifecycle/ShutdownReport.cs`：结构化退出结果；
- `scripts/systems/content/ProcessContentHost.cs`：canonical raw content roots 与 sealed load phase；
- `scripts/systems/content/EngineAssetResolver.cs`：process-shared path-backed 引擎资产的 canonical
  load/borrow 边界；
- `scripts/systems/content/ContentSnapshot.cs`：immutable typed catalog root；
- `scripts/utils/NativeLeaseScope.cs`：显式 runtime-native owner；
- `scripts/utils/GodotProjectionLease.cs`：短期 Godot projection owner；
- `tests/shared/TestExitCoordinator.cs`：测试关闭适配器。

现有 `GodotObjectOwnership.cs` 在迁移期只保留 audit/bridge，并最终缩减为弱引用诊断 registry；不把
新 coordinator、content host、lease 和 report 再合并进这个文件。

## 项目上下文单元影响

实施会改变以下 owner/read-set：

- CU-02：新增 application/process content owner；`GameSession` 从内容 owner 改为 snapshot borrower；
- CU-06：`GameRuntimeFacade` shutdown 由 coordinator 调度，但仍不拥有生命周期总状态；
- CU-15/CU-16：battle 与 AI decision 获得显式 lifetime/lease；
- CU-18：跨帧展示 wrapper 由 UI Node lease 拥有；
- CU-19：`TestHarness.Finish` 不再被描述为完整 barrier，测试退出改由 `TestExitCoordinator`；
- CU-21：headless runner 必须委托同一 shutdown pipeline。

`docs/design/project_context_units.md` 在 spec 提交时只增加本设计的推荐读入口并纠正当前 TestHarness
边界；未来 owner 链在对应实施阶段落地后再同步更新，不能提前把尚未实现的结构写成当前事实。

## 已决策事项

- 采用方案 B，不采用持续 suppress/quarantine，也暂不采用离线内容编译。
- 最终状态不保留 project-level Godot wrapper suppress fallback。
- V1 无运行时内容热重载。
- save version 12 和当前 payload shape 不变。
- raw `.tres` 是 immutable authoring source，runtime 只消费 plain snapshot。
- process shutdown 必须在 Godot 存活时完成 owner drain 与 GC barrier。
- 完整实施拆成五个顺序阶段，每阶段单独计划、TDD、验证和提交。
