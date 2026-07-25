# 战斗模拟、数值分析与 AI 调参系统说明

> 状态：`Current / Implemented`
> 核对日期：`2026-07-25`

## 关联上下文单元

- CU-15：战斗运行时总编排
- CU-16：战斗状态模型、边规则、伤害、AI 规则层
- CU-19：自动化回归与截图辅助

当前实现边界以 [`project_context_units.md`](../project_context_units.md) 为准；本文记录 battle simulation / balance analysis 的使用说明与数据规范。

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

- 场景 authoring/import 资源：`res://scripts/systems/battle/sim/BattleSimScenarioDef.cs`
- 单位 authoring/import 资源：`res://scripts/systems/battle/sim/BattleSimUnitSpec.cs`
- 不可变场景运行定义：`res://scripts/systems/battle/sim/BattleSimScenarioDefinition.cs`
- 不可变单位运行定义：`res://scripts/systems/battle/sim/BattleSimUnitDefinition.cs`
- 正式角色 fixture：`res://scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`
- profile 定义：`res://scripts/systems/battle/sim/BattleSimProfileDef.cs`
- patch 应用：`res://scripts/systems/battle/sim/BattleSimOverrideApplier.cs`
- 汇总报表：`res://scripts/systems/battle/sim/BattleSimReportBuilder.cs`
- Trace 精简报表：`res://scripts/systems/battle/sim/BattleSimTraceSummaryBuilder.cs`
- 批量执行器：`res://scripts/systems/battle/sim/BattleSimRunner.cs`
- 基础执行循环：`res://scripts/systems/battle/sim/BattleSimExecutionLoop.cs`
- CLI 入口：`res://tests/battle_runtime/simulation/run_battle_balance_simulation.cs`
- LLM 分析包导出：`tools/build_battle_sim_analysis_packet.py`
- Repo 内分析 skill：`.codex/skills/battle-sim-analysis`

模拟依赖的运行时链路：

- `BattleRuntimeModule`
- `BattleAiService`
- `BattleAiScoreService`
- `BattleAiContext`
- `EnemyAiAction` 及具体 action 脚本

## 生命周期边界

`BattleSimScenarioDef` 与 `BattleSimUnitSpec` 是同步 authoring/import `Resource`。它们只在加载 `.tres`、解析导出字段、校验输入并执行投影时存在于入口边界；入口必须在同一同步调用链内调用 `ToDefinition()`，不能把这两个 authored `Resource` 保存在 runner、执行循环、报告或文件输出对象中。

`BattleSimScenarioDef.ToDefinition()` 会把场景字段、seed、地格快照和双方单位一次性投影为不可变的 plain `BattleSimScenarioDefinition`。其中每个 authored `BattleSimUnitSpec` 会通过 `ToDefinition()` 变成 `BattleSimUnitDefinition`；单位定义保存深拷贝后的 canonical plain 快照，并私有保存不进入 canonical codec 的规范化装备能力投影种子。每次运行先重建一份新的可变 `BattleUnitState`，再把该种子原子安装到新单位。因此：

- `ToDefinition()` 返回后再修改 authored scenario/unit `Resource`，不会改变已生成的运行定义。
- 不同 run 从同一 `BattleSimUnitDefinition` 重建的 `BattleUnitState` 不共享可变状态；装备能力 source 与 runtime-only temporal modifier 也由 definition 防御复制，不借用来源单位或另一局的集合。
- 当前 `.tres` 的 `BattleSimUnitSpec` schema 不生成装备能力 source 或 temporal modifier，因此 authored scenario 的 seed 通常为空；这条合同保证的是 plain/programmatic 入口已经持有的 normal runtime projection 不会再被 definition clone 或普通 Runner 的开战交接剥离，不代表 authored scenario 或未注入完整 projection catalog 的 formal benchmark 会产生非空 temporal 内容。
- `BattleSimRunner`、`BattleSimExecutionLoop`、`BattleSimScenarioReport`、`BattleSimReportProjection`、`BattleSimFilePayloadProjection` 与 `BattleSimTraceSummaryBuilder` 只消费 `BattleSimScenarioDefinition` / `BattleSimUnitDefinition` 及其他 plain projection，不持有原始 scenario/unit `Resource`。
- 普通 `BattleSimRunner` 为每局创建 fresh ally/enemy `BattleUnitState`，经一次性 `BattleStartUnitRoster` 把所有权交给 `BattleRuntimeModule`；对应 start context 只携带地形、出生点和选项，不再重复携带 `battle_party` / `enemy_units` canonical payload。runtime 会拒绝同一阵营同时由 typed roster 与 context 提供，避免双重真相。
- `BattleSimFormalCombatFixture` 从同一 runtime/context 构造 fresh hostile units，并由 `BattleSimFormalRuntimeStartInput` 把 caller-owned context lease 与 enemy-only typed roster 绑定为同一次同步 start 输入；hostile 不再经过 canonical `enemy_units` 往返，ally 仍由 party gateway 和 `ally_member_ids` 构造。这样会保留 factory 已经生成的 equipment source 与 runtime-only temporal projection。
- 其他同步 schema/序列化调用所需的 Godot `Dictionary` 仍只在请求边界临时投影，并由对应的 `GodotProjectionLease` 在使用后释放；它们不是长期运行状态，也不是普通模拟内部的单位交接通道。

从路径加载的 scenario 资源由 `ResourceLoader` 缓存管理。CLI 或 benchmark 只需要加载它、立即调用 `ToDefinition()`，然后丢弃局部引用；benchmark 不拥有这个 path-backed `Resource`，不得手工调用 `Dispose()` 或 `Free()`。benchmark 仍应显式释放自己创建并拥有的 fixture、runtime service、文件 scope 和 projection lease。

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
godot --headless --script tests/battle_runtime/simulation/run_battle_balance_simulation.cs -- \
  res://data/configs/battle_sim/scenarios/archer_pressure_example.tres \
  res://data/configs/battle_sim/profiles/baseline.tres \
  res://data/configs/battle_sim/profiles/pinning_shot_blocked.tres \
  res://data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres
```

AI vs AI 示例：

```bash
godot --headless --script tests/battle_runtime/simulation/run_battle_balance_simulation.cs -- \
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
- `runs`
- `completed`
- `unfinished`
- `report_json`
- `traces_jsonl`

只有所有 runs 都以 `battle_ended` 正常结束时 CLI 才返回 `0`。存在 idle stall、迭代预算耗尽或 invalid runtime 时，CLI 仍先写出诊断报告，再返回 `2`；这类报告不能发布为正式平衡结论。

文件写入目录：

```text
user://simulation_reports/<scenario_id>/
```

每次 `RunScenario()` 使用 `<unix_seconds>_<guid>` 作为独立批次标识，因此同一秒、同一场景及并发进程不会复用输出文件名。`BattleSimReportFileWriter` 先写 trace 与可选 trace summary，最后写主 report 作为本批次成功标志；`FileAccess.Open`、`StoreString` / `StoreLine` 或 flush 任一失败都会向上传播，并清理本批次已生成的残缺文件。只有完整产物集确认存在后，runner 才发布 `OutputFiles` 并打印 `report-written`。

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
  - 会从每个 profile 的 `battle_ended=true` runs 重新计算正常均值、胜率、技能、action、faction metrics 与 comparison；不会信任可能被未完成局污染的原始顶层 aggregate。
  - 如果旧报告缺少足够的逐局事实，相关字段会置空并列入 `unavailable_completed_only_metrics`，而不是回退使用不可靠 aggregate。
- `analysis_brief.md`
  - 会直接展开 completed-only profile 级别的 top skill successes / attempts / failures，以及 comparison 里的对应 delta，适合先做人工速读。
- `focus_traces.jsonl`
  - 只导出正常结束 runs 的 trace；未完成 run 的事实仍留在原始报告中用于卡死诊断。

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

`BattleSimRunner` 与专项分析脚本开启 AI trace 时，应同时保留完整 trace 和精简 trace summary：

- 完整报告：保留 `ai_turn_traces`，用于必要时全量复盘。
- 精简报告：由 `BattleSimTraceSummaryBuilder` 生成，路径通常是完整报告同名 `_trace_summary.json`，用于快速判断 wait、资源阻断、候选评分和 focus 阵营回合。
- 专项脚本如 `RunMixed6v12MirrorAnalysis.cs` 可用 `TRACE_AI=1` 打开 trace；默认会自动写出同目录 `_trace_summary.json`，也可用 `TRACE_SUMMARY_FILE` 指定路径。

## 运行时执行流程

完整执行顺序如下：

1. 入口同步读取 `BattleSimScenarioDef` authoring 资源，并立即调用 `ToDefinition()`；其中的 `BattleSimUnitSpec` 同步投影为 plain `BattleSimUnitDefinition`。
2. 读取所有 profile 资源，并在交给 runner 前投影为 typed definition。
3. 从此处开始，`BattleSimRunner` 只持有 immutable plain `BattleSimScenarioDefinition`，并为每个 profile 遍历其中的所有 seed。
4. 入口把进程级 `ContentSnapshot` 绑定到 `BattleSimContentProvider`；每次单场运行只新建独立的 `BattleRuntimeModule`，不会为每个 seed 新建或保留 `GameSession`。
5. runner 从 `BattleSimContentProvider` 的 typed process snapshot 读取当前仓库注册的：
   - `skill_defs`
   - `enemy_ai_brains`
   - `enemy_templates`
6. `BattleSimOverrideApplier` 深拷贝技能和 AI brain 资源，再把 profile 的 patch 应用到拷贝上，避免污染原始资源。
7. runtime 使用被 patch 后的资源完成 `setup(...)`。
8. runtime 开启 AI trace，并设置本次运行使用的 `BattleAiScoreProfile`。
9. 普通 `BattleSimRunner` 通过 `BuildRuntimeStartContextLease()` 临时投影不含单位 payload 的开战上下文，并通过 `CreateRuntimeRosterTyped()` 为本局创建一次性 typed roster，明确给出：
   - fresh 友军单位
   - fresh 敌军单位
   - 出生点
   - 地图大小
   - 地格定义
   - runtime 开战所需的 `tu_per_tick`；执行循环直接从 plain definition 读取 `timeline_ticks_per_step`
10. runtime 开始战斗，并把返回的 state 交给执行循环。
11. `BattleSimExecutionLoop` 先验证传入 state 与 `runtime.GetState()` 是同一引用。启动失败时 runtime 已清空正式 state，因此循环不会执行，并直接返回 `invalid_runtime`、0 iterations、0 idle loops；runner 只在收到 `invalid_runtime` 后读取 `BattleStartFailureSnapshot`，供失败 run 的结构化诊断使用。只有验证成功的 runtime-owned state 才进入单场基础推进。
12. 如果当前行动单位是手动单位，则按 `manual_policy` 发指令。
13. 如果当前行动单位是 AI，则 runtime 走正常 AI 决策链，并记录 turn trace。
14. 如果时间轴已有 `ready_unit_ids`，执行循环会先用 `advance(0)` drain 当前 ready 队列；只有 ready 队列为空时才按 `timeline_ticks_per_step / tu_per_tick` 推进下一段 TU。
15. 直到战斗结束、达到最大迭代数，或触发 idle guard。
16. runner 收集本场的：
   - 胜负结果
   - 最终 TU
   - 迭代数
   - 单位存活数
   - `metrics`
   - `ai_turn_traces`
   - `final_units`
17. `BattleSimReportBuilder` 生成 profile summary 与 baseline 对比；report 保存 plain scenario definition，不回挂 authored scenario `Resource`。
18. runner 委托 `BattleSimReportFileWriter` 把完整 `report_json` 和扁平化的 `turn_trace_jsonl` 写到 `user://simulation_reports/...`；report/file/trace projection 只在写出时临时创建并释放 Godot wrapper。如果完整 report 中存在 `ai_turn_traces`，writer 还会用 `BattleSimTraceSummaryBuilder` 同步写出 `trace_summary_json`。完整产物集成功前不发布 output paths。

## 场景定义

`BattleSimScenarioDef` 是“单组实验环境”的 authoring/import 定义。它决定这次模拟跑什么地图、有哪些单位、按什么时间轴跑、要跑哪些随机 seed，但不会越过同步 `ToDefinition()` 边界进入 runtime。runner 实际消费的是不可变 plain `BattleSimScenarioDefinition`。

关键字段如下：

- `scenario_id`
  - 场景唯一标识，也会进入输出路径。
- `display_name`
  - 显示名。
- `description`
  - 文本说明。
- `map_size`
  - 这是 battle sim 场景资源自己的地图大小字段，不是 runtime battle start 的 legacy `map_size` 输入。手工平地布局会直接使用它；当开启正式地形生成时，`BattleSimScenarioDefinition.BuildRuntimeStartContextLease()` 会把它转换成正式输入字段 `battle_map_size`。
- `terrain_profile_id`
  - 地形 profile 标识。
- `use_formal_terrain_generation`
  - 是否跳过模拟场景内置的平地 `cells` / 出生点拼装，改为复用正式 `BattleTerrainGenerator`。
- `world_coord`
  - 传给正式地形生成器的世界坐标；会参与 battle seed 计算。
- `ally_units`
  - 显式模拟单位列表，元素是 `BattleSimUnitSpec`；正式角色 fixture 场景应保持为空，由入口脚本通过 `BattleSimFormalCombatFixture` 生成。
- `enemy_units`
  - 显式模拟单位列表，元素是 `BattleSimUnitSpec`；正式角色 fixture 场景应保持为空，由入口脚本通过 `BattleSimFormalCombatFixture` 生成。
- `cell_overrides`
  - 按格子覆盖地形、地势、地格效果。
- `timeline_ticks_per_step`
  - runtime 推进时每轮传入的整数 tick 数。
- `tu_per_tick`
  - 时间轴每 tick 增长值。
- 单位行动阈值
  - scenario 不提供全局行动阈值。显式 `BattleSimUnitSpec` 夹具可在单位上声明；正式角色 fixture 场景通过建卡 payload 写入 `action_threshold`，再由 `AttributeService` 快照投影到 `BattleUnitState.action_threshold`。
- `max_iterations`
  - 单场最大循环次数。
- `manual_policy`
  - 当前只正式支持 `wait`。
- `seeds`
  - 用于改变和复现正式地形生成的种子列表；另有显式 fixture seed 接线时，也可决定对应的建卡等实验输入。
  - battle seed 不驱动命中、伤害、豁免、随机目标、装备能力随机分支等战斗内掷骰。它们必须继续使用彼此独立的 `TrueRandomSeedService` 随机结果。
  - 因此相同 battle seed 只能复现地形及显式 seed-derived fixture 输入，不能复现完整战斗过程或胜负结果。

普通 `BattleSimRunner` 使用 `BattleSimScenarioDefinition.BuildRuntimeStartContextLease()`，把 plain scenario definition 临时投影为不含单位 payload 的开战上下文；调用方在本次单场运行的请求作用域结束时释放 lease。单位由同一 scenario 的 `CreateRuntimeRosterTyped()` 独立重建并一次性交给 runtime，不能再向该 context 加入 `battle_party` / `enemy_units`。context 字段包括：

- 手工布局模式：
  - `ally_spawns`
  - `enemy_spawns`
  - `map_size`
  - `cells`
  - `world_coord`
  - `tu_per_tick`
  - `battle_terrain_profile`
- 正式地形生成模式：
  - `battle_map_size`
  - `world_coord`
  - `tu_per_tick`
  - `battle_terrain_profile`

`BuildStartContextLease()` 仍保留给 schema projection 和显式 canonical payload 的同步调用方；它会加入 `battle_party` / `enemy_units`，这些调用方必须继续使用四参数 legacy start，不能把同一 context 与 typed roster 混用。普通 Runner 与 formal fixture 的实际 runtime start 都改用不含场景单位 payload 的 `BuildRuntimeStartContextLease()` 基础 context。

当 `use_formal_terrain_generation = true` 时，模拟不会再因为 `map_size` / `cells` / `ally_spawns` / `enemy_spawns` 命中 `BattleUnitFactory` 的手工地形回退路径，而是直接走正式 `BattleTerrainGenerator`。这适合做“模拟地图必须与正式战斗同尺寸、同峡谷生成逻辑”的 AI 对战。

### 单位定义

`BattleSimUnitSpec` 用于旧式显式参战单位夹具。它是同步 authoring/import `Resource`，职责不是“引用一个模板并自动生成全部内容”，而是“把模拟需要的单位状态显式写出来”。`BattleSimUnitSpec.ToDefinition()` 会把它深拷贝为 immutable plain `BattleSimUnitDefinition`；运行时每场战斗都由该 definition 新建独立的 `BattleUnitState`，不会持有或复用原始 `BattleSimUnitSpec`。plain/programmatic 调用方可通过 `BattleSimScenarioUnitEntry.FromProjectedState(...)` 把已经投影完成的 unit 纳入 scenario definition；该入口会捕获 normal equipment-projection seed，并在每次 scenario roster materialization 时防御复制。当前 authored `BattleSimUnitSpec` 没有生成该 seed 的字段。

如果模拟目标是玩家角色、队伍成员、武器/equipment view、技能进度、职业生命成长或建卡属性，优先使用 `BattleSimFormalCombatFixture`，不要在 `.tres` 场景里写 `base_attributes` / `attribute_overrides` / `weapon_projection`。当前 `mixed_2sword_1arch_mirror_simulation` 与 `mixed_6v12_mirror_simulation` 就是这种模式：场景资源只保留地图、地形、时间轴和 seed，单位由 fixture 走 `CharacterCreationService`、`CharacterManagementModule`、`AttributeService` 与现有正式角色/装备视图投影生成，并在开战前按装备与职业被动后的有效 `hp_max` 补满所有成员当前生命。hostile units 由 factory 生成后直接经 enemy-only typed roster 移交 runtime，不再 canonicalize 到 `enemy_units`，因此 factory 已产生的 runtime-only temporal modifier 不会在开战交接中丢失；ally 仍由正式 character gateway 路径生成。当前 formal 默认 loadout 本身不产生 temporal modifier，而且两个实际 benchmark 的 runtime setup 尚未注入 trait/equipment-binding catalog，所以这里不能推定默认 benchmark 已出现非空装备能力投影。formal fixture 会显式开启 `validate_spawn_reachability` 与 `validate_bidirectional_spawn_reachability`；如果生成出的地图导致 player 与 hostile 任一方向无法抵达可攻击位置，`BattleRuntimeModule.start_battle()` 会用下一个 terrain seed attempt 重刷地图，而不是把不可交战地图纳入模拟样本。

正式角色 fixture 支持 roster options：

- `main_character_member_id`：指定友军主角；缺省时使用第一个友军。
- `leader_member_id`：指定友军队长；缺省时跟随主角。
- `main_character_reroll_count`：主角出生幸运烘焙使用的 reroll 次数；缺省为 `0`。
- `attribute_roll_seed`：正式建卡六维的 `5D3-1` 骰子 seed；`RunMixed6v12MirrorAnalysis.cs` 缺省使用本场 battle seed，所以不同 run 会拥有不同属性分布。这里是 fixture 对建卡输入的显式 seed 接线，不会把后续 combat RNG 变成可重放序列。

`mixed_6v12_mirror_simulation` 的 6 人方按“主角 + 5 名队友”建模，12 人方按敌方单位建模，双方六维都由 fixture 走建卡骰子生成。选中的主角会通过 `CharacterCreationService.bake_hidden_luck_at_birth()` 烘焙 `hidden_luck_at_birth`，其余友军和敌方单位保持默认 `0`。`run_mixed_2s1a_mirror_analysis.cs` 与 `RunMixed6v12MirrorAnalysis.cs` 缺省用 `TrueRandomSeedService.generate_seed()` 生成 `START_SEED`，报告会写出 `start_seed` 与 `start_seed_source`；需要复现地形与显式 seed-derived fixture 输入时可传入 `START_SEED`，但这不会复现后续战斗掷骰或完整结果。这些脚本还可通过环境变量传入同名大写参数，例如 PowerShell：

```powershell
$env:MAIN_CHARACTER_MEMBER_ID='elite_archer_0'
$env:MAIN_CHARACTER_REROLL_COUNT='0'
$env:START_SEED='12345'
$env:COUNT='1'
godot --headless --script tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs
```

`RunMixed6v12MirrorAnalysis.cs` 还有批量运行墙钟保护：`SIM_TIMEOUT_SECONDS` 缺省为 `1800`，表示 30 分钟。超时只在两场模拟之间停止继续排新 run，不中断正在结算的一场战斗。原始报告现在遵守正式完成态契约：`run_count` 表示已经产生 `runs[]` 记录的尝试数，`completed_run_count / ended_count` 表示 `termination_kind == battle_ended` 的轮数，`requested_run_count` 表示请求轮数，`timed_out` 标记是否命中墙钟保护；未完成 run 会分别计入 idle stall、iteration budget exhausted 或 invalid runtime。每条 run 都显式输出 `battle_ended` 与 `termination_kind`，顶层 `wins_by_faction` 保存完成局胜负次数，`win_rate` 保存以 `completed_run_count` 为分母的比例；均值、技能、faction 与 per-unit aggregate 同样只消费 battle-ended runs。请求轮数没有全部正常结束时 `is_complete=false`，runner 写完诊断报告后返回 `2`；配套 trace summary 会继承同一批次的 `requested_run_count / timeout_seconds / timed_out / is_complete`，不会因已尝试的几局恰好都结束而误报整批完成。正式分析仍优先运行 `build_battle_sim_analysis_packet.py` 生成低 token packet；旧报告若缺少逐 run `battle_ended`，且 completed draw 与 unfinished run 无法可靠区分，packet 会设置 `completion_classification_complete=false`、输出 unknown 计数/warning，并把正常指标标为 unavailable，不会猜测归类。设置 `SIM_TIMEOUT_SECONDS=0` 可关闭墙钟保护。

三个手写 benchmark runner（`RunMixed6v12MirrorAnalysis.cs`、`run_mixed_2s1a_mirror_analysis.cs`、`run_longsword_3v3_mastery_analysis.cs`）均遵守相同契约：所有 attempt 计入 `run_count`，只有 battle-ended runs 进入正常指标；存在 unfinished run 或未完成全部请求轮数时，报告保留终止分类并返回 `2`。

`RunMixed6v12MirrorAnalysis.cs` 默认会向 stdout 打印批量进度，即使 `OUTPUT_FILE` 指向 JSON 文件也会打印。进度包含实际请求轮数、本场 seed、运行阶段、结算后的迭代数、`timeline_steps`、胜者与累计耗时。可用 `PROGRESS=0` 关闭。

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

## Trace Summary 结构

`trace_summary_json` 是完整 trace 的低 token sidecar。它与完整 `report_json` / `turn_trace_jsonl` 共存，不替代完整 trace。

顶层重点字段：

- `source_report`
- `run_count`
- `completed_run_count`
- `unfinished_run_count`
- `has_unfinished_runs`
- `is_complete`
- `trace_count`
- `trace_compaction`
- `runs`

每个 compact run 至少包含：

- `profile_id`
- `seed`
- `battle_ended`
- `termination_kind`
- `stalled`
- `start_failure`
- `winner_faction_id`
- `iterations`
- `timeline_steps`
- `action_counts_by_faction`
- `command_counts_by_faction`
- `wait_counts_by_faction`
- `block_reasons_by_faction`
- `focus_turns`
- `focus_wait_turns`

默认 focus faction 是 `player`。每个 focus turn 只保留回合级 chosen action / command / score，以及 action trace 的 `block_reasons`、计数和前 2 个候选摘要；如果要完整候选列表，回到原始 `report_json` 或 `turn_trace_jsonl`。

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
- `termination_kind`
- `stalled`
- `start_failure`
- `winner_faction_id`
- `final_tu`
- `iterations`
- `timeline_steps`
- `idle_loops`
- `ally_alive`
- `enemy_alive`
- `metrics`
- `ai_turn_traces`
- `final_units`

适合的解读方式：

- `termination_kind`
  - `battle_ended`：唯一可进入正常胜率、均值、技能、action 与 faction metrics 汇总的有效样本。
  - `idle_stall`：idle guard 判定没有继续推进。
  - `iteration_budget_exhausted`：达到 `max_iterations` 时战斗仍未结束。
  - `invalid_runtime`：runtime/state 缺失、state 不是 runtime 当前持有的正式 state，或出现无法归入前三类的无效终止。战斗启动失败属于这一类，不会先经过 idle guard。
- `start_failure`
  - 成功启动时为空对象。
  - 启动失败时保留 `invalid_start_units`、`spawn_reachability` 或 `placement_exhausted` 等原因，以及当前可用的单位数、布阵尝试和出生可达性诊断。
- `battle_ended == false`
  - 等价于该 run 不是正常完成样本；它仍保留在 `runs` 中供诊断，但不会进入正常统计。
- `idle_loops` 偏高
  - 常表示行动链停滞、站位无法推进，或行为策略互相抵消。
- `iterations` 与 `timeline_steps`
  - `iterations` 是执行循环步数，包含 AI 行动、manual wait、ready 队列 drain 和 TU tick，用于保护模拟不会无限循环。
  - `timeline_steps` 只统计实际让时间轴 TU 前进的 tick，适合判断战斗经过了多少次正式时间推进。
- `final_units`
  - 适合回看战斗结束时的单位状态，而不是只看胜负。

## Profile Summary 结构

`BattleSimReportBuilder` 会对每个 profile 生成 summary。

当前字段：

- `profile_id`
- `display_name`
- `run_count`
- `completed_run_count`
- `unfinished_run_count`
- `stalled_run_count`
- `iteration_budget_exhausted_run_count`
- `invalid_runtime_run_count`
- `has_unfinished_runs`
- `is_complete`
- `wins_by_faction`
- `win_rate_by_faction`
- `average_final_tu`
- `average_iterations`
- `average_timeline_steps`
- `skill_attempt_totals`
- `skill_usage_totals`
- `skill_failure_totals`
- `action_choice_counts`
- `faction_metric_totals`

`run_count` 表示实际尝试数，不因失败而缩小；`completed_run_count` 才是正常统计分母。`wins_by_faction`、胜率、三个平均值，以及 skill/action/faction totals 都只消费 `termination_kind == battle_ended` 的 runs。未完成 run 的局部 metrics 不会混入 profile comparison。

### 这些字段分别适合回答什么问题

`wins_by_faction` 与 `win_rate_by_faction`

- 哪边赢得更多。
- 改完 profile 后整体胜率是升是降。

`average_final_tu`

- 战斗是变快了还是拖长了。
- 更激进的 AI 往往会把这个值压低。

`average_iterations`

- 用于识别模拟推进是否更卡、更绕、或更容易停滞。

`average_timeline_steps`

- 用于观察真实时间轴推进次数，和 `average_iterations` 分开看可以判断耗时来自 TU tick，还是来自同一 TU 下的行动 / ready 队列 drain。

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
- `baseline_run_count`
- `baseline_completed_run_count`
- `candidate_run_count`
- `candidate_completed_run_count`
- `has_unfinished_runs`
- `is_complete`
- `average_final_tu_delta`
- `average_iterations_delta`
- `average_timeline_steps_delta`
- `win_rate_delta`
- `skill_usage_delta`
- `action_choice_delta`

注意：

- baseline 永远是 `profile_entries[0]`。
- 如果要做正式实验，建议把真正的对照组放在参数列表第一位。
- comparison 只从各 profile 的 completed-only summary 计算；任一侧没有 completed sample 时不生成 comparison。部分完成时仍会输出差值，但 `is_complete == false`，不得当作完整实验结论。
- 战斗掷骰彼此独立，不按 seed 配对或取 seed 交集；样本完整性由上述计数表达。

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
2. 看 `average_final_tu`、`average_iterations` 和 `average_timeline_steps`
   - 确认策略变化是不是让战斗拖慢，以及拖慢来自真实 TU 推进还是同一 TU 下的行动 / ready 队列 drain。
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
- battle seed 只改变地形及显式接线的 fixture 输入；战斗掷骰仍会为每场 run 产生独立随机样本。因此正式统计既要覆盖足够的地形 seed，也要有足够总 run 数吸收 combat RNG 方差。
- 如果差值量级和总体随机噪声接近，不要把本轮结果直接当稳定结论。

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

3. `battle_ended == false` 的 run 只用于诊断。
   - shared report builder 已自动从正常胜率、均值、技能/action/faction 汇总中排除这类 run，并通过 `termination_kind` 区分迭代预算耗尽、idle stall 和无效 runtime。
   - CLI 仍会先写出诊断报告，但只要存在 unfinished run 就返回非零；不要忽略退出码或 `is_complete == false`。

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

- 异常 run 的单个 `seed`，用于还原地形和显式 seed-derived fixture 输入；它不能单独还原战斗掷骰
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
- 未完成 run 会保留诊断事实但自动排除出正常汇总；任何 `is_complete == false` 的批次都不能发布为正式平衡结论。

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
