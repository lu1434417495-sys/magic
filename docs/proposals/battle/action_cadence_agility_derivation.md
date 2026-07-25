# 行动节奏敏捷派生提案

> 状态：`Proposal / Not implemented`
> 更新日期：`2026-07-25`
> 关联上下文单元：CU-14（属性服务）、CU-15（战斗运行时总编排）、CU-16（战斗规则）
> 相关提案：[`counterattack_system.md`](counterattack_system.md)（反应预算已按同一思路做敏捷派生）

## 一、为什么单独立提案

反击系统把反应预算做成了敏捷派生（反击提案决策 4/5）。行动节奏理应得到同样待遇，但它**改的是已上线的时间线系统**，影响全部单位与全部 TU 计价内容。按 AGENTS.md 的文档纪律，不能夹带在反击提案里落地。

## 二、当前事实（已核对源码）

### 2.1 角色的行动节奏是写死的 30

```
AttributeService.cs:687-692
    if (attributeId == ACTION_THRESHOLD
        && !unitBaseAttributes.custom_stats.ContainsKey(ACTION_THRESHOLD))
        return DEFAULT_CHARACTER_ACTION_THRESHOLD;   // = 30
```

**敏捷对行动节奏毫无影响。** 除非显式写进 `custom_stats`，所有角色一律 30 TU 行动一次。这是本提案要改的核心。

### 2.2 敌人模板绕过属性系统

```
EncounterRosterBuilder.cs:731
    unitState.SetActionThresholdTyped(
        template != null ? template.ActionThreshold : BattleUnitState.DefaultActionThreshold);
```

敌人的 `action_threshold` **直接从模板写入单位，不经 `AttributeSnapshot`**。40 个模板手写 **25–60**（25×3、30×11、35×11、40×5、45×4、50×4、55×1、60×1），密集在 30/35；agility 分布 6–16（mod -2 ~ +3）。

### 2.3 引擎已有一条逐百分点的速率轴

```csharp
// BattleUnitActionClockState.cs:43
internal int ConsumeRateScaledGain(int baseProgressDelta, int ratePercent)
{
    int raw = baseProgressDelta * ratePercent + Math.Max(_actionProgressRateRemainder, 0);
    _actionProgressRateRemainder = raw % 100;   // 余数进位，零漂移
    return raw / 100;
}
```

调用链：`BattleTimelineDriver.CollectTimelineReadyUnits` → `BattleTemporalStatusService.ConsumeActionProgressGain(unitState, tuDelta)` → `ConsumeActionProgressRateGainTyped(tuDelta, ratePercent)` → 上面这段。

**加速 / 减速就是走这条轴。** 它是逐百分点的连续量，带精确余数进位，已实现且经验证。

### 2.4 两个尺度并存

| 来源 | `action_threshold` |
|---|---|
| `AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD` | 30 |
| `BattleUnitState.DefaultActionThreshold` | 120 |
| 敌人模板实际值 | 25–60 |
| `data/configs/battle_sim/scenarios/*.tres` | 110 / 120 |

这是既存技术债，不是本提案引入的。**采用速率轴后本提案不再受它阻塞**（§3.2）。

---

## 三、设计：派生到速率，不派生到阈值

### 3.1 公式

```
action_progress_rate_percent = clamp( 100 + agility_modifier × 15, 50, 250 )

action_threshold 完全不动。
```

设计锚：**每点敏捷修正 +15% 行动速率；极限敏捷（mod +7）恰好达到基准的两倍行动频率。**

| agility | mod | 速率 | TU/行动（threshold 30） | 相对基准 |
|---|---|---|---|---|
| ≤6 | -2 | 70% | 42.9 | 0.70× |
| 8 | -1 | 85% | 35.3 | 0.85× |
| 10–11 | 0 | 100% | 30.0 | 1.00× |
| **12–13** | +1 | 115% | 26.1 | 1.15× |
| **14–15** | +2 | 130% | 23.1 | 1.30× |
| 16–17 | +3 | 145% | 20.7 | 1.45× |
| 20 | +5 | 175% | 17.1 | 1.75× |
| 24 | +7 | 205% | 14.6 | **2.05×** |

全区间跨度 **2.94 倍**；敌人模板实际 agility 区间（mod -2 ~ +3）跨度 **2.07 倍**。作为参照，现有 40 个模板手写的 25–60 只有 2.4 倍跨度——本提案让敏捷成为一个真正强势的属性，这是有意的（§7 决策 2）。

### 3.2 为什么必须是速率轴而不是阈值轴

**阈值轴数学上做不到细分。** `action_threshold` 基数 30、`TuGranularity` 5，最多只能表达 4–6 个档位。任何基于阈值的敏捷公式都会出现死区（相邻两点敏捷映射到同一档），或者一步跳 17% 全局吞吐。速率轴是百分比，无粒度约束，每一点敏捷都产生效果。

**速率轴对两种尺度都正确。** 速率是乘法的，threshold 120 的 sim 单位同样得到 115% → 104 TU 的正确**相对**效果。阈值轴上任何锚死在基数 30 的公式套到 120 尺度都会算出荒谬结果。§2.4 的技术债因此不阻塞本提案。

**速率轴不触碰归一化。** 两层 `NormalizeActionThreshold`（`AttributeService.cs:878` / `BattleTimelineDriver.cs:393`）完全不用改，5 TU 整数倍约束继续只管阈值。

**速率轴不会制造"单 step 多次 ready"。** `AdvanceAndConsumeThresholds` 的 while 循环在一个 step 内跨过两次阈值时只产出一次 ready，第二次进度被吞——那是真正的断点，发生在 `rate ≥ 600%`（threshold 30、step 5 TU）。250% 时是每 2.4 个 step 一次行动，离断点很远。**所以 clamp [50, 250] 是设计护栏，不是物理约束**，将来想再拉大只需改常数。

### 3.3 与加速 / 减速的合流

`BattleTemporalStatusService.GetActionProgressRatePercent(...)`（cs:60）当前分支：

```
time_stasis        -> 0
time_slow          -> TimeSlowProgressRatePercent（固定）
temporal modifier  -> 该 modifier 的百分比
否则               -> FullProgressRatePercent（100）
```

改动：

- **敏捷派生值替换最后那个默认 100**，成为该单位的基础速率
- **temporal modifier 在其上做乘法**：`effective = base × modifier / 100`
- **`time_stasis`（0）与 `time_slow`（固定值）保持覆盖语义不变**

`time_slow` 不叠敏捷是有意的：它是绝对压制，"时缓"就该无视你多灵活。这样也不改动任何已上线内容的行为。

### 3.4 敌人侧：模板显式值优先，现存模板零改动

保持 `EncounterRosterBuilder.cs:731` 的现状——**模板写了 `action_threshold` 就用模板的**。敏捷派生作用在速率上，与模板阈值正交，二者相乘。

这意味着现存 40 个模板的阈值一字不改，但它们会**额外**获得与自身 agility 相称的速率修正。例如 agility 15、threshold 35 的模板：35 ÷ 1.30 = 26.9 TU/行动，比原来快。

**这是一次真实的敌人强度变化，必须在 P2 复核**（§5）。如果某些模板不希望被加速，给 `EnemyTemplateDef` 加一个显式的速率覆盖字段，而不是回退整个机制。

---

## 四、设计后果：敏捷是续航属性，不是爆发属性

冷却是**绝对 TU**，行动节奏是**相对频率**。687 个技能的冷却密集在 80 / 10 / 20 / 60 / 40 / 15 / 120 / 160 TU，状态时长密集在 80 / 60 / 120 / 100 / 40 TU。

```
速率 100%，冷却 80 TU  ->  该技能每 2.7 次行动可用一次
速率 205%，冷却 80 TU  ->  该技能每 5.5 次行动可用一次
```

**变快会让带冷却的技能相对变稀，让普攻与走位相对变密。**

这不是缺陷，是本提案接受的定位：**敏捷买到的是更多填充行动，不是更多爆发**。在 2× 频率差之下这条会变得非常明显，并因此产生真实的构筑分化——高敏 build 偏好低冷却 / 无冷却的连打型套路，高力量慢速 build 偏好大冷却爆发技。

需要在 P2 实测的是"是否有职业因此完全失去可玩性"，而不是"要不要消除这个效应"。

（曾考虑把冷却改成按行动次数计价来消除它，那要重定义 687 个技能的计价单位，代价远超收益，不做。）

---

## 五、影响面与必须复核的清单

| 面 | 影响 | 必须做什么 |
|---|---|---|
| 技能冷却（687 个） | §4，高敏角色技能相对变稀 | 每职业核心循环做"每 N 次行动能放几次技能"的前后对比 |
| 状态时长 | 时长按 TU 计；变快方在同一 debuff 窗口内多行动几次 | 检查控制类状态是否相对变弱、增益是否相对变强 |
| 体力恢复 | `ApplyStaminaRecovery` 按 `tuDelta` 走，与速率无关 → 变快的单位每次行动回复的体力**变少** | 确认高敏 build 不会体力卡死 |
| 敌人强度 | §3.4，现存 40 个模板全部获得速率修正 | 逐模板复核；必要时加显式速率覆盖字段 |
| 反应预算 | 反击提案已彻底解耦（决策 2/6），**不受本提案影响** | 无需改动，但要有断言解耦成立的回归 |
| Pending cast | 读条按 TU 计；变快的施法者读条期间损失更多自身行动 | 检查读条技能是否相对变弱 |
| AI | `BattleAiWaitActionEvaluator` 的等待价值随节奏变化 | 复跑 AI 行为回归 |
| 敏捷总价值 | 叠加反击提案后，敏捷同时驱动行动频率、反应容量、反应间隔、AC | 记录在案：敏捷是本作最强属性，这是有意选择（§7 决策 2） |

---

## 六、分期

**P1**：速率派生 + `GetActionProgressRatePercent` 合流改造 + 回归。**不调任何内容数值。**

**P2**：按 §5 清单实测，只在确认出现系统性偏移时调整内容。

拆开的理由：P1 是机制变更，P2 是平衡响应。混在一起会分不清手感变化来自机制还是来自数值补偿。

---

## 七、决策记录

1. **派生到 `action_progress_rate_percent`，不派生到 `action_threshold`。** 阈值轴基数 30 × 粒度 5，只能表达 4–6 档且必然出现死区；速率轴逐百分点连续、余数进位已实现、对 30/120 两种尺度都给出正确相对效果。

2. **系数 15%/点，全区间 2.94 倍。** 曾提议 5%/点（1.5 倍），差异不足以让敏捷成为有分量的选择。本作其它模块加值本就强势，敏捷做成强属性是有意的。设计锚是"极限敏捷 = 双倍行动频率"。

3. **clamp [50, 250] 是设计护栏不是物理约束。** 真正的引擎断点在 600%（单 step 多次跨阈值会吞掉行动）。将来想拉大只改常数，不需要改机制。

4. **`time_slow` / `time_stasis` 保持覆盖语义，不叠敏捷。** 绝对压制类效果无视灵活度，且不改动已上线内容行为。

## 八、待决

1. **§3.4 的敌人加速是否接受。** 现存 40 个模板会因自身 agility 获得速率修正，密集区（agility 12–15）会快 15–30%。这是一次全局敌人强度上调，需确认，或决定给模板加显式速率覆盖。
2. **§2.4 的双尺度技术债**是否借这次一并收敛，还是留作独立迁移。本提案不依赖它。

---

## 九、回归清单

| 文件 | 覆盖 |
|---|---|
| `tests/progression/attributes/run_action_rate_agility_derivation_regression.cs` | §3.1 表逐档；两个 clamp；每一点敏捷都产生不同速率（无死区） |
| `tests/battle_runtime/runtime/run_action_rate_temporal_composition_regression.cs` | 敏捷基础速率 × temporal modifier 乘法合流；`time_stasis` / `time_slow` 覆盖语义不变 |
| `tests/battle_runtime/runtime/run_action_rate_remainder_regression.cs` | 余数进位零漂移；非整除速率长程累计正确；250% 下不出现单 step 多次 ready |
| `tests/battle_runtime/runtime/run_action_rate_scale_independence_regression.cs` | threshold 30 与 120 两种尺度获得一致的**相对**效果 |
| `tests/battle_runtime/runtime/run_reaction_budget_cadence_independence_regression.cs` | 改变行动速率**不**影响反应预算补充频率（反击决策 2/6 的解耦断言） |

按 AGENTS.md，数值模拟 runner 不进常规全量回归；§5 的平衡复核属于显式平衡分析任务。
