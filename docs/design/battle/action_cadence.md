# 行动节奏（action_threshold）

> 关联上下文单元：CU-14（属性服务）、CU-15（战斗运行时总编排）、CU-16（战斗规则）
> 运行时存储与 codec 契约见 [`runtime_module.md`](runtime_module.md)（`BattleUnitActionClockState`），
> 本文只描述**阈值这个数值从哪来**。

单位每积累 `action_threshold` 点 action progress 行动一次。该值**由 agility 调整值派生**，
角色与敌人共用同一张表，没有第二条来源。

## 一、派生表

单一真源是 `ActionCadenceContentRules`（`scripts/player/progression/`，`content_definition` 层）。
运行时查表，不实时计算——`GetActionThreshold` 在时间线每 step 对每个单位调用，
`BattleAiScoreService` 的威胁预估也在调，而定义域只有 30 个整数点。

| 调整值 | −3 | −2 | −1 | **0** | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **阈值** | 85 | 60 | 45 | **40** | 35 | 30 | 30 | 30 | 25 | 25 | 25 | 25 | 25 | 20 | 20 | 20 |

| 调整值 | 13–16 | 17–25 | **26+** |
|---|---|---|---|
| **阈值** | 20 | 15 | **10** |

30 个条目，索引偏移 `+3`；**定义域外 clamp 到两端**（`m < −3` 取 85，`m > 26` 取 10）。

生成器（回归逐点比对，防止有人手改表值）：

```
m < 0:  BASE + GRANULARITY · m²                    慢侧二次加速惩罚
m = 0:  BASE
m > 0:  max(BASE - GRANULARITY · ⌈√m⌉, FLOOR)      快侧平方根递减收益
```

| 常量 | 值 | 含义 |
|---|---:|---|
| `BaseActionThresholdTu` | 40 | 基数，见 §3 |
| `ActionThresholdGranularityTu` | 5 | 等于 `tu_per_tick` |
| `MinDerivedActionThresholdTu` | 10 | 属性可达的最快阈值，**引擎边界**，见 §4 |
| `MaxTemporalProgressRatePercent` | 200 | 由 `FLOOR × 100 / GRANULARITY` 推导，不写死 |

两侧是同一条 `BASE ± GRANULARITY · m^e` 取 `e = 2` 与 `e = ½`。全整数运算
（`⌈√m⌉` 用 `isqrt` 加一次补正），输出恒为 `GRANULARITY` 的倍数，**不需要任何量化或舍入步骤**。

快侧档位结构是闭式：**第 k 档入场点 = `(k−1)² + 1`（1, 2, 5, 10, 17, 26），成本 = `2k−1`**。
第一点调整值就换一档（40 → 35），之后逐档变贵。

## 二、生效路径

| 侧 | 路径 |
|---|---|
| 角色 | `AttributeService.CalculateBaseActionThreshold`：`custom_stats` 有显式值则优先，否则读 `resolvedBaseValues` 的 agility（**已叠加装备修正**）查表。与 `CalculateBaseArmorClass` 同构 |
| 敌人 | `EncounterRosterBuilder` 在属性快照构建完成后读 `AttributeSnapshot` 的 `ACTION_THRESHOLD`。**`EnemyTemplateDef` 没有 `action_threshold` 字段** |
| sim | `BattleSimUnitSpec`：有 `base_attributes` 时走属性快照；spec 的 `action_threshold` 字段此时**失效**，要钉值必须写 `attribute_overrides["action_threshold"]` |

**作者要让某只怪更快或更慢，调它的 agility。** 模板手写阈值与 agility 派生并存会双重计价——
废除该字段前，40 个模板的 `pearson(threshold, agility) = −0.788`，作者已经把敏捷手写进阈值里了。

`BattleUnitState.DefaultActionThreshold` 与 `AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD`
同源（都转发到 `BaseActionThresholdTu`），不存在第二套尺度。

## 三、基数 40 由内容分布决定

不是手感数字，是从内容自己的 TU 计价反推的：

- 主流冷却 `40 / 80 / 120 / 160` 在 40 TU 上正好落在 **1 / 2 / 3 / 4 次行动**；冷却整除率 44.5%、状态时长整除率 55.6%，均为候选基数中最高。
- 按冷却量化对齐效率复核（单位只能在自己的行动点施放，实际循环是 `⌈c/T⌉ · T`），**40 TU 是 25–90 全区间对齐最好的一档**：92.3% 平均效率、63.9% 零等待。相邻的 35 / 45 是主流区里最差两档（80.8% / 0% 与 83.1% / 3.4%）——量化轴不可能同时对齐所有档位，这是已知且接受的代价。

**改动 `BASE` 会让 687 个技能的冷却与全部状态时长的语义整体漂移。** 回归
`run_action_cadence_tu_invariance_regression` 断言 40/80/120/160 整除基数，即为此设的绊线。

## 四、`FLOOR = 10` 是引擎边界

`BattleUnitActionClockState.AdvanceAndConsumeThresholds` 的 while 循环在一个 step 内跨过两次阈值时
只产出一次 ready，多余那次行动被**吞掉**。断点通式：

```
rate >= (100 × threshold + 1) / tu_per_tick        (tu_per_tick = 5)
```

| 阈值 | 断点 |
|---:|---:|
| 15 TU | 301% |
| **10 TU** | **201%** |
| 5 TU | 101% |

属性派生恒 100%，但**临时效果走速率轴**（`BattleTemporalStatusService`），所以最快阈值决定了
内容侧允许的速率上限。5 TU 档的 101% 意味着任何加速状态都会吞行动，因此封在 10。
现存最强的 `sands_time_pack` 是 200%，**只差 1 个百分点**——
`EquipmentAbilityBindingValidator` 因此有 `EQA_TEMPORAL_PROGRESS_MODIFIER_RATE_TOO_HIGH` 校验，
上限从 `FLOOR / tu_per_tick` 推导。

`run_action_threshold_floor_breakpoint_regression` 把 `FLOOR` 与该上限钉在一起：
放宽任一侧而不同步另一侧都会失败。

## 五、设计后果：敏捷买填充行动，不买爆发

一场长度 `L` 的战斗里，阈值为 `T` 的单位：

| 量 | 表达式 | 随 `T` 变化 |
|---|---|---|
| 某技能的施放次数 | `L / cooldown` | **否** |
| 被某状态罩住的时长 | `duration` | **否** |
| 蓄力损失的时间 | `windup_tu`（蓄力期间 action progress 冻结） | **否** |
| 体力回复总量 | `L × (11 + CON) / 50` | **否** |
| **行动次数** | `L / T` | **是** |

**TU 计价的绝对量一个都不随阈值变化，变的只有行动次数。** 直接推论：

- 变快不会让技能变强或变弱，只会让普攻与走位在行动里占比更高。
- **高敏 build 不可能体力卡死**：受体力约束的技能施放次数/TU 与阈值无关；
  且 `stamina_max = 24 + 5·CON + STR + AGI`，池子本身随敏捷上升。
- 控制类状态没有相对变弱，增益也没有相对变强。

`run_action_cadence_tu_invariance_regression` 守住这条：把体力回复或冷却改成按行动次数计价会让它失败，
那正是会把敏捷从续航属性变成爆发属性的改动。

## 六、内容侧需要知道的两件事

1. **快侧入场点比直觉贵。** agility 16 只到调整值 +3（30 TU），想要 25 TU 需要 agility 20。
   改前作者可以直接手写 25 TU，现在这条路封了。
2. **建卡掷骰是 `5d3 − 1`（下限 4，期望 9，硬上限 14）**，`RaceDef` / `SubraceDef` 的
   `attribute_modifiers` 全仓库无一条被填充。所以建卡能拿到的最快是**调整值 +2 / 30 TU**，
   且需要用创建界面的阈值 reroll 功能去争（代价是出生隐藏幸运，对数计价）。
   敌人模板的 agility 均值是 12.4，比建卡期望高——**敏捷是玩家要主动投入才不落后的属性**。

## 七、回归清单

| 文件 | 覆盖 |
|---|---|
| `tests/progression/attributes/run_action_threshold_agility_derivation_regression.cs` | 静态表逐点比对生成器（防手改表值）；表长 30、偏移 3；输出恒为 `GRANULARITY` 正整数倍；域外 clamp；`custom_stats` 显式值优先；`AttributeService` 端到端 |
| `tests/progression/attributes/run_action_threshold_curve_property_regression.cs` | 档位入场点 `(k−1)²+1`、成本 `2k−1`；慢侧惩罚先加速后收敛；快侧价值/成本序列；两端跨度 > 8× |
| `tests/battle_runtime/runtime/run_action_threshold_floor_breakpoint_regression.cs` | `FLOOR` 与引擎断点绑定：10 TU 在 200% 下不吞行动、201% 下吞；5 TU 在 101% 下即吞 |
| `tests/battle_runtime/runtime/run_enemy_action_threshold_derivation_regression.cs` | 敌人阈值来自 agility 派生且必须落进 `attribute_snapshot`；同 agility 的角色与敌人同档 |
| `tests/battle_runtime/runtime/run_action_cadence_tu_invariance_regression.cs` | §5 的不变性：体力按 TU 回复、冷却按流逝 TU 递减、主流冷却整除基数 |
| `tests/battle_runtime/simulation/run_battle_sim_unit_spec_defaults_regression.cs` | sim spec 的 `action_threshold` 字段在有 `base_attributes` 时失效 |
