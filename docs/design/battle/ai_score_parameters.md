# 战斗 AI 评分参数当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-04`

## 定位

本文记录当前 `BattleAiScoreProfile`、immutable profile definition、评分服务和 battle-sim tuner 的接线。尚未落地的新增信号与分期方案位于 [`../../proposals/battle/ai_score_parameterization_roadmap.md`](../../proposals/battle/ai_score_parameterization_roadmap.md)，不属于当前字段合同。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `scripts/systems/battle/ai/BattleAiScoreProfile.cs`、enemy brain/profile `.tres` | 声明可导出的评分参数和中性/基线默认值 |
| 内容投影 | `BattleAiScoreProfileDefinition`、enemy definition graph、`ContentSnapshot` | 把 authored profile 冻结为 runtime 可借用的 typed definition |
| 评分输入 | `BattleAiScoreInput` 与各 evaluator/context adapter | 收集候选动作的伤害、目标、位置、资源、风险和状态事实 |
| 评分聚合 | `BattleAiScoreService` 及其 `Effects`、`Position`、`Scoring`、`Taunt` partial | 读取当前 profile，把 typed input 聚合为分项和总分 |
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
- 可选蓄力候选由 evaluator 从 canonical `BattleWindupQuote` 投影 `FinalStaminaCost` 与 `DelayedResolutionTu`，通用 scorer 只消费这两个事实，不复刻力量、体质、等级、挡位或武器骰规则。`FinalStaminaCost` 覆盖技能基础体力后再统一进入 `stamina_cost_weight`、reserve floor/pressure/breach；`DelayedResolutionTu` 单独生成 `delayed_resolution_score = (TU / 5) * delayed_resolution_cost_per_5_tu`，并与 `resource_cost_score` 各从总分扣除一次。非蓄力候选两项延迟字段均为 0。
- `delayed_resolution_cost_per_5_tu` 的默认值为 `1`，同时存在于 authoring Resource、immutable definition、plain/profile trace、simulation scalar patch 与 tuner 搜索空间。它表示确定性机会成本，不是命中率、打断率、逃离率或成功概率；这些风险在有正式可预览模型前不得借用 `execute_kill_probability_basis_points` 或隐式折损伤害。
- `guarding` 由状态语义触发短窗物理减伤投影，不按具体技能 id 分支。scorer 对每个射程内敌人保留一个“最佳可用技能或武器攻击”威胁，按 action progress、threshold 与正式时间进度倍率估算距下次行动的 TU；只有 `ready_in_tu < guarding.duration_tu` 的攻击进入候选减伤。每个正式物理 damage breakdown 独立计算 `damage - max(damage - power, 1)`，非物理段不减伤，敌人仍可改用伤害更高的非物理攻击；已有 `guarding` 以当前剩余 TU/强度形成 pre-action 基线，重复施放只获得边际收益。
- `taunted` 使用专用 `estimated_taunt_ally_damage_relief`，不计入 `estimated_status_count` 或 `estimated_control_count`。scorer 仅对有效认知为 `sapient`、且能在挑衅到期前行动的敌人估值；攻击候选必须在该次行动到来时完成冷却、按正式 AP 重置与体力恢复投影付得起成本，并通过 runtime 的 canonical cast-block 检查。对每名受保护友军选择该敌人当前射程内预期伤害最高的攻击，并用 `on_hit_damage * (p - p²)` 估算从正常命中降为劣势命中的收益，再复用 `damage_weight`。目标对该友军已有攻击劣势、攻击为 `direct_effect` 或任何规则层认定的 force-hit-no-crit、没有非挑衅者友军可保护或认知被疯狂/装备上限压低时，增量收益为零。
- 自施加的有害移动状态不计为泛化正向 status/control 收益。带 `MoveCostDelta` 的状态按当前移动点在施放前后的可达格数差乘 `movement_cost_weight` 计入 `resource_cost_score`；同状态刷新只计算超过现有效果的边际移动成本，其他移动状态仍可叠加。
- AI mutation snapshot/capture/compare 只在定义 `MAGIC_AI_MUTATION_DIAGNOSTICS` 的构建中存在；Debug 默认启用，普通 Release 默认关闭。需要 Release 诊断时显式传入 `-p:MagicEnableAiMutationDiagnostics=true`。未包含诊断代码的构建若请求非 disabled guard mode 必须立即失败，不能静默跳过检查。
- 调参场景与不可变基线分离。数值搜索不能把 benchmark/baseline fixture 直接改成训练场景。

## 代表性回归

- `tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_ordering_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_input_metrics_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_context_adapter_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs`
- `tests/battle_runtime/skills/run_warrior_heavy_blow_windup_regression.cs`

模拟与调参命令见 [`balance_simulation.md`](balance_simulation.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-16 和 CU-20。
