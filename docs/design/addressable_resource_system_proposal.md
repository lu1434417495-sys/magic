# Addressable 式资源系统方案设计书

> **状态**：设计草案（对抗评审后修订版）  
> **范围**：仅输出设计文档，不修改任何代码  
> **版本**：v1.0（2026-05-29）  

---

## 1. 引言与决策背景

本方案源于对项目当前资源加载系统的全面审计。经红蓝双方对抗性评审，达成以下**不可辩驳的事实共识**：

| 事实 | 来源 |
|---|---|
| 17 个 ContentRegistry（.cs），15 个用 `GD.Load<Resource>`，1 个用 `ResourceLoader.Load` | 代码审计 |
| `GodotContentResourceLifetime` 是 17 行的全局静态 `List<Resource>`，只增不减，无卸载接口 | 代码审计 |
| data/ 目录有 **885 个 .tres 文件，总大小 2.01 MB** | 文件系统统计 |
| 所有加载均在**主线程同步执行**，无任何异步加载 | 代码审计 |
| 项目当前**无任何性能问题报告或启动卡顿反馈** | 现状确认 |

双方分歧在于**响应策略**：红方主张立即引入完整的 Addressable 式系统以偿还技术债务；蓝方认为当前属于"预防性过度工程"，应以最小改动解决真实痛点。

**本最终方案的定位**：采纳红方提出的**完整架构蓝图**作为长期目标，但将实施路径修订为**分阶段、带决策门的渐进式推进**。每一阶段都设置明确的 go/no-go 指标，若未触及阈值则停止推进，避免蓝方警示的"为假想瓶颈支付真实复杂度"。

---

## 2. 当前系统诊断（双方共识版）

### 2.1 真实的债务

1. **全局静态泄露池**：`GodotContentResourceLifetime.Keep()` 将资源钉死在静态列表中，进程生命周期内永不释放。即使 Godot 的 `ResourceLoader` 缓存可弱引用回收，C# 侧的强引用阻止了 GC。
2. **同步阻塞初始化**：`SkillContentRegistry`（1621 行）在构造函数中同步加载 550+ 技能配置并执行 schema 校验。虽然当前无卡顿反馈，但这意味着**启动时间存在一个不可控的阻塞块**。
3. **路径硬编码蔓延**：`res://data/configs/...` 字符串散落在 17 个 Registry 中，目录重构或平台差异化时需改动 C# 源码。
4. **Seed 子资源生命周期真空**：`EnemyContentRegistry` 对 Seed 内嵌 `Array<Resource>` 的元素逐个 `Keep()`，但子资源无独立生命周期管理，Seed 与目录扫描两种模式的归属约定不一致。
5. **Registry 间隐式依赖**：`BattleSpecialProfileRegistry` 依赖 `SkillContentRegistry` 的初始化结果，但依赖关系通过调用顺序硬编码，无显式声明机制。

### 2.2 当前无需解决的问题

| 红方原主张 | 蓝方反驳 | 本方案裁定 |
|---|---|---|
| 2MB 未来会增长，需提前预埋 | 2MB 即使增长 50 倍到 100MB，Godot 原生缓存仍够用 | **暂不采纳"为未来规模预埋"作为充分条件** |
| 需要异步加载队列消除帧冻结 | 当前无帧冻结反馈，且 2MB 配置的阻塞时间以毫秒计 | **暂不实施异步加载队列** |
| 需要引用计数自动卸载 | 当前内存占用微不足道，按 Group 卸载节省的内存可能小于系统自身开销 | **暂不实施 Group 级自动卸载** |

---

## 3. 完整架构蓝图（红方设计，保留为长期目标）

以下架构作为**技术愿景**完整保留。若未来项目触及第 6 节的 go 阈值，可直接按此蓝图实施。

### 3.1 核心抽象

```
┌─────────────────────────────────────────────────────────────┐
│  AddressableCatalog          (Key → Path 映射表)              │
│  - 编译期/打包期生成 catalog.json                            │
│  - 运行时只读加载                                             │
├─────────────────────────────────────────────────────────────┤
│  IAddressableLoader            (加载接口抽象)                 │
│  - LoadAsync<T>(key)                                         │
│  - LoadBatchAsync<T>(keys[])                                 │
│  - LoadByLabelAsync<T>(label)                                │
├─────────────────────────────────────────────────────────────┤
│  ResourceHandle<T>             (引用计数句柄)                 │
│  - Retain() / Dispose()                                      │
│  - 计数归零时通知生命周期管理器                                │
├─────────────────────────────────────────────────────────────┤
│  AddressableResourceLifetime   (替代 GodotContentResourceLifetime)
│  - 引用计数表：key → (resource, refCount)                    │
│  - 双层级缓存：Addressable 管"谁在用"，Godot 管"是否读盘"      │
├─────────────────────────────────────────────────────────────┤
│  AssetReference                (可序列化资源引用)              │
│  - 在 Editor/配置中用 Key 替代硬编码路径字符串                 │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 与 Godot 子引用（Seed 模式）的兼容性

Seed 资源的内嵌 `Array<Resource>` 子元素不注册独立 Key，而是通过**代理句柄**暴露统一接口，其生命周期绑定到父 Seed 的 `ResourceHandle`。

```
EnemyContentSeed (key="seed:enemies")
  ├─ ParentHandle ──► AddressableResourceLifetime
  └─ Array<Resource> enemy_ai_brains
       └─ ChildProxyHandle[] (Dispose no-op，随 ParentHandle 释放)
```

### 3.3 Registry 异步初始化模式（长期目标）

```csharp
public interface IAddressableRegistry
{
    Task InitializeAsync(IAddressableLoader loader, IRegistryProvider registryProvider);
    void Shutdown(); // 显式释放所有 ResourceHandle
}
```

- 构造函数移除同步 `rebuild()`
- `GameSession` 初始化时按依赖图顺序 `await` 各 Registry
- 依赖声明通过 `AssetReference DependsOnRegistry` 显式配置

---

## 4. 渐进式实施路径（对抗后修订版）

本方案将红方的"7 周完整迁移"拆分为**4 个阶段**，每阶段末尾设置**决策门（Decision Gate）**。若该阶段未能验证下一阶段的必要性，则停止推进，保持当前阶段的改进成果。

### 阶段 1：最小可行改进（MVIP）—— 1 周

**目标**：零风险地修复当前真实痛点，并为未来架构切换预留接口。

#### 4.1.1 修复 `GodotContentResourceLifetime`

在现有 17 行代码基础上增加卸载能力，**不引入任何新抽象**。

```csharp
// 新增于 GodotContentResourceLifetime.cs
public static void ReleaseUnused()
{
    for (int i = _resources.Count - 1; i >= 0; i--)
    {
        var res = _resources[i];
        if (res == null || !GodotObject.IsInstanceValid(res))
        {
            _resources.RemoveAt(i);
            continue;
        }
        // 若只有 Lifetime 和 Godot 缓存持有引用，则释放
        if (res is RefCounted rc && rc.GetReferenceCount() <= 2)
        {
            _resources.RemoveAt(i);
        }
    }
}
```

- **调用时机**：关卡切换、返回主菜单、存档加载前等明确的内存边界。
- **风险**：零。若逻辑有误，最坏情况是资源未被释放（与当前行为一致）。

#### 4.1.2 收敛 `GD.Load` 调用点

新增静态封装层（零运行时开销）：

```csharp
// scripts/utils/ContentResourceLoader.cs
public static class ContentResourceLoader
{
    public static T Load<T>(string path) where T : Resource
        => GD.Load<T>(path);
}
```

将 17 个 Registry 中的 `GD.Load<Resource>(...)` 和 `ResourceLoader.Load(...)` 统一替换为 `ContentResourceLoader.Load<T>(...)`。

- **收益**：加载路径收敛到一个点。若未来需要切换为异步或缓存策略，改动范围从"面"变为"点"。
- **风险**：纯文本替换，语义不变，零行为差异。

#### 4.1.3 推广 Seed 文件合并

借鉴 `EnemyContentRegistry` 的 Seed 模式，将高频加载的同类配置合并为少数 Seed `.tres`：

| 当前模式 | 改进方向 | 预期收益 |
|---|---|---|
| `SkillContentRegistry` 扫描 550+ 个独立 .tres | 合并为核心技能 Seed + 扩展包 Seed（3~5 个文件） | 减少 I/O 次数，启动更快 |
| `RaceContentRegistry`, `SubraceContentRegistry`, `RaceTraitContentRegistry` 各扫各的目录 | 合并为 `IdentityContentSeed.tres` | 减少 Registry 间重复扫描 |

- **风险**：低。Seed 模式已在 `EnemyContentRegistry` 中验证，Godot 会自动解析内嵌的 `Array<Resource>` 子引用。

**阶段 1 决策门（DG-1）**：

| 检查项 | 通过标准 |
|---|---|
| `ReleaseUnused()` 是否安全 | 运行 50 次完整游戏循环无崩溃 |
| 启动加载时间是否改善 | 在最低配目标设备上测量，记录基线 |
| Seed 合并后配置数据完整性 | 所有现有测试通过，无配置丢失 |

**若全部通过 → 进入阶段 2；若有任何一项未通过 → 回滚并停留在阶段 1 成果。**

---

### 阶段 2：Addressable 基础设施预埋 —— 1.5 周

**目标**：建立完整的 Addressable 核心系统，但**仅用于新功能**，不迁移现有 Registry。

#### 4.2.1 创建 `scripts/systems/addressables/` 目录

实现以下类（纯新增，不影响现有代码）：

| 类 | 说明 | 当前是否被使用 |
|---|---|---|
| `IAddressableLoader` | 加载接口 | 否（仅接口定义） |
| `ResourceHandle<T>` | 引用计数句柄 | 否 |
| `AddressableCatalog` | Key→Path 映射，从 catalog.json 加载 | 否 |
| `AddressableResourceLifetime` | 替代 GodotContentResourceLifetime，支持引用计数 | 否 |
| `AssetReference` | 可序列化资源引用（用于 .tres 和场景配置） | 否 |
| `ContentResourceLoaderV2` | 阶段 1 `ContentResourceLoader` 的 Addressable 兼容实现 | 否 |

```csharp
// ContentResourceLoaderV2.cs（预埋，当前不切换）
public static class ContentResourceLoader
{
    private static readonly IAddressableLoader _loader = new AddressableLoaderImpl();
    private static readonly bool _useAddressable = false; //  feature flag，默认关闭

    public static T Load<T>(string path) where T : Resource
    {
        if (_useAddressable && AddressableCatalog.Instance?.ResolvePath(path) != null)
            // 未来可切换为异步同步回退
            return _loader.LoadImmediate<T>(path);
        return GD.Load<T>(path);
    }
}
```

#### 4.2.2 Catalog 生成工具

创建 `tools/generate_addressable_catalog.py`：

- 扫描 `data/configs/` 目录
- 按约定生成 Key（如 `res://data/configs/skills/fireball.tres` → `skill:fireball`）
- 输出 `data/addressable_catalog.json`
- CI 集成：构建前自动运行，检测 Key 冲突和孤立文件

#### 4.2.3 新增 Registry 的 Addressable 试点

选择**下一个新创建的 Registry**（如未来新增的内容域）作为试点，强制使用 Addressable 接口：

```csharp
public partial class NewContentRegistry : RefCounted, IAddressableRegistry
{
    public async Task InitializeAsync(IAddressableLoader loader)
    {
        var handles = await loader.LoadByLabelAsync<NewContentDef>("new_content");
        // ...
    }
}
```

- **目的**：在低风险场景下验证接口设计的可用性。
- **若试点失败**：接口可立即修改，不影响任何存量代码。

**阶段 2 决策门（DG-2）**：

| 检查项 | 通过标准 |
|---|---|
| Catalog 生成工具是否正确 | catalog.json 完整覆盖 885 个 .tres，无冲突 |
| 新增 Registry 试点是否成功 | 新内容域加载正常，ResourceHandle 无泄漏 |
| Addressable 核心系统是否通过 headless 测试 | `tests/addressables/run_lifetime_smoke.gd` 通过 |

**若全部通过 → 进入阶段 3；若未通过 → 冻结 Addressable 系统，仅保留阶段 1 成果。**

---

### 阶段 3：存量 Registry 批量迁移 —— 2 周（仅当 DG-2 通过后才启动）

**目标**：将 17 个存量 Registry 逐步迁移到 Addressable 接口。

#### 4.3.1 迁移顺序（按依赖深度排序）

| 批次 | Registry | 理由 | 预估改动行数 |
|---|---|---|---|
| 3a | `WorldPresetRegistry` | 无依赖、数据量小、模式最简单 | ~20 |
| 3b | `Age`, `Race`, `Subrace`, `RaceTrait`, `Bloodline`, `Barrier`, `Ascension`, `StageAdvancement` | progression 系列模式相似，可批量复制 | 各 ~15 |
| 3c | `RecipeContentRegistry`, `ItemContentRegistry` | 仓库系统，校验逻辑相对简单 | 各 ~25 |
| 3d | `SkillContentRegistry` | 数据量大（550+ 配置），但收益最高 | ~40 |
| 3e | `EnemyContentRegistry` | 需验证 Seed 兼容方案 | ~35 |
| 3f | `BattleSpecialProfileRegistry` | 依赖 SkillContentRegistry，验证依赖图 | ~20 |
| 3g | `WorldMapDataContext`（懒加载） | 验证懒加载在 Addressable 中的语义 | ~15 |

#### 4.3.2 迁移模式（每个 Registry 的标准化步骤）

1. **移除构造函数同步加载**：`rebuild()` 改为 `InitializeAsync()`
2. **替换加载调用**：`GD.Load<Resource>(path)` → `await loader.LoadAsync<Resource>(key)`
3. **替换生命周期**：`GodotContentResourceLifetime.Keep(resource)` → `ResourceHandle<T>` 持有
4. **增加卸载接口**：`Shutdown()` 释放所有 `ResourceHandle`
5. **更新测试**：初始化前增加 `await registry.InitializeAsync()`

#### 4.3.3 同步回退安全网

为每个迁移后的 Registry 保留同步回退路径：

```csharp
public SkillDef GetSkill(StringName skillId)
{
    if (!_initialized)
    {
        GD.PushWarning("Synchronous fallback used. Consider preloading before access.");
        InitializeSyncFallback(); // 内部仍走 GD.Load，标记 obsolete
    }
    return _handles.TryGetValue(skillId, out var h) ? h.Asset : null;
}
```

**阶段 3 决策门（DG-3）**：

| 检查项 | 通过标准 |
|---|---|
| 全部 17 个 Registry 测试通过 | 原有回归测试 100% 通过 |
| 启动加载时间对比 | 不低于阶段 1 基线（异步本身不应让启动更慢） |
| 内存占用对比 | 不高于阶段 1 基线（引用计数不应增加泄漏） |
| 战斗、世界地图、仓库等核心流程手动测试 | 无异常 |

**若全部通过 → 进入阶段 4；若未通过 → 回滚未通过的 Registry，保留已通过的部分。**

---

### 阶段 4：全局清理与废弃 —— 0.5 周（仅当 DG-3 通过后才启动）

- 标记 `GodotContentResourceLifetime` 为 `[Obsolete]`
- 删除所有残留的 `res://data/configs/...` 硬编码字符串（已被 catalog key 替代）
- 在 `ContentResourceLoader` 中将 `_useAddressable` feature flag 设为 `true`
- 运行完整回归测试

---

## 5. 风险矩阵与缓解措施

| 风险 | 概率 | 影响 | 缓解措施 |
|---|---|---|---|
| C# async/await 在 Godot 4.6 中触发 Resource 悬空 | 中 | 高 | 阶段 3 保留同步回退路径；所有 `ResourceHandle.Asset` 访问前检查 `_disposed` |
| 引用计数与 Godot RefCount/C# GC 三重生命周期冲突 | 中 | 高 | `ResourceHandle.Dispose()` 不调用 `Free()`，仅解除 C# 强引用，让 Godot 缓存决定回收时机 |
| 迁移导致配置数据时序变化（异步→访问时未就绪） | 高 | 中 | 阶段 3 每批次 1 个 Registry，小步快跑；`GetXxx()` 在 `_initialized==false` 时抛异常而非静默返回 null |
| Seed 子资源在父 Seed 卸载后仍被访问 | 低 | 高 | Seed 子资源使用代理句柄（Dispose no-op），生命周期严格绑定父句柄 |
| Catalog 维护成本（key 冲突、路径变更） | 低 | 中 | CI 自动生成和验证 catalog.json；key 冲突时构建失败 |

---

## 6. Go / No-Go 决策指标

本方案的核心原则是：**不基于假设的规模引入复杂度**。只有在以下指标被实际观测到，且**阶段 1 的 MVIP 无法解决**时，才推进到阶段 2 及以后。

| 指标 | 阈值 | 测量方法 | 若触及阈值时的行动 |
|---|---|---|---|
| **启动加载时间** | > 3 秒（最低配目标设备） | Godot Profiler 或自定义计时 | 推进阶段 2，验证异步队列收益 |
| **运行时配置数据内存占用** | > 50 MB | `OS.GetStaticMemoryUsage()` 或 `GC.GetTotalMemory()` | 推进阶段 2，验证引用计数卸载收益 |
| **.tres 文件数量** | > 10,000 个 | 文件系统统计 | 优先用 Seed 合并（阶段 1），若仍不足则推进阶段 2 |
| **运行时加载卡顿报告** | 有用户/测试反馈，且 Profiler 证明阻塞在 `GD.Load` | Godot Profiler 火焰图 | 推进阶段 2 |
| **产品层面需要热更新配置** | 确定需要 DLC/热更 `.tres` | 产品需求文档 | 立即推进阶段 2（这是 Addressable 的真正主场） |

**当前状态（2026-05-29）**：
- 启动加载时间：未知（未测量）
- 配置数据内存：~10-20 MB（估算，未精确测量）
- .tres 文件数量：885 个
- 热更新需求：无

**裁定**：当前不满足任何 go 阈值。建议**立即执行阶段 1（MVIP，1 周）**，并在阶段 1 结束时测量启动时间和内存基线，再决定是否申请阶段 2 的资源。

---

## 7. 附录：当前 Registry 全景与迁移优先级

| Registry | 行数 | 加载方式 | 数据量 | 依赖 | 迁移批次 |
|---|---|---|---|---|---|
| `WorldPresetRegistry` | 134 | 仅存路径字符串 | 极小 | 无 | 3a |
| `AgeContentRegistry` | 226 | `GD.Load` + Keep | 小 | 无 | 3b |
| `RaceContentRegistry` | 143 | `GD.Load` + Keep | 小 | 无 | 3b |
| `SubraceContentRegistry` | 140 | `GD.Load` + Keep | 小 | 无 | 3b |
| `RaceTraitContentRegistry` | 143 | `GD.Load` + Keep | 小 | 无 | 3b |
| `BarrierContentRegistry` | 188 | `GD.Load` + Keep | 小 | 无 | 3b |
| `BloodlineContentRegistry` | 200 | `GD.Load` + Keep | 小 | 无 | 3b |
| `AscensionContentRegistry` | 231 | `GD.Load` + Keep | 小 | 无 | 3b |
| `StageAdvancementContentRegistry` | 164 | `GD.Load` + Keep | 小 | 无 | 3b |
| `ProfessionContentRegistry` | 640 | `GD.Load` + Keep | 中 | 无 | 3b |
| `RecipeContentRegistry` | 199 | `GD.Load` + Keep | 极小 | 无 | 3c |
| `ItemContentRegistry` | 785 | `ResourceLoader.Load` + Keep | 中 | 无 | 3c |
| `BattleSpecialProfileRegistry` | 244 | `GD.Load` + Keep | 极小 | SkillContentRegistry | 3f |
| `EnemyContentRegistry` | 378 | `GD.Load` + Keep / Seed | 中 | 无 | 3e |
| `SkillContentRegistry` | **1621** | `GD.Load` + Keep | **550+ 配置** | 无 | **3d** |
| `WorldMapDataContext` | 499 | `GD.Load`（懒加载） | 小 | 无 | 3g |
| `WorldMapSpawnSystem` | ~1500 | `GD.Load<Bundle>` | 小 | 无 | 3g |

---

## 8. 结论

本方案是对红蓝双方对抗性评审的**折中输出**：

- **蓝方的胜利**：当前不满足任何 go 阈值，完整 Addressable 系统暂不实施；阶段 1 的 MVIP 完全采纳蓝方设计。
- **红方的遗产**：完整的 Addressable 架构蓝图作为长期目标保留，基础设施预埋（阶段 2）可随时启动；一旦项目触及热更新需求或性能阈值，无需重新设计即可按图施工。

**立即行动项**：
1. 执行阶段 1（1 周）：修复 `GodotContentResourceLifetime` + 收敛 `GD.Load` + 推广 Seed 合并。
2. 阶段 1 结束时测量启动时间和内存基线。
3. 若基线数据支持，申请阶段 2 资源。
