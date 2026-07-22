# AI 模块检视剩余问题

原始检视日期：`2026-05-11`  
当前代码复核：`2026-07-20`

本文只保留沿当前 C# definition → assembler → evaluator → decision → execution 链重新核验后仍成立的问题。原 GDScript Resource 直接操作 live state、缺内容时合成 fallback 敌人、`harrier_pressure` 未注册、脚本暗中追加 action、selector 拼写不校验、enemy registry 可变缓存、blackboard 序列化和 String/StringName 热路径等已由当前实现覆盖，已从活跃意见移除。

## P0 / P1 正确性问题

### 目标同分时没有稳定 `unit_id` 兜底

- 候选单位来自 `BattleState` 内部 Dictionary values。
- `BattleAiTypedActionHelper` 的目标比较在距离和 HP 都相等时没有 `unit_id` 最终排序。
- 完全同分的候选因此可受 Dictionary 枚举顺序影响，破坏可重现性。

### Action/skill 静态校验没有校验 target mode 适配

- `EnemyAiAction.ValidateSkillReferences(...)` 主要检查 skill id 是否存在。
- unit-skill 和 ground-skill evaluator 在运行时遇到错误 target mode 仍只是跳过候选。
- 有效但类型错误的 skill 引用会通过内容校验，到战斗时才静默失效并落入其它 action / wait。

### 部分 evaluator 的 trace span 仍不具备异常安全

- `BattleAiChargeActionEvaluator`、`BattleAiChargePathAoeActionEvaluator` 与 `BattleAiMoveToRangeActionEvaluator` 仍有裸 `AiTraceRecorder.Enter/Exit`。
- 中间的 preview、path 或 score 调用抛异常时不会执行 `Exit`；`AiTraceRecorder` 依赖栈顶配对，后续外层 Exit 会 mismatch 并留下残帧。
- 项目已有 exception-safe `BattleAiTraceSpan`，应统一替换这些迁移后仍遗留的手工配对。旧 GDScript owner 已删除不代表同一失败模式已经消失。

## P1 / P2 AI 评估质量

### 普通伤害评分仍没有完整复用正式结算语义

旧报告“完全不传 target”已过时；当前已按 target 计算命中/豁免等部分事实。但 `BattleAiScoreService.Scoring.EstimateDamageForTargetResult(...)` 仍自行估算普通伤害，其部分路径把 `ShieldAbsorbed` 固定为 0，也没有完整复用正式 guard / fixed mitigation 语义。AI 仍可高估打不穿的目标或错估击杀线。

### Retreat 只枚举相邻四格

`BattleAiRetreatActionEvaluator` 仍只对 actor 相邻四格建立撤退候选，没有使用整回合可达区。安全格在两三步外时，AI 会浪费移动力或留在威胁区。

### 威胁距离仍主要是几何启发式

`nearest_role_threat_enemy` 和 safe-distance 主要使用有效射程与格距，没有把墙、深水、泥地绕路和边阻挡统一投入威胁成本。这属于 AI 模型限制，不是 runtime correctness bug。

### 多目标候选只取排序后连续窗口

`BattleAiMultiUnitSkillEvaluator` 仍使用 `startIndex + offset` 生成连续组合，不枚举非连续组合。最优“低血目标 + 高威胁治疗者”可能在评分前就被剪掉。

### Trace top candidates 与真实选择器排序不一致

`AiActionTrace` 的 top candidates 仍只按 `TotalScore` 排序，真实 decision engine 会先比较友伤、生存、致命威胁、bucket 等字段。旧报告所说的“Resource 中又复制一份 comparator”已修复，但 trace 排序仍可误导分析。

## Simulation / 可观测性

### `--include-simulation` 会收集无参数必败的 CLI runner

`tests/run_regression_suite.py` 的 simulation 发现只按目录放行；`run_battle_balance_simulation.cs` 在无参数时返回失败。显式 simulation 扩展入口因此会自行收集一个 CLI-only 必败项。

### Faction metrics 丢失 action / skill 计数

collector 已按 faction 记录 action count、skill attempt 和 skill success，但 faction summary 只保留伤害、治疗、击杀、死亡等标量，丢掉三张计数 map。

### Trace summary 仍缺真实排序所需字段

lethal threat、survival 和 resource 等部分字段已补，但 friendly-fire / friendly-lethal 以及 healing/status/terrain/cooldown/desired-distance 仍未完整保留，低 token summary 仍可无法解释真实胜出原因。

### 报告路径非空不等于写盘成功

`BattleSimRunner` 会预先填充 output path，`FileAccess.Open(...)` 失败时没有失败结果；现有 regression 主要断言路径字符串非空，没有证明文件存在且可 parse。文件名还只使用秒级 Unix 时间，同 scenario 在同一秒连续或并发运行会覆盖同一路径。

### Simulation 异常路径不会可靠清理全局 trace 与 runtime

execution loop 设置全局 `AiTraceRecorder` 后没有 `finally` 恢复，runner 也只在成功尾部 dispose runtime。一次异常可能污染后续 simulation 的 trace owner，并泄漏本场 runtime 资源。

### Balance CLI 默认把 progress log 写入仓库

`run_battle_balance_simulation.cs:58` 仍设置 `res://battle_sim_progress.log`，会制造工作树瞬态文件。默认应使用 `user://simulation_reports/...` 或要求显式参数。

### Trace disabled 时仍有候选 trace 构建成本

evaluator/helper 在 `trace_enabled` 关闭时仍可创建 trace、offer candidate 并 clone/sort，到 `RecordActionTrace` 才丢弃。这是 production 热路径性能债，不影响决策正确性。

## 测试优先级

1. 完全同分目标按 `unit_id` 稳定选择。
2. unit/ground action 引用错 target mode 时内容校验失败。
3. simulation output 实际存在、可 JSON parse、同秒不覆盖，且异常后 recorder/runtime 均恢复。

## Project Context Units Impact

AI decision → runtime commit 没有改变 owner；simulation 修复新增了 execution-loop 终止分类到 report/summary/CLI 的结果有效性契约，已同步更新 CU-20 的推荐读集和边界说明。
