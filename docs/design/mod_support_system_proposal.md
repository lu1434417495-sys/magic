# MOD 支持系统方案设计书

> **状态**：设计草案（红蓝对抗评审后修订版）  
> **范围**：仅输出设计文档，不修改任何代码  
> **版本**：v1.0（2026-05-29）  
> **前置依赖**：`docs/design/addressable_resource_system_proposal.md`（Addressable 资源系统提案）

---

## 1. 引言与决策背景

本方案源于红蓝双方就"是否应在当前阶段引入 MOD 支持"进行的对抗性技术评审。

### 1.1 双方共识（不可辩驳的事实）

| 事实 | 来源 |
|---|---|
| 项目文档/需求/PRD 中**没有任何 MOD、UGC、Workshop、社区内容**的提及 | 全文搜索审计 |
| 17 个 ContentRegistry 全部以 `StringName` key 驱动，但路径硬编码为 `res://data/configs/...` | 代码审计 |
| 当前验证系统是 **Fail-Fast 零容忍** 模式：重复 key 报错、引用断裂报错 | 代码审计 |
| `GodotContentResourceLifetime` 是 17 行全局静态 `List<Resource>`，只增不减 | 代码审计 |
| 项目有 428 个 `.cs` 和 198 个 `.gd`，处于 GDScript→C# 迁移期 | 文件系统统计 |
| Godot 4.6 原生支持 `.pck` 运行时加载 (`ProjectSettings.LoadResourcePack()`) | Godot 引擎能力 |
| 全部 885 个 `.tres` 是纯 Godot Resource 序列化格式，编辑器可直接编辑 | 文件审计 |

### 1.2 双方分歧

| 维度 | 蓝方立场 | 红方立场 |
|---|---|---|
| **必要性** | 产品零需求、零用户、零文档，现在做是"对空集的服务" | 薄层预埋成本极低，有需求时无需等待 |
| **架构路径** | 必须推翻 Registry 初始化时序、路径解析层、资源生命周期三层基础 | 在现有架构缝隙中加薄层，Registry 零改动 |
| **成本估算** | 8.5–13.5 周（Registry 重写 + 验证分级 + 存档兼容 + 生命周期替换） | 1–2 周（400 行薄层代码） |
| **验证策略** | Fail-Fast 改分级需改 2500+ 行渗透式代码 | 不修改任何验证逻辑，MOD 内容在 Registry 验证完成后注入 |
| **存档兼容** | 必须引入 SaveVersion 升级 + 内容存活校验 + 降级策略 | 不修改存档格式，卸载 MOD 的降级由现有 null-check 承接 |
| **生命周期** | 必须替换 `GodotContentResourceLifetime` 为引用计数系统 | 冷加载-only，MOD 与官方资源同等对待，无需卸载 |

### 1.3 最终裁定

**本方案采纳红方的"薄层增量"架构设计，但将实施决策与蓝方提出的"产品需求确认"绑定。**

核心逻辑：
- 红方的 1–2 周薄层方案在技术上确实可行，且巧妙地绕开了蓝方提出的所有结构性障碍。
- 但蓝方的"零需求"警告是成立的——在没有产品确认的情况下，即使成本很低，也不应无条件投入。
- **裁定**：将 MOD 薄层作为 Addressable 阶段 2 的**可选并行任务**，总工作量控制在 **2 周以内**。产品层面出现 MOD 需求时，该薄层可在 1 周内激活为完整功能；若产品始终无 MOD 需求，该薄层作为技术储备存在，不影响任何现有代码路径。

---

## 2. 架构设计：薄层增量 MOD 系统

### 2.1 核心原则

| 原则 | 说明 |
|---|---|
| **Registry 无感知** | 不修改任何 Registry 的 `rebuild()`、扫描路径、验证逻辑 |
| **GameSession 层注入** | 在官方内容全部加载并验证通过后，将 MOD 内容注入 GameSession 的本地缓存字典 |
| **冷加载-only** | MOD 只在游戏启动时加载一次，不支持运行时热插拔 |
| **存档零侵入** | 不修改 `SaveSerializer` 的 payload 格式、版本号、`HasExactKeys` 校验 |
| **数据驱动** | MOD 作者复制官方 `.tres`、修改数值、放入目录，无需编写代码 |

### 2.2 系统架构图

```
┌─────────────────────────────────────────────────────────────┐
│  GameSession 构造函数                                         │
│  ├─ _refresh_progression_content()    ← 官方内容加载        │
│  ├─ _refresh_battle_special_profiles()                     │
│  ├─ _refresh_item_content()                                │
│  ├─ _refresh_recipe_content()                              │
│  ├─ _refresh_enemy_content()                               │
│  │                                                          │
│  ├─ [NEW] ModScanner.ScanAll()          ← 扫描 MOD 目录   │
│  ├─ [NEW] ModContentInjector.InjectAndRefresh(this)        │
│  │     ├─ 追加 MOD 新内容到 GameSession 字典               │
│  │     ├─ 处理 overrides（显式覆盖声明）                    │
│  │     └─ 重算依赖链（special profile / item / recipe）    │
│  │                                                          │
│  └─ _refresh_content_validation_snapshot()                 │
│       └─ cross-reference 验证器覆盖 MOD 内容               │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 关键设计：注入点选择

经对 `GameSession.cs` 初始化序列的逐行审计，确认以下注入点安全：

```csharp
// GameSession.cs（当前代码，审计确认）
public GameSession()
{
    _save_serializer.setup(...);
    
    _refresh_progression_content();      // _skill_defs, _race_defs, ... 填充
    _refresh_battle_special_profiles();  // 依赖 _skill_defs
    _refresh_item_content();             // 依赖 _skill_defs（skill book 工厂）
    _refresh_recipe_content();           // 依赖 _item_defs
    _refresh_enemy_content();            // _enemy_templates 填充
    _refresh_quest_content();            // _quest_defs 填充
    
    // === 注入点 ===
    // 在此处插入 MOD 注入：
    // ModContentInjector.InjectAndRefresh(this, ModScanner.ScanAll());
    // ================
    
    _refresh_content_validation_snapshot();  // cross-reference 验证
    _report_content_validation_errors();
}
```

**为什么安全**：
- `_refresh_*()` 已将官方内容从 Registry 复制到 GameSession 的本地字典
- `ModContentInjector` 修改的是 GameSession 的本地字典，不是 Registry 的内部状态
- `_refresh_content_validation_snapshot()` 运行 cross-reference 验证时，验证器看到的是"官方 + MOD"的合并视图
- 若 MOD 内容导致验证失败，行为与"官方内容配置错误"完全一致：报错并阻止进入游戏

### 2.4 MOD 包格式

```
user://mods/{mod_id}/
  mod.json
  skills/
    new_skill.tres
  items/
    new_item.tres
    override_longsword.tres
  enemies/
    new_boss.tres
  quests/
    new_quest.tres
```

`mod.json` 规范：

```json
{
  "id": "example_mod",
  "name": "示例 MOD",
  "version": "1.0.0",
  "author": "ModAuthor",
  "game_version_compat": ">=0.5.0",
  "load_priority": 100,
  "dependencies": [
    { "id": "base_game", "version": ">=0.5.0" }
  ],
  "overrides": {
    "skill": ["fireball"],
    "item": ["steel_longsword"],
    "enemy_template": []
  }
}
```

### 2.5 覆盖语义

Registry 层的 Fail-Fast 校验在注入前已完成，因此 Registry 对重复 key 的报错**不触及 MOD 内容**。

注入器的两步策略：

```csharp
// Step 1: 追加所有 MOD 内容（新 key 直接插入；已存在 key 暂不覆盖）
foreach (var skill in mod.Skills)
    targetDict[skill.Key] = skill.Value;

// Step 2: 处理显式覆盖声明
foreach (var overrideKey in manifest.Overrides["skill"])
    if (mod.Skills.ContainsKey(overrideKey))
        targetDict[overrideKey] = mod.Skills[overrideKey];  // 强制替换
```

- 无覆盖声明的冲突 key 按"先加载先保留"处理，避免静默覆盖
- 显式覆盖给 MOD 作者完全控制权，同时让冲突行为可审计
- 多 MOD 覆盖同一 key 时，按 `load_priority` 排序，后加载者生效

### 2.6 存档兼容性

**不修改存档格式**。`SaveSerializer` 的 `HasExactKeys` 和 `_save_version = 7` 不受影响。

卸载 MOD 后的加载行为：

| 场景 | 行为 | 现有代码是否已处理 |
|---|---|---|
| 存档引用 MOD 技能 `skill:mod_fireball`，该 MOD 已卸载 | `GameSession._skill_defs` 中找不到该 key，`GetSkill()` 返回 null | ✅ 是，所有 `GetXxx()` 已有 null fallback |
| 角色装备了 MOD 物品 `item:mod_sword`，该 MOD 已卸载 | 物品槽位反序列化时 `ItemDef` 为 null，UI 显示为"未知物品"或空槽 | ⚠️ 部分处理，可能需要 UI 层增加"缺失内容"提示 |
| 任务进度引用了 MOD 任务 `quest:mod_quest` | `QuestState.from_dict()` 中找不到 quest def，任务状态保持为未激活 | ✅ 是，任务系统已有缺失回退 |

**未来增强（非必需）**：可在 `ModContentInjector` 中增加"加载时存活校验"：扫描存档中引用的所有内容 ID，若发现来自已卸载 MOD 的 ID，向玩家展示警告列表。

---

## 3. 核心组件设计

### 3.1 ModScanner

```csharp
// scripts/systems/mod/ModScanner.cs
public static class ModScanner
{
    public static ModScanResult ScanAll()
    {
        var result = new ModScanResult();
        if (!DirAccess.DirExistsAbsolute("user://mods")) return result;
        
        using var dir = DirAccess.Open("user://mods");
        dir.ListDirBegin();
        string entry;
        while ((entry = dir.GetNext()) != "")
        {
            if (entry == "." || entry == "..") continue;
            string modPath = $"user://mods/{entry}";
            if (!FileAccess.FileExists($"{modPath}/mod.json")) continue;
            
            var manifest = ParseManifest($"{modPath}/mod.json");
            var content = LoadModContent(modPath, manifest);
            result.Mods.Add(content);
        }
        dir.ListDirEnd();
        
        // 按 load_priority 排序
        result.Mods.Sort((a, b) => a.Manifest.LoadPriority.CompareTo(b.Manifest.LoadPriority));
        return result;
    }
    
    private static ModManifest ParseManifest(string path) { ... }
    private static ModContent LoadModContent(string modPath, ModManifest manifest) { ... }
}
```

`.tres` 加载使用 `ResourceLoader.Load<T>()`。Godot 4.x 确认支持从 `user://` 路径加载 `[GlobalClass]` 自定义 Resource 类。

### 3.2 ModContentInjector

```csharp
// scripts/systems/mod/ModContentInjector.cs
public static class ModContentInjector
{
    public static void InjectAndRefresh(GameSession session, ModScanResult mods)
    {
        if (mods.Mods.Count == 0) return;
        
        // 1. 注入各内容域
        InjectSkills(session, mods);
        InjectItems(session, mods);
        InjectEnemies(session, mods);
        InjectQuests(session, mods);
        // ... 其他域
        
        // 2. 处理覆盖
        ApplyOverrides(session, mods);
        
        // 3. 重算依赖链（使 MOD 内容参与 cross-reference 验证）
        session._refresh_battle_special_profiles();
        session._refresh_item_content();   // skill book 工厂可能需要 MOD 技能
        session._refresh_recipe_content(); // 配方可能需要 MOD 物品
    }
    
    private static void InjectSkills(GameSession session, ModScanResult mods)
    {
        var dict = session.GetSkillDefs();  // 内部字典引用
        foreach (var mod in mods.Mods)
            foreach (var skill in mod.Skills)
                if (!dict.ContainsKey(skill.Key))
                    dict[skill.Key] = skill.Value;
    }
    
    private static void ApplyOverrides(GameSession session, ModScanResult mods)
    {
        var skillDict = session.GetSkillDefs();
        foreach (var mod in mods.Mods)
            foreach (var overrideKey in mod.Manifest.Overrides.GetValueOrDefault("skill", Array.Empty<StringName>()))
                if (mod.Skills.ContainsKey(overrideKey))
                    skillDict[overrideKey] = mod.Skills[overrideKey];
        // 其他域同理
    }
}
```

### 3.3 验证覆盖度

| 验证层 | 检查对象 | MOD 内容是否被覆盖 | 说明 |
|---|---|---|---|
| Registry self-validation | Registry 内部字典 | ❌ 否 | Registry 只验证官方内容，MOD 不触发 Fail-Fast |
| GameSession cross-reference | GameSession 合并字典 | ✅ 是 | `SkillBookItemContentValidator`、`QuestContentValidator` 等检查 MOD 内容的引用完整性 |
| MOD 轻量校验（可选） | MOD 自身内容 | ✅ 是 | `ModValidationRules` 检查 MOD 的必填字段和类型 |

这是有意设计：Registry 层的 Fail-Fast 保护官方内容的内部一致性；GameSession 层的 cross-reference 验证保护合并后内容生态的关联完整性。

---

## 4. 与 Addressable 资源系统的协同

本方案不是 Addressable 的替代，而是其**前置增强**。两者在时序上可并行实施。

| 组件 | Addressable 阶段 1-2 | 本 MOD 方案 | 协同方式 |
|---|---|---|---|
| 加载接口 | `ContentResourceLoader`（阶段 1 收敛） | `ResourceLoader.Load<T>()` | 阶段 1 的收敛为 MOD 路径切换预留接口；阶段 2 的 `ContentResourceLoaderV2` 可统一处理 `res://` 和 `user://` |
| 路径解析 | `AddressableCatalog`（阶段 2） | `mod.json` + 目录约定 | MOD 的 key 遵循同一命名规范，未来可纳入 Catalog |
| 生命周期 | `AddressableResourceLifetime`（阶段 2） | `GodotContentResourceLifetime.Keep()` | 冷加载场景下两者行为等价；MOD 资源与官方资源同等对待 |
| 验证分级 | 阶段 3 讨论 | 不修改验证逻辑，仅在 GameSession 层注入 | 若未来 Addressable 阶段 3 引入验证分级，MOD 系统自然受益 |

### 4.1 时序建议

```
Week 1-2:  Addressable 阶段 1（MVIP）
           └─ 修复 GodotContentResourceLifetime + 收敛 GD.Load + Seed 合并
           
Week 3-4:  [并行] 本 MOD 薄层方案
           └─ ModScanner + ModContentInjector + headless 测试
           
Week 5-6:  Addressable 阶段 2（若 DG-1 通过）
           └─ AddressableCatalog + ContentResourceLoaderV2
           └─ MOD 系统可切换至 ContentResourceLoaderV2 路径（改动 ~50 行）
```

---

## 5. 风险评估

### 5.1 技术风险

| 风险 | 概率 | 影响 | 缓解措施 |
|---|---|---|---|
| MOD `.tres` 引用了官方不存在的子资源 | 中 | 中 | `ResourceLoader.Load<T>()` 返回 null 时跳过该文件，记录到 MOD 错误日志，不阻断其他 MOD |
| MOD 覆盖导致关键官方内容缺失 | 低 | 高 | `overrides` 需显式声明；注入器记录所有覆盖到日志；可增加"关键内容保护名单"（如主线任务技能） |
| 多 MOD 覆盖同一 key | 低 | 中 | 按 `load_priority` 排序，结果可预测；在 MOD 加载日志中记录最终生效的 MOD |
| `user://` 加载 `[GlobalClass]` 资源类型识别失败 | 低 | 高 | Godot 4.x 社区已验证 `ResourceLoader.Load<T>("user://path.tres")` 对 `[GlobalClass]` 有效（运行时上下文） |
| MOD 内容通过验证但引发平衡性崩溃 | 中 | 低 | 非技术风险。平衡性问题由 MOD 作者和玩家社区解决，与官方内容配置错误的处理方式一致 |

### 5.2 蓝方 objections 最终回应

| 蓝方 objection | 本方案回应 |
|---|---|
| **O1: 零需求** | 本方案定位为"薄层预埋"。2 周工作量产出 400 行隔离代码，不占用核心系统带宽。产品出现需求时可 1 周内激活；无需求时作为技术储备无害。 |
| **O2: 硬编码 Registry 路径** | **不需要修改任何 Registry**。MOD 内容从 `user://mods/` 加载，通过注入器进入 GameSession 字典，Registry 的扫描路径完全不变。 |
| **O3: Fail-Fast 验证** | **不修改任何验证逻辑**。增量 MOD 使用新 key 不触发重复 key；覆盖内容通过 `mod.json` 显式声明，由注入器在 Registry 验证完成后执行替换。Registry 的 Fail-Fast 对 MOD 不可见。 |
| **O4: GodotContentResourceLifetime 泄露池** | **冷加载不增加泄漏风险**。MOD 资源在启动时一次性加载，通过 `Keep()` 加入泄漏池，与官方资源处理方式一致。没有运行时加载/卸载，无需生命周期管理。 |
| **O5: SaveVersion=7 严格匹配** | **不修改存档格式**。不在 payload 中增加新键，`HasExactKeys` 不受影响。卸载 MOD 后的降级由现有 null-check 承接。 |
| **O6: C#/GDScript 混合门槛** | **数据驱动 MOD 无需代码**。MOD 作者复制官方 `.tres`、修改数值、放入目录即可。 |
| **O7: 8.5–13.5 周成本** | **薄层仅需 1–2 周**。400 行代码，零 Registry 修改，零 SaveSerializer 修改，零生命周期替换。 |
| **O8: 团队工作重心冲突** | MOD 薄层由 1 名工程师在 2 周内完成，可与 Addressable 阶段 1 并行，不阻塞核心玩法迭代。 |

---

## 6. 实施路线图与工作量

### 6.1 任务拆分

| 任务 | 工时 | 产出 | 依赖 |
|---|---|---|---|
| T1: `ModScanner` + `ModManifest` | 2 天 | `ModScanner.cs` (~150 行) + `ModManifest.cs` (~30 行) | 无 |
| T2: `ModContentInjector` | 3 天 | `ModContentInjector.cs` (~200 行) | T1 |
| T3: GameSession 注入点集成 | 0.5 天 | `GameSession.cs` 修改 (~5 行) | T2 |
| T4: MOD 加载 headless 回归测试 | 2 天 | `tests/mod/run_mod_loading_smoke.gd` | T3 |
| T5: 覆盖语义与冲突处理 | 1 天 | `mod.json` schema + 注入器覆盖逻辑 | T2 |
| T6: 文档与示例 MOD | 0.5 天 | 本文档 + 示例 MOD 包 | 无 |
| **总计** | **~9 工作日 ≈ 2 周** | **~430 行新增代码** | |

### 6.2 代码规模

| 文件 | 预估行数 | 职责 |
|---|---|---|
| `ModScanner.cs` | ~150 | 扫描 `user://mods/`，解析 `mod.json`，加载 `.tres` |
| `ModContentInjector.cs` | ~200 | 注入 MOD 内容到 GameSession，处理覆盖，重算依赖 |
| `ModManifest.cs` | ~30 | `mod.json` 的 C# 数据类 |
| `ModValidationRules.cs` | ~50 | MOD 内容的轻量校验（可选） |
| `GameSession.cs` | ~5 | 插入注入调用 |
| **新增总计** | **~430 行** | **零 Registry 修改，零 SaveSerializer 修改** |

---

## 7. Go / No-Go 决策框架

### 7.1 立即执行的条件（满足任一即可启动）

- [ ] 产品 PRD 或 roadmap 中新增 MOD/UGC 条目
- [ ] 收到明确的社区 MOD 需求（如论坛高赞请求）
- [ ] 核心玩法已完成 MVP 验收（battle → progression → world map 闭环 ≥ 2 小时可玩）
- [ ] Addressable 阶段 1（MVIP）已完成，团队有剩余带宽

### 7.2 阶段交付的决策门

| 决策门 | 通过标准 | 未通过时的行动 |
|---|---|---|
| **DG-MOD-1**（T1-T3 完成后） | 1. 官方内容 + 测试 MOD 内容能正常加载；2. Cross-reference 验证覆盖 MOD 内容；3. 50 次完整游戏循环无崩溃 | 回滚 `GameSession.cs` 的 5 行修改，保留 ModScanner 代码但不激活 |
| **DG-MOD-2**（T4 完成后） | Headless 回归测试通过：单 MOD 加载、多 MOD 加载顺序、覆盖语义、卸载后存档降级 | 修复测试发现的 bug，或降级为"仅支持增量 MOD，不支持覆盖" |

### 7.3 与 Addressable 决策门的衔接

- 若 Addressable DG-1（阶段 1）未通过：仍可按计划执行 MOD 薄层（两者无阻塞依赖）
- 若 Addressable DG-2（阶段 2）通过：将 `ModScanner` 的加载路径切换至 `ContentResourceLoaderV2`，改动范围 ~50 行
- 若 Addressable DG-2 未通过：MOD 薄层继续使用 `ResourceLoader.Load<T>()`，功能不受影响

---

## 8. 替代方案对比

| 方案 | 工作量 | Registry 改动 | SaveSerializer 改动 | 存档兼容 | 运行时卸载 | 推荐度 |
|---|---|---|---|---|---|---|
| **蓝方：全面重构** | 8.5–13.5 周 | 17 个全部重写 | 版本升级 + 降级策略 | 需显式处理 | 支持 | ❌ 当前不推荐 |
| **红方：薄层增量（本方案）** | 1–2 周 | **零改动** | **零改动** | 现有 null-check | 不支持（冷加载） | ✅ **推荐** |
| **仅做可行性实验** | 1–2 天 | 零改动 | 零改动 | 不测试 | 不支持 | ⚠️ 可作为 DG-MOD-1 的前置 |
| **PCK 包方案** | 1 周 | 零改动 | 零改动 | 不测试 | 不支持 | ⚠️ 仅适合美术/音频替换型 MOD |

---

## 9. 结论与行动建议

### 9.1 核心结论

1. **技术上可行**：经逐行审计，全部 17 个 Registry、GameSession 初始化序列、`SaveSerializer` 的约束条件，均支持"不修改核心代码、仅在 GameSession 层注入"的薄层方案。
2. **成本可控**：1–2 周，430 行代码，1 名工程师，与 Addressable 阶段 1 并行。
3. **风险隔离**：MOD 薄层代码完全隔离在 `scripts/systems/mod/` 目录，不激活时零运行时开销。
4. **产品绑定的决策**：即使技术成本极低，也应在产品确认 MOD 需求或核心玩法 MVP 完成后启动。

### 9.2 立即行动建议

1. **产品层面**：确认未来 6–12 个月内是否有 MOD/UGC/社区内容规划。
2. **技术层面**：若产品确认有需求，或 Addressable 阶段 1 完成后团队有剩余带宽，立即启动 T1（ModScanner）。
3. **验证层面**：若对产品需求不确定，可用 2 天时间实现"技能增量 MOD"原型（仅支持 `SkillContentRegistry` 注入），验证薄层可行性后作为技术储备。

### 9.3 附录：审计数据来源

| 审计项 | 文件路径 | 关键发现 |
|---|---|---|
| Registry 加载模式 | `SkillContentRegistry.cs:204-232` | `GD.Load` + `Keep()` + duplicate key check |
| Registry 返回语义 | `ProgressionContentRegistry.cs` | `get_skill_defs()` 返回字典副本 |
| GameSession 初始化 | `GameSession.cs` 构造函数 | 7 步 `_refresh_*()` 序列，注入点明确 |
| 验证分层 | `GameSession.cs` | Registry validate + cross-reference validate 双层结构 |
| SaveSerializer | `SaveSerializer.cs` | `HasExactKeys` + `_save_version=7`，但存档不引用内容定义 |
| GodotContentResourceLifetime | `GodotContentResourceLifetime.cs` | 17 行静态 List，无卸载接口 |
| Godot user:// 加载能力 | Godot 4.x 官方文档 + 社区验证 | `ResourceLoader.Load<T>("user://path.tres")` 对 `[GlobalClass]` 资源有效 |
