# GodotSharp 生命周期 / finalizer crash 处理机制

## Problem

这是一个 **GodotSharp 生命周期 / finalizer crash**，不是 AI 业务逻辑失败。日志里 finalizer 线程进入 `Godot.GodotObject.Dispose(bool)`，随后命中 `gchandle.is_released()` fatal；而且固定出现在 16 线程 headless regression 的进程退出 / GC finalizer 阶段。

你已经验证过：

- 单纯给 runtime state `RefCounted` 构造时 `GC.SuppressFinalize` 不够，AI 子集仍然固定 fatal；
- 对 `GD.Load / ResourceLoader.Load` 得到的 `.tres` 静态内容资源在 load/register 阶段全局 suppress 会破坏重复 `GD.Load` / 重复 `GameSession`，出现 `Handle is not initialized`；
- 测试只是高频触发，正式代码里同样存在 `new` / `Duplicate(true)` 的 transient Resource graph。

这些约束说明问题必须在生产代码层面建立 lifecycle / ownership 协议，而不是在几个测试里补 `try/finally Dispose`。

我的判断是：**当前方向部分正确，但边界错了**。

正确的部分是：你已经意识到要区分 `.tres` 内容资源和 runtime/transient 资源。

错误的部分是：你现在在这些消费点里尝试判断并 suppress：

```text
BattleAiService.Setup
BattleAiContext.RebuildSkillDefs
BattleAiScoreService.BuildSkillScoreInput
BattleAiRuntimeActionPlan.AddAction
```

消费点只知道“我拿到了一个 `SkillDef` / `EnemyAiAction`”，但不知道它是：

```text
GD.Load / ResourceLoader.Load 得到的 .tres borrowed content
手工 new 出来的 transient resource
Duplicate(true) 出来的 transient resource graph
被其他 owner 托管的 runtime graph
被 metadata / Godot collections / Variant 间接持有的 shutdown graph
```

所以消费点做 suppress 太晚，也太模糊。

应该改成：

> **在创建 / Duplicate 点声明 ownership；在 owner teardown 时统一 drain；消费点只做 ownership audit，不做 suppress。**

---

## Root Cause Hypothesis

根因假设如下：同一批 CLR 类型，例如：

```text
SkillDef
CombatSkillDef
CombatEffectDef
CombatCastVariantDef
EnemyAiBrainDef
EnemyAiStateDef
EnemyAiAction
EnemyAiTransitionConditionDef
```

同时承担了三种完全不同的生命周期角色：

```text
1. .tres / GD.Load / ResourceLoader.Load 得到的静态内容资源；
2. AI / runtime / test 中手工 new 或 Duplicate(true) 出来的 transient resource graph；
3. 某些被 runtime state、metadata、Godot collections、Dictionary、Variant 间接引用的 shutdown graph。
```

在 headless regression 退出时，可能发生这种顺序：

```text
GameSession / SceneTree / Godot native side 开始释放
↓
部分 GodotObject / Resource 的 native handle / gchandle 已经释放
↓
C# wrapper 仍然可被 GC finalizer 看到
↓
finalizer 调 GodotObject.Dispose(false)
↓
Dispose 发现 gchandle 已 released
↓
fatal
```

结合你提供的固定失败测试和代码边界，最可能的遗漏点是：

```text
AI scoring / action plan / runtime skill execution
    ↓
new / Duplicate transient CombatEffectDef / EnemyAiAction / CombatCastVariantDef
    ↓
被 Godot.Collections.Array / Dictionary / Variant / metadata / action plan 间接持有
    ↓
没有被明确 owner 托管，也没有在 teardown 前 suppress / clear
    ↓
退出时进入 finalizer
```

因此，重点不是“所有对象 Dispose”，而是建立 **Borrowed vs Owned** 的硬边界。

---

## Object Taxonomy

### 1. Borrowed Static Content Resource

代表类型：

```csharp
SkillDef
CombatSkillDef
CombatEffectDef
CombatCastVariantDef
EnemyAiBrainDef
EnemyAiStateDef
EnemyAiAction
EnemyAiTransitionConditionDef
ItemDef
BattleSpecialProfileManifest
BattleAiScoreProfile
```

来源：

```csharp
GD.Load<Resource>(path)
ResourceLoader.Load<Resource>(path)
```

典型特征：

```text
ResourcePath 非空
或者已经由 ContentRegistry / ContentCache 注册为 borrowed content
```

注意：**`ResourcePath` 只能作为辅助信号，不能作为唯一判断依据。**

原因：

```text
1. 手工 new 的 Resource 也可能被错误设置 ResourcePath；
2. duplicate / subresource 的 ResourcePath 语义可能复杂；
3. pathless Resource 不等于 owned transient；
4. borrowed content graph 里可能包含 pathless subresource。
```

生命周期策略：

```text
owner: ContentRegistry / ContentCache / Godot resource cache
AI / BattleRuntime / GameSession: 只借用，不拥有
normal teardown: 清引用，不 Dispose，不 suppress
process shutdown: 可 shutdown-only suppress
```

禁止：

```text
禁止在 load/register 阶段 GC.SuppressFinalize
禁止在 BattleAiService.Setup 里 Dispose / suppress
禁止在 RebuildSkillDefs 里 Dispose / suppress
禁止把 borrowed content 当成 transient owned graph
禁止运行时修改 borrowed content；需要改就 Duplicate 成 owned transient
```

---

### 2. Owned Transient Runtime Resource Graph

来源：

```csharp
new SkillDef { ... }
new EnemyAiBrainDef { ... }
new CombatEffectDef { ... }
resource.Duplicate(true)
CombatEffectDef.DuplicateForRuntime()
BattleAiActionAssembler duplicate EnemyAiAction
BattleAiScoreService scoring 中 pathStepEffect.DuplicateForRuntime()
BattleDamageResolver / skill execution 中 new 或 duplicate CombatEffectDef
EnemyAiAction 内部创建 CombatCastVariantDef
```

这些是这次 AI 子集最需要处理的对象。

生命周期：

```text
owner: GameSession / BattleRuntimeModule / BattleAiEvaluationScope / RuntimeResourceScope
创建时: 必须 MarkTransientOwned
使用中: 可以传给 AI service / scorer / resolver，但 owner 不转移
teardown: owner scope 统一 suppress graph + clear references
final GC drain: 只在 owner teardown 后执行
```

短期建议：

```text
对 owned transient Resource graph：创建/duplicate 后立即 GC.SuppressFinalize graph
不要依赖 finalizer
不要在消费点临时猜测
```

这会牺牲一部分“让 finalizer 自动释放 wrapper/native ref”的语义，但你当前目标是先消除 GodotSharp finalizer fatal。后续再评估 deterministic Dispose 是否能安全启用。

---

### 3. Runtime / Save RefCounted Data Container

代表类型：

```csharp
BattleState
BattleUnitState
PartyState
PartyMemberState
UnitProgress
AttributeSnapshot
EquipmentState
WarehouseState
EquipmentInstanceState
TraitInstanceState
TraitRollValueState
BattleCommand
BattlePreview
AttackPreviewData
BattleCellState
BattleStatusEffectState
```

这些类型本质是业务状态，不应该依赖 Godot `RefCounted` 生命周期。

短期策略：

```text
构造时 GC.SuppressFinalize 可以保留
但要集中化，不能散落在每个构造函数里
BattleState / PartyState / Preview / Command 仍需要 owner graph cleanup
teardown 时清 Godot.Collections.Array / Dictionary 中的引用
不要对这些 state 调 Object.Free()
谨慎对待 Dispose；短期优先 suppress + clear
```

长期策略：

```text
迁移为 plain C# class / record
Godot.Collections.Array<T> → List<T>
Godot.Collections.Dictionary → Dictionary<K,V>
Resource 引用 → id / snapshot / plain DTO
只在 Godot serialization / editor boundary 使用 Resource / RefCounted
```

这是根治方向。runtime/save state 作为 `RefCounted` 会把纯业务数据拖进 GodotObject finalizer 系统。

---

### 4. Node / Autoload / SceneTree Owner

代表：

```text
Node
Control
Node2D
SceneTree 下的 runtime scene
Autoload singleton
```

生命周期：

```text
owner: SceneTree / parent Node
释放: queue_free / parent free
C# 引用: queue_free 后清空
```

禁止：

```text
禁止用 GC.SuppressFinalize 代替 queue_free
禁止在 worker thread teardown Node
禁止把 Node 引用放进 static cache
禁止对 SceneTree-owned child 乱 Dispose
```

---

### 5. Plain C# Service / Helper

代表：

```csharp
BattleAiService
BattleAiContext
BattleAiScoreService
BattleAiRuntimeActionPlan
BattleRuntimeModule
GameRuntimeFacade
ContentRegistry
```

生命周期：

```text
它们本身不应该继承 GodotObject
可以 IDisposable / Teardown
可以拥有 transient scope
可以借用 content registry 的 Resource
```

核心规则：

```text
Service 可以拥有 scope
Service 不拥有 borrowed content
Service 不能在消费 borrowed content 时 suppress
Service teardown 必须 clear 所有 GodotObject 引用
```

---

## Recommended Lifecycle Protocol

建议引入一条总协议：

```text
Borrowed content 不动 finalizer；
Owned transient 创建即登记；
Runtime state 短期 finalizerless；
Node 交给 SceneTree；
所有 GodotObject 引用在 owner teardown 时清空；
GC drain 只发生在所有 owner teardown 之后。
```

---

### 1. Borrowed Content Protocol

加载阶段：

```csharp
var resource = GD.Load<Resource>(resourcePath);

if (resource is SkillDef skillDef)
{
    GodotContentOwnership.RegisterBorrowedContent(skillDef, resourcePath);
    _skillDefs[skillDef.skill_id] = skillDef;
}
```

只登记，不 suppress。

Session 使用阶段：

```text
GameSession / BattleAiService / BattleAiContext 可以缓存 borrowed reference
但必须在 teardown 清掉引用
```

进程 shutdown 阶段：

```text
所有 GameSession 都结束
没有后续 GD.Load / ResourceLoader.Load
没有后续重复 GameSession
ContentRegistry.PrepareForProcessShutdown()
    → shutdown-only suppress borrowed content graph
    → clear registry dictionaries
runner CollectPendingFinalizers()
```

这一步是你踩坑之后的折中点：**不是 load/register suppress，而是 shutdown-only suppress**。

---

### 2. Owned Transient Resource Protocol

所有这些创建点必须改：

```csharp
new CombatEffectDef()
new EnemyAiAction()
new CombatCastVariantDef()
Duplicate(true)
DuplicateForRuntime()
```

统一通过 factory / scope：

```csharp
var effect = runtimeResourceFactory.NewCombatEffectDef(e =>
{
    e.effect_type = "damage";
    e.power = 10;
    e.target_filter = "any";
}, "ai_score_path_step");
```

或者：

```csharp
var action = runtimeResourceFactory.DuplicateAction(sourceAction, "BattleAiActionAssembler");
```

创建后立即：

```text
MarkTransientOwned(root)
    → assert root is not borrowed content
    → typed walk graph
    → register all pathless owned Resource
    → GC.SuppressFinalize on owned GodotObject graph
```

teardown 时：

```text
scope.DrainFinalizerRisk()
    → suppress graph again, idempotent
    → clear root list
    → clear service caches / action plans / metadata
```

这里借用的是 .NET Dispose pattern 的原则：deterministic cleanup 后用 `GC.SuppressFinalize(this)` 防止 finalizer 再运行。但在当前方案里，`GC.SuppressFinalize` 的作用不是“释放资源”，而是防止 GodotSharp wrapper 在进程退出时走到危险 finalizer 路径。

---

### 3. Runtime State Protocol

短期：

```text
构造时 suppress 保留
增加 RuntimeStateGraphWalker.SuppressAndClear(root)
BattleRuntimeModule.Teardown 里从 BattleState / PartyState / Preview root 统一处理
```

示例：

```csharp
internal static class RuntimeStateLifecycle
{
    internal static T MarkFinalizerless<T>(T state) where T : GodotObject
    {
        if (state != null)
            GC.SuppressFinalize(state);
        return state;
    }

    internal static void SuppressStateGraph(BattleState state)
    {
        RuntimeStateGraphWalker.Visit(state, obj =>
        {
            if (obj != null)
                GC.SuppressFinalize(obj);
        });
    }
}
```

构造函数里不要直接写裸 `GC.SuppressFinalize(this)`，统一成：

```csharp
public BattleUnitState()
{
    RuntimeStateLifecycle.MarkFinalizerless(this);
    RefreshFootprint();
}
```

长期：

```text
BattleState / PartyState / AttributeSnapshot / EquipmentState / WarehouseState 全部迁移为 plain C# class
```

---

### 4. Thread / Runner Protocol

runner / facade 的顺序必须是：

```text
stop accepting new jobs
cancel / finish AI workers
join all threads/tasks
teardown GameSession / BattleRuntimeModule / AI services
shutdown-only suppress content cache if process is exiting
GC.Collect()
GC.WaitForPendingFinalizers()
GC.Collect()
```

不要在 AI job 还可能访问 `Resource` 时 drain finalizer。

---

## APIs To Add

### 1. Ownership Kind

```csharp
internal enum GodotObjectOwnershipKind
{
    Unknown = 0,

    // GD.Load / ResourceLoader.Load content.
    BorrowedContent,

    // new / Duplicate / runtime-generated Resource graph.
    OwnedTransientResource,

    // BattleState / PartyState / Preview / Command etc.
    RuntimeState,

    // SceneTree owned Node.
    NodeTreeOwned,

    // Borrowed content only during final process shutdown.
    ShutdownBorrowedContent
}
```

---

### 2. Ownership Registry

```csharp
internal static class GodotObjectOwnershipRegistry
{
    private sealed class Entry
    {
        public GodotObjectOwnershipKind Kind;
        public WeakReference Owner;
        public string Reason;
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<ulong, Entry> Entries = new();

    internal static void Register(
        GodotObject obj,
        GodotObjectOwnershipKind kind,
        object owner,
        string reason
    )
    {
        if (obj == null)
            return;

        lock (Sync)
        {
            Entries[obj.GetInstanceId()] = new Entry
            {
                Kind = kind,
                Owner = owner != null ? new WeakReference(owner) : null,
                Reason = reason ?? ""
            };
        }
    }

    internal static bool TryGetKind(GodotObject obj, out GodotObjectOwnershipKind kind)
    {
        kind = GodotObjectOwnershipKind.Unknown;
        if (obj == null)
            return false;

        lock (Sync)
        {
            if (!Entries.TryGetValue(obj.GetInstanceId(), out var entry))
                return false;

            kind = entry.Kind;
            return true;
        }
    }

    internal static bool IsOwnedTransient(GodotObject obj)
    {
        return TryGetKind(obj, out var kind)
            && kind == GodotObjectOwnershipKind.OwnedTransientResource;
    }

    internal static void AssertOwnedTransient(GodotObject obj, string reason)
    {
        if (!IsOwnedTransient(obj))
        {
            throw new InvalidOperationException(
                $"Expected owned transient GodotObject. " +
                $"type={obj?.GetType().Name}, reason={reason}"
            );
        }
    }
}
```

注意：

```text
这里用 GetInstanceId 做 key，主要用于 audit / assert。
不要把它当成强 ownership 存储。
不要因为 registry 里有对象就延长它的生命周期。
```

---

### 3. Borrowed Content Registry

```csharp
internal static class GodotContentOwnership
{
    private static readonly object Sync = new();
    private static readonly HashSet<ulong> BorrowedContentIds = new();

    internal static void RegisterBorrowedContent(Resource root, string resourcePath)
    {
        if (root == null)
            return;

        GodotTypedResourceGraphWalker.Visit(root, obj =>
        {
            if (obj is not Resource resource)
                return GodotGraphVisitResult.Continue;

            lock (Sync)
                BorrowedContentIds.Add(resource.GetInstanceId());

            GodotObjectOwnershipRegistry.Register(
                resource,
                GodotObjectOwnershipKind.BorrowedContent,
                owner: null,
                reason: resourcePath
            );

            return GodotGraphVisitResult.Continue;
        });
    }

    internal static bool IsBorrowedContent(Resource resource)
    {
        if (resource == null)
            return false;

        lock (Sync)
            return BorrowedContentIds.Contains(resource.GetInstanceId());
    }

    internal static void AssertBorrowedContent(Resource resource, string reason)
    {
        if (!IsBorrowedContent(resource))
        {
            throw new InvalidOperationException(
                $"Expected borrowed content resource. " +
                $"type={resource?.GetType().Name}, path={resource?.ResourcePath}, reason={reason}"
            );
        }
    }

    internal static void ShutdownSuppressBorrowedContentGraph(Resource root)
    {
        if (root == null)
            return;

        GodotTypedResourceGraphWalker.Visit(root, obj =>
        {
            if (obj is not Resource resource)
                return GodotGraphVisitResult.Continue;

            if (!IsBorrowedContent(resource))
                return GodotGraphVisitResult.SkipChildren;

            GodotObjectOwnershipRegistry.Register(
                resource,
                GodotObjectOwnershipKind.ShutdownBorrowedContent,
                owner: null,
                reason: "process shutdown"
            );

            GC.SuppressFinalize(resource);
            return GodotGraphVisitResult.Continue;
        });
    }
}
```

关键点：

```text
RegisterBorrowedContent 只登记，不 suppress。
ShutdownSuppressBorrowedContentGraph 只能在 process shutdown 阶段调用。
普通 GameSession.Dispose 不允许调用 shutdown-only suppress。
```

---

### 4. Transient Resource Scope

```csharp
internal sealed class GodotTransientResourceScope : IDisposable
{
    private readonly List<Resource> _roots = new();
    private int _disposed;

    public string Name { get; }

    public GodotTransientResourceScope(string name)
    {
        Name = name;
    }

    public T Own<T>(T resource, string reason) where T : Resource
    {
        ThrowIfDisposed();

        if (resource == null)
            return null;

        GodotRuntimeResourceOwnership.MarkTransientOwned(resource, this, reason);
        _roots.Add(resource);
        return resource;
    }

    public void DrainFinalizerRisk()
    {
        if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
            return;

        foreach (var root in _roots)
            GodotRuntimeResourceOwnership.SuppressOwnedTransientGraph(root);

        _roots.Clear();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        foreach (var root in _roots)
            GodotRuntimeResourceOwnership.SuppressOwnedTransientGraph(root);

        _roots.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
            throw new ObjectDisposedException(nameof(GodotTransientResourceScope));
    }
}
```

这个 scope 应该挂在：

```text
GameSession
BattleRuntimeModule
BattleAiEvaluationScope
共享 regression runner 的每个 test/session context
```

不要做全局 static scope，16 线程下很容易把不同测试的对象混在一起。

---

### 5. Runtime Resource Ownership

```csharp
internal static class GodotRuntimeResourceOwnership
{
    internal static T MarkTransientOwned<T>(
        T root,
        GodotTransientResourceScope owner,
        string reason
    ) where T : Resource
    {
        if (root == null)
            return null;

        if (GodotContentOwnership.IsBorrowedContent(root))
        {
            throw new InvalidOperationException(
                $"Cannot mark borrowed content as transient owned. " +
                $"type={root.GetType().Name}, path={root.ResourcePath}, reason={reason}"
            );
        }

        GodotTypedResourceGraphWalker.Visit(root, visitor: obj =>
        {
            if (obj == null)
                return GodotGraphVisitResult.Continue;

            if (obj is Resource resource)
            {
                if (GodotContentOwnership.IsBorrowedContent(resource))
                {
                    throw new InvalidOperationException(
                        $"Owned transient graph contains borrowed content. " +
                        $"root={root.GetType().Name}, child={resource.GetType().Name}, " +
                        $"path={resource.ResourcePath}, reason={reason}"
                    );
                }

                if (!string.IsNullOrEmpty(resource.ResourcePath))
                {
                    throw new InvalidOperationException(
                        $"Transient owned resource has non-empty ResourcePath. " +
                        $"type={resource.GetType().Name}, path={resource.ResourcePath}, reason={reason}"
                    );
                }
            }

            GodotObjectOwnershipRegistry.Register(
                obj,
                GodotObjectOwnershipKind.OwnedTransientResource,
                owner,
                reason
            );

            GC.SuppressFinalize(obj);
            return GodotGraphVisitResult.Continue;
        });

        return root;
    }

    internal static void SuppressOwnedTransientGraph(Resource root)
    {
        if (root == null)
            return;

        GodotTypedResourceGraphWalker.Visit(root, visitor: obj =>
        {
            if (obj == null)
                return GodotGraphVisitResult.Continue;

            if (!GodotObjectOwnershipRegistry.IsOwnedTransient(obj))
            {
                // 不要在这里扩大 suppress 范围。
                // 只处理已经由创建点登记过的 owned transient。
                return GodotGraphVisitResult.SkipChildren;
            }

            GC.SuppressFinalize(obj);
            return GodotGraphVisitResult.Continue;
        });
    }

    internal static void AssertBorrowedOrOwnedKnown(Resource root, string reason)
    {
        if (root == null)
            return;

        if (GodotContentOwnership.IsBorrowedContent(root))
            return;

        if (GodotObjectOwnershipRegistry.IsOwnedTransient(root))
            return;

        throw new InvalidOperationException(
            $"Unknown Resource ownership. " +
            $"type={root.GetType().Name}, path={root.ResourcePath}, reason={reason}"
        );
    }
}
```

关键点：

```text
MarkTransientOwned 只能处理明确 owned 的 root
不能靠 ResourcePath == "" 自动猜
不能从消费点偷偷调用
```

---

### 6. Runtime Resource Factory

建议不要让业务代码直接 `new Resource` 或直接 `Duplicate(true)`。

#### RuntimeSkillDefFactory

```csharp
internal sealed class RuntimeSkillDefFactory
{
    private readonly GodotTransientResourceScope _scope;

    public RuntimeSkillDefFactory(GodotTransientResourceScope scope)
    {
        _scope = scope;
    }

    public SkillDef NewSkill(Action<SkillDef> init, string reason)
    {
        var skill = new SkillDef();
        init?.Invoke(skill);
        return _scope.Own(skill, reason);
    }

    public CombatSkillDef NewCombatProfile(Action<CombatSkillDef> init, string reason)
    {
        var profile = new CombatSkillDef();
        init?.Invoke(profile);
        return _scope.Own(profile, reason);
    }

    public CombatEffectDef NewEffect(Action<CombatEffectDef> init, string reason)
    {
        var effect = new CombatEffectDef();
        init?.Invoke(effect);
        return _scope.Own(effect, reason);
    }

    public CombatEffectDef DuplicateEffect(CombatEffectDef source, string reason)
    {
        if (source == null)
            return null;

        var copy = (CombatEffectDef)source.Duplicate(true);
        copy.@params = copy.@params != null
            ? copy.@params.Duplicate(true)
            : new Godot.Collections.Dictionary();

        return _scope.Own(copy, reason);
    }
}
```

#### RuntimeEnemyAiResourceFactory

```csharp
internal sealed class RuntimeEnemyAiResourceFactory
{
    private readonly GodotTransientResourceScope _scope;

    public RuntimeEnemyAiResourceFactory(GodotTransientResourceScope scope)
    {
        _scope = scope;
    }

    public EnemyAiBrainDef NewBrain(Action<EnemyAiBrainDef> init, string reason)
    {
        var brain = new EnemyAiBrainDef();
        init?.Invoke(brain);
        return _scope.Own(brain, reason);
    }

    public EnemyAiStateDef NewState(Action<EnemyAiStateDef> init, string reason)
    {
        var state = new EnemyAiStateDef();
        init?.Invoke(state);
        return _scope.Own(state, reason);
    }

    public EnemyAiAction NewAction(Action<EnemyAiAction> init, string reason)
    {
        var action = new EnemyAiAction();
        init?.Invoke(action);
        return _scope.Own(action, reason);
    }

    public EnemyAiAction DuplicateAction(EnemyAiAction source, string reason)
    {
        if (source == null)
            return null;

        var copy = (EnemyAiAction)source.Duplicate(true);
        return _scope.Own(copy, reason);
    }

    public CombatCastVariantDef NewCastVariant(
        Action<CombatCastVariantDef> init,
        string reason
    )
    {
        var variant = new CombatCastVariantDef();
        init?.Invoke(variant);
        return _scope.Own(variant, reason);
    }
}
```

---

### 7. Replace `DuplicateForRuntime()`

当前：

```csharp
public CombatEffectDef DuplicateForRuntime()
{
    var copy = (CombatEffectDef)Duplicate(true);
    copy.@params = copy.@params != null ? copy.@params.Duplicate(true) : new();
    return copy;
}
```

建议改成：

```csharp
[Obsolete("Use DuplicateForRuntime(GodotTransientResourceScope owner, string reason).")]
public CombatEffectDef DuplicateForRuntime()
{
    throw new InvalidOperationException(
        "DuplicateForRuntime without owner is forbidden. " +
        "Use DuplicateForRuntime(scope, reason)."
    );
}

public CombatEffectDef DuplicateForRuntime(
    GodotTransientResourceScope owner,
    string reason
)
{
    var copy = (CombatEffectDef)Duplicate(true);
    copy.@params = copy.@params != null
        ? copy.@params.Duplicate(true)
        : new Godot.Collections.Dictionary();

    return owner.Own(copy, reason);
}
```

如果一次性改不完，可以先保留 legacy 版本，但必须打日志：

```csharp
internal static CombatEffectDef DuplicateForRuntimeLegacyUnsafe(CombatEffectDef source)
{
    var copy = (CombatEffectDef)source.Duplicate(true);
    copy.@params = copy.@params != null
        ? copy.@params.Duplicate(true)
        : new Godot.Collections.Dictionary();

    GodotLifecycleAudit.ReportUnsafeDuplicate(copy, "legacy DuplicateForRuntime()");
    return copy;
}
```

AI 子集稳定阶段不建议继续允许 silent legacy。

---

### 8. Typed Graph Walker

当前反射 walker 应该降级为 debug fallback。正式处理用 typed walker。

```csharp
internal enum GodotGraphVisitResult
{
    Continue,
    SkipChildren
}

internal static class GodotTypedResourceGraphWalker
{
    internal static void Visit(
        GodotObject root,
        Func<GodotObject, GodotGraphVisitResult> visitor
    )
    {
        var visited = new HashSet<ulong>();
        VisitObject(root, visitor, visited);
    }

    private static void VisitObject(
        GodotObject obj,
        Func<GodotObject, GodotGraphVisitResult> visitor,
        HashSet<ulong> visited
    )
    {
        if (obj == null)
            return;

        ulong id = obj.GetInstanceId();
        if (!visited.Add(id))
            return;

        var result = visitor(obj);
        if (result == GodotGraphVisitResult.SkipChildren)
            return;

        switch (obj)
        {
            case SkillDef skill:
                VisitObject(skill.combat_profile, visitor, visited);
                break;

            case CombatSkillDef profile:
                VisitArray(profile.effect_defs, visitor, visited);
                VisitArray(profile.passive_effect_defs, visitor, visited);
                VisitArray(profile.cast_variants, visitor, visited);
                break;

            case CombatCastVariantDef variant:
                VisitArray(variant.effect_defs, visitor, visited);
                VisitDictionary(variant.@params, visitor, visited);
                break;

            case CombatEffectDef effect:
                VisitDictionary(effect.@params, visitor, visited);
                break;

            case EnemyAiBrainDef brain:
                VisitArray(brain.states, visitor, visited);
                VisitObject(brain.score_profile, visitor, visited);
                break;

            case EnemyAiStateDef state:
                VisitArray(state.actions, visitor, visited);
                VisitArray(state.transitions, visitor, visited);
                break;

            case EnemyAiAction action:
                VisitDictionary(action.@params, visitor, visited);
                break;

            case EnemyAiTransitionConditionDef transition:
                // state_ids 是 StringName array，不需要处理 GodotObject。
                break;
        }
    }

    private static void VisitArray<T>(
        Godot.Collections.Array<T> array,
        Func<GodotObject, GodotGraphVisitResult> visitor,
        HashSet<ulong> visited
    )
    {
        if (array == null)
            return;

        foreach (var item in array)
            VisitPotentialValue(item, visitor, visited);
    }

    private static void VisitDictionary(
        Godot.Collections.Dictionary dict,
        Func<GodotObject, GodotGraphVisitResult> visitor,
        HashSet<ulong> visited
    )
    {
        if (dict == null)
            return;

        foreach (var key in dict.Keys)
            VisitPotentialValue(key, visitor, visited);

        foreach (var value in dict.Values)
            VisitPotentialValue(value, visitor, visited);
    }

    private static void VisitPotentialValue(
        object value,
        Func<GodotObject, GodotGraphVisitResult> visitor,
        HashSet<ulong> visited
    )
    {
        switch (value)
        {
            case null:
                return;

            case GodotObject obj:
                VisitObject(obj, visitor, visited);
                return;

            case Godot.Collections.Dictionary dict:
                VisitDictionary(dict, visitor, visited);
                return;

            case Godot.Collections.Array array:
                foreach (var item in array)
                    VisitPotentialValue(item, visitor, visited);
                return;

            // Variant 如果项目里实际以 Variant 装 GodotObject，
            // 需要在这里加 Variant.AsGodotObject / Variant.Obj 分支。
            default:
                return;
        }
    }
}
```

---

## Code Placement / Owner Boundaries

### ContentRegistry

职责：

```text
加载 .tres
登记 borrowed content
提供 IReadOnlyDictionary 给 runtime
shutdown-only suppress content graph
```

正常 load：

```csharp
var resource = GD.Load<Resource>(resourcePath);

if (resource is SkillDef skillDef)
{
    GodotContentOwnership.RegisterBorrowedContent(skillDef, resourcePath);
    _skillDefs[skillDef.skill_id] = skillDef;
}

if (resource is EnemyAiBrainDef brainDef)
{
    GodotContentOwnership.RegisterBorrowedContent(brainDef, resourcePath);
    _enemyAiBrains[brainDef.brain_id] = brainDef;
}
```

禁止：

```text
这里不能 GC.SuppressFinalize
这里不能 Dispose
这里不能 DuplicateForRuntime
```

shutdown-only：

```csharp
internal void PrepareForProcessShutdown()
{
    foreach (var skill in _skillDefs.Values)
        GodotContentOwnership.ShutdownSuppressBorrowedContentGraph(skill);

    foreach (var brain in _enemyAiBrains.Values)
        GodotContentOwnership.ShutdownSuppressBorrowedContentGraph(brain);

    _skillDefs.Clear();
    _enemyAiBrains.Clear();
}
```

这个方法只能由 app exit / shared runner 最后阶段调用，不能在普通 `GameSession.Dispose` 里调用。

---

### GameSession

职责：

```text
拥有本 session 的 runtime modules
拥有 session-level GodotTransientResourceScope
不拥有 global content registry
```

结构：

```csharp
internal sealed class GameSession : IDisposable
{
    private readonly GodotTransientResourceScope _resourceScope;
    private readonly BattleRuntimeModule _battleRuntimeModule;
    private int _disposed;

    public GameSession(ContentRegistry contentRegistry)
    {
        _resourceScope = new GodotTransientResourceScope("GameSession");
        _battleRuntimeModule = new BattleRuntimeModule(
            contentRegistry,
            _resourceScope
        );
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _battleRuntimeModule.Teardown();

        _resourceScope.DrainFinalizerRisk();
        _resourceScope.Dispose();
    }
}
```

---

### GameRuntimeFacade

职责：

```text
停止新任务
停止 worker
teardown GameSession
runner/app exit 时触发 content shutdown-only suppress
```

顺序：

```csharp
internal void ShutdownForRegressionRunner()
{
    StopAcceptingNewWork();
    WaitForAllJobsToFinish();

    _currentSession?.Dispose();
    _currentSession = null;

    _contentRegistry.PrepareForProcessShutdown();

    GodotObjectLifecycle.CollectPendingFinalizers();
}
```

---

### BattleRuntimeModule

职责：

```text
拥有 BattleState / Preview / Command / RuntimeResourceFactory
管理 BattleAiService teardown
```

teardown 顺序：

```csharp
internal void Teardown()
{
    _battleAiService.Teardown();

    RuntimeStateLifecycle.SuppressStateGraph(_battleState);
    RuntimeStateLifecycle.SuppressStateGraph(_partyState);

    ClearBattleCaches();
    _battleState = null;
    _partyState = null;
}
```

---

### BattleAiService

`Setup` 不再 suppress：

```csharp
internal void Setup(
    IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = null
)
{
    _enemyAiBrains.Clear();

    if (enemyAiBrains == null)
        return;

    foreach (var entry in enemyAiBrains)
    {
        GodotRuntimeResourceOwnership.AssertBorrowedOrOwnedKnown(
            entry.Value,
            "BattleAiService.Setup"
        );

        _enemyAiBrains[entry.Key] = entry.Value;
    }
}
```

新增 teardown：

```csharp
internal void Teardown()
{
    _runtimeActionPlan?.Clear();
    _runtimeActionPlan = null;

    _enemyAiBrains.Clear();

    _scoreService?.Teardown();
    _context?.Teardown();
}
```

---

### BattleAiContext

`RebuildSkillDefs` 不再 suppress：

```csharp
private void RebuildSkillDefs(
    IReadOnlyDictionary<StringName, SkillDef> skillDefs
)
{
    _skillDefsById = new Dictionary<StringName, SkillDef>();

    foreach (var entry in skillDefs)
    {
        GodotRuntimeResourceOwnership.AssertBorrowedOrOwnedKnown(
            entry.Value,
            "BattleAiContext.RebuildSkillDefs"
        );

        _skillDefsById[entry.Key] = entry.Value;
    }
}

internal void Teardown()
{
    _skillDefsById?.Clear();
    _skillDefsById = null;
}
```

---

### BattleAiScoreService

`BuildSkillScoreInput` 不拥有输入对象，只读取。

如果评分过程中需要 clone：

```csharp
var runtimeEffect = pathStepEffect.DuplicateForRuntime(
    _runtimeResourceScope,
    "BattleAiScoreService.pathStepEffect"
);
```

建议 `BattleAiScoreInput` 尽量改成 plain snapshot：

```csharp
internal sealed class BattleAiScoreInput
{
    public StringName SkillId;
    public int ApCost;
    public int StaminaCost;
    public IReadOnlyList<BattleAiEffectScoreSnapshot> Effects;
}
```

不要让 score input 长期持有 `SkillDef` / `CombatEffectDef` Resource，除非 owner 明确。

---

### BattleAiRuntimeActionPlan

不要只有一个模糊的 `AddAction`。

建议拆成两个 API：

```csharp
internal void AddBorrowedContentAction(
    StringName stateId,
    EnemyAiAction action,
    RuntimeActionMetadata metadata = null
)
{
    GodotContentOwnership.AssertBorrowedContent(
        action,
        "BattleAiRuntimeActionPlan.AddBorrowedContentAction"
    );

    AddActionInternal(stateId, action, metadata);
}

internal void AddOwnedTransientAction(
    StringName stateId,
    EnemyAiAction action,
    RuntimeActionMetadata metadata = null
)
{
    GodotObjectOwnershipRegistry.AssertOwnedTransient(
        action,
        "BattleAiRuntimeActionPlan.AddOwnedTransientAction"
    );

    AddActionInternal(stateId, action, metadata);
}
```

或者至少加 ownership 参数：

```csharp
internal enum ActionResourceOwnership
{
    BorrowedContent,
    OwnedTransient
}

internal void AddAction(
    StringName stateId,
    EnemyAiAction action,
    ActionResourceOwnership ownership,
    RuntimeActionMetadata metadata = null
)
{
    switch (ownership)
    {
        case ActionResourceOwnership.BorrowedContent:
            GodotContentOwnership.AssertBorrowedContent(action, "AddAction");
            break;

        case ActionResourceOwnership.OwnedTransient:
            GodotObjectOwnershipRegistry.AssertOwnedTransient(action, "AddAction");
            break;
    }

    AddActionInternal(stateId, action, metadata);
}
```

这样 `AddAction` 不再偷偷改变生命周期。

---

## Forbidden Actions

这些要作为代码注释、audit 规则、review checklist 固化下来。

### 绝对禁止

```text
1. 禁止在 GD.Load / ResourceLoader.Load / registry register 阶段
   对 .tres content graph GC.SuppressFinalize。

2. 禁止在 BattleAiService.Setup /
   BattleAiContext.RebuildSkillDefs /
   BattleAiScoreService.BuildSkillScoreInput /
   BattleAiRuntimeActionPlan.AddAction
   这些消费点根据类型猜测 suppress。

3. 禁止把 ResourcePath == "" 当成“我拥有它”的充分条件。

4. 禁止对 borrowed content Resource Dispose / Free。

5. 禁止对 RefCounted / Resource 调 Object.Free()。

6. 禁止在 worker 线程还可能访问 GodotObject 时做 finalizer drain。

7. 禁止让 transient Resource 进入 static dictionary / global cache。

8. 禁止运行时直接修改 borrowed .tres content。
   需要修改时必须通过 factory duplicate 成 owned transient。

9. 禁止 reflection SuppressFinalizerGraph 扫任意对象图。

10. 禁止 Node 用 GC.SuppressFinalize 代替 queue_free。
```

---

## Minimal Migration Plan

### Phase 0：先加 audit，不改业务语义

目标：定位还有哪些 pathless Resource 没有 owner。

新增：

```text
GodotObjectOwnershipRegistry
GodotContentOwnership
GodotRuntimeResourceOwnership
GodotTypedResourceGraphWalker
GodotTransientResourceScope
RuntimeSkillDefFactory
RuntimeEnemyAiResourceFactory
```

在这些消费点先改成 assert/log，不 suppress：

```text
BattleAiService.Setup
BattleAiContext.RebuildSkillDefs
BattleAiScoreService.BuildSkillScoreInput
BattleAiRuntimeActionPlan.AddAction
```

日志分类建议：

```text
borrowed_content_registered
transient_owned_registered
transient_graph_suppressed
shutdown_content_suppressed
unowned_pathless_resource_detected
borrowed_content_in_owned_graph
resourcepath_nonempty_transient_detected
```

---

### Phase 1：先稳定 AI 子集 16 线程

优先改固定失败相关路径。

#### 1. 改测试 helper / shared runner 的 graph 创建

把这种：

```csharp
var skill = new SkillDef
{
    skill_id = "friendly_fire_fireball_probe",
    combat_profile = new CombatSkillDef
    {
        effect_defs = new Godot.Collections.Array<CombatEffectDef>
        {
            new CombatEffectDef { effect_type = "damage", power = 10 }
        }
    }
};
```

改成：

```csharp
var skill = skillFactory.NewSkill(skill =>
{
    skill.skill_id = "friendly_fire_fireball_probe";
    skill.display_name = "Friendly Fire Fireball Probe";

    skill.combat_profile = skillFactory.NewCombatProfile(profile =>
    {
        profile.effect_defs = new Godot.Collections.Array<CombatEffectDef>
        {
            skillFactory.NewEffect(effect =>
            {
                effect.effect_type = "damage";
                effect.power = 10;
                effect.target_filter = "any";
            }, "test.friendly_fire.effect")
        };
    }, "test.friendly_fire.profile");
}, "test.friendly_fire.skill");
```

`brain` 同理通过 `RuntimeEnemyAiResourceFactory`。

注意：这不是“在测试里 finally Dispose”，而是让测试使用和生产一致的 runtime factory / ownership 协议。

#### 2. 改正式 duplicate/new 点

优先改这些：

```text
CombatEffectDef.DuplicateForRuntime
BattleAiActionAssembler resource.Duplicate(true) as EnemyAiAction
BattleAiScoreService.Scoring pathStepEffect.DuplicateForRuntime()
BattleDamageResolver new CombatEffectDef
BattleDamageResolver DuplicateForRuntime()
BattleRuntime skill execution new / duplicate CombatEffectDef
EnemyAiAction 内部创建 CombatCastVariantDef
```

所有这些都必须接收：

```csharp
GodotTransientResourceScope scope
```

或者接收：

```csharp
RuntimeSkillDefFactory
RuntimeEnemyAiResourceFactory
```

#### 3. 移除消费点 suppress

删除或禁用这些调用：

```text
BattleAiService.Setup(entry.Value) suppress
BattleAiContext.RebuildSkillDefs(skillDef) suppress
BattleAiScoreService.BuildSkillScoreInput(skillDef/effectDefs) suppress
BattleAiRuntimeActionPlan.AddAction(action) suppress
```

换成：

```text
AssertBorrowedOrOwnedKnown
```

#### 4. 加 BattleAiService.Teardown

确保：

```text
_runtimeActionPlan.Clear()
_score caches clear
_context skill defs clear
_enemyAiBrains clear
metadata clear
```

#### 5. runner 每个 session 统一 drain

共享 runner 做：

```text
session.Dispose()
GodotObjectLifecycle.CollectPendingFinalizers()
```

但 `CollectPendingFinalizers` 前必须保证：

```text
所有 AI job 已结束
所有 service 已 teardown
所有 transient scope 已 drain
```

---

### Phase 2：AI 子集稳定后跑全量 16 线程 10 轮

扩展 walker / factory 到其他 Resource 类型：

```text
ItemDef
BattleSpecialProfileManifest
TraitDef
EquipmentDef
BattleAiScoreProfile
其他 runtime duplicate/new Resource
```

增加 shutdown-only content suppress：

```text
ContentRegistry.PrepareForProcessShutdown
```

增加 teardown audit：

```text
所有 registered owned transient root 已 drain
没有 unowned pathless Resource 残留在 AI service / runtime module
没有 normal-phase borrowed content suppress
没有重复 GameSession 的 Handle is not initialized
```

---

### Phase 3：长期重构

```text
BattleState / PartyState / EquipmentState / WarehouseState
从 RefCounted 迁移为 plain C# class

SkillDef / EnemyAiBrainDef 等 editor content 保持 Resource

runtime skill/effect/action 使用 plain DTO snapshot
只在 content boundary 使用 Resource
```

最终目标是：

```text
Resource = editor/content asset
Plain C# = runtime state / AI scoring / command / preview
Node = scene object
```

---

## Verification Plan

### 1. Build

```bash
dotnet build magic.csproj
```

要求：

```text
无编译错误
Obsolete DuplicateForRuntime 调用全部处理或明确记录
lifecycle audit 无初始化阶段异常
```

---

### 2. AI 子集单轮

```bash
python tests/run_regression_suite.py --jobs 16 --pattern tests/battle_runtime/ai --finalizer-crash-retries 0 --stop-on-failure
```

通过标准：

```text
exit code = 0
没有 FATAL gchandle.is_released
没有 Handle is not initialized
没有 unowned_pathless_resource_detected
没有 borrowed_content_in_owned_graph
没有 normal-phase borrowed suppress
```

---

### 3. AI 子集 10 轮

Linux / macOS shell：

```bash
for i in $(seq 1 10); do
  echo "AI regression round $i"
  python tests/run_regression_suite.py \
    --jobs 16 \
    --pattern tests/battle_runtime/ai \
    --finalizer-crash-retries 0 \
    --stop-on-failure || exit 1
done
```

Windows cmd：

```bat
for /l %i in (1,1,10) do python tests/run_regression_suite.py --jobs 16 --pattern tests/battle_runtime/ai --finalizer-crash-retries 0 --stop-on-failure
```

---

### 4. 全量 16 线程单轮

```bash
python tests/run_regression_suite.py --jobs 16 --finalizer-crash-retries 0 --stop-on-failure
```

---

### 5. 全量 16 线程 10 轮

```bash
for i in $(seq 1 10); do
  echo "full regression round $i"
  python tests/run_regression_suite.py \
    --jobs 16 \
    --finalizer-crash-retries 0 \
    --stop-on-failure || exit 1
done
```

---

### 6. 专门加一个 lifecycle regression

建议新增一个小测试，不测 AI 分数，只测生命周期：

```text
1. 创建 RuntimeResourceScope
2. 用 factory 创建 SkillDef graph
3. graph 中包含：
   - CombatSkillDef
   - effect_defs
   - passive_effect_defs
   - cast_variants
   - params dictionary
   - dictionary 内嵌 Godot.Collections.Array
4. 用 factory 创建 EnemyAiBrainDef graph
5. 传入 BattleAiService / BattleAiRuntimeActionPlan
6. service.Teardown()
7. scope.DrainFinalizerRisk()
8. GC.Collect()
9. GC.WaitForPendingFinalizers()
10. GC.Collect()
```

通过标准：

```text
不 fatal
audit 显示所有 transient resource 被 owned + suppressed
没有 borrowed content 被 normal suppress
```

---

## Risks / Open Questions

### 1. `GC.SuppressFinalize` 不是真正释放

短期用 suppress 是为了避免 GodotSharp finalizer crash，不是为了释放所有 native 资源。对 transient data-like Resource 来说，这是可接受的止血方案；但要加计数日志，防止长时间 gameplay 里泄漏增长。

建议记录：

```text
owned transient resource count by type
suppressed graph count
scope drain count
max live transient roots per session
```

---

### 2. 是否应该对 owned transient Resource 调 `Dispose()`

短期不建议默认启用。

原因：

```text
Resource / RefCounted 可能被 Godot native refcount 管理
Duplicate graph 里可能共享子资源
一旦误 Dispose borrowed content，会回到 Handle is not initialized / invalid handle
```

可以后续加 feature flag：

```text
MAGIC_GODOT_LIFECYCLE_DISPOSE_OWNED_TRANSIENT=1
```

只在 audit 确认 graph 全部 owned、无 borrowed child、无 SceneTree owner 后测试。

---

### 3. `ResourcePath` 不能作为唯一边界

当前 helper 里：

```csharp
if (!string.IsNullOrEmpty(resource.ResourcePath))
    return;
```

这个只能避免一部分 `.tres` 误处理，但不能证明 pathless 就 owned。正确判断应该来自：

```text
ContentRegistry.RegisterBorrowedContent
GodotTransientResourceScope.Own
GodotObjectOwnershipRegistry
```

`ResourcePath` 只能作为 assert 辅助条件。

---

### 4. Reflection walker 可能误伤或漏扫

反射 walker 的问题：

```text
可能扫到 borrowed content
可能触发属性 getter 副作用
可能漏掉 Godot.Collections.Dictionary / Variant 内隐藏对象
可能深度限制 8 导致漏扫
可能在未来字段变化后静默失效
```

建议：

```text
生产默认 typed walker
reflection walker 只在 debug audit / process shutdown fallback 使用
```

允许使用场景：

```text
1. root 已经在 OwnershipRegistry 中标记为 OwnedTransientResource
2. process shutdown-only 阶段，已经禁止后续 GD.Load / GameSession
3. debug 模式下只扫描并报告，不 suppress
```

---

### 5. AI 仍可能持有 borrowed content 太久

`BattleAiContext` 缓存 `SkillDef`，`BattleAiService` 缓存 `EnemyAiBrainDef` 本身可以，但 teardown 必须清掉。否则 content registry shutdown-only suppress 时，AI service 仍持有旧 wrapper，下一次 session 可能复用出错。

所以 teardown 顺序必须是：

```text
AI service clear borrowed refs
Battle runtime clear state
GameSession dispose
ContentRegistry shutdown-only suppress
GC drain
```

不能反过来。

---

### 6. 最终建议的工程原则

最重要的三条：

```text
1. 消费点不改生命周期，只做 ownership assert。
2. 创建点必须声明 ownership，所有 new / Duplicate 进入 factory。
3. .tres borrowed content 只在 process shutdown-only suppress，绝不在 load/register suppress。
```

这套方案落地后，AI 子集的固定 fatal 应该优先从这些链路上消失：

```text
CombatEffectDef.DuplicateForRuntime
BattleAiActionAssembler Duplicate(true)
BattleAiRuntimeActionPlan 缓存 action
score metadata / params dictionary
```
