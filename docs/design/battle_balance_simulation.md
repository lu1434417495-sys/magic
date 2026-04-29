# 战斗模拟、数值分析与 AI 调参系统说明

## 关联上下文单元

- CU-15：战斗运行时总编排
- CU-16：战斗状态模型、边规则、伤害、AI 规则层
- CU-19：自动化回归与截图辅助

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文记录 battle simulation / balance analysis 的使用说明与数据规范。

## 文档目的

这份文档描述当前仓库内已经落地的战斗模拟系统。它不是一份“怎么写一个类似系统”的设计稿，而是当前实现的真实使用说明和数据规范，目标是让人或外部模型可以直接基于它：

- 批量跑战斗模拟
- 做技能数值分析
- 调整敌人 AI 行动逻辑
- 调整 AI 评分权重
- 解析 `report_json` 与 `turn_trace_jsonl`
- 根据输出结果反推下一轮要改技能、AI 动作参数还是评分逻辑

如果后续把结果交给 GPT Pro、Claude 或其他分析模型，本文件就是它们需要优先阅读的系统说明。

## 系统定位

这套系统用于在不改主流程运行方式的前提下，构造明确的战斗输入，按多个 seed、多个 profile 批量跑战斗，并把结果以结构化 JSON/JSONL 输出。

它解决的是三类问题：

- 技能数值问题。
  - 某个技能是否过强、过弱、资源成本过低、冷却不合理。
- AI 逻辑问题。
  - 某类敌人是否太激进、太保守、站位失真、撤退太晚或太早。
- AI 评分问题。
  - 同一轮中，AI 为什么偏好某技能、走位、撤退或等待。

它不负责：

- 做最终 UI 可视化面板。
- 自动生成图表。
- 自动给出平衡结论。
- 自动修改仓库内容。

当前它负责把“可分析的真实输入”和“可归因的真实输出”稳定产出来。

## 核心入口

系统主入口：

- 场景定义：`res://scripts/systems/battle/sim/battle_sim_scenario_def.gd`
- 单位定义：`res://scripts/systems/battle/sim/battle_sim_unit_spec.gd`
- profile 定义：`res://scripts/systems/battle/sim/battle_sim_profile_def.gd`
- patch 应用：`res://scripts/systems/battle/sim/battle_sim_override_applier.gd`
- 汇总报表：`res://scripts/systems/battle/sim/battle_sim_report_builder.gd`
- 批量执行器：`res://scripts/systems/battle/sim/battle_sim_runner.gd`
- CLI 入口：`res://tests/battle_runtime/run_battle_balance_simulation.gd`
- LLM 分析包导出：`tools/build_battle_sim_analysis_packet.py`
- Repo 内分析 skill：`.codex/skills/battle-sim-analysis`

模拟依赖的运行时链路：

- `BattleRuntimeModule`
- `BattleAiService`
- `BattleAiScoreService`
- `BattleAiContext`
- `EnemyAiAction` 及具体 action 脚本

## 仓内示例资源

当前仓内已提供可直接运行的示例场景与 profile：

- `res://data/configs/battle_sim/scenarios/archer_pressure_example.tres`
- `res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres`
- `res://data/configs/battle_sim/profiles/baseline.tres`
- `res://data/configs/battle_sim/profiles/pinning_shot_blocked.tres`
- `res://data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres`

这三组 profile 分别对应：

- `baseline`
  - 不额外改动，作为对照组。
- `pinning_shot_blocked`
  - 通过提高 `archer_pinning_shot` 体力消耗，使其资源上不可用，用于验证 AI 是否回退到其他技能。
- `ranged_suppressor_cautious`
  - 同时提高撤退倾向、拉开站位距离，并提高撤退/移动在评分中的价值，用于验证保守型远程 AI 行为。

新增的 `ai_vs_ai_duel_example` 场景用于另一类验证：

- ally 与 enemy 都使用 `control_mode = ai`
- 更适合看整场战斗结果、胜负、战斗长度、双方真实技能使用与站位连锁反应
- 不再把玩家侧当木桩，因此更适合验证“数值 + AI 逻辑”组合后的整体表现

## 直接运行

运行示例：

```bash
godot --headless --script tests/battle_runtime/run_battle_balance_simulation.gd -- \
  res://data/configs/battle_sim/scenarios/archer_pressure_example.tres \
  res://data/configs/battle_sim/profiles/baseline.tres \
  res://data/configs/battle_sim/profiles/pinning_shot_blocked.tres \
  res://data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres
```

AI vs AI 示例：

```bash
godot --headless --script tests/battle_runtime/run_battle_balance_simulation.gd -- \
  res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres \
  res://data/configs/battle_sim/profiles/baseline.tres
```

CLI 脚本参数规则：

- 第一个参数必须是 `BattleSimScenarioDef` 资源。
- 后续参数可以是任意数量的 `BattleSimProfileDef` 资源。
- 如果不传 profile，runner 会自动补一个 `baseline` profile。

运行成功后，CLI 会输出：

- `scenario`
- `profiles`
- `comparisons`
- `report_json`
- `traces_jsonl`

文件写入目录：

```text
user://simulation_reports/<scenario_id>/
```

## LLM 分析包导出

如果目标是继续做低 token 分析，或把结果交给 GPT Pro、Claude 之类的外部模型，不应该直接整包读取 full `report.json` 和 full `turn_traces.jsonl`。应该先导出紧凑分析包。

推荐命令：

```bash
python tools/build_battle_sim_analysis_packet.py --report <report.json> --include-baseline-traces
```

脚本会生成：

- `summary_for_llm.json`
- `focus_traces.jsonl`
- `analysis_brief.md`

其中：

- `summary_for_llm.json`
  - 现在会把每个 profile 的技能成功次数、技能尝试次数、技能失败次数一起写出，优先读这里，不要再去翻 full report。
- `analysis_brief.md`
  - 现在会直接展开 profile 级别的 top skill successes / attempts / failures，以及 comparison 里的对应 delta，适合先做人工速读。

默认输出目录：

```text
<report.json 同级目录>/<report_stem>_llm_packet/
```

推荐读取顺序：

1. `summary_for_llm.json`
2. `analysis_brief.md`
3. `focus_traces.jsonl`
4. 只有紧凑包不够时，才回到原始 `report.json` 或完整 `turn_traces.jsonl`

这样做的原因：

- 原始 `report.json` 已经内嵌 `ai_turn_traces`
- 如果再把完整 `turn_traces.jsonl` 一起喂给模型，通常会重复输入同一批 trace
- 大多数平衡或 AI 诊断先看 summary 和少量 focus trace 就够了

## 运行时执行流程

完整执行顺序如下：

1. 读取 scenario 资源。
2. 读取所有 profile 资源。
3. `BattleSimRunner` 为每个 profile 遍历 scenario 中的所有 seed。
4. 每次单场运行都会新建一个 `GameSession` 和一个 `BattleRuntimeModule`。
5. runner 从 `GameSession` 读取当前仓库注册的：
   - `skill_defs`
   - `enemy_ai_brains`
   - `enemy_templates`
6. `BattleSimOverrideApplier` 深拷贝技能和 AI brain 资源，再把 profile 的 patch 应用到拷贝上，避免污染原始资源。
7. runtime 使用被 patch 后的资源完成 `setup(...)`。
8. runtime 开启 AI trace，并设置本次运行使用的 `BattleAiScoreProfile`。
9. scenario 通过 `build_start_context()` 构造开战上下文，明确给出：
   - 友军单位
   - 敌军单位
   - 出生点
   - 地图大小
   - 地格定义
   - 时间轴参数
10. runtime 开始战斗。
11. 如果当前行动单位是手动单位，则 runner 按 `manual_policy` 发指令。
12. 如果当前行动单位是 AI，则 runtime 走正常 AI 决策链，并记录 turn trace。
13. 直到战斗结束、达到最大迭代数，或触发 idle guard。
14. runner 收集本场的：
   - 胜负结果
   - 最终 TU
   - 迭代数
   - 单位存活数
   - `metrics`
   - `ai_turn_traces`
   - `final_units`
15. `BattleSimReportBuilder` 生成 profile summary 与 baseline 对比。
16. runner 把完整 `report_json` 和扁平化的 `turn_trace_jsonl` 写到 `user://simulation_reports/...`。

## 场景定义

`BattleSimScenarioDef` 是“单组实验环境”的定义。它决定这次模拟跑什么地图、有哪些单位、按什么时间轴跑、要跑哪些随机 seed。

关键字段如下：

- `scenario_id`
  - 场景唯一标识，也会进入输出路径。
- `display_name`
  - 显示名。
- `description`
  - 文本说明。
- `map_size`
  - 这是 battle sim 场景资源自己的地图大小字段，不是 runtime battle start 的 legacy `map_size` 输入。手工平地布局会直接使用它；当开启正式地形生成时，`build_start_context()` 会把它转换成正式输入字段 `battle_map_size`。
- `terrain_profile_id`
  - 地形 profile 标识。
- `use_formal_terrain_generation`
  - 是否跳过模拟场景内置的平地 `cells` / 出生点拼装，改为复用正式 `BattleTerrainGenerator`。
- `world_coord`
  - 传给正式地形生成器的世界坐标；会参与 battle seed 计算。
- `ally_units`
  - 友军单位列表，元素是 `BattleSimUnitSpec`。
- `enemy_units`
  - 敌军单位列表，元素是 `BattleSimUnitSpec`。
- `cell_overrides`
  - 按格子覆盖地形、地势、地格效果。
- `tick_interval_seconds`
  - runtime 推进时每轮传入的 delta。
- `tu_per_tick`
  - 时间轴每 tick 增长值。
- 单位行动阈值
  - 写在每个 `BattleSimUnitSpec.action_threshold` / `BattleUnitState.action_threshold` 上；scenario 不再提供全局行动阈值。
- `max_iterations`
  - 单场最大循环次数。
- `manual_policy`
  - 当前只正式支持 `wait`。
- `seeds`
  - 用于重复实验的种子列表。

`build_start_context()` 会把 scenario 转成 runtime 真正使用的开战上下文，字段包括：

- 手工布局模式：
  - `battle_party`
  - `enemy_units`
  - `ally_spawns`
  - `enemy_spawns`
  - `map_size`
  - `cells`
  - `tick_interval_seconds`
  - `tu_per_tick`
  - `battle_terrain_profile`
- 正式地形生成模式：
  - `battle_party`
  - `enemy_units`
  - `battle_map_size`
  - `world_coord`
  - `tick_interval_seconds`
  - `tu_per_tick`
  - `battle_terrain_profile`

当 `use_formal_terrain_generation = true` 时，模拟不会再因为 `map_size` / `cells` / `ally_spawns` / `enemy_spawns` 命中 `BattleUnitFactory` 的手工地形回退路径，而是直接走正式 `BattleTerrainGenerator`。这适合做“模拟地图必须与正式战斗同尺寸、同峡谷生成逻辑”的 AI 对战。

### 单位定义

`BattleSimUnitSpec` 用于声明单个参战单位。它的职责不是“引用一个模板并自动生成全部内容”，而是“把模拟需要的单位状态显式写出来”。

常用字段：

- `unit_id`
- `source_member_id`
- `display_name`
- `faction_id`
- `control_mode`
- `ai_brain_id`
- `ai_state_id`
- `coord`
- `body_size`
- `current_hp`
- `current_mp`
- `current_stamina`
- `current_aura`
- `current_ap`
- `attribute_overrides`
- `skill_ids`
- `skill_level_map`
- `movement_tags`
- `status_effects`

设计意图：

- 如果是玩家侧“木桩”或测试单位，可以只给基础属性，不挂复杂技能。
- 如果是 AI 单位，可以显式指定 `ai_brain_id`、初始 `ai_state_id` 和技能集合。
- 这能把模拟场景控制在最小可解释输入上，而不是依赖世界外部状态。

### 地格覆盖

`cell_overrides` 支持覆盖默认生成的地格。

每条 override 可用字段：

- `coord`
- `base_terrain`
- `base_height`
- `height_offset`
- `flow_direction`
- `terrain_effect_ids`
- `prop_ids`

用途：

- 构造高地优势测试。
- 构造带地格效果的区域。
- 构造狭窄通道、阻隔、河流或危险区。

## Profile 定义

`BattleSimProfileDef` 表示“一组可对照的实验配置”。它本身不定义战斗场景，而是定义：

- 本轮要用什么 AI 评分权重。
- 本轮要 patch 哪些技能、brain、action 或 score profile。

关键字段：

- `profile_id`
- `display_name`
- `description`
- `ai_score_profile`
- `override_patches`

一条 profile 可以同时改：

- 技能数值
- 敌人 brain 参数
- 敌人某个 action 的字段
- AI 评分权重

这意味着：

- “技能变强了但 AI 不会用” 和 “技能没变但 AI 更爱用” 这两个问题可以分开验证。
- “行为更保守” 可以通过 brain/action 参数做，也可以通过评分权重做。

## Patch 机制

`BattleSimOverrideApplier` 负责把 profile 写成的 patch 应用到本次运行的资源副本中。

支持的 `target_type`：

- `skill`
- `brain`
- `action`
- `ai_score_profile`

### 技能 patch

示例：

```text
target_type = "skill"
target_id = "archer_pinning_shot"
path = "combat_profile.stamina_cost"
value = 999
```

适合改：

- `combat_profile.ap_cost`
- `combat_profile.mp_cost`
- `combat_profile.stamina_cost`
- `combat_profile.aura_cost`
- `combat_profile.cooldown_tu`
- `combat_profile.range_value`
- `combat_profile.effect_defs.0.power`

### Brain patch

示例：

```text
target_type = "brain"
target_id = "ranged_suppressor"
path = "retreat_hp_ratio"
value = 0.6
```

适合改：

- `retreat_hp_ratio`
- `support_hp_ratio`
- `pressure_distance`
- `default_state_id`

### Action patch

示例：

```text
target_type = "action"
brain_id = "ranged_suppressor"
state_id = "pressure"
action_id = "harrier_keep_range"
path = "desired_min_distance"
value = 5
```

常见可改字段：

- `desired_min_distance`
- `desired_max_distance`
- `minimum_safe_distance`
- `score_bucket_id`
- `target_selector`
- 动作脚本自身暴露的其他 `@export` 字段

### AI 评分 patch

示例：

```text
target_type = "ai_score_profile"
path = "movement_cost_weight"
value = 6
```

也可以直接在 `ai_score_profile` 子资源里写默认值，不一定要走 patch。

### Path 规则

patch 的 `path` 使用点号路径：

- `combat_profile.stamina_cost`
- `effect_defs.0.power`
- `action_base_scores.move`
- `bucket_priorities.harrier_pressure`

当前 path 解析支持：

- Resource 字段
- Dictionary 字段
- Array 下标

当前值类型会被尽量按原字段类型转换：

- `StringName`
- `Vector2i`
- `int`
- `float`
- `bool`

## AI 行动逻辑是怎么被优化的

这套系统支持两层 AI 优化。

第一层是“配置级优化”：

- 改 skill 资源。
- 改 brain 资源。
- 改具体 action 参数。
- 改 `BattleAiScoreProfile`。

这层适合快速试验，不需要改代码。

第二层是“代码级优化”：

- 改 `BattleAiService`
- 改 `BattleAiScoreService`
- 改 `EnemyAiAction`
- 改 `use_unit_skill_action.gd`
- 改 `move_to_range_action.gd`
- 改 `retreat_action.gd`
- 改 `wait_action.gd`
- 改其他具体 action

这层适合现有行为模型本身不够用的时候，比如：

- 需要新的目标选择逻辑。
- 需要新的站位目标函数。
- 需要新的状态切换规则。
- 需要更复杂的技能候选枚举。

## AI 状态切换逻辑

当前 `BattleAiService` 的状态分流大致如下：

- 如果存在 `retreat` 状态，且当前生命比例 `<= retreat_hp_ratio`，进入 `retreat`。
- 否则如果存在 `support` 状态，且满足支援窗口，进入 `support`。
- 否则寻找最近敌人。
- 如果存在 `pressure` 状态，且最近敌人距离 `<= pressure_distance`，进入 `pressure`。
- 如果当前已经在 `pressure`，且距离没有超出 `pressure_distance + 1`，继续维持 `pressure`。
- 否则如果存在 `engage`，进入 `engage`。
- 都不满足时，回到当前或默认状态。

这意味着：

- `brain` 层参数会直接影响进入什么状态。
- 同一个状态里具体做什么，由 state 下 action 列表和评分共同决定。

## AI 评分系统

`BattleAiScoreProfile` 是本系统里最核心的 AI 行为偏好参数集。它不定义“有没有这个动作”，它定义“候选动作之间怎么选”。

### 评分字段

当前可用权重大致分为四类。

伤害/收益相关：

- `damage_weight`
- `heal_weight`
- `status_weight`
- `terrain_weight`
- `height_weight`
- `target_count_weight`

资源成本相关：

- `ap_cost_weight`
- `mp_cost_weight`
- `stamina_cost_weight`
- `aura_cost_weight`
- `cooldown_weight`
- `movement_cost_weight`

站位目标相关：

- `position_base_score`
- `position_distance_step`
- `position_undershoot_penalty`
- `position_overshoot_penalty`

动作优先级相关：

- `action_base_scores.skill`
- `action_base_scores.move`
- `action_base_scores.retreat`
- `action_base_scores.wait`
- `default_bucket_priority`
- `bucket_priorities.<bucket_id>`

### 技能评分公式

技能类动作的总分当前按这个结构计算：

```text
total_score =
  action_base_score
  + hit_payoff_score
  + target_count * target_count_weight
  - resource_cost_score
  + position_objective_score
```

其中：

- `hit_payoff_score`
  - 来自预估伤害、治疗、状态、地格效果、高低差，并乘上命中率。
- `resource_cost_score`
  - 来自 AP、MP、Stamina、Aura、Cooldown 的加权和。
- `position_objective_score`
  - 来自当前动作对期望距离带的满足程度。

### 非技能动作评分公式

`move`、`retreat`、`wait` 这类动作的总分当前按这个结构计算：

```text
total_score =
  action_base_score
  + position_objective_score
  + target_count * metadata.target_count_weight
  - move_cost * movement_cost_weight
```

这里的重点是：

- AI 现在不会只给技能算分。
- 走位、撤退、等待也进入统一的比较流程。

### 位置目标

当前位置目标主要有几种：

- `cast_distance`
  - 更偏向把施法/攻击距离落在期望区间内。
- `distance_band`
  - 更偏向和目标单位保持某段距离。
- `distance_floor`
  - 常用于撤退，达到最小安全距离后还会继续有正向收益。
- `none`
  - 不计位置分。

### 候选动作比较顺序

当多个动作都有评分时，当前比较顺序是：

1. `score_bucket_priority` 高者优先。
2. `total_score` 高者优先。
3. `hit_payoff_score` 高者优先。
4. `target_count` 高者优先。
5. `position_objective_score` 高者优先。
6. `resource_cost_score` 低者优先。
7. 如果以上都一样，则按 action 列表中的先后顺序。

这意味着：

- bucket priority 仍然重要，但不再是“先命中某 bucket 就直接结束”。
- 当前系统会把所有已评分候选放到同一比较平面上。

## Score Input 结构

每个最终被选中的动作，都会产出一个 `score_input` 摘要。这个结构是后续分析 AI 决策最重要的中间件。

字段包括：

- `action_kind`
- `action_label`
- `score_bucket_id`
- `score_bucket_priority`
- `command_type`
- `skill_id`
- `primary_coord`
- `target_unit_ids`
- `target_coords`
- `target_count`
- `estimated_damage`
- `estimated_healing`
- `estimated_status_count`
- `estimated_terrain_effect_count`
- `estimated_height_delta`
- `estimated_hit_rate_percent`
- `hit_payoff_score`
- `ap_cost`
- `mp_cost`
- `stamina_cost`
- `aura_cost`
- `cooldown_tu`
- `resource_cost_score`
- `move_cost`
- `position_objective_kind`
- `desired_min_distance`
- `desired_max_distance`
- `position_anchor_coord`
- `distance_to_primary_coord`
- `position_objective_score`
- `total_score`

读这个结构时建议这样理解：

- 看 `action_kind` 和 `skill_id`
  - 确认它到底是技能、移动、撤退还是等待。
- 看 `score_bucket_priority` 和 `total_score`
  - 确认它为什么赢了别的候选。
- 看 `resource_cost_score`
  - 确认是收益压过成本，还是成本压过收益。
- 看 `position_objective_*`
  - 确认它是不是因为站位目标而被选中。

## Turn Trace 结构

`turn_trace_jsonl` 里每一行表示一次 AI 单位回合的最终决策快照。

顶层字段包括：

- `scenario_id`
- `profile_id`
- `seed`
- `battle_id`
- `turn_started_tu`
- `unit_id`
- `unit_name`
- `faction_id`
- `brain_id`
- `state_id`
- `action_id`
- `reason_text`
- `command`
- `score_input`
- `action_traces`

### action_traces 字段

`action_traces` 记录该回合中每个 action 的评估过程。每条 action trace 至少包含：

- `trace_id`
- `action_id`
- `score_bucket_id`
- `metadata`
- `evaluation_count`
- `blocked_count`
- `preview_reject_count`
- `candidate_count`
- `block_reasons`
- `top_candidates`
- `chosen`

如果这个 action 找到了最佳候选，还会带上：

- `best_reason_text`
- `best_command`
- `best_score_input`

如果最终整轮决策把它选中，还会在回合结束后带上：

- `chosen_reason_text`
- `chosen_command`
- `chosen_score_input`

### block_reasons 的意义

`block_reasons` 不是一个固定枚举表，而是各具体 action 在评估时上报的阻断原因计数。常见用途：

- 看某技能是否总被冷却挡住。
- 看某动作是否总被预览非法挡住。
- 看目标选择是否经常找不到合法目标。

### top_candidates 的意义

`top_candidates` 记录每个 action 内部最强的少量候选摘要，当前最多保留 5 个。它适合回答这类问题：

- 为什么没选技能 A 的另外一个目标？
- 这个 action 内部是否其实有更高伤害方案，但因为资源或位置分被压掉？
- 同一个技能的不同变体，谁在 action 内部更优？

## Metrics 结构

runtime 会在每场战斗中维护 `_battle_metrics`，最终进入 `run_result.metrics`。

顶层字段：

- `battle_id`
- `seed`
- `units`
- `factions`

### units.<unit_id>

每个单位当前会累计这些指标：

- `unit_id`
- `display_name`
- `faction_id`
- `control_mode`
- `source_member_id`
- `turn_count`
- `action_counts`
- `skill_attempt_counts`
- `skill_success_counts`
- `successful_skill_count`
- `total_damage_done`
- `total_healing_done`
- `total_damage_taken`
- `total_healing_received`
- `kill_count`
- `death_count`

### factions.<faction_id>

每个阵营当前会累计这些指标：

- `faction_id`
- `unit_count`
- `turn_count`
- `action_counts`
- `skill_attempt_counts`
- `skill_success_counts`
- `successful_skill_count`
- `total_damage_done`
- `total_healing_done`
- `total_damage_taken`
- `total_healing_received`
- `kill_count`
- `death_count`

这些指标适合做：

- 总体输出与承伤分析。
- AI 是否过于依赖某单一技能。
- 某 profile 是否让撤退/等待变多。
- 某阵营是否因为策略变化导致击杀效率下降。

## Run Result 结构

`report_json` 中每条 run 当前至少包含：

- `scenario_id`
- `profile_id`
- `seed`
- `battle_id`
- `battle_ended`
- `winner_faction_id`
- `final_tu`
- `iterations`
- `idle_loops`
- `ally_alive`
- `enemy_alive`
- `metrics`
- `ai_turn_traces`
- `final_units`

适合的解读方式：

- `battle_ended == false`
  - 常表示达到迭代上限或触发 idle guard，不能直接把它当正常平衡结果。
- `idle_loops` 偏高
  - 常表示行动链停滞、站位无法推进，或行为策略互相抵消。
- `final_units`
  - 适合回看战斗结束时的单位状态，而不是只看胜负。

## Profile Summary 结构

`BattleSimReportBuilder` 会对每个 profile 生成 summary。

当前字段：

- `profile_id`
- `display_name`
- `run_count`
- `wins_by_faction`
- `win_rate_by_faction`
- `average_final_tu`
- `average_iterations`
- `skill_usage_totals`
- `action_choice_counts`
- `faction_metric_totals`

### 这些字段分别适合回答什么问题

`wins_by_faction` 与 `win_rate_by_faction`

- 哪边赢得更多。
- 改完 profile 后整体胜率是升是降。

`average_final_tu`

- 战斗是变快了还是拖长了。
- 更激进的 AI 往往会把这个值压低。

`average_iterations`

- 用于识别模拟推进是否更卡、更绕、或更容易停滞。

`skill_usage_totals`

- 最适合做技能数值分析。
- 如果一个技能被极度偏用，通常要继续看：
  - 它是否总能打出最高 `total_score`
  - 它是否资源成本太低
  - 它是否状态收益被高估

`action_choice_counts`

- 最适合做 AI 行为倾向分析。
- 看移动、等待、撤退、某具体技能动作是否偏多。

`faction_metric_totals`

- 最适合做总体战斗表现分析。
- 可以看输出、治疗、击杀、死亡的整体方向有没有偏。

## Comparisons 结构

如果传入多个 profile，`comparisons[]` 会把第一个 profile 当 baseline，其余 profile 依次与它做差值。

当前字段：

- `baseline_profile_id`
- `candidate_profile_id`
- `average_final_tu_delta`
- `average_iterations_delta`
- `win_rate_delta`
- `skill_usage_delta`
- `action_choice_delta`

注意：

- baseline 永远是 `profile_entries[0]`。
- 如果要做正式实验，建议把真正的对照组放在参数列表第一位。

## 如何用输出做数值分析

如果目标是分析技能数值，推荐按这个顺序看：

1. 看 `comparisons[].skill_usage_delta`
   - 确认技能使用量变化。
2. 看 `win_rate_delta`
   - 确认技能改动是否真的影响强度，而不只是影响偏好。
3. 看 `faction_metric_totals`
   - 确认伤害、治疗、击杀有没有同步变化。
4. 下钻 `turn_trace_jsonl`
   - 看 AI 选择该技能时，究竟是：
     - `hit_payoff_score` 太高
     - `resource_cost_score` 太低
     - `position_objective_score` 太占优
     - 还是 bucket priority 过高

典型问题判断方式：

- 使用率高，但胜率和输出没有明显提升。
  - 可能是技能“被偏爱但不一定强”，优先查评分逻辑。
- 使用率高，胜率和总伤害也明显上升。
  - 更可能是技能数值本身过强。
- 使用率下降，但战斗时间变长。
  - 可能是削弱后 AI 缺少合理替代动作。

## 如何用输出做 AI 逻辑分析

如果目标是分析敌人 AI 行动逻辑，推荐按这个顺序看：

1. 看 `action_choice_counts`
   - 先确认“行为倾向”有没有变。
2. 看 `average_final_tu` 和 `average_iterations`
   - 确认策略变化是不是让战斗拖慢。
3. 看 `turn_trace_jsonl`
   - 看每轮候选动作和阻断原因。
4. 看具体 `score_input`
   - 看它为什么宁可移动也不用技能，或为什么宁可等待也不撤退。

典型问题判断方式：

- `wait` 选择显著变多。
  - 常见原因是：
    - 技能成本过高。
    - 走位位置分太差。
    - `wait` 基础分不够低。
- `retreat` 过少。
  - 常见原因是：
    - `retreat_hp_ratio` 太低。
    - `distance_floor` 目标分不足。
    - `retreat` 的 `action_base_scores.retreat` 太低。
- `move` 过多但收益不高。
  - 常见原因是：
    - `movement_cost_weight` 太低。
    - 某动作的目标距离带太苛刻，导致频繁修正站位。

## 推荐实验方法

推荐使用 A/B 或 baseline/candidate 对照法。

建议流程：

1. 固定一个 scenario，不要同时改地图和单位。
2. 第一组永远保留 baseline。
3. 每轮只改一类因素。
   - 只改技能。
   - 或只改 brain/action。
   - 或只改 score profile。
4. 先看 summary 与 comparisons。
5. 再看 trace 下钻具体原因。
6. 如果变化方向不清晰，再增加 seed 数量。

为什么这么做：

- 可以把“AI 更爱用某技能”和“某技能真的更强”拆开。
- 可以避免多因素同时变化导致结论不可归因。

种子数量建议：

- 如果只是验证方向是否明显反转，少量 seed 可以先做烟雾检查。
- 如果要把某个 `*_delta` 当成正式结论，单个 profile 建议至少跑 `20+` seeds。
- 如果差值量级和 seed 噪声接近，不要把本轮结果直接当稳定结论。

## Repo Skill

仓内已经提供专用分析 skill：

- `.codex/skills/battle-sim-analysis`

它会把 battle simulation 的分析顺序固定成：

1. 先读上下文图与本说明。
2. 先导出 `summary_for_llm.json`、`focus_traces.jsonl`、`analysis_brief.md`。
3. 先看 summary，再看 focus trace。
4. 最后才回到 skill / brain / action / score 资源与代码。

如果后续由其他 agent 或模型接手 battle simulation 分析，应该优先按这个 skill 的顺序执行，而不是直接加载全量输出。

## 分析护栏

下面这些点在分析时必须一直记住：

1. `manual_policy` 目前只正式支持 `wait`。
   - 玩家侧单位在 simulation 里本质上是木桩。
   - 这套系统适合测 AI 自己的决策偏好，不适合拿来验证“AI 对抗智能玩家”的真实性能。

2. baseline 默认取 `profile_entries[0]`。
   - CLI 参数顺序写错，整组 comparison 的方向就会反。
   - 脚本化批跑时，建议把 baseline 的 `profile_id` 命名成 `00_baseline_*`，降低误用概率。

3. `battle_ended == false` 的 run 要单独过滤。
   - 这类 run 通常表示达到 `max_iterations` 或触发 idle guard。
   - 除非你就是在分析停滞行为，否则不要把它们直接混入正常胜率结论。

4. `estimated_*` 是 AI 预估值，不是实打结果。
   - `score_input.estimated_damage`、`estimated_hit_rate_percent` 等字段描述的是 AI 选择时看到的价值模型。
   - 如果预估模型和真实战斗结果存在偏差，就会出现“技能使用率高，但总输出和胜率不上升”的情况。

5. seed 数量不足时，不要过度解释小差值。
   - 想看显著差异，单个 profile 建议至少 `20+` seeds。
   - 如果 `*_delta` 很小，而 seed 数又少，这更像待验证信号，不是稳定结论。

6. `top_candidates` 只保留每个 action 的前 5 个候选。
   - 在多目标、多格子、高密度场景里，它是截断后的摘要，不是完整候选全集。
   - 下钻单回合决策时，必须意识到 trace 里可能看不到所有落选方案。

7. 想验证整场对战表现时，优先用 AI vs AI 场景。
   - `manual_policy=wait` 适合低噪声动作偏好测试。
   - `control_mode=ai` 的 ally/enemy 双边对战更适合验证真实对局结果。

## 推荐给外部模型的输入包

如果要让 GPT Pro、Claude 或其他模型分析，优先给它们以下材料：

- 本文档。
- 目标 scenario 资源。
- 参与对比的 profile 资源。
- 一份 `summary_for_llm.json`。
- 一份 `analysis_brief.md`。
- 需要时再补 `focus_traces.jsonl`。
- 如果问题聚焦在某个敌人脑上，再附：
  - 对应 `brain` 资源
  - 对应 `skill` 资源

如果问题聚焦在某段 AI 行动异常，建议额外附上：

- 异常 run 的单个 `seed`
- 该 run 中相关单位的若干条 focus trace

## 推荐给外部模型的分析任务模板

可以直接把下面这段任务描述发给外部模型：

```text
你正在分析一个 Godot 战斗模拟系统的结构化输出。

请基于 battle_balance_simulation.md 的说明，阅读我提供的：
- scenario
- profile
- summary_for_llm.json
- analysis_brief.md
- 需要时再看 focus_traces.jsonl

然后回答：
1. 这组 profile 相对 baseline 的主要行为变化是什么。
2. 变化更像是技能数值问题、AI 行为参数问题，还是 AI 评分问题。
3. 给出最多 3 个最值得继续验证的改动点。
4. 每个改动点都要说明它影响的字段、预期现象、以及应该重点观察 summary_for_llm.json 还是 focus_traces.jsonl 的哪些字段。

不要泛泛而谈，要基于字段做判断。
```

## 已知限制

当前系统有这些明确限制：

- `manual_policy` 目前只正式支持 `wait`。如需 AI vs AI 整场对战，请把 ally 单位的 `control_mode` 设为 `&"ai"` 并填写 `ai_brain_id` / `ai_state_id`，决策会直接走 `BattleAiService`，不再经过 `manual_policy` 分支。参见 `res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres`。
- 没有内建图表或可视化 dashboard。
- `top_candidates` 当前每个 action 最多保留 5 个。
- baseline 对比默认取第一个 profile。
- 单场运行有 `max_iterations` 和 `MAX_IDLE_LOOPS` 双重保护。
- `battle_ended == false` 的 run 需要单独识别，不能直接纳入“正常胜率”解释。

这些都不影响数值分析和 AI 调参，但会影响结论的表达方式。

## 当前结论

当前仓库里的这套系统已经具备完整闭环：

- 能构造明确场景。
- 能批量跑多 seed、多 profile。
- 能 patch 技能、AI 脑、具体动作和评分权重。
- 能记录技能、移动、撤退、等待的统一评分结果。
- 能保留候选动作 trace 和阻断原因。
- 能输出适合继续做自动分析的结构化结果。

因此后续不管是我自己迭代，还是让 GPT Pro、Claude 参与分析，都已经有足够的输入基础，不需要再先补一套新的模拟框架。
