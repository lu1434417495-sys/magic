# AI state transition consensus solution

日期：`2026-05-16`

来源：`docs/design/ai_module_subagent_review_2026-05-11.md` 中“brain 状态机表达力被硬编码死了”审查意见。

状态：四个子代理已进行对抗性审查并达成一致。本文件用于后续实现前的可行性审查，不包含代码修改。

## Problem

当前 enemy AI brain 表面上是数据驱动的：`.tres` 可以声明 `states`，每个 state 可以声明 authored actions 和 generation slots。

但“什么时候进入哪个 state”的逻辑仍写死在 `BattleAiService._resolve_state_id()`：

- 低血进入 `retreat`
- 有支援窗口进入 `support`
- 近敌进入 `pressure`
- `pressure_distance + 1` 作为 pressure sticky
- 否则回 `engage`

这导致 brain 资源无法表达新的状态机规则。新增 `burst`、`aoe_pressure`、`recover`、`kite` 等状态后，AI 不会自动进入这些状态，除非继续修改 service 脚本。

上一轮已完成的 affordance classifier、generation slot、runtime action plan 解决的是“状态内有哪些 action 可用”，没有解决“本回合应该切到哪个 state”。

## Consensus

采用“小型声明式 transition table”，不是通用表达式语言，也不是继续往 `_resolve_state_id()` 增加硬编码分支。

核心原则：

- `EnemyAiBrainDef` 拥有状态转移数据。
- `BattleAiStateResolver` 只做纯读解析。
- `BattleAiService` 是唯一提交 `unit_state.ai_state_id` 和 AI bookkeeping 的地方。
- transition 不依赖 generation slots、runtime action plan generated actions、action metadata、preview 或 action `decide()`。
- 不做旧字段兼容，不做 alias，不做 fallback 到旧逻辑。

## Data Model

### EnemyAiBrainDef

保留字段：

```gdscript
brain_id: StringName
default_state_id: StringName
states: Variant
```

新增字段：

```gdscript
transition_rules: Array
```

移除并不再读取：

```gdscript
retreat_hp_basis_points
support_hp_basis_points
pressure_distance
```

这些旧字段不能作为兼容路径保留。正式 `.tres` 和测试 fixture 都必须迁移。

### EnemyAiTransitionRuleDef

建议新增：`scripts/enemies/enemy_ai_transition_rule_def.gd`

字段：

```gdscript
rule_id: StringName
order: int
from_state_ids: Array[StringName] # empty means global
target_state_id: StringName
conditions: Array[EnemyAiTransitionConditionDef]
designer_note: String
```

规则：

- `conditions` 全部 AND。
- 需要 OR 时写多条 rule。
- 不提供 `condition_mode`。
- `conditions` 不允许为空。
- 用作 catch-all 的 rule 必须使用显式 `always` condition。
- brain 可以不声明 catch-all rule；无 rule 命中时 resolver 使用安全底线：当前 state 合法则保留当前 state，否则使用 `default_state_id`。
- rule 选择排序固定为：`order asc -> rule_id asc -> target_state_id asc`。
- `order` 是内容作者可见的全序，schema 拒绝重复 `order` 和重复 `rule_id`。`rule_id -> target_state_id` 只作为防御性排序兜底，避免未校验测试资源或诊断路径依赖 Array 偶然顺序。

### EnemyAiTransitionConditionDef

建议新增：`scripts/enemies/enemy_ai_transition_condition_def.gd`

字段：

```gdscript
predicate: StringName
state_ids: Array[StringName]
basis_points: int
distance: int
affordances: Array[StringName]
```

首批白名单谓词：

```gdscript
always
current_state_is
self_hp_at_or_below_basis_points
ally_hp_at_or_below_basis_points
nearest_enemy_distance_at_or_below
has_skill_affordance
```

语义约定：

- `always` 必须显式声明，不能用空 conditions 暗含 always。
- `current_state_is` 使用 `state_ids`。
- `self_hp_at_or_below_basis_points` 使用 `basis_points`，范围 `[0, 10000]`。
- `ally_hp_at_or_below_basis_points` 默认只看其他同阵营存活单位，不包含自己。
- `nearest_enemy_distance_at_or_below` 使用 `distance`，距离来自 grid service 的 unit distance。
- `has_skill_affordance` 只扫描当前 unit 的 `known_active_skill_ids`、`skill_defs` 和 `BattleAiSkillAffordanceClassifier` 输出。
- `has_skill_affordance` 不访问 generation slot、runtime action plan、action metadata，也不执行 preview/decide。
- `has_skill_affordance` 的语义来源是 skill classification，不是 generated actions。生产路径优先读取 `BattleAiRuntimeActionPlan` 上的 per-skill affordance cache；如果没有 runtime plan，`BattleAiContext` 提供本次 resolve 内的 lazy cache，确保 resolver 仍可独立输出 state。
- `EnemyAiTransitionConditionDef.to_trace_dict()` 输出固定 trace shape，例如 `{predicate, state_ids, basis_points, distance, affordances}`，不得把 Resource 引用直接放进 trace。

## Runtime Flow

新增：`scripts/systems/battle/ai/battle_ai_state_resolver.gd`

resolver 输入：

```gdscript
context
brain
```

resolver 输出结构化 result：

```gdscript
{
	"previous_state_id": StringName,
	"state_id": StringName,
	"rule_id": StringName,
	"reason": String,
	"matched_conditions": Array[Dictionary],
}
```

运行时流程：

1. `BattleAiService.choose_command()` 获取 brain。
2. 调用 `BattleAiStateResolver.resolve(context, brain)`。
3. `BattleAiService` 写入 `unit_state.ai_state_id = result.state_id`。
4. 根据 state id 从 runtime action plan 取 actions。
5. 选择 action 并生成 decision。
6. transition result 写入 `BattleAiDecision.transition`，turn trace 从 decision 拷贝该字段。

`BattleAiStateResolver` 不得写：

- `unit_state`
- `ai_blackboard`
- `state`
- `cells`
- `runtime_action_plan`

`BattleAiService` 仍是唯一允许写 AI bookkeeping 的层。

## Trace Contract

transition 不能只拼进 `reason_text`。`BattleAiDecision` 增加结构化字段：

```gdscript
"transition": {
	"previous_state_id": "...",
	"state_id": "...",
	"rule_id": "...",
	"reason": "...",
	"matched_conditions": [...]
}
```

无命中 fallback 也应有可追踪 reason：

- current state legal: `no_rule_matched_keep_current`
- current state invalid: `no_rule_matched_default`

## Runtime Plan Fingerprint

`BattleAiRuntimeActionPlan` 的 brain signature 需要纳入 transition rule/condition signature。

这是防御性 fingerprint，不是 action-plan 正确性的硬契约。transition 规则变化通常不会改变 per-state actions，但会改变 state 选择行为；把 transition signature 纳入 fingerprint 能让调试和 stale 检测更保守。

要求：

- `EnemyAiTransitionRuleDef.to_signature()`
- `EnemyAiTransitionConditionDef.to_signature()`
- `_build_brain_shape_signature()` 纳入 transition signatures
- `BattleAiRuntimeActionPlan` 维护 per-skill affordance cache，作为 resolver 的生产路径性能缓存；cache 内容来自 skill classification，不包含 generated actions。

transition 本身不依赖 runtime plan generated actions。stale plan 仍由 service 在 action resolution 阶段 fail closed。

## Schema Validation

`EnemyAiBrainDef.validate_schema()` 必须校验：

- `transition_rules` 必填，正式 brain 不允许空。
- rule 资源类型正确。
- `rule_id` 非空。
- `order` 不重复。
- `rule_id` 不重复。
- `target_state_id` 存在于 states。
- `from_state_ids` 中的 state 都存在；空数组表示全局。
- `conditions` 非空。
- condition 资源类型正确。
- 未知 predicate 直接失败。
- predicate 需要的参数类型和范围正确。
- `basis_points` 在 `[0, 10000]`。
- `distance >= 0`。
- `state_ids` 引用的 state 存在。
- `has_skill_affordance` 的 `affordances` 非空。

文本级迁移 guard 必须禁止 formal brain `.tres` 回流旧字段：

```text
retreat_hp_basis_points
support_hp_basis_points
pressure_distance
```

## Formal Brain Migration Shape

每个正式 brain 迁移为 4 到 6 条规则，保持人工可读。

典型 melee/frontline：

```text
10 low_hp_retreat:
  self_hp_at_or_below_basis_points -> retreat

20 ally_low_hp_support:
  ally_hp_at_or_below_basis_points
  has_skill_affordance(unit_ally.support / heal / guard-like affordance)
  -> support

30 near_enemy_pressure:
  nearest_enemy_distance_at_or_below -> pressure

40 pressure_sticky:
  current_state_is pressure
  nearest_enemy_distance_at_or_below hysteresis_distance
  -> pressure

90 default_engage:
  always -> engage
```

典型 ranged/controller：

```text
10 low_hp_retreat -> retreat
20 ally_low_hp_support -> support, if this brain has support affordance
30 near_enemy_pressure -> pressure
40 pressure_sticky -> pressure
90 default_pressure or default_engage, matching existing formal behavior
```

注意：`ally_hp_at_or_below_basis_points` 不包含自己；旧实现的 `_has_support_window()` 会把自己也算入同阵营扫描。因此所有 formal brain 迁移必须附行为等价对照表，逐 brain 写清旧条件如何映射到新 rules。

如果旧行为依赖“自己低血但有 guard/support 技能进入 support”，必须显式加：

```text
self_hp_at_or_below_basis_points
has_skill_affordance(...)
-> support
```

行为等价对照表至少包含：

```text
brain_id | old condition | old target state | new rule_id | new conditions | new target state | equivalent?
```

## TDD Plan

先写红用例，再实现。

新增建议：

- `tests/battle_runtime/ai/run_enemy_ai_transition_schema_regression.gd`
- `tests/battle_runtime/ai/run_battle_ai_state_resolver_regression.gd`

扩展现有：

- `tests/battle_runtime/ai/run_enemy_ai_generation_slots_content_regression.gd`
- `tests/battle_runtime/ai/run_battle_ai_runtime_action_plan_regression.gd`
- `tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd`
- `tests/battle_runtime/ai/run_move_to_range_progress_regression.gd`

必须覆盖：

1. 非保留 state id 可转移。
   - 使用 `recover`、`aid_ally`、`close_range`，证明不再硬编码 `retreat/support/pressure/engage`。

2. 优先级确定性。
   - 多个 rule 同时满足时，按 `order -> rule_id -> target_state_id` 稳定选择。

3. low HP transition。
   - `self_hp_at_or_below_basis_points` 切到自定义 `recover`。

4. ally support transition。
   - `ally_hp_at_or_below_basis_points + has_skill_affordance` 切到自定义 `aid_ally`。

5. distance pressure transition。
   - `nearest_enemy_distance_at_or_below` 切到自定义 `close_range`。

6. pressure hysteresis。
   - `current_state_is + nearest_enemy_distance_at_or_below` 显式保留 pressure，不再由 service 写死 `+1`。

7. no-match fallback。
   - 当前 state 合法时保留当前 state。
   - 当前 state 非法时回到 `default_state_id`。

8. resolver purity。
   - resolver 不写 `unit_state.ai_state_id` 和 `ai_blackboard`。
   - `BattleAiService` 提交后才改变 `ai_state_id`。

9. transition trace。
   - turn trace 或 decision 里有结构化 transition payload。

10. schema failure。
   - 未知 predicate。
   - 缺 target state。
   - 缺 from state。
   - 重复 rule id。
   - 重复 order。
   - 阈值越界。
   - 空 conditions。

11. formal content migration。
   - 所有 formal brain validate 通过。
   - 所有 formal brain 有 transition rules。
   - `.tres` 文本不包含旧字段。

12. runtime plan stale。
   - transition rule 或 condition signature 变化后，plan stale。

13. resolver 与 runtime plan 解耦。
   - brain 有 transition rules，但 `runtime_action_plan == null` 时，resolver 仍能输出 state。
   - state 没有 generation slots 时，resolver 仍能输出 state。
   - 如果需要 `has_skill_affordance`，无 runtime plan 路径应走 `BattleAiContext` 的本次 resolve lazy cache，而不是访问 generated actions。

## Existing Test Migration

以下模式必须清理：

```gdscript
brain.pressure_distance = ...
brain.retreat_hp_basis_points = ...
brain.support_hp_basis_points = ...
```

旧 fixture 要改成显式 transition rules，或者直接设置初始/current state 并确保 resolver 不覆盖。

已知需要重点搜索：

- `tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd`
- `tests/battle_runtime/ai/run_move_to_range_progress_regression.gd`
- `tests/battle_runtime/benchmarks/*.gd`
- `tests/battle_runtime/ai_baseline.json`

`_resolve_probe_target_distance()` 不能再读取 `brain.pressure_distance`。测试应固定布阵或从 action/rule fixture 明确给出距离。

已知旧字段使用面：

- `tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd`：6 处 `brain.pressure_distance = ...`，以及 `_resolve_probe_target_distance()` 读取 `brain.pressure_distance`。
- `tests/battle_runtime/ai/run_move_to_range_progress_regression.gd`：2 处 `brain.pressure_distance = ...`。
- `tests/battle_runtime/benchmarks/run_battle_6v40_headless_benchmark.gd`：1 处 `brain.pressure_distance = ...`。
- `data/configs/battle_sim/profiles/mist_controller_aggressive.tres`：patch `pressure_distance`。
- `data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres`：patch `retreat_hp_basis_points`。

删除旧 resolver 后，AI baseline 可能因 pressure sticky 等价表达而微调。正式迁移 PR 必须重录 `tests/battle_runtime/ai_baseline.json`，并在说明里标出变化来自 state transition schema 迁移。

## Sim Profile Migration

现有 battle simulation profile 可以 patch brain 字段。删除旧字段会让以下 patch 断裂：

```text
data/configs/battle_sim/profiles/mist_controller_aggressive.tres -> pressure_distance
data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres -> retreat_hp_basis_points
```

迁移要求：

- profile patch 路径改为 transition rule / condition 的深路径，例如 `transition_rules.<index>.conditions.<index>.distance` 或 `transition_rules.<index>.conditions.<index>.basis_points`。
- `BattleSimOverrideApplier._set_value_recursive()` 对未知 path 不得 silent no-op；至少 `push_error`，测试中应作为失败处理。
- sim profile migration 与 formal brain migration 同步完成，避免模拟实验静默回退到默认 AI 行为。

## Non-Goals

本次不做：

- 通用布尔表达式解释器。
- 字符串字段路径 DSL。
- 嵌套 AND/OR/NOT 表达式树。
- Dictionary 参数随便塞字段。
- 旧字段 fallback。
- dynamic state alias。
- transition 条件访问 generation slot 或 runtime action plan generated actions。
- transition 条件执行 action `decide()`。
- transition 条件执行 `preview_command()`。
- transition 条件枚举全图候选格。
- save payload 兼容迁移。

## Project Context Units Impact

实现时需要更新 `docs/design/project_context_units.md`：

- CU-16：加入 `BattleAiStateResolver`、state transition evaluator、transition rule signature。
- CU-20：加入 `EnemyAiTransitionRuleDef`、`EnemyAiTransitionConditionDef`、AI brain transition rules。
- 推荐装载组合“只改敌方模板、敌方技能表、AI brain”需要明确包含 transition rule/condition。

当前本文档只落地设计总结，尚未修改代码，因此本次不更新 `project_context_units.md`。

## Review Questions For Opus

请重点审查这些问题：

1. `ally_hp_at_or_below_basis_points` 默认排除 self 是否合理？是否需要单独提供 `any_faction_member_hp_at_or_below_basis_points`？
2. `has_skill_affordance` 依赖 affordance classifier 是否会让 transition 与 action generation 产生隐性耦合？当前约束是不访问 generation slots 和 runtime plan，只复用 skill classification。
3. `order` 是否应唯一？共识方案要求唯一，避免内容作者误以为同 order 会保留数组顺序。
4. fallback 策略是否应由显式 `always -> state` rule 完全承担？共识方案仍允许“无命中保留当前合法 state/default”作为安全底线。
5. transition signature 纳入 runtime action plan fingerprint 是否足够，还是需要独立 `brain_behavior_signature`？
6. 是否需要把 transition result 加到 `BattleAiDecision` 类，还是只写 `BattleAiContext.build_turn_trace()`？
7. `.tres` 规模是否可维护？每个 formal brain 控制在 4 到 6 条 rule 是当前边界。

## Opus 可行性审查 2026-05-16

总体：技术可行，方案完整度高。所有依赖基础设施（`BattleAiSkillAffordanceClassifier`、turn trace、runtime plan fingerprint、schema validator）已就位，没有缺失的底层能力。设计原则与现有 mutation guard、runtime plan stale 检测一致。但存在 3 个必须在实现前先决议的语义/集成问题，以及若干文档未覆盖的迁移面。

### 阻塞级问题（实现前必须先决议）

1. `ally_hp_at_or_below_basis_points` 默认排除 self 与现状不一致。
   - 当前 `BattleAiService._has_support_window()` 遍历同阵营单位时不跳过 self，"自己低血"也会触发 support state。
   - 新谓词若默认排除 self，会静默改变 `frontline_bulwark`、`healer_controller` 等 brain 的 support 触发行为。
   - 第 264-270 行已意识到差异，但需要的不是"如果某个旧行为依赖"，而是逐 brain 评估。
   - 落地要求：Migration Shape 章节加一条——所有迁移 PR 必须附"行为等价对照表"，逐 brain 给出旧条件 → 新 rule 集合的映射，证明等价。

2. sim profile 的 override patch 路径会直接断裂。
   - `data/configs/battle_sim/profiles/mist_controller_aggressive.tres` 用 `path=pressure_distance` 改 brain；`ranged_suppressor_cautious.tres` 用 `path=retreat_hp_basis_points`。
   - `BattleSimOverrideApplier._set_value_recursive()` 对未知 path **silent no-op**，删字段后 sim 行为会静默回退到默认值，实验结论被污染。
   - 落地要求：
     - 把这两个 profile 一并迁移到深路径 patch（如 `transition_rules.<index>.conditions.<index>.distance`）。
     - 或在 `BattleSimOverrideApplier` 加"未知 path 必须 push_error"硬校验。
     - 文档需要新增一节 `Sim Profile Migration`，明确 brain patch 的新路径示例。

3. `has_skill_affordance` 的调用代价与缓存归属未定义。
   - `BattleAiSkillAffordanceClassifier.classify_skill()` 当前只在 `BattleAiActionAssembler.build_unit_action_plan()` 内调用一次（plan 构建期）。
   - Resolver 若每回合每单位重新扫所有 known active skills，对 6v40 benchmark 这种压力场景会重复计算。
   - 落地要求：在 `BattleAiContext` 或 `BattleAiRuntimeActionPlan` lazily 缓存 `Dictionary[skill_id -> affordance set]`，resolver 走该缓存。文档必须明确这块拥有权（plan 还是 context），否则实现可能私拉 classifier 实例造成隐性副本。

### 设计层面建议（不阻塞但建议定稿）

4. `order` 全序与排序 tiebreaker 自相矛盾。
   - 第 86 行同时声明"order asc -> rule_id asc -> target_state_id asc"和"schema 拒绝重复 order"。后者使前两个 tiebreaker 永远不触发。
   - 落地要求：明确选一种——要么 order 唯一（tiebreaker 是纯防御冗余，可保留）；要么允许 order tie（提升内容编排弹性）。在文档里写清理由，避免实现者按字面双写。

5. fallback rule 半显式半隐式。
   - 第 85 行要求"fallback 也必须使用显式 `always` condition"；第 181-184 行又允许"无命中"走 keep-current/default。两者表面冲突。
   - 落地要求：明确表态——brain 可以不写 `always` rule；resolver 在无命中时按 keep-current（合法）/ default（非法）兜底，trace 写 `reason="no_rule_matched_keep_current"` 或 `"no_rule_matched_default"`。

6. transition signature 进 fingerprint 的实际必要性偏低。
   - 当前 `_build_brain_shape_signature()` 完全不包含 retreat/support/pressure 字段，生产没出过问题——因为 plan 内 actions 与 transition 字段正交。
   - 迁移后情况一样：transition rules 改了，per-state actions 仍是同一批。
   - 落地要求：保留 signature 纳入 fingerprint（对应 review question #5 的答复倾向"够用"），但在文档里写明这是"防御性 fingerprint"，避免后续 reviewer 误以为是契约。

7. transition trace 落点：写入 `BattleAiDecision` 类。
   - 针对 review question #6，建议写 decision：`_commit_decision()` 已经把 brain_id/state_id/action_id/reason_text 写进 ai_blackboard，transition payload 与 reason 同源，作为 decision 字段最自然；`BattleAiContext.build_turn_trace()` 读 decision 时顺便拷过去，零额外耦合；trace recorder 走 decision payload 无需新增 hook。

8. condition 序列化协议未指定。
   - `result.matched_conditions: Array[Dictionary]`（第 144 行）暗示 condition 要序列化，但 `EnemyAiTransitionConditionDef` 是 Resource。
   - 落地要求：condition 提供 `to_trace_dict()`，输出固定 shape `{predicate, params...}`，便于 trace 消费方稳定解析。

### 文档遗漏的迁移面

9. 测试 fixture 比文档列的范围更广，必须量化清单：
   - `tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd`：6 处 `brain.pressure_distance = ...`，外加 helper `_resolve_probe_target_distance()`（行 4067-4072）直接读 `brain.pressure_distance`。
   - `tests/battle_runtime/ai/run_move_to_range_progress_regression.gd`：2 处。
   - `tests/battle_runtime/benchmarks/run_battle_6v40_headless_benchmark.gd`：1 处。
   - 删除 `_resolve_state_id` 会让 ai baseline 测试结果发生微调（pressure sticky 的"+1"等价表达需要明确 `current_state_is + nearest_enemy_distance_at_or_below=N+1`），**必须重录 `tests/battle_runtime/ai_baseline.json`**，文档应显式列出该步骤。

10. TDD 计划缺一条 resolver 与 runtime plan 解耦的验证。
    - 现有 #8（resolver purity）只校验"不写 ai_state_id/ai_blackboard"。
    - 建议新增 #13：在 brain 有 transition rules 但 `runtime_action_plan == null` 或 `generation_slots == []` 的 fixture 上，resolver 仍能正常输出 state。这是 spec 的核心解耦原则，但当前测试列表无法覆盖。

### 工作量与风险评估

- 代码变更：3 个新脚本（rule/condition/resolver）+ brain def 改造 + service `_resolve_state_id` 删除 + fingerprint 扩展 ≈ +800 / -200 行。
- 数据迁移：9 个正式 brain `.tres`（每个 +60 / -3）+ 2 个 sim profile ≈ 600 行。
- 测试：3 个测试 fixture 重写 + 2 个新 regression（800-1000 行）+ ai_baseline 重录。
- 总规模：约 2500-3000 行变更。
- 风险等级：中。核心风险来自语义差异（self 是否计入 support 触发）与 ai_baseline 漂移，不是技术不可行。

### 建议落地顺序

1. 先决议阻塞问题 1-3（self 语义、sim profile 路径、affordance 缓存归属）。
2. 实现 condition + rule + resolver + schema，全用单元测试驱动，不动正式 brain。
3. 单独 PR 迁移正式 brain，附等价对照表，重录 ai_baseline。
4. 第三个 PR 迁移 sim profile + override applier 严格化。

## Opus 审查处理裁决

Opus 的总体结论是：方案技术可行，风险等级为中；阻塞点来自语义和迁移面，而不是底层能力缺失。

用户已确认的实施口径：

- `ally_hp_at_or_below_basis_points` 排除自己。
- `BattleSimOverrideApplier` 遇到未知 patch path 必须报错，不允许 silent no-op。
- `has_skill_affordance` 的技能分类缓存归属：生产路径放 `BattleAiRuntimeActionPlan`，无 runtime plan 的测试/特殊路径由 `BattleAiContext` 做本次 resolve lazy cache。

采纳裁决：

| Opus 意见 | 裁决 | 文档处理 |
| --- | --- | --- |
| `ally_hp_at_or_below_basis_points` 排除 self 会改变旧 support 触发语义 | 采纳 | 明确该谓词排除 self，并要求 formal brain 迁移附逐 brain 行为等价对照表；需要自己低血触发 support 时显式写 `self_hp_at_or_below + has_skill_affordance` rule。 |
| sim profile patch 旧字段会断裂，且 override applier 可能 silent no-op | 采纳 | 新增 `Sim Profile Migration`，要求迁移两个已知 profile，并让未知 patch path 硬报错。 |
| `has_skill_affordance` 调用代价与缓存归属未定义 | 采纳 | 明确生产路径 cache 放在 `BattleAiRuntimeActionPlan`，无 plan 时 `BattleAiContext` 做本次 resolve lazy cache。 |
| `order` 唯一与 tiebreaker 表述冲突 | 采纳 | 明确 `order` 是唯一全序；`rule_id -> target_state_id` 只是防御性排序兜底。 |
| fallback 规则半显式半隐式 | 采纳 | 明确 catch-all rule 必须显式 `always`，但 brain 可以没有 catch-all；无 rule 命中走 keep-current/default 并输出结构化 reason。 |
| transition signature 进入 fingerprint 的必要性偏低 | 部分采纳 | 保留纳入 fingerprint，但标注为防御性 fingerprint，不把它描述为 action plan 正确性的硬契约。 |
| transition trace 应写入 `BattleAiDecision` | 采纳 | Runtime flow 和 Trace Contract 改为 `BattleAiDecision.transition`，turn trace 从 decision 拷贝。 |
| condition 序列化协议未指定 | 采纳 | 增加 `EnemyAiTransitionConditionDef.to_trace_dict()` 固定 trace shape。 |
| 迁移清单不够量化 | 采纳 | Existing Test Migration 增加已知旧字段使用面、sim profile、ai baseline 重录要求。 |
| 缺 resolver 与 runtime plan 解耦测试 | 采纳 | TDD Plan 新增第 13 条。 |

按以上裁决，Opus 审查中的 3 个阻塞级问题均已在文档中决议。
