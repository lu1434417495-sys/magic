# 测试基础设施与 CharacterManagementModule 架构审查

> 状态：分析完成，待决策  
> 涉及文件：`tests/` 全部 135 个测试文件、`scripts/systems/progression/character_management_module.gd`（2306行）

---

## 第一部分：测试基础设施缺失

### 1. 问题规模

| 指标 | 数值 |
|------|------|
| 测试文件总数 | **135** 个（`run_*.gd`） |
| 访问私有成员 `runtime._*` / `facade._*` 的测试 | **39 个（29%）** |
| `runtime._state = state` 硬注入 | **261+ 处**，在所有战斗测试中反复出现 |
| 双层私有穿透（`facade._battle_runtime._state`） | **7 个文件** |
| 测试内定义的 stub/mock 类 | **~68 个** |
| 共享测试 helper 文件 | **仅 2 个**（`deterministic_battle_damage_resolver.gd` 15行、`deterministic_battle_hit_resolver.gd` 13行） |
| 使用共享 helper 的测试 | **仅 4 个文件** |
| `_build_unit()` 辅助函数 | 27 个文件各自定义一份 |

### 2. 核心问题：零共享基础设施

项目没有：
- **测试基类** — 每个文件从 `extends SceneTree` 开始，`_run()` 入口要自己写
- **共享 fixture 工厂** — `_build_unit()`、`_build_state()`、`_build_runtime()` 在 27 个文件中各写一份
- **共享 stub 库** — 同样的类在不同文件中独立定义
- **共享 assertion 工具** — `_assert_true()` / `_assert_eq()` 在每个文件中重复定义

### 3. Stub 重复定义问题

以下 stub 类在**多个文件中独立定义**，每次实现略有差异：

| Stub 类 | 独立定义次数 | 定义文件 |
|---------|-------------|---------|
| **`MockRuntime`** | **5 次** | `run_game_runtime_settlement_command_handler_regression.gd:68`、`run_game_runtime_party_command_handler_regression.gd:174`、`run_game_runtime_reward_flow_handler_regression.gd:97`、`run_settlement_forge_service_regression.gd:27`、`run_world_map_runtime_proxy_regression.gd:8` |
| **`FakeRuntime`** | **4 次** | `run_battle_hit_rate_legacy_cleanup_regression.gd:35`、`run_battle_unit_factory_regression.gd:81`、`run_passive_status_orchestrator_regression.gd:26`、`run_character_info_identity_regression.gd:34` |
| **`StubRng`** | **3 次** | `run_fate_attack_formula_regression.gd:6`、`run_fortuna_guidance_regression.gd:25`、`run_fortune_service_regression.gd:11` |
| **`MasteryGatewayStub`** | **2 次** | `run_battle_runtime_smoke.gd:44`、`run_battle_weapon_dice_regression.gd:42` |
| **`TrapDamageResolver`** | **2 次** | `run_battle_hit_preview_contract_regression.gd`、`run_battle_runtime_ai_regression.gd` |
| **`FixedRollDamageResolver`** | **2 次** | `run_magic_backlash_regression.gd`、`run_battle_weapon_dice_regression.gd` |

**最严重的例子** — `MockRuntime` 的 5 个独立实现：

```
# settlement handler 测试中的 MockRuntime（142行，17个字段）
class MockRuntime:
    var _active_settlement_id, _active_modal_id, _fog_system, _settlement_states, ...

# party handler 测试中的 MockRuntime（120行，13个字段）  
class MockRuntime:
    var _generation_config, _party_state, _party_selected_member_id, ...
```

两个实现字段完全不同、行为不同。任何一个 facade 内部字段变更，需要逐个文件修改各自的 `MockRuntime`。

### 4. 测试 setup/assertion 比例

以 `run_battle_runtime_smoke.gd`（3297 行）为例：

| 组成部分 | 行数 | 占比 |
|---------|------|------|
| 测试函数体（含 setup + assert） | 2616 行 | 79.3% |
| 辅助/夹具函数 | 511 行 | 15.5% |
| 导入 + stub 定义 | 99 行 | 3.0% |
| 测试入口 `_run()` | 67 行 | 2.0% |
| **断言语句**（422 条） | ~422 行 | **~12.8%** |

一个典型测试函数（35 行）中 **23 行是 setup，只有 6 行是断言**，比例约 **4:1**。

典型 setup 流程（每个战斗测试都重写一遍）：
```gdscript
1. var runtime := BattleRuntimeModule.new()
2. runtime.setup(stub_gateway, skill_defs, {}, {})
3. var state := BattleState.new()
4. state.battle_id = &"test_..."
5. state.phase = &"timeline_running"
6. state.map_size = Vector2i(8, 8)
7. state.cells = _build_test_cells()        // 本地函数
8. state.timeline = BattleTimelineState.new()
9. runtime._state = state                    // 直接覆盖私有字段
10. var unit := _build_unit(...)             // 本地函数
11. state.units[unit.unit_id] = unit
12. runtime._grid_service.place_unit(...)     // 直接调用私有字段
// 才能开始写 assert
```

### 5. 建议：建立共享测试基础设施

在 `tests/` 下新建 `tests/shared/` 目录：

```
tests/shared/
├── battle_test_fixture.gd          # 统一 _build_unit/_build_state/_build_runtime
├── stub_damage_resolvers.gd        # FixedHitOneDamageResolver 等集中定义
├── stub_runtime.gd                 # MockRuntime/FakeRuntime 统一定义
├── stub_game_session.gd            # MockGameSession 统一定义
├── stub_rng.gd                     # StubRng 统一定义
├── battle_test_assertions.gd       # _assert_state_valid, _assert_unit_alive 等
└── test_runner.gd                  # _run() 基础设施（_assert_true 等）
```

**第一步收益**（不做任何业务代码变更）：
- 消除 5 次 `MockRuntime` 重复定义 → 1 份
- 消除 4 次 `FakeRuntime` 重复定义 → 1 份
- 消除 3 次 `StubRng` 重复定义 → 1 份
- `_build_unit()` 从 27 份 → 1 份（带参数覆盖）
- 新增战斗测试的 boilerplate 从 200-500 行 → 10 行

这比拆分任何业务代码的投入产出比更高。基础设施建立后，BattleRuntimeModule 拆分时测试迁移成本大幅降低。

---

## 第二部分：CharacterManagementModule 架构分析

### 1. 文件规模

| 指标 | 数值 |
|------|------|
| 总行数 | **2306** |
| 公开方法 | **54 个** |
| 私有方法 | ~40 个 |
| 字段 | 14 个 |
| preload 常量 | 26 个（+ 10 个类型别名） |
| 直接持有的子系统 | 7 个（`_party_warehouse_service`、`_party_equipment_service`、`_bloodline_apply_service`、`_ascension_apply_service`、`_stage_advancement_apply_service`、`_quest_progress_service`、`_party_state`） |
| 外部静态/工具依赖 | 5 个（`AgeStageResolver`、`BodySizeRules`、`ProgressionDataUtils` 等） |
| 每次调用临时创建的 transient 服务 | 2 组（`ProgressionService` 链 4 个 + `AttributeService` + `AttributeGrowthService`） |

### 2. 职责域分类

```
CharacterManagementModule（2306行）
├── 身份管理 (~500行)
│   ├── 种族/亚种/血脉/飞升 getter（11个方法）
│   ├── 血脉 apply/revoke
│   ├── 飞升 apply/revoke
│   ├── 阶段提升 add/remove
│   ├── 身份摘要构建 get_identity_summary_for_member
│   └── 体型/年龄阶段刷新
│
├── 属性系统 (~300行)
│   ├── 属性快照构建 get_member_attribute_snapshot
│   ├── 被动源上下文构建 build_passive_source_context
│   ├── 武器投射/物理伤害标签
│   └── 装备属性修饰器集成
│
├── 技能知识管理 (~200行)
│   ├── learn_skill / learn_knowledge
│   ├── grant_racial_skill
│   ├── grant_battle_mastery / grant_skill_mastery_from_source
│   └── 种族技能回填/撤销
│
├── 奖励处理 (~250行)
│   ├── build_pending_character_reward
│   ├── apply_pending_character_reward（最复杂方法，102行）
│   ├── PendingCharacterReward 生命周期
│   └── 临时服务创建链（ProgressionService + AttributeService）
│
├── 成就系统 (~100行)
│   ├── record_achievement_event
│   ├── unlock_achievement
│   └── get_member_achievement_summary
│
├── 任务系统 (~150行)
│   ├── accept/complete/submit/claim quest
│   └── apply_quest_progress_events
│
├── 职业晋升 (~50行)
│   └── promote_profession
│
└── 战斗写回 (~50行)
    ├── commit_battle_resources
    ├── commit_battle_death / commit_battle_ko
    └── flush_after_battle
```

### 3. 核心问题

#### 3.1 直接修改多个数据源，无变更抽象层

```gdscript
# 同一个模块中，以下操作分散在不同方法里：
_bloodline_apply_service.apply(...)                    # 身份变更
_ascension_apply_service.apply(...)                     # 身份变更
_party_warehouse_service.commit_batch_swap(...)         # 仓库读写
_party_state.warehouse_state = warehouse_state_before   # 回滚直接赋值字段
member_state.current_hp = maxi(hp, 0)                  # 战斗写回
member_state.progression.set_skill_progress(...)        # 技能进度
member_state.progression.set_achievement_progress_state(...)  # 成就进度
_party_state.set_gold(...)                              # 金钱变更
_party_state.remove_member_from_rosters(...)            # 成员移除
equipment_state.pop_equipped_instance(...)              # 装备回收
```

没有统一的变更追踪、没有事务边界、回滚靠手动 `warehouse_state = warehouse_state_before` 快照恢复（出现在 quest claim 流程中，`character_management_module.gd:454, 514`）。

#### 3.2 临时服务每次调用重新创建

```gdscript
# character_management_module.gd:1295
func _build_progression_service(progression_state) -> ProgressionService:
    var svc = PROGRESSION_SERVICE_SCRIPT.new()          # new() 每次调用
    svc.skill_defs = _skill_defs
    svc.profession_defs = _profession_defs
    svc.achievement_defs = _achievement_defs
    svc.item_defs = _item_defs
    svc.progression_state = progression_state
    svc.profession_assigner = PROFESSION_ASSIGNMENT_SERVICE_SCRIPT.new()   # new()
    svc.skill_merger = SKILL_MERGE_SERVICE_SCRIPT.new()                    # new()
    svc.profession_rules = PROFESSION_RULE_SERVICE_SCRIPT.new()            # new()
    return svc
```

`_build_progression_service()` 在 `learn_skill()`、`grant_battle_mastery()`、`promote_profession()`、`apply_pending_character_reward()` 中每次调用都重新创建 `ProgressionService` + 3 个子服务。这些服务都是无状态的（只依赖传入的 `progression_state` 和定义字典），完全可以在 module 中复用。

同样，`_build_attribute_service()`（`character_management_module.gd:1471`）每次调用创建新的 `AttributeService`，`apply_pending_character_reward()` 中额外创建 `AttributeGrowthService.new()`。

**影响**：每次 `learn_skill()` 调用产生 4 次 `new()` + 4 次字段赋值，在批量奖励处理时开销累加。

#### 3.3 最复杂方法：`apply_pending_character_reward()`

`character_management_module.gd:910` — **102 行**，包含：
- 5 个 `match` 分支（knowledge_unlock / skill_unlock / skill_mastery / attribute_delta / attribute_progress）
- 6 条状态变更路径
- 4 个服务编排（`AttributeService`、`AttributeGrowthService`、`ProgressionService`、`SkillMergeService`）
- 执行前后状态快照对比计算 delta

### 4. 可提取方向

| 提取目标 | 预估行数 | 风险 | 说明 |
|---------|---------|------|------|
| **IdentityManager** | ~500 | 中 | 种族/亚种/血脉/飞升 getter + apply/revoke + 体型/年龄刷新。内部已经有 `BloodlineApplyService`、`AscensionApplyService`、`StageAdvancementApplyService`，只需要把它们提取到管理器内聚。 |
| **RewardProcessor** | ~250 | 中 | `apply_pending_character_reward()` + `build_pending_character_reward()` + `build_pending_skill_mastery_reward()` + `enqueue_pending_character_rewards()`。最复杂的单一方法，值得独立测试。 |
| **BattleWritebackService** | ~50 | 低 | `commit_battle_resources()` / `commit_battle_death()` / `commit_battle_ko()` / `flush_after_battle()` + `_salvage_member_equipment()`。简单但职责独立。 |
| **消除 transient service 创建** | 不增行 | 低 | 将 `_build_progression_service()` 改为字段级复用（`_progression_service`），`_build_attribute_service()` 同理。每次调用省 4 次 `new()`。 |

### 5. 建议提取顺序

| 步骤 | 目标 | 收益 |
|------|------|------|
| 1 | 消除 transient service 创建（改为字段复用） | 零行变更，减少 GC 压力 |
| 2 | `BattleWritebackService` 提取 | 50 行，风险极低，验证模式 |
| 3 | `RewardProcessor` 提取 | 250 行，最复杂方法独立测试 |
| 4 | `IdentityManager` 提取 | 500 行，最大单一提取块 |

---

## 三、两点联动

### 测试基础设施 vs CharacterManagementModule 拆分

`CharacterManagementModule` 的测试也在测试文件中直接访问私有成员。执行 module 拆分前先建立共享测试 infrastructure，可以让拆分后的回归测试成本降低。

### 与已有工作的关系

| 已有工作 | 本文档关系 |
|---------|-----------|
| `battle_runtime_module_split_plan.md` | 并行无冲突。`CharacterManagementModule` 和 `BattleRuntimeModule` 通过 `_character_gateway` 接口交互，两边独立拆分不碰撞 |
| `project_architecture_review.md` | 互补。审查报告聚焦 facade/session，本文聚焦测试基础设施和角色管理模块 |
