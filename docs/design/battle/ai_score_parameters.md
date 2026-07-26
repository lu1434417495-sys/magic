# 战斗 AI 评分参数当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-17`

## 定位

本文记录当前 `BattleAiScoreProfile`、immutable profile definition、评分服务和 battle-sim tuner 的接线。尚未落地的新增信号与分期方案位于 [`../../proposals/battle/ai_score_parameterization_roadmap.md`](../../proposals/battle/ai_score_parameterization_roadmap.md)，不属于当前字段合同。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `scripts/systems/battle/ai/BattleAiScoreProfile.cs`、enemy brain/profile `.tres` | 声明可导出的评分参数和中性/基线默认值 |
| 内容投影 | `BattleAiScoreProfileDefinition`、enemy definition graph、`ContentSnapshot` | 把 authored profile 冻结为 runtime 可借用的 typed definition |
| 评分输入 | `BattleAiScoreInput` 与各 evaluator/context adapter | 收集候选动作的伤害、目标、位置、资源、风险和状态事实 |
| 评分聚合 | `BattleAiScoreService` 及其 `Effects`、`Position`、`Scoring` partial | 读取当前 profile，把 typed input 聚合为分项和总分 |
| 选择与 trace | `BattleAiService`、`BattleAiDecisionResult`、score trace/report | 排序候选并交付 detached 决策与可解释评分事实 |
| 调参 | `tools/battle_sim_tuner/search_space.py`、BattleSim profile override | 定义 GPU/CMA 搜索空间并按字段名投影本次模拟 override |

## 当前链路

```text
BattleAiScoreProfile Resource
  -> EnemyContentRegistry / ContentSnapshot
  -> BattleAiScoreProfileDefinition
  -> BattleAiService decision scope
  -> BattleAiScoreInput
  -> BattleAiScoreService
  -> score breakdown + total score + detached trace
```

## 实现约束

- Runtime 和 BattleSim 只消费 `BattleAiScoreProfileDefinition`；authoring Resource 不执行评分算法，也不逃逸出内容构建边界。
- 新增或改名参数时，必须同时检查 authoring export、definition 投影、评分消费、`ToDictionary()`/trace 表面以及 tuner `search_space.py`；不能只改 `.tres` 或只改 scorer。
- 参数默认值属于行为兼容面。要求保持现有行为的新字段必须采用中性默认，并由评分回归证明默认 profile 排序不变。
- Profile override 是 simulation-local copy-on-write，不得改写 process `ContentSnapshot` 或 authored Resource。
- 一次 decision 结束后只交付 deep-copied command、score 和 trace；context、profile borrower 和 mutation snapshot 不能逃逸到下一次决策。
- AI mutation snapshot/capture/compare 只在定义 `MAGIC_AI_MUTATION_DIAGNOSTICS` 的构建中存在；Debug 默认启用，普通 Release 默认关闭。需要 Release 诊断时显式传入 `-p:MagicEnableAiMutationDiagnostics=true`。未包含诊断代码的构建若请求非 disabled guard mode 必须立即失败，不能静默跳过检查。
- 调参场景与不可变基线分离。数值搜索不能把 benchmark/baseline fixture 直接改成训练场景。

## 代表性回归

- `tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_ordering_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_input_metrics_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_context_adapter_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs`

模拟与调参命令见 [`balance_simulation.md`](balance_simulation.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-16 和 CU-20。
