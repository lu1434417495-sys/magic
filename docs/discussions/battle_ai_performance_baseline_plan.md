# 战斗 AI 性能 Profile 与基线状态记录

更新日期：`2026-06-11`

## 文档定位

这份文档记录战斗 AI 性能观测方案的当前状态和取舍。

它不再是旧版 `.gd` 时代的实施计划；当前仓库的 AI、benchmark 辅助和 trace 工具已经 C# 化。后续不要再按旧文档中的 GDScript subclass runner、`EngineProfiler` spike 或三层方案推进，除非重新开设计评审。

## 当前结论

当前需要保留的是手动性能诊断能力，不需要继续补全完整 CI 性能门禁。

理由：

- 战斗 AI 性能波动受 seed、战局收敛、地图、单位密度和 Windows 调度噪声影响较大。
- 大场景运行成本高，作为 CI gate 容易变成维护负担。
- 现有 `AI_PROFILE` + trace / hotspots 输出已经能支撑本地定位热点。
- 近期优化需求更适合按问题定向跑模拟，而不是每次 PR 强制跑大规模性能基线。

## 已落地能力

### 1. 手动 AI Profile 入口

`tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs` 支持通过环境变量开启 AI profile：

```powershell
$env:AI_PROFILE='1'
$env:AI_PROFILE_TOP_N='30'
$env:AI_PROFILE_SORT='self_usec'
$env:AI_PROFILE_FILTER=''
$env:AI_PROFILE_TRACE_JSON='0'
$env:COUNT='1'
$env:SEEDS='59339390'
godot --headless --script tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs
```

常用环境变量：

- `AI_PROFILE=1`：开启 profile。
- `AI_PROFILE_OUTPUT_DIR`：输出目录，默认 `user://simulation_reports/ai_profiles/`。
- `AI_PROFILE_TOP_N`：hotspots 文本报告展示条数。
- `AI_PROFILE_SORT`：排序字段，常用 `self_usec`。
- `AI_PROFILE_FILTER`：函数名过滤；空字符串表示不过滤。
- `AI_PROFILE_TRACE_JSON=1`：额外输出 Chrome trace JSON。

### 2. Trace 与热点统计

当前 profile 主路径是：

- `scripts/dev_tools/AiTraceRecorder.cs`
- `tests/battle_runtime/benchmarks/ai_profile_capture.cs`
- `tests/battle_runtime/benchmarks/ai_hotspots_formatter.cs`

`AiTraceRecorder` 在无 active instance 时是 no-op；开启 profile 后会记录手工标记的热路径 span，并聚合：

- `ncalls`
- `self_usec`
- `total_usec`
- `max_usec`

`AiProfileCapture` 可以输出：

- `*.hotspots.txt`
- `*.functions.csv`
- 可选 `*.trace.json`

当前热路径标记已经覆盖多处 AI / action / preview / movement 代码，例如：

- `BattleAiService`
- `BattleAiScoreService`
- `MoveToRangeAction`
- 各类 `EnemyAiAction`
- `BattleRuntimeModule` AI advance 段
- 技能 preview 与移动 preview 段

### 3. 完整战斗 AI baseline 入口

当前已有手动 baseline runner：

- `tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.cs`
- `tests/battle_runtime/benchmarks/ai_baseline_diff.cs`
- `tests/battle_runtime/benchmarks/baselines/ai_baseline.json`

当前 baseline 的停止条件是完整战斗结束：

- `completion_policy = battle_ended`
- `AI_BASELINE_MAX_ITERATIONS` 只作为防卡死保护，不是测量目标
- 旧 `target_tu=200` 只保留为 `legacy_target_tu` 元信息，不再截断运行
- 性能 baseline 默认关闭 AI mutation guard；guard-on 用于其他行为测试的安全防护网，或通过 `AI_BASELINE_MUTATION_GUARD_ENABLED=1` 显式做诊断，不作为默认性能口径

当前 baseline json 内的场景口径是：

- `small_4v8`
- `medium_6v20`
- `large_6v40`

这不同于旧方案中的 `small_2v4 / medium_6v12 / large_6v40`。

更新 baseline 的推荐命令：

```powershell
$env:UPDATE_BASELINE='1'
$env:AI_BASELINE_REPEAT_COUNT='2'        # 1 warmup + 1 measured
$env:AI_BASELINE_MAX_ITERATIONS='100000'
$env:AI_BASELINE_REQUIRE_COMPLETED='1'
godot --headless --path . --script tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.cs
```

普通对比运行可以省略 `UPDATE_BASELINE`，并用 `AI_BASELINE_OUTPUT_FILE` 指定 snapshot 输出路径。

## 暂缓或不再推进

### 1. EngineProfiler 方案

旧文档里的 `EngineDebugger.register_profiler` / `EngineProfiler dump` 不再作为当前方向推进。

原因：

- 当前 C# 热路径已经有 `AiTraceRecorder` 和手工 span。
- `AI_PROFILE` 输出已经能定位本地热点。
- EngineProfiler 的 headless 数据结构和维护成本不再值得为当前需求单独验证。

后续只有在手工 span 明显不足、且需要非侵入式全函数采样时，才重新评估。

### 2. 完整 CI 性能门禁

暂不接入 CI 性能 gate。

原因：

- 性能波动大，阈值容易误报。
- large 场景耗时高，不适合常规 PR 必跑。
- 当前更需要定向 profile 和模拟验证，而不是固定门禁。

如果将来频繁出现“AI 改动导致性能回退但未被发现”的问题，再考虑恢复这个方向。

### 3. 旧 `.gd` Runner / Probe 方案

不再推进。

当前实现已经 C# 化，旧方案中的 `.gd` subclass wrapper、`.gd` baseline diff、`.gd` profile runner 都不应作为未来任务来源。

## 如果未来确实需要 CI 性能门禁

只建议做轻量版本：

1. 复用现有 C# baseline runner 和 `AiBaselineDiff`，不要恢复旧 `.gd` probe。
2. 只跑 `small_4v8` 和 `medium_6v20`，不把 `large_6v40` 作为必过门禁。
3. 阈值保持宽松，例如 `+30%`，并设置噪声地板。
4. baseline 更新必须显式通过 `UPDATE_BASELINE=1` 或等价参数触发。
5. 输出失败时必须打印 scenario / layer / metric / baseline / current / delta。

可选命令形态：

```powershell
$env:AI_BASELINE_SCENARIOS='small_4v8,medium_6v20'
$env:AI_BASELINE_TOLERANCE_PCT='30'
godot --headless --path . --script tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.cs
```

注意：CI 若要启用，应单独确认运行时长预算。完整战斗 baseline 当前适合手动更新，不适合默认全量 PR 门禁。

## 文档维护规则

- 本文件只记录性能观测方案状态，不记录单次模拟结果。
- 单次模拟报告继续放在 `tmp/` 或 `user://simulation_reports/`。
- 如果 baseline 场景、停止条件或 repeat 口径变化，再把命令和有效场景写回这里。
- 如果删除历史 baseline 辅助代码，也同步删掉本文件中对应说明。
