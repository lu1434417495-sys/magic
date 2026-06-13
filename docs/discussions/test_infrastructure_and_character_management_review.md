# 测试基础设施与 CharacterManagementModule 架构审查（更新版）

> 状态：**基于 C# 迁移后状态更新**（原 GDScript 时代分析已部分过时）
> 更新日期：2026-06-13
> 涉及文件：
> - `tests/` 全部 245 个 C# 测试文件（`run_*.cs`）
> - `scripts/systems/progression/CharacterManagementModule.cs`（4298 行）
> - `tests/shared/` 现有 C# 共享基础设施
> - `docs/design/csharp_migration.md`

---

## 背景：项目已发生根本性变化

自本文档初次分析以来，项目已完成从 GDScript 到 C# 的核心运行时迁移：

| 指标 | 原分析状态 | 当前状态 |
|------|-----------|---------|
| 测试文件 | 135 个 `run_*.gd` | **245 个 `run_*.cs`，0 个 `run_*.gd`** |
| CharacterManagementModule | `character_management_module.gd`（2306 行） | `CharacterManagementModule.cs`（**4298 行**） |
| 公开方法 | 54 个 | **155 个** |
| `private` 成员出现次数 | ~54 | **172 处** |

这意味着原分析中的**诊断结论仍然成立**，但**具体数据、实现建议、文件路径已全部需要基于 C# 重新表述**。同时，由于 C# 迁移目标明确禁止“为测试重新暴露 private state 或 test hook”，测试基础设施的补全必须与 `docs/design/csharp_migration.md` 保持一致。

---

## 第一部分：测试基础设施现状

### 1. 已取得的进展

项目已经建立了 C# 版本的共享测试基础设施，位于 `tests/shared/`：

```
tests/shared/
├── TestHarness.cs                          # 统一断言工具
├── run_shared_test_fixture_regression.cs   # fixture 行为回归测试
├── SnapshotTestRuntime.cs
├── FixedRollDamageResolver.cs
├── FixedHitOneDamageResolver.cs
├── FixedHitMaxDamageResolver.cs
├── FixedMissOneDamageResolver.cs
├── FixedCriticalOneDamageResolver.cs
├── FixedSuccessOneDamageResolver.cs
├── FixedFailedSaveDamageResolver.cs
├── FixedSuccessFailedSecondarySaveOneDamageResolver.cs
├── StageOutcomeDamageResolver.cs
├── CountingDamageResolver.cs
├── SpySequenceDamageResolver.cs
└── TrapDamageResolver.cs
```

`TestHarness` 已提供 `True` / `False` / `Eq` / `Ne` / `Fail` / `Finish` 等基础断言能力，相比原 GDScript 时代每个文件重复定义 `_assert_true()` / `_assert_eq()` 是显著进步。

`StubRng` 已被集中定义到 `tests/shared/run_shared_test_fixture_regression.cs` 中，原 GDScript 时代 3 次重复定义的问题已消失。

### 2. 仍然存在的 gaps

#### 2.1 缺少统一的 battle fixture 工厂

当前 `tests/shared/run_shared_test_fixture_regression.cs` 中虽然有 `BuildState()` / `BuildUnit()` / `AddUnits()` 等静态 helper，但它们是**内嵌在单个回归测试文件中的 private 静态方法**，并未作为公共 fixture 库暴露给其他测试使用。

各 C# 测试文件仍大概率各自实现类似的 `BuildUnit()` / `BuildState()` 逻辑，重复问题从 GDScript 时代转移到了 C# 时代，只是形式上更隐蔽。

#### 2.2 私有状态访问仍未根除

即使在共享 fixture 的回归测试自身中，仍然存在直接写入私有字段：

```csharp
// tests/shared/run_shared_test_fixture_regression.cs:49
runtime._state = state;
```

这与 `docs/design/csharp_migration.md` 中的要求冲突：

> 不允许为测试重新暴露 private state 或 test hook。
> 旧测试如果依赖 private dictionary、旧 create()、Variant/dictionary payload，必须改为 public behavior 断言。

#### 2.3 缺少 C# 版的通用 runtime / session stub

原 GDScript 时代 `MockRuntime` / `FakeRuntime` 的多文件重复问题已随语言迁移自然消失，但 C# 测试中如果存在跨模块集成测试，仍可能需要统一的 `MockGameSession`、`MockCharacterGateway`、`MockBattleRuntime` 等可复用 stub。目前 `tests/shared/` 中尚未看到这类通用 mock。

### 3. 当前量化快照

| 指标 | 数值 | 备注 |
|------|------|------|
| C# 测试文件总数 | **245** | `find tests -name 'run_*.cs'` |
| GDScript 测试文件总数 | **0** | 已完成迁移 |
| `tests/shared/` 中共享文件数 | **18** | 主要是 resolver 和 TestHarness |
| 共享 fixture 工厂 | **不完整** | helper 内嵌在单个文件中 |
| 测试直接访问 `_state` 等私有字段 | **仍存在** | 需统计具体数量 |

### 4. 更新后的建议

在 `tests/shared/` 下补全 C# 共享测试基础设施：

```
tests/shared/
├── TestHarness.cs                          # 已存在
├── BattleTestFixture.cs                    # 新增：公共 BuildState / BuildUnit / AddUnits
├── BattleTestAssertions.cs                 # 新增：_assert_state_valid, _assert_unit_alive 等
├── MockGameSession.cs                      # 新增：通用 session stub
├── MockCharacterGateway.cs                 # 新增：CharacterManagementModule 边界 stub
├── StubRng.cs                              # 新增：从 run_shared_test_fixture_regression.cs 提取
├── StubDamageResolvers/                    # 已存在，保持
│   ├── FixedRollDamageResolver.cs
│   └── ...
└── run_shared_test_fixture_regression.cs   # 改为使用 BattleTestFixture + StubRng
```

关键约束：
- fixture 只能通过 **public API** 安装状态，不能直接写私有字段 `_state`
- stub 类应使用接口/抽象边界注入，而不是反射或内部可见性 hack
- 新增共享代码必须服务于生产代码迁移，不单独作为产出

---

## 第二部分：CharacterManagementModule 架构分析（C# 状态）

### 1. 文件规模

| 指标 | 数值 |
|------|------|
| 文件 | `scripts/systems/progression/CharacterManagementModule.cs` |
| 总行数 | **4298** |
| 公开方法声明 | **155** 处 |
| `private` 成员出现 | **172** 处 |
| 字段 | 至少 14 个（含 `_party_state`、`_skill_def_index`、`_profession_def_index` 等） |
| 直接持有的子系统/服务 | 7+（`_party_warehouse_service`、`_party_equipment_service`、`_bloodline_apply_service`、`_ascension_apply_service`、`_stage_advancement_apply_service`、`_quest_progress_service`、`_party_state`） |
| 外部服务依赖 | `AttributeService`、`ProgressionService`、`AttributeGrowthService`、`ProfessionAssignmentService`、`SkillMergeService`、`ProfessionRuleService` 等 |

**结论**：迁移到 C# 后，模块规模不但没有收缩，反而从 2306 行增长到 4298 行。文档中提出的拆分建议不但没有过时，反而更加紧迫。

### 2. 职责域分类（仍然不变）

```
CharacterManagementModule（4298行）
├── 身份管理 (~500行以上)
│   ├── 种族/亚种/血脉/飞升 getter
│   ├── 血脉 apply/revoke
│   ├── 飞升 apply/revoke
│   ├── 阶段提升 add/remove
│   ├── 身份摘要构建
│   └── 体型/年龄阶段刷新
│
├── 属性系统 (~300行以上)
│   ├── 属性快照构建
│   ├── 被动源上下文构建
│   ├── 武器投射/物理伤害标签
│   └── 装备属性修饰器集成
│
├── 技能知识管理 (~200行以上)
│   ├── learn_skill / learn_knowledge
│   ├── grant_racial_skill
│   ├── grant_battle_mastery
│   └── 种族技能回填/撤销
│
├── 奖励处理 (~250行以上)
│   ├── build_pending_character_reward
│   ├── apply_pending_character_reward（最复杂方法）
│   ├── PendingCharacterReward 生命周期
│   └── 临时服务创建链
│
├── 成就系统 (~100行)
├── 任务系统 (~150行)
├── 职业晋升 (~50行)
└── 战斗写回 (~50行)
```

### 3. 核心问题（仍然全部存在）

#### 3.1 临时服务每次调用重新创建

```csharp
// scripts/systems/progression/CharacterManagementModule.cs:2097
private ProgressionService BuildProgressionService(UnitProgress progression)
{
    var assignment_service = new ProfessionAssignmentService();   // new
    assignment_service.Setup(progression, _skill_def_index, _profession_def_index);
    var merge_service = new SkillMergeService();                   // new
    merge_service.Setup(progression, _skill_def_index, assignment_service);
    var rule_service = new ProfessionRuleService();                // new
    rule_service.Setup(progression, _skill_def_index, _profession_def_index);
    var progression_service = new ProgressionService();            // new
    progression_service.Setup(
        progression,
        _skill_def_index,
        _profession_def_index,
        rule_service,
        assignment_service,
        merge_service
    );
    return progression_service;
}
```

每次调用 `BuildProgressionService` 产生 **4 次 `new()` + 多次 `Setup()`**。这些子服务现在已经是独立的 C# 类：
- `ProfessionAssignmentService.cs`
- `SkillMergeService.cs`
- `ProfessionRuleService.cs`
- `ProgressionService.cs`

它们只依赖 `_skill_def_index`、`_profession_def_index` 和传入的 `progression`，完全可以在 module 级别复用。

同样：
- `_build_attribute_service()` 每次调用 `new AttributeService()`
- `_apply_level_trigger_attribute_growth()` 中 `new AttributeGrowthService()`
- `apply_pending_character_reward()` 中也可能创建 `AttributeGrowthService`

#### 3.2 直接修改多个数据源，无变更抽象层

C# 版本中仍然存在：

```csharp
_bloodline_apply_service.Apply(...)
_ascension_apply_service.Apply(...)
_party_warehouse_service.CommitBatchSwap(...)
member_state.current_hp = Mathf.Max(hp, 0)
member_state.progression.SetSkillProgress(...)
member_state.progression.SetAchievementProgressState(...)
_party_state.SetGold(...)
_party_state.RemoveMemberFromRosters(...)
```

没有统一的变更追踪、没有事务边界、回滚仍靠手动快照恢复。

#### 3.3 最复杂方法：`apply_pending_character_reward()`

仍然是模块中最复杂的方法之一，处理：
- 多个 reward 类型分支
- 多服务编排（`AttributeService`、`AttributeGrowthService`、`ProgressionService`）
- 执行前后状态快照对比
- delta 计算与写入

### 4. 可提取方向（更新为 C# 语境）

| 提取目标 | 预估行数 | 风险 | 说明 |
|---------|---------|------|------|
| **IdentityManager** | ~500+ | 中 | 种族/亚种/血脉/飞升 getter + apply/revoke + 体型/年龄刷新。`_bloodline_apply_service`、`_ascension_apply_service`、`_stage_advancement_apply_service` 已经是独立服务，只需聚合到管理器。 |
| **RewardProcessor** | ~250+ | 中 | `apply_pending_character_reward()` + `build_pending_character_reward()` + 相关 enqueue 逻辑。最复杂的单一职责块，值得独立测试。 |
| **BattleWritebackService** | ~50+ | 低 | 战斗资源/死亡/KO/战后 flush + 装备回收。职责独立，风险最低。 |
| **消除 transient service 创建** | 不增行 | 低 | 将 `BuildProgressionService()` 改为字段级复用（`_progression_service`），`_build_attribute_service()` 同理。每次调用省 4 次 `new()`。 |

### 5. 更新后的建议提取顺序

| 步骤 | 目标 | 收益 |
|------|------|------|
| 1 | **消除 transient service 创建** | 零行增长，减少 GC 和 Setup 开销 |
| 2 | **提取 `BattleWritebackService`** | 低风险验证拆分模式 |
| 3 | **提取 `RewardProcessor`** | 解决最复杂方法，可独立测试 |
| 4 | **提取 `IdentityManager`** | 最大单一职责块 |

---

## 三、联动关系更新

### 测试基础设施 vs CharacterManagementModule 拆分

C# 迁移文档明确要求测试不应依赖 private state。这意味着：

1. 在拆分 `CharacterManagementModule` 之前，必须先建立**基于 public API 的共享 fixture 和 stub 库**
2. 旧 GDScript 时代通过 `runtime._state = state` 直接注入私有字段的测试模式必须被替换
3. 拆分后的每个新 manager/processor 应该能通过其 public API 被独立测试

### 与 C# 迁移目标的关系

`docs/design/csharp_migration.md` 是当前项目的主导约束，与本审查的关系：

| C# 迁移要求 | 本审查的影响 |
|------------|-------------|
| 禁止为测试暴露 private state | 共享 fixture 不能写 `_state`，必须通过 setup 方法或构造函数 |
| 禁止新增 `ToDictionary()` 桥被核心逻辑回读 | 拆分后的服务间通信应使用 typed DTO，不是 dictionary |
| runtime 状态改为 `Dictionary<TKey,TValue>` / typed DTO | `CharacterManagementModule` 中的 `GDictionary` 业务状态应继续清理 |
| 新增/重写测试默认写 C# | 所有新回归测试和 fixture 都应写 C# |

### 与旧文档的关系

原文档提到的：
- `battle_runtime_module_split_plan.md`
- `project_architecture_review.md`

在当前 `docs/discussions/` 和 `docs/design/` 中已找不到。需要确认它们是否被归档、合并或重命名。在此之前，本审查不再引用这两份文档。

---

## 四、下一步行动建议

### 立即可以做（低风险）

1. **统计当前 C# 测试中私有访问的准确数量**
   - 搜索 `\._[a-z_]+` 在 `tests/` 中的使用
   - 识别仍直接写私有字段的测试

2. **消除 `BuildProgressionService` 的 transient 创建**
   - 在 `CharacterManagementModule` 初始化时创建 `_profession_assignment_service`、`_skill_merge_service`、`_profession_rule_service`、`_progression_service`
   - 仅在 def 索引变化时重新 setup

3. **消除 `_build_attribute_service` 的 transient 创建**
   - 将 `_attribute_service` 提升为字段级复用

### 短期做（中风险）

4. **补全 `tests/shared/` C# fixture 工厂**
   - 提取 `BattleTestFixture` 类
   - 将 `StubRng` 从 `run_shared_test_fixture_regression.cs` 中独立出来
   - 添加 `MockCharacterGateway` / `MockGameSession`

5. **提取 `BattleWritebackService`**
   - 将 `commit_battle_resources`、`commit_battle_death`、`commit_battle_ko`、`flush_after_battle` 等移入新类

### 中期做（中高风险）

6. **提取 `RewardProcessor`**
7. **提取 `IdentityManager`**

---

## 五、结论

原审查文档的核心诊断——测试基础设施薄弱、`CharacterManagementModule` 职责过重、transient service 创建过多——在 C# 迁移后**依然成立且更加紧迫**。

但原文档中的具体数据、文件路径、实现建议已经过时。当前项目处于 C# 迁移完成后的整理阶段，所有后续重构都必须符合 `docs/design/csharp_migration.md` 的约束，尤其是**不为测试暴露私有状态**和**不在核心逻辑新增 dictionary 桥接**。

建议优先执行：
1. 补全 C# 共享测试基础设施（基于 public API）
2. 消除 `CharacterManagementModule` 中的 transient service 创建
3. 按低风险到高风险的顺序拆分 `BattleWritebackService`、`RewardProcessor`、`IdentityManager`
