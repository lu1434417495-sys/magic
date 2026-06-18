# 战斗 AI 评分参数化设计

> **状态：已落地实现。** 本文描述战斗 AI 评分（`BattleAiScoreProfile`）
> 全面参数化的字段目录和训练接线。新增字段已接入 C# 评分聚合、role-threat 目标排序、
> `BattleAiScoreProfile.ToDictionary()` 与 `tools/battle_sim_tuner/search_space.py` 的 GPU/CMA
> 训练空间。关联文档：[`battle_balance_simulation.md`](battle_balance_simulation.md)
> （模拟/调参使用说明）、[`project_context_units.md`](project_context_units.md)（CU-15/16 边界）。

## 关联上下文单元

- CU-15：战斗运行时总编排
- CU-16：战斗状态模型、伤害、AI 规则层（`BattleAiScoreService` / `BattleAiScoreProfile` 所在）

---

## 背景与关键发现

战斗 AI 的可调面是 `BattleAiScoreProfile`（约 35 个权重，由 `tools/battle_sim_tuner/` 的 CMA-ES 调参）。
深入排查 `BattleAiScoreService` 后发现一个关键事实：

> **评分引擎已经计算了约 30 个战术信号（`BattleAiScoreInput`），但最终 `total_score` 只用了其中很少
> 一部分；大量信号被算出来却丢弃。**

最终聚合（`scripts/systems/battle/ai/BattleAiScoreService.cs:363` 技能 / `:436` 动作）只有：

```
技能: total = 基础分 + hit_payoff_score + 有效目标数·target_count_weight − resource_cost_score + position_objective_score
动作: total = 基础分 + position_objective_score + 目标数·target_count_weight − resource_cost_score
```

被算出却**完全没进 `total_score`** 的高价值信号（最严重的是自身威胁投影）：

| 已计算信号 | 含义 | 现状 |
|---|---|---|
| `post_action_survival_margin` / `pre_action_survival_margin` | 行动后自身安全度的变化 | 算了，主评分未用 |
| `post_action_remaining_threat_count` / `..._expected_damage` | 行动后还剩多少威胁、预计挨多少伤害 | 算了，主评分未用 |
| `post_action_is_lethal_survival_risk` | 这么做会不会让自己处于被秒风险 | 算了，主评分未用 |
| `estimated_hit_rate_percent` | 命中率 | 仅内部硬编码 ÷100，无可靠性偏好权重 |
| `estimated_post_save_damage` | 豁免后仍落地的伤害（可靠伤害） | 无独立权重 |
| `estimated_shield_absorbed` | 护盾吸收 | 硬编码 ÷4，无参数 |
| `estimated_control_count` | 控制数（与 `status_count` 分开算） | 无独立权重 |
| `ground_control_score` / `enemy_target_count` / `estimated_chain_enemy_target_count` | 地面控制 / 敌方目标数 / 链式命中敌人数 | 都算了，未单独加权 |

**结论：AI 已经"看得见"自己会不会死、命中可不可靠、护盾/控制/链式效果，但主评分把这些都丢了。**
最高杠杆、最低风险的参数化，就是给这些**已计算信号**接上可调权重并折进 `total_score`。

**核心安全原则：** 所有新字段默认**中性**（权重 0 / 不触发阈值），调参前 AI 行为（含不可变 6v12 基线）
逐字节不变 —— 天然不引入回归。所需数据全部已可达：`BattleAiScoreInput` 字段；自身 HP/资源
`current_hp/current_mp/current_stamina/current_aura`（`BattleUnitState`）+ 上限属性键
`hp_max/mp_max/stamina_max/aura_max`（`AttributeService`，经 `attribute_snapshot.GetValue(key)`，
aura 另有 `GetAuraMax()`）。

---

## 全量参数目录（按决策维度分组）

"类型"：**(提)**=把硬编码常量提成参数（零新逻辑）、**(接)**=给已算信号接权重（低风险）、
**(新)**=需少量新计算。"信号来源"=该参数所乘的已计算量。

### A 组 — 自身生存 / 威胁投影（已算却丢弃，最高价值）— (接)

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `survival_margin_gain_weight` | 奖励行动后自身安全度提升 | post − pre `survival_margin` | 0 |
| `post_action_threat_damage_weight` | 惩罚行动后仍压在自己身上的预计伤害 | `post_action_remaining_threat_expected_damage` | 0 |
| `post_action_threat_count_weight` | 惩罚行动后剩余威胁单位数 | `post_action_remaining_threat_count` | 0 |
| `lethal_survival_risk_penalty` | 行动后处于被秒风险则硬惩罚 | `post_action_is_lethal_survival_risk` | 0 |
| `incoming_threat_relief_weight` | 奖励削减自身承受威胁 | pre − post `threat_expected_damage` | 0 |

→ 让**每个**单位都会权衡"这么做会不会害死自己"，而不只法师/弓手的特例生存桶。

### B 组 — 资源 / 续航（L1+L2+L3，MP/耐力/灵气）— (新)

对每种 R ∈ {mp, stamina, aura}：

| 参数 | 作用 | 默认 |
|---|---|---|
| `<R>_reserve_floor_bp` | 施放后池子占比低于此值开始加压（万分比） | 0（关） |
| `<R>_reserve_pressure_weight` | L1 平滑爬坡：越接近空越贵 | 0 |
| `<R>_reserve_breach_penalty` | L2 跌破底线的硬惩罚 | 0 |

外加全局 `resource_conservation_weight`（L3 乘子，默认 100 = 1.0×）。共 **10 个**。
AP/冷却是每回合资源，保持平的、不纳入续航。公式见「集成点 · B 组」。

### C 组 — HP 紧急度 / 处决 / 过量伤害 — (接/新)

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `low_hp_urgency_threshold_bp` / `low_hp_urgency_weight` | 自身低血时抬高自保类动作价值 | `current_hp / hp_max` | 0 |
| `execute_target_hp_threshold_bp` / `execute_bonus_weight` | 优先终结残血敌人 | 目标 HP | 0 |
| `overkill_damage_penalty_weight` | 惩罚超出目标血量的浪费伤害 | `estimated_damage` vs 目标 HP | 0 |

### D 组 — 目标选择 / 聚火 — (提/接)

| 参数 | 作用 | 类型 | 默认 |
|---|---|---|---|
| `role_threat_min_effective_range` / `_distance_window` / `_max_approach_distance` / `_max_contact_range` / `_in_range_score_step` | 提升 `EnemyAiAction` 里写死的 role-threat 几何常量与 `1000 + tr·10` 的步长 | (提) | 4 / 4 / 7 / 2 / 10 |
| `enemy_target_count_weight` | 单独奖励命中更多敌人（区别于含友军的通用目标数） | (接) `enemy_target_count` | 0 |
| `chain_enemy_target_weight` | 奖励链式弹射命中敌人 | (接) `estimated_chain_enemy_target_count` | 0 |
| `focus_fire_wounded_target_weight` | 偏好已受伤目标（集中火力） | (接) 目标 HP | 0 |

### E 组 — 命中可靠性 / 风险 — (接)

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `hit_rate_reliability_weight` | 偏好高命中动作 / 惩罚低命中赌博 | `estimated_hit_rate_percent` | 0 |
| `save_reliable_damage_weight` | 偏好豁免后仍落地的可靠伤害 | `estimated_post_save_damage` | 0 |

### F 组 — 伤害质量 — (提)

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `shield_absorbed_weight` | 把硬编码 ÷4 的护盾吸收估值提成参数 | `estimated_shield_absorbed` | 等效当前（damage_weight/4） |

### G 组 — 状态 / 控制粒度 — (接/新)

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `control_weight` | 控制数独立加权（现仅 `status_weight` 笼统涵盖） | `estimated_control_count` | 0 |
| `ground_control_weight` | 地面控制评分加权 | `ground_control_score` | 0 |
| `status_redundancy_penalty` | 惩罚对已有同状态目标重复上状态 | (新) 目标现有状态 | 0 |

### H 组 — 站位 — (提/接)

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `position_objective_weight` | 把目前隐式系数 1 的 `position_objective_score` 变可调 | `position_objective_score` | 100（=1.0×） |
| `safe_distance_adherence_weight` | 奖励处于/超过安全距离 | `position_safe_distance` vs 当前距离 | 0 |

### I 组 — 剩余硬编码阈值提参 — (提)

这些在评分代码里写死、却明显影响行为的阈值，提成参数后可调（默认 = 当前常量，行为不变）：

| 参数 | 作用 | 来源 | 默认 |
|---|---|---|---|
| `min_ranged_threat_range` | 判定"算远程威胁/远程攻击"的最小有效射程 | `MinRangedThreatRange`（`BattleAiScoreService.cs:15`，多处引用） | 3 |
| `friendly_lethal_min_probability_percent` | 触发"会致死友军"风险标记的概率阈值 | `FriendlyLethalMinProbabilityThreshold`（`BattleAiScoreService.cs:16`） | 15 |
| `meteor_cluster_min_enemy_count` | meteor/大 AoE 判定为"集群"用例的最小敌人数 | `enemy_target_count >= 3`（`BattleAiScoreService.Effects.cs:97`） | 3 |

> 注：豁免减半 `damageBeforeSave / 2`（`Scoring.cs:1036`）、骰子期望 `(sides+1)/2`（`Effects.cs:948/954`）
> 是**游戏规则/数学恒等式**，不应作为 AI 偏好参数，**不**提取。

### J 组 — 伤害类型 / 抗性匹配（完全缺失的维度）— (新)

单位的 `damage_resistances`（`BattleUnitState`）目前**只被 `BattleAiMutationGuard` 快照/还原**，
**从不参与目标或技能选择**——AI 不会"挑目标弱点打、避开被该目标高抗的伤害类型"。这是一个完整缺失的
智能维度，需新计算（比对技能效果的伤害类型 vs 目标抗性）。

| 参数 | 作用 | 信号来源 | 默认 |
|---|---|---|---|
| `damage_matchup_weakness_weight` | 奖励对目标弱点(低抗/易伤)的伤害类型 | 效果伤害类型 vs 目标 `damage_resistances` | 0 |
| `damage_matchup_resist_penalty_weight` | 惩罚打在目标高抗伤害类型上 | 同上 | 0 |

（仅 `GetPreResistanceDamageMultiplier` 读了效果自带的 `pre_resistance_damage_multiplier`，那是技能数据，
不是目标抗性匹配。）

**合计新增/提升 ≈ 41 个参数**（多数为 (接)/(提)，骑在已算信号上，零或极少新计算，默认中性）。

---

## 需新增信号的参数组（K–O，更高成本）

继续分析发现的维度，评分**目前完全不计算**，价值高但要先写"信号采集"（往 `BattleAiScoreInput` 加字段
并在对应 `Populate*` 里填）。每组先标注**需采集的新信号 → 采集来源**，再给参数。属第五期及以后。

### K 组 — 阵型 / 间距（cohesion & spacing）— (新)
**新信号**：`nearest_ally_distance`、`allies_within_cohesion_radius`、`own_units_in_self_aoe_blast`
→ 采集来源：`BattleState.GetUnitsTyped()` 遍历同 `faction_id` 友军坐标 + `grid_service.GetDistance`。

| 参数 | 作用 | 默认 |
|---|---|---|
| `cohesion_radius` | 判定"友军在身边"的半径 | 0（关） |
| `ally_cohesion_weight` | 奖励落点附近有友军（抱团/互保） | 0 |
| `isolation_penalty_weight` | 惩罚最近友军超出半径（落单） | 0 |
| `self_aoe_cluster_penalty_weight` | 惩罚己方挤在一起（怕被敌方 AoE 一锅端） | 0 |

### L 组 — 掩体 / 视线（cover & LOS）— (新，需先建 LOS 辅助)
**新信号**：`breaks_los_to_ranged_threat_count`、`in_cover_vs_ranged`
→ 采集来源：**需新写**沿连线逐格 `IsWallBlocked` 的 LOS 辅助（`BattleGridService` 现仅有相邻
`IsWallBlocked` + `GetDistance`，无远距射线 API）。

| 参数 | 作用 | 默认 |
|---|---|---|
| `los_break_vs_ranged_weight` | 奖励落点对远程威胁断线 | 0 |
| `cover_vs_ranged_weight` | 奖励处于掩体后（远程命中受阻） | 0 |

### M 组 — 自身增益 / 护盾保全 — (新)
**新信号**：`self_shield_hp_after`、`self_shield_expiring`、`self_active_buff_count`
→ 采集来源：`unit_state.current_shield_hp` / `shield_duration` / 自身状态列表。

| 参数 | 作用 | 默认 |
|---|---|---|
| `self_shield_preserve_weight` | 价值保留/续上自身护盾 | 0 |
| `self_buff_waste_penalty_weight` | 惩罚浪费/打断自身已有增益的动作 | 0 |

### N 组 — 治疗目标质量 — (接/新)
**新信号**：`heal_target_missing_hp`、`heal_overflow`（溢出治疗量）
→ 采集来源：治疗目标 `current_hp / hp_max`（现有 `estimated_healing` 是平的，不分缺血程度）。

| 参数 | 作用 | 默认 |
|---|---|---|
| `heal_missing_hp_weight` | 按目标缺血量放大治疗价值（先救濒死/缺血多的） | 0 |
| `overheal_penalty_weight` | 惩罚溢出治疗（给满血/接近满血续奶） | 0 |

### O 组 — 行动经济 / 节奏（tempo）— (新)
**新信号**：`ap_remaining_after`、`enables_second_action`、`action_progress_gain`
→ 采集来源：`current_ap` / `action_threshold` / `action_progress`。注：一回合多动接近多步 lookahead，
本组只做单步近似（"这步是否给二动留出 AP / 推进出手进度"）。

| 参数 | 作用 | 默认 |
|---|---|---|
| `ap_efficiency_weight` | 奖励行动后仍留有可二动的 AP | 0 |
| `tempo_progress_weight` | 奖励推进出手进度 / 抢先手 | 0 |

**K–O 合计 ≈ 13 个参数**（全 (新)，均需先加信号采集；L 还需新建 LOS 辅助）。
**全文核心 (A–I) + 新维度 (J–O) 合计 ≈ 56 个可参数化点。**

---

## 通用接线配方（每个标量字段）

`BattleAiScoreProfile` 走反射机制，加字段是机械的：

1. `scripts/systems/battle/ai/BattleAiScoreProfile.cs`（约 L48-157）：`[Export] public int <name> = <中性值>;`
2. 同文件 `ToDictionary()`（约 L272-320）：加 `["<name>"] = <name>,`
3. `tools/battle_sim_tuner/search_space.py`：`_SCORE_WEIGHTS` 加 `("<name>", lo, hi)`、`SCORE_DEFAULTS` 加默认值。
4. `scripts/systems/battle/sim/BattleSimOverrideApplier.cs` **无需改**——`target_type="faction_ai_score_profile"`
   按字段名反射 patch（`_set_value_by_path`，约 L297-421），新字段自动可被 patch。

---

## 集成点（每组接到哪段代码）

### A / C / E / F / G / H 组（评分项）
折进 `BattleAiScoreService.cs` 的 `total_score` 聚合（约 L363 技能、L436 动作）。每项形如
`+ weight * signal`（惩罚则 `−`），`weight=0` 时无影响。A 组用已由 `PopulatePostActionThreatProjection`
填好的字段；其余用 `BuildSkillScoreInput` 已填的 `BattleAiScoreInput` 字段。

### B 组（续航）
加到 `scripts/systems/battle/ai/BattleAiScoreService.Position.cs` 的 `PopulateResourceCostMetrics()`
（约 L444-473），在现有平项之后：

```
sustain = 0
对每个 R ∈ {mp, stamina, aura}:
    max_R = actor.attribute_snapshot.GetValue("<R>_max")   # aura 用 GetAuraMax()
    若 max_R <= 0 或 cost_R <= 0: 跳过
    fill_after_bp = clamp((cur_R - cost_R) / max_R, 0, 1) * 10000
    below = max(0, <R>_reserve_floor_bp - fill_after_bp)
    sustain += <R>_reserve_pressure_weight * below / 10000              # L1 平滑爬坡
    若 fill_after_bp < <R>_reserve_floor_bp:
        sustain += <R>_reserve_breach_penalty                          # L2 跌破底线硬罚
resource_cost_score += sustain * resource_conservation_weight / 100    # L3 全局乘子
```

当所有 floor/weight=0、conservation=100 时，`sustain == 0`，与当前完全一致。

### D 组（聚火 / 目标几何）— 跨层
`EnemyAiAction` 只持有 `BattleAiContext`，它不暴露已解析的 `BattleAiScoreProfile`，需把 profile 接上：

1. `scripts/systems/battle/ai/BattleAiContext.cs`：加 `public BattleAiScoreProfile active_score_profile { get; set; }`。
2. `scripts/systems/battle/ai/BattleAiService.cs` 的 `ChooseCommand()`（约 L72）：在
   `_scoreService.BeginDecisionScope(...)` 解析出 profile 之后，立即设
   `context.active_score_profile = _scoreService.GetScoreProfile();`
   （`GetProfile()` 在 `BattleAiScoreService.cs:289`，经 `BattleAiService.GetScoreProfile()` 暴露）。
3. `scripts/enemies/EnemyAiAction.cs`：`_get_role_threat_selector_score` 等处把读 `const`（`EnemyAiAction.cs:8-12`）
   改成 `context.active_score_profile?.<field> ?? <const>`（保留 const 作为 null-profile/测试路径兜底）。

D 组的 enemy / chain / focus-fire 三个权重则在评分项里接（同 A 组方式）。

---

## 分期建议（控制 CMA-ES 维度）

约 41 个新参数会把调参维度从 35 提到约 76，一次性调很难。建议分组调、其余冻结：

1. **第一期：A 组（自身生存投影）+ B 组（续航）** —— 价值最高、互补，直接补上"会不会害死自己 /
   续航"两大盲区。
2. **第二期：C + E + F + I**（HP 紧急/处决、命中可靠性、护盾、剩余硬编码阈值）—— 都是 (接)/(提)，低风险。
3. **第三期：D + G + H**（聚火跨层、控制粒度、站位）—— 含唯一结构性改动（D 的 context 接线）与
   少量新计算（G 冗余）。
4. **第四期：J**（伤害类型/抗性匹配）—— 完整新维度，需新计算，单独一期评估收益。
5. **第五期+：K–O**（阵型/间距、掩体/视线、护盾保全、治疗质量、行动经济）—— 均需先加信号采集，
   L 还要新建 LOS 辅助；逐组评估，收益不确定者可不落地。

每期独立调参 + 验证通过后再开下一期。

---

## 调参 / 验证要点（重要）

这些参数只在「资源/HP/生存真正吃紧」的战斗里才有优化梯度。现有不可变 6v12 基线极偏向玩家
（94% 胜率、战斗短），会让它们全部自由漂移（连现有 35 权重调参在基线上都纹丝不动）。要调它们
必须新建**消耗/对峙型场景**（更小资源池或更高 `max_iterations`、持续交战），作为**新的非基线场景**
放在 `data/configs/battle_sim/scenarios/`，**不要碰不可变基线**。`tools/battle_sim_tuner/objective.py`
（已集中化）可能也需加入续航/存活信号，而非只 `net_kills + win_rate`。

### 验证步骤
1. `dotnet build magic.csproj -nologo -clp:ErrorsOnly` —— 必须 0 错误。
2. **无回归证明（默认中性）**：跑 AI 评分回归套件
   `tests/battle_runtime/ai/run_battle_ai_score_{selection,ordering,input_metrics,save_probability,context_adapter}_regression.cs`，
   全部应原样通过，证明中性默认复现当前行为。
3. **6v12 基线不变**：`COUNT=20 godot --headless -s
   res://tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs`，聚合须与改前一致。
4. **逐组验证参数生效**：用 `.tmp_tuner` 渲染探针 profile，分别拉高某组一个权重，通过 AI trace 确认
   行为按预期变化（如 `lethal_survival_risk_penalty` 高时单位不再做送死动作；`mp_reserve_pressure_weight`
   高时法师低 MP 推迟昂贵法术；`focus_fire_wounded_target_weight` 高时集中打残血）。
5. **调参冒烟**：把第一期参数加进 `search_space.py`，在消耗型场景跑一小段 CMA，确认新字段被 patch
   且整轮跑完。

---

## 不在本设计范围（后续路线图）

- action 层逐技能资源/血量门控（"stamina > X% 才放"），需动每个 `scripts/enemies/actions/*.cs`；
  本文 scoring 侧的续航模型（B 组）是更便宜的第一杠杆。
- 参数稀疏 brain（`melee_aggressor` / `ranged_archer`）的 per-archetype 激进/接战阈值。
- `brain_ai_score_profile` override target（按 brain 调参，如只调弓箭手）。
- 多回合 lookahead / combo 序列（A 组的单步威胁投影是其低成本近似）。

- 需新增信号的维度（阵型/间距、掩体/视线、护盾保全、治疗质量、行动经济）已正式化为上文
  **K–O 组**，属第五期及以后。
