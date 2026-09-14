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

## T3.5 修复与复跑

修复没有放宽 20 completed、3 attempts 或强度上下界：

- 导出 schema 为 `mp_cost`、`damage_tag`、`save_tag`、`effect_categories`、`attribute_growth_progress` 增加生成约束描述。
- 成长预算错误改用 `skill.validation.attribute_growth_total`，pointer 为 `/entries/0/attribute_growth_progress`，期望为 `sum equal to 60 for growth_tier basic`，实际为 `sum=30`。
- multi-unit 候选改用具备 `use_multi_unit_skill` action family 的 `ranged_archer`，普通远程/魔法 unit 基准改为 `mage_frost_bolt`。
- baseline/candidate 双方按候选单次完整资源成本提供三次容量；scenario iteration budget 从 600 增至 2000，unfinished 仍拒绝且不进入分母。

三个应接受源文件随后作为同一批进入四级链，四级的 `validated_entry_count` 均为 3。最终统计：

| 指标 | T3.5 结果 |
|---|---:|
| 尝试 | 7 |
| 接受 | 3（42.86%） |
| 拒绝 | 4（57.14%） |
| schema / domain / 跨域 / BattleSim 拒绝 | 各 1 |
| 误报 | 0 / 3（0%） |
| 误放 | 0 / 4（0%） |

同批 BattleSim 共得到 60 个 candidate completed samples：

| 技能 | baseline/candidate completed | candidate attempts | 胜率差 | 伤害比 |
|---|---:|---:|---:|---:|
| `mage_generated_force_needle` | 20 / 20 | 240 | 0 bp | 8788 bp |
| `mage_generated_ember_orb` | 20 / 20 | 60 | 0 bp | 6419 bp |
| `mage_generated_reserve_comet` | 20 / 20 | 60 | 0 bp | 7455 bp |

强度反例保持 20/20 completed，并以 `high_strength_outlier` 拒绝：胜率差 10000 bp、伤害比 100000 bp。其余三个反例仍分别在 schema、domain、跨域阶段拒绝，全部携带字段级 pointer 与 expected/actual。

入库后按同一预入库 snapshot 过滤规则重复整批，结果再次为 3 接受、4 拒绝、四级真拒绝各 1、误报/误放均 0；三个通过候选与强度反例的 baseline/candidate 仍全部 20/20 completed，说明修复不再依赖挑选某一次运行。

## DG-3 入库证据

四级同批 PASS 后，三个通过文件按原始字节机械复制到 `data/configs/json/skills/`，没有模板提取、字段改写或人工修补。fixture 与生产文件 SHA256 一致：

| 生产文件 | SHA256 |
|---|---|
| `generated_stage3_force_needle.json` | `FAAB6BABB122123FDF76915C76BBE5B98C0B2D8B8DCB44611E9E1CACD9A5FB9E` |
| `generated_stage3_ember_orb.json` | `0938DE70A557734C475CF5CF3B8D3CC8054337CC74691384F09FE7D2072DF651` |
| `generated_stage3_reserve_comet.json` | `2E055356BFAB3DB49E16512AE0F665D944C73197ED0E2BA1FE520DD371061253` |

`run_skill_generation_stage3_admission_regression.cs` 还验证了三条内容能从正式 process snapshot 加载，并保持 internal active、MP 成本、magical projectile、spell delivery 与非武器伤害骰边界。DG-3 到此停止；本次不扩大生成规模，也不进入阶段 4 装备闭包。

## 最终验证边界

- `dotnet build magic.csproj`：成功，0 error、2 warning。
- schema guidance、四级 validation pipeline、schema exporter、validator diagnostic golden、Definition projector parity、JSON directory round-trip、正式入库与专项 BattleSim gate 回归均 PASS。
- 修正三条入库技能对应的数量与 Definition SHA256 基线后，默认全量例行套件为 503/508；所有阶段 3 测试均 PASS，因此该结果不能记作全量 PASS。
- 5 个失败中，`run_battle_ground_effect_typed_sets_regression.cs` 在单独复跑时 PASS；其余 4 个在单独复跑时稳定失败，分别位于共享工作区正在修改的 multi-unit AI、HUD `gear_set_summaries` outward schema，以及 save version 已为 19 而两个旧断言仍要求 18。它们不读取或断言本批生成技能，但仍作为当前 checkout 的仓库级验证限制保留。
