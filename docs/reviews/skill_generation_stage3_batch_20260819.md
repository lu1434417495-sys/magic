# 阶段 3 生成技能小批量实跑（2026-08-19）

## 范围与口径

本次只评估 7 个带人工判定标签的单条目 JSON case。每个 case 独立经过 schema、domain、跨域 ID、BattleSim 四级短路链；BattleSim 使用 T3.3 固定的 20 个 seed 和双臂对照。误报定义为“人工判定应接受，但自动链路拒绝”，分母为 3 个应接受 case；误放定义为“人工判定应拒绝，但自动链路接受”，分母为 4 个应拒绝 case。

语料位于 `tests/fixtures/skill_generation/stage3_batch/`，可重复入口为：

```powershell
godot --headless --script res://tests/battle_runtime/simulation/run_skill_generation_stage3_batch_evaluation.cs
```

该入口位于 `/simulation/`，不进入 routine full suite。

## 生成校准探针

正式统计前的未校准生成结果为 0/7 接受：schema 拒绝 4 条，domain 拒绝 3 条，尚未抵达跨域或模拟。它暴露出以下作者信息不足，不能计作 validator 误报：

- 生成方使用了 `cold`，而正式伤害标签为 `freeze`。
- 生成方把任意技能标识写入 closed `save_tag`，而该字段只接受登记的豁免语义标签。
- force damage 遗漏了显式 `force_effect` category。
- `basic` 的 `attribute_growth_progress` 合计未达到 60。

校准只让语料分别命中预定的门禁表面，没有更改任何 validator 或 BattleSim 规则。上述重复错误应在 T3.5 优先通过导出 schema 的字段描述消除。

## T3.4 正式首轮与重复结果

| 指标 | 首轮 | 同语料重复 |
|---|---:|---:|
| 尝试 | 7 | 7 |
| 接受 | 1（14.29%） | 0（0%） |
| 拒绝 | 6（85.71%） | 7（100%） |
| schema 拒绝 | 1（14.29%） | 1（14.29%） |
| domain 拒绝 | 1（14.29%） | 1（14.29%） |
| 跨域拒绝 | 1（14.29%） | 1（14.29%） |
| BattleSim 拒绝 | 3（42.86%） | 4（57.14%） |
| 误报 | 2 / 3（66.67%，6667 bp） | 3 / 3（100%，10000 bp） |
| 误放 | 0 / 4（0%） | 0 / 4（0%） |

两次使用相同 20 个 seed，但 completed 数并不稳定，因此 T3.4 的诚实结论是区间而非选择一次较好结果：接受率 0–14.29%，误报率 66.67%–100%。schema、domain、跨域三个真拒绝的分级保持稳定；漂移全部来自 BattleSim completed sample。

逐项结果：

| case | 人工判定 | 自动结果 | 关键证据 |
|---|---|---|---|
| `accepted_ember_orb` | 接受 | 首轮接受；重复时误报 | 首轮 baseline/candidate 均 20 completed、20 attempts、伤害比 10000 bp；重复时双方均 19 completed |
| `accepted_force_needle` | 接受 | BattleSim 拒绝（误报） | `incomplete_samples`；重复探针出现 19/18、18/19 与 19/19 completed，候选 attempts 始终为 0 |
| `expected_accept_reserve_comet` | 接受 | BattleSim 拒绝（误报） | `incomplete_samples`；候选 0 attempts，固定 120 MP 低于技能 130 MP 成本；completed 也在 17–19 间漂移 |
| `reject_schema_mana_cost` | 拒绝 | schema 拒绝 | `/entries/0/combat_profile/mana_cost`，`skill.dto.invalid_entry` |
| `reject_domain_growth_total` | 拒绝 | domain 拒绝 | `basic` 实际总量 30、期望 60；当前 pointer 仍停在 `/entries/0` |
| `reject_cross_missing_skill` | 拒绝 | 跨域拒绝 | `/entries/0/learn_requirements/0`，缺失 ID 原值可见 |
| `reject_simulation_outlier` | 拒绝 | BattleSim 拒绝 | 首轮以 20/20 completed 得到胜率差 10000 bp、伤害比 100000 bp；重复时 baseline 仅 17 completed，先由 `incomplete_samples` 拒绝 |

## T3.5 输入

1. schema 应补充 `damage_tag`、`save_tag`、`effect_categories` 和 `attribute_growth_progress` 的生成约束说明，先减少生成方重复犯错。
2. domain 成长预算诊断必须从 entry 根定位到 `/attribute_growth_progress`，并给出期望总量与实际总量。
3. BattleSim 标准夹具必须按候选真实资源成本提供可比较资源，并替换无法稳定完成/使用的 unit benchmark；修复后仍须保持 20 completed 与至少 3 candidate attempts，不能降低正式阈值来消除误报。
4. T3.5 复跑目标是 3 个应接受 case 全部通过、4 个应拒绝 case 仍在原定阶段拒绝，误报与误放均为 0。

T3.4 不执行生产入库；只有 T3.5 复跑与 DG-3 通过后，才允许把通过的原始 JSON 字节机械复制到 `data/configs/json/skills/`。
