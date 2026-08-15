# 战斗 AI 评分参数当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-14`

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
- 带 `approach_attack_profile` 的 unit skill 强制委托 canonical command preview；只有正交、最短推进、通行、屏障与相对起始格绝对高度全部合法时才形成候选。preview 的 `resolved_anchor_coord` 是评分位置，`source_advance_path` 只作为 detached trace 事实；scorer 继续使用普通标准武器攻击期望伤害、AP/体力/冷却代价与最终位置威胁，不增加技能专用权重。普通移动与基础攻击已经能安全完成同一目的时，较高资源/冷却成本自然降低该技能排序；普通移动点为 0 或行动锁定不影响该技能候选，但 typed 移动限制状态仍由 canonical preview 拒绝。
- 带 `line_through_attack_profile` 的 unit skill 同样强制委托 canonical command preview。候选以选定敌人为终点，preview 公开有序途中目标、终点、敌后落点与 capped-success 命中阶段；scorer 对途中攻击按各自命中率计入期望1W收益，对终点按“途中成功数状态概率 × 该状态终点命中率 × 该状态武器骰组数”汇总期望伤害，随后不再重复乘一次全局命中率。最终落点继续进入通用位置风险与威胁评分，不增加具体技能 id 权重。
- 带 `sequential_line_hit_profile` 的 unit skill 强制委托 canonical command preview；普通单位枚举只负责提出首目标候选，正交、首敌、LOS/墙体/屏障、每段续行距离和有序后续目标均由正式 preview 决定。preview 的每个 `AttackPreviewStage` 同时提供该段命中率与由前序成功相乘得到的到达概率，scorer 以两者乘积缩放该目标的标准伤害期望，并继续复用既有 AP、MP、冷却、目标价值和击杀评分；不增加技能 id 分支或专用权重。
- `aura_cost` 在直接资源权重中按 `round(100 * cost / max(aura_max, current_aura))` 的容量百分比计价，并夹在 `1..100`；原始绝对消耗仍保留在 score input、可支付门禁和 reserve floor/pressure/breach 中。这样长期成长后的大额斗气技能不会仅因绝对整数尺度压倒所有收益，同时高占比消耗与越过储备线仍受到惩罚。
- `delayed_resolution_cost_per_5_tu` 的默认值为 `1`，同时存在于 authoring Resource、immutable definition、plain/profile trace、simulation scalar patch 与 tuner 搜索空间。它表示确定性机会成本，不是命中率、打断率、逃离率或成功概率；这些风险在有正式可预览模型前不得借用 `execute_kill_probability_basis_points` 或隐式折损伤害。
- `guarding` 由状态语义触发短窗物理减伤投影，不按具体技能 id 分支。scorer 对每个射程内敌人保留一个“最佳可用技能或武器攻击”威胁，按 action progress、threshold 与正式时间进度倍率估算距下次行动的 TU；只有 `ready_in_tu < guarding.duration_tu` 的攻击进入候选减伤。每个正式物理 damage breakdown 独立计算 `damage - max(damage - power, 1)`，非物理段不减伤，敌人仍可改用伤害更高的非物理攻击；已有 `guarding` 以当前剩余 TU/强度形成 pre-action 基线，重复施放只获得边际收益。
- `taunted` 使用专用 `estimated_taunt_ally_damage_relief`，不计入 `estimated_status_count` 或 `estimated_control_count`。scorer 仅对有效认知为 `sapient`、且能在挑衅到期前行动的敌人估值；攻击候选必须在该次行动到来时完成冷却、按正式 AP 重置与体力恢复投影付得起成本，并通过 runtime 的 canonical cast-block 检查。对每名受保护友军选择该敌人当前射程内预期伤害最高的攻击，并用 `on_hit_damage * (p - p²)` 估算从正常命中降为劣势命中的收益，再复用 `damage_weight`。目标对该友军已有攻击劣势、攻击为 `direct_effect` 或任何规则层认定的 force-hit-no-crit、没有非挑衅者友军可保护或认知被疯狂/装备上限压低时，增量收益为零。
- 自施加的有害移动状态不计为泛化正向 status/control 收益。带 `MoveCostDelta` 的状态按当前移动点在施放前后的可达格数差乘 `movement_cost_weight` 计入 `resource_cost_score`；同状态刷新只计算超过现有效果的边际移动成本，其他移动状态仍可叠加。
- source-definition scoped 的有害状态只按当前候选来源的边际变化计入 `estimated_status_count`，不冒充硬控制。其他单位或其他定义的来源即使已满层，也不会吞掉本来源首次施加的收益；当前单位同一定义已经满层且时长不增长时，边际状态收益为零。该判定使用 `BattleStatusSemanticTable.MergeStatus(...)` 的纯合并结果，不按具体状态或技能 id 分支。
- aggregate `refresh` 语义的有害控制状态同样只按边际变化估值。目标没有该状态时计一次完整控制；刷新提高强度或层数时仍计完整控制；只延长持续时间时，按“新增 TU / 本次施加 TU”写入 `estimated_control_probability_basis_points`；目标已有同等或更长持续时间且强度不提高时不再领取控制收益。该规则读取状态语义和 `MergeStatus(...)` 的纯合并结果，不按状态 id 或技能 id 分支。
- 纯 shield 技能由 `BattleAiSkillAffordanceClassifier` 归为 support，不领取泛化 status/control 数量分。evaluator 通过 `BattleShieldPreviewRules` 计算每个合法友方目标相对现有护盾的期望净增量，分别写入 `estimated_shield_gain_basis_points` 与 `estimated_ally_shield_gain_basis_points`，并沿 fingerprint、decision clone 与 trace 保留；scorer 复用现有 `heal_weight` 折算该收益，不新增 profile/tuner 参数。没有期望净增量且不能延长同家族护盾时收益为零，所有计算均不得推进正式 RNG。
- 装备耐久伤害通过 `BattleEquipmentDurabilityResolver.BuildPreview(...)` 生成 `estimated_equipment_durability_loss_basis_points` 与 `estimated_equipment_destruction_probability_basis_points`，并沿 fingerprint、decision clone 与 trace 保留。scorer 分别复用 `damage_weight` 和 `status_weight` 折算收益，不新增 profile/tuner 参数；目标没有匹配装备时两项均为零，但候选仍保留并继续按普通攻击、位置和资源事实评分，不能用目标合法性过滤代替算法选择。
- `airborne_pull` 的 unit-skill evaluator 枚举每个合法目标与最终落点的笛卡尔组合，并强制以 canonical preview 过滤。`BattleAiScoreInput` 保留位移距离、友方近战接战数变化、可生效落地 field 增量、敌方高度降低量、施法者暴露惩罚和总落点评分；scorer 分别复用 `status_weight`、`target_count_weight`、`terrain_weight` 与 `height_weight`，不新增 profile/tuner 参数。把敌人送入施法者武器威胁距离会产生显式惩罚，有其他近战友军共同接战时惩罚减半；这些事实沿 fingerprint、decision clone、candidate extra 与 trace 保留，不能按具体技能 id 自动决定目标或落点。
- `wind_push` 地面候选继续由 ground-skill evaluator 枚举相邻方向并进入 canonical preview。preview 在 detached battle state 中按风向由远到近模拟范围内目标，先过滤体型、静滞、强制位移免疫、豁免免疫和零可达距离，再把逐目标失败概率与失败分支落点交给 `BattleAiScoreService.ForcedMove`。scorer 以失败概率折算位移距离、友方近战接战变化、落点 terrain 增量、高度变化和施法者暴露惩罚；空放及全部目标都无法移动的候选有效目标数为0，由既有 minimum-hit/value-floor 拒绝。该链只按 typed `wind_push` 与 preview facts 分派，不读取技能 id。
- AI mutation snapshot/capture/compare 只在定义 `MAGIC_AI_MUTATION_DIAGNOSTICS` 的构建中存在；Debug 默认启用，普通 Release 默认关闭。需要 Release 诊断时显式传入 `-p:MagicEnableAiMutationDiagnostics=true`。未包含诊断代码的构建若请求非 disabled guard mode 必须立即失败，不能静默跳过检查。
- 调参场景与不可变基线分离。数值搜索不能把 benchmark/baseline fixture 直接改成训练场景。

## 代表性回归

- `tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_ordering_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_input_metrics_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_context_adapter_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs`
- `tests/battle_runtime/skills/run_warrior_heavy_blow_windup_regression.cs`
- `tests/battle_runtime/skills/run_warrior_piercing_thrust_regression.cs`
- `tests/battle_runtime/skills/run_mage_voltage_hook_regression.cs`

模拟与调参命令见 [`balance_simulation.md`](balance_simulation.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-16 和 CU-20。
