# 战斗 AI 行为模块 — 性能 Profile & 基线方案

> 状态：待审查（Codex review pending）
> 日期：2026-05-12
> 适用范围：`scripts/systems/battle/ai/`

## Context

战斗 AI 模块目前没有专属的性能度量。现有 benchmark（`tests/battle_runtime/benchmarks/run_battle_6v40_headless_benchmark.gd`）只测整体 `advance_step` 耗时与 HUD 快照开销，**完全无法定位 AI 占多少，更看不到具体哪个内部函数是热点**。当 AI 模块改动（评分公式、地毯轰击专项逻辑、新加敌人 brain state）时，回归没有量化抓手；只有跑大场景看总时间长了就猜是 AI 慢了。

本方案要解决两个分离但相关的问题：

- **问题 A — CI 级回归基线**：每次 PR 跑得起，整体看 AI 模块有没有变慢，超阈值即 fail；不要求看到内部细节。
- **问题 B — 优化驱动的深度 profile**：当 A 报告告警，或主动想做性能优化时，需要**真正的函数级 profile**：能看到 score service 内部哪几个函数占大头，能画 flame graph、按 self time 排 top-N、定位嵌套循环的瓶颈。

之前的初版方案只解决了 A（4 层入口的粗粒度计时）。Codex review 之前已经先升级，加入 B。

## 调研结论（决定路径的关键事实）

项目当前已经在 **Godot 4.6**（`project.godot:15` 中 `config/features=PackedStringArray("4.6", "Forward Plus")`），这影响后续技术选型。Godot 4 在 headless 下做函数级 GDScript profiling 的现实选项：

| 路径 | 结论 | 备注 |
|---|---|---|
| `--debug-profiling` 启动参数 | 不可用 | 主要面向编辑器 GUI，headless 下无标准 dump 机制 |
| **`EngineDebugger.register_profiler` + 自写 EngineProfiler 子类** | **采用（Layer 2-B）** | 4.4+ 内建 API；引擎已经在采集所有 GDScript 函数 self/total/ncalls，子类只负责 dump。零代码侵入，用官方 4.6 binary 即可 |
| Tracy 官方集成（PR #113279） | 不采用 | 4.6 已合并，**但必须自己 `scons profiler=tracy` 重编 Godot**；CI 也要装自定义 binary，运维成本过高 |
| **手工 instrument + Chrome Tracing JSON** | **采用（Layer 2-C）** | 仅对少数关键热点函数标记；用于看火焰图与时间线（spike 定位），不作为常规手段 |
| GDScript 装饰器 `@profile_function` | 不支持 | GDScript 4 无装饰器语法 |

**关键判断**：

- **Layer 2-B（EngineProfiler dump）当主力**：零侵入、零重编、用官方 binary，输出 cProfile 风格 self/total/ncalls 排序表。能覆盖 90% "找热点"需求。
- **Layer 2-C（手工 instrument）当补充**：仅在 B 报告高 self_time 函数但需要看**时间线 / 调用栈分布 / spike**时启用。只标 ~10 个函数（不像之前方案那样标 15-20），保持侵入最小。

两条路输出不同格式（B = 文本表 / CSV；C = Chrome Tracing JSON），各自独立可用。

## 用户已确认的三个决策（保留）

- **粒度**：分层 service + score + assembler（Layer 1）
- **规模**：小（2v4） / 中（6v12，复用现有 mirror fixture） / 大（6v40，复用 mixed_pressure 场景）
- **持久化**：JSON 落盘 + 阈值告警，默认 `+20%` 阈值

## 三层结构总览

```
Layer 1  CI 回归基线（subclass wrapper，零侵入、零开销）
  └─ run_battle_ai_performance_baseline.gd
       └─ 输出 ai_baseline.json + 阈值告警
       └─ 4 层粗粒度：choose / skill_input / action_input / assembler

Layer 2-B  深度 Profile · EngineProfiler dump（默认入口、零侵入）
  └─ run_battle_ai_profile.gd  (mode=engine_profiler)
       └─ AiEngineProfiler 注册到 "script" channel
       └─ 引擎自动采集所有 GDScript 函数 self/total/ncalls
       └─ 输出 ai_profile_<scenario>.hotspots.txt（cProfile 风格 top-N）
       └─ 输出 ai_profile_<scenario>.functions.csv（全函数 dump，可 grep）

Layer 2-C  深度 Profile · Chrome Tracing（按需启用、轻侵入）
  └─ run_battle_ai_profile.gd  (mode=trace, AI_TRACE=1)
       └─ AiTraceRecorder + 在 ~10 个 T1 热点函数加 _TR.enter/_TR.exit
       └─ 默认 no-op（不开 AI_TRACE 时 ~50ns/调用）
       └─ 输出 ai_profile_<scenario>.trace.json（Chrome Tracing 格式）
       └─ 用 perfetto.dev/viewer 看火焰图与时间线
```

三层用同一份 fixture 与场景定义。CI 永远只跑 Layer 1；本地常规优化跑 Layer 2-B；需要看时间线/spike 时再叠加 Layer 2-C。

---

# Layer 1 ─ CI 回归基线（零侵入、低开销）

## 1.1 总体架构

零侵入生产代码，全部通过**子类化 `BattleAiService` / `BattleAiActionAssembler` + 在 benchmark 脚本里直接替换 `runtime._ai_service` / `runtime._ai_action_assembler` 字段**实现。

**新增 4 个文件，生产代码 0 修改：**

| 路径 | 用途 |
|---|---|
| `tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.gd` | `extends SceneTree` 入口；解析环境变量、构造三档场景、装入 probe、跑循环、写 JSON、调用 diff 退出 |
| `tests/battle_runtime/benchmarks/ai_performance_probe.gd` | 两个探针类：`AiServiceProbe extends BattleAiService` 与 `AiAssemblerProbe extends BattleAiActionAssembler`；含 `LayerStats` 内部类（call_count / total_usec / samples / max） |
| `tests/battle_runtime/benchmarks/ai_baseline_diff.gd` | 读写 baseline.json + 计算 delta_pct + 输出 `[REGRESSION]` / `[OK]` 行 + 决定 exit code |
| `tests/battle_runtime/benchmarks/baselines/ai_baseline.json` | 首次运行用 `UPDATE_BASELINE=1` 生成；纳入版本控制，PR diff 友好 |

## 1.2 关键已验证事实（影响实现可行性）

- `battle_runtime_module.gd:187` `_ai_service: BattleAiService = BATTLE_AI_SERVICE_SCRIPT.new()` 是普通字段，**无 `_set` 拦截**，外部脚本可直接赋值；`is-a` 关系成立（probe 是 `BattleAiService` 子类）。
- `battle_runtime_module.gd:188` `_ai_action_assembler` 同理。
- `battle_runtime_module.gd:490-500` 每次 AI 决策都**重新构造 `ai_context` 并重新 bind callback**（`Callable(_ai_service, "build_skill_score_input")` 等），所以**替换 `_ai_service` 后立刻生效**，不存在旧引用残留问题。
- `battle_ai_service.gd:5` `class_name BattleAiService extends RefCounted`，子类用 `super.choose_command(context)` 调用父类方法，Godot 4 GDScript 原生支持。
- `BattleAiService` 自身会被 `Callable(_ai_service, "build_skill_score_input")` 调到，意味着它确实有 `build_skill_score_input` / `build_action_score_input` 这两个方法（forward 给内部 `_score_service`，见 `battle_ai_service.gd:24` 的 `_score_service` 字段）。在 service 层 wrap 而非 score_service 层 wrap，覆盖率一致且替换点更少。

## 1.3 三档场景定义

| 档位 | 单位 | 场景源 | TU | 重复次数 |
|---|---|---|---|---|
| `small_2v4` | 2 manual + 4 AI（2 melee_aggressor + 2 ranged_suppressor） | **新写 inline fixture**，参考 `run_battle_6v40_headless_benchmark.gd` 里的 `_build_manual_benchmark_unit` / `_build_ai_benchmark_unit` 与 mixed_pressure 装填逻辑，缩到 4 AI；20×14 地图 | 100 | 1 warmup + 2 measured |
| `medium_6v12` | 6 manual + 12 AI | **复用** `data/configs/battle_sim/scenarios/mixed_6v12_mirror_simulation.tres`，调用方式参考 `run_mixed_6v12_mirror_analysis.gd:431-453` 的 `BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT` 路径 | 200 | 1 warmup + 2 measured |
| `large_6v40` | 6 manual + 40 AI（mixed_pressure） | **复用** `run_battle_6v40_headless_benchmark.gd` 的 `_build_runtime` + `_populate_mixed_pressure_units`（不复用 ground_skill_heavy，mixed_pressure 评分路径更多样） | 200 | 1 warmup + 2 measured |

固定 seed=42 + 固定 `BENCHMARK_TARGET_TU` 保证可重现。

## 1.4 分层计时实现骨架

`ai_performance_probe.gd`：

```gdscript
extends "res://scripts/systems/battle/ai/battle_ai_service.gd"
class_name AiServiceProbe

class LayerStats:
    var call_count: int = 0
    var total_usec: int = 0
    var max_usec: int = 0
    var samples_usec: PackedInt64Array = PackedInt64Array()

var stats_choose := LayerStats.new()
var stats_skill_input := LayerStats.new()
var stats_action_input := LayerStats.new()

func choose_command(context):
    var t := Time.get_ticks_usec()
    var result = super.choose_command(context)
    _record(stats_choose, Time.get_ticks_usec() - t)
    return result

func build_skill_score_input(context, skill_def, command, preview, effect_defs := [], metadata := {}):
    var t := Time.get_ticks_usec()
    var result = super.build_skill_score_input(context, skill_def, command, preview, effect_defs, metadata)
    _record(stats_skill_input, Time.get_ticks_usec() - t)
    return result

# build_action_score_input 同理
```

`AiAssemblerProbe` 同样 extends `battle_ai_action_assembler.gd`，重写 `build_unit_action_plan`。

**注入位置**（在 benchmark 脚本里）：

```gdscript
var ai_probe = AiServiceProbe.new()
runtime._ai_service = ai_probe                     # 替换
ai_probe.setup(runtime._enemy_ai_brains, runtime._damage_resolver)  # 重新 init

var assembler_probe = AiAssemblerProbe.new()
runtime._ai_action_assembler = assembler_probe
# 注意：若 runtime 在 _populate_xxx 之前已经调用过 _ai_action_assembler.build_unit_action_plan
# 来填 _ai_action_plans_by_unit_id，则替换后需要重新调一次。验证步骤里覆盖。
```

**self vs inclusive 时间**：`choose_command` 内部会通过 callback 调到 `build_skill_score_input`，所以 `choose_command.total_usec` 是 inclusive。JSON 中同时输出 `total_inclusive_usec` 与 `total_self_usec = inclusive − Σ子层 total_usec`，避免人工 review 把 AI 慢算到 choose_command 自身上。

## 1.5 指标与 JSON 结构

每层采集：`call_count` / `avg_usec` / `p50_usec` / `p95_usec` / `max_usec` / `total_inclusive_usec` / `total_self_usec`（仅顶层有意义）。samples 排序取百分位。

```jsonc
{
  "schema_version": 1,
  "godot_version": "4.x.x",
  "generated_at_unix": 1747000000,
  "git_commit": "abc1234",
  "scenarios": {
    "large_6v40": {
      "target_tu": 200,
      "ai_turns": 312,
      "layers": {
        "choose_command": {
          "call_count": 312, "avg_usec": 1850, "p50_usec": 1620, "p95_usec": 4100,
          "max_usec": 12000, "total_self_usec": 420000, "total_inclusive_usec": 577200
        },
        "build_skill_score_input":  {"call_count": 1840, "avg_usec": 62, "p50_usec": 55, "p95_usec": 140, "max_usec": 820},
        "build_action_score_input": {"call_count": 624,  "avg_usec": 34, "p50_usec": 30, "p95_usec": 78,  "max_usec": 410},
        "build_unit_action_plan":   {"call_count": 40,   "avg_usec": 210,"p50_usec": 190,"p95_usec": 480, "max_usec": 700}
      }
    }
  }
}
```

JSON 用 tab 缩进 + 排序 key，PR diff 上看变化清晰。

## 1.6 基线对比与告警

`ai_baseline_diff.gd` 的逻辑：

- **`UPDATE_BASELINE=1` 或首次运行（baseline.json 不存在）**：覆盖写盘，stdout `[BASELINE] wrote tests/.../ai_baseline.json`，`quit(0)`。
- **常规运行**：读 baseline → 对每场景 × 每层 × `{avg, p50, p95}` 计算 `delta_pct = (current - baseline) / baseline`；
- **阈值默认 +20%**，环境变量 `BASELINE_TOLERANCE_PCT=15` 可覆写；
- **噪声地板**：忽略 baseline 中 `avg_usec < 30` 的层（μs 级抖动太大），但仍打印对比行；
- **超阈值**：stderr 输出 `[REGRESSION] scenario=large_6v40 layer=choose_command metric=p95 baseline=4100us current=5500us delta=+34.1%`，所有超阈值聚合后 `quit(1)`；
- **schema 不兼容**：强制要求 `UPDATE_BASELINE=1` 才能继续。

## 1.7 CLI 接口

```bash
# 首次生成基线
UPDATE_BASELINE=1 godot --headless --script res://tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.gd

# 常规跑（含对比）
godot --headless --script res://tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.gd

# 自定义阈值与场景过滤
BASELINE_TOLERANCE_PCT=15 BASELINE_SCENARIOS=large_6v40 godot --headless --script ...
```

环境变量清单：
- `BENCHMARK_TARGET_TU`（沿用现有 6v40 脚本的名字）
- `BASELINE_TOLERANCE_PCT`（默认 20）
- `UPDATE_BASELINE`（=1 时覆盖写）
- `BASELINE_SCENARIOS`（逗号分隔，默认全跑）
- `BASELINE_REPEAT_COUNT`（默认 3 = 1 warmup + 2 measured；噪声大时调到 5）

CI 占位：`.github/workflows/` 或同等位置加 step `godot --headless --script ...`，exit code 决定 PR 状态。

---

# Layer 2-B ─ EngineProfiler dump（深度 profile 主入口，零侵入）

## 2B.1 设计目标

让 score service 等内部任意函数（不需要预先标记）都能拿到 `self_time / total_time / call_count` 数据，输出 cProfile 风格 top-N 表，足够"找到哪几个函数是热点"。零代码侵入、零引擎重编、官方 4.6 binary 直接跑。

## 2B.2 核心 API：`EngineProfiler` + `register_profiler`

Godot 4.4+ 内建了 `EngineProfiler` 抽象类（参考 [class_engineprofiler](https://docs.godotengine.org/en/latest/classes/class_engineprofiler.html)），暴露 4 个回调：

```gdscript
class_name AiEngineProfiler
extends EngineProfiler

func _toggle(enable: bool, opts: Array) -> void: ...  # 开关
func _add_frame(data: Array) -> void: ...             # 每帧聚合数据
func _tick(frame_time, idle_time, physics_time, physics_frame_time) -> void: ...
```

GDScript 项目调用 `EngineDebugger.register_profiler(&"ai_func_profile", AiEngineProfiler.new())` 后，引擎会在 `_toggle(true, ...)` 后开始把 GDScript 函数级数据通过 `_add_frame` 推过来。**关键**：引擎已经在采集所有 GDScript 函数 self/total/ncalls，profiler 子类只做 dump，不需要任何编译期 hook。

> **风险**：`_add_frame` 推送的 `data` 数组结构在官方文档里没有详细字段规约（社区样例非常稀少）。验证步骤需要先跑一个 spike 脚本 dump `data` 的实际结构，确认能解析出 `function_name / total_time / self_time / call_count`，若 schema 与预期不符要降级到方案 C（手工 instrument）。这是本路径**最大的未知数**，必须前置验证。

## 2B.3 文件清单

**新增 3 个文件，生产代码 0 修改：**

| 路径 | 用途 |
|---|---|
| `scripts/dev_tools/ai_engine_profiler.gd` | `extends EngineProfiler` 子类；累积每帧 `_add_frame` 数据到 `_func_stats: Dictionary[name -> {ncalls, self_usec, total_usec}]` |
| `tests/battle_runtime/benchmarks/run_battle_ai_profile.gd` | `extends SceneTree` 入口；注册 profiler、装入 Layer 1 subclass probe（拿 ai_turns / battle_ended）、跑三档场景之一、退出时 dump 报告 |
| `tests/battle_runtime/benchmarks/ai_hotspots_formatter.gd` | 从 `_func_stats` 排序 + 格式化成 cProfile 风格表与 CSV |

## 2B.4 输出格式

**`ai_profile_<scenario>_<timestamp>.hotspots.txt`**（默认排序 by `self_usec` 降序，top 20）：

```
=== AI Profile · large_6v40 ·  Godot 4.6 · commit abc1234 ===
total_frames: 4823    ai_turns: 312    total_ai_time: 587.4 ms

  ncalls   tottime(ms)   percall(us)   cumtime(ms)   percall(us)   function
   22080         54.22           2.5         54.22           2.5   battle_ai_score_service.gd:_populate_target_effect_metrics
    1840         38.91          21.1         99.13          53.9   battle_ai_score_service.gd:_populate_hit_metrics
    7520         17.85           2.4         17.85           2.4   battle_ai_score_service.gd:_resolve_target_role_threat_multiplier_basis_points
     312         29.40          94.2         99.20         318.0   battle_ai_score_service.gd:_populate_special_profile_metrics
    1840        115.04          62.5        420.31         228.4   battle_ai_score_service.gd:build_skill_score_input
   ...
```

**`ai_profile_<scenario>_<timestamp>.functions.csv`**：全函数 dump，便于 grep / Excel 透视，列 `file,function,ncalls,self_usec,total_usec,self_per_call_usec`。

filter 选项（环境变量 `AI_PROFILE_FILTER=battle_ai_`）：只保留文件路径匹配前缀的函数，避免被项目其它 hot path（如 `attribute_service`、`battle_damage_resolver`）噪声盖掉 AI 信号——但 filter 仅作用于文本报告，CSV 总是全量输出，方便定位"AI 内部慢是因为下游调用了某个非 AI 函数"的情况。

## 2B.5 CLI 接口

```bash
# 默认跑 medium 档 + 默认 filter
godot --headless --script res://tests/battle_runtime/benchmarks/run_battle_ai_profile.gd

# 大场景
BASELINE_SCENARIOS=large_6v40 godot --headless --script res://tests/battle_runtime/benchmarks/run_battle_ai_profile.gd

# 全函数报告（不过滤）
AI_PROFILE_FILTER= godot --headless --script ...

# 排序与 top-N
AI_PROFILE_SORT=cumtime AI_PROFILE_TOP_N=50 godot --headless --script ...
```

新增环境变量：
- `AI_PROFILE_FILTER`（默认 `battle_ai_`；空字符串 = 不过滤）
- `AI_PROFILE_SORT`（`tottime`|`cumtime`|`ncalls`，默认 `tottime`）
- `AI_PROFILE_TOP_N`（默认 20）
- `AI_PROFILE_OUTPUT_DIR`（默认 `tests/battle_runtime/benchmarks/profiles/`）

---

# Layer 2-C ─ Chrome Tracing trace（按需开启，看时间线/火焰图）

## 2C.1 何时使用

仅在 Layer 2-B 已经给出 self_time 排序、但你需要回答以下问题时启用：

- 是不是某次特别长的 spike 拉高了 max_usec？（看时间线）
- 哪个调用栈深度下最慢？（看火焰图）
- 两次 AI turn 之间发生了什么？（看 inter-call gap）

否则**不要开**，因为它需要在 score service 加 ~10 处 enter/exit 标记。

## 2C.2 标记范围（最小集）

只标 T1 热点（基于探索结论）：

- `BattleAiService.choose_command`
- `BattleAiService._evaluate_action`（如果存在；评分主循环）
- `BattleAiScoreService.build_skill_score_input` / `build_action_score_input`
- `BattleAiScoreService._populate_hit_metrics`
- `BattleAiScoreService._populate_special_profile_metrics`
- `BattleAiScoreService._populate_target_effect_metrics`
- `BattleAiScoreService._resolve_target_role_threat_multiplier_basis_points`
- `BattleAiScoreService._resolve_meteor_threat_rank`
- `BattleAiActionAssembler.build_unit_action_plan`

合计 ~10 个函数 × 每函数 2 行（enter + exit） = ~20-25 行侵入（含早 return 路径）。比之前方案的 15-20 函数减半。

## 2C.3 Tracer 设计（默认 no-op 关键）

`scripts/dev_tools/ai_trace_recorder.gd`：

```gdscript
class_name AiTraceRecorder
extends RefCounted

static var instance: AiTraceRecorder = null   # 默认 null = no-op

static func enter(name: StringName) -> void:
    var i := instance
    if i == null: return
    i._events.append({"name": name, "ph": "B", "ts": Time.get_ticks_usec(), "pid": 1, "tid": 1})

static func exit(name: StringName) -> void:
    var i := instance
    if i == null: return
    i._events.append({"name": name, "ph": "E", "ts": Time.get_ticks_usec(), "pid": 1, "tid": 1})

var _events: Array = []

func dump(path: String) -> void:
    var doc := {"traceEvents": _events, "displayTimeUnit": "us"}
    var file := FileAccess.open(path, FileAccess.WRITE)
    file.store_string(JSON.stringify(doc))
```

**关闭时开销**：`var i := instance` + `if i == null: return` = ~30-50 ns/调用，相对最快的 score 内部函数（~2-3μs）是 ~1.5% 噪声，可接受。`AI_TRACE` 不开时**完全无副作用**（不进 `_events`、不查 `Time`）。

**侵入代码**（`battle_ai_score_service.gd` 内）：

```gdscript
const _TR := preload("res://scripts/dev_tools/ai_trace_recorder.gd")

func _populate_hit_metrics(...):
    _TR.enter(&"_populate_hit_metrics")
    # ... 原函数体 ...
    _TR.exit(&"_populate_hit_metrics")
```

**早 return 处理**：每个 early return 前必须补 `_TR.exit`。Codex review 必须逐条核对，否则火焰图会错位。备选方案：把函数体 wrap 进 `_impl()`，外层 wrap：

```gdscript
func _populate_hit_metrics(args...):
    _TR.enter(&"_populate_hit_metrics")
    var r = _populate_hit_metrics_impl(args...)
    _TR.exit(&"_populate_hit_metrics")
    return r
```

代价是 diff 更大、函数嵌套加深。**默认采取直接侵入**，由 review 兜底；若 early return 太多再切换 impl wrap。

## 2C.4 输出与查看

`ai_profile_<scenario>_<timestamp>.trace.json`，[Chrome Trace Event Format](https://docs.google.com/document/d/1CvAClvFfyA5R-PhYUmn5OOQtYMH4h6I0nSsKchNAySU/preview)。

打开方式（任选）：
- Chrome: `chrome://tracing` → Load
- **推荐**: [ui.perfetto.dev](https://ui.perfetto.dev/) → 拖入

文件体积估算：6v40 跑 300 ai_turns × 平均每 turn 进入 10 个标记函数 × 每对 enter/exit 2 个事件 = 6000 事件 ≈ 1MB JSON。无需采样。如果未来标记函数增多到吃力，再加 `AI_TRACE_SAMPLE_EVERY=N` 跳采开关。

## 2C.5 CLI 接口

```bash
# 启用 trace（必须显式开 AI_TRACE）
AI_TRACE=1 godot --headless --script res://tests/battle_runtime/benchmarks/run_battle_ai_profile.gd

# trace + 大场景
AI_TRACE=1 BASELINE_SCENARIOS=large_6v40 godot --headless --script ...

# trace + 同时跑 EngineProfiler（两份输出都生成）
AI_TRACE=1 AI_PROFILE=1 godot --headless --script ...
```

新增环境变量：
- `AI_TRACE`（=1 启用，默认 0；不启用时项目代码里的 `_TR.enter` 等于 no-op）
- `AI_PROFILE`（=1 启用 Layer 2-B；默认 1，因为 B 是 profile 主入口）

`run_battle_ai_profile.gd` 同时检查两个变量：B 默认开，C 默认关，互相独立。

---

## 关键文件清单

**修改的生产代码：无。**

**新增（全部测试代码）：**
- `tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.gd`
- `tests/battle_runtime/benchmarks/ai_performance_probe.gd`
- `tests/battle_runtime/benchmarks/ai_baseline_diff.gd`
- `tests/battle_runtime/benchmarks/baselines/ai_baseline.json`（首次跑生成）

**参考与复用的现有文件：**
- `scripts/systems/battle/ai/battle_ai_service.gd:40` — `choose_command` 入口，被子类化
- `scripts/systems/battle/ai/battle_ai_score_service.gd:44` — `build_skill_score_input` 真正实现（probe 通过 `BattleAiService` 间接 wrap，无需直接修改此处）
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd` — `build_unit_action_plan` 被子类化
- `scripts/systems/battle/runtime/battle_runtime_module.gd:187-188` — `_ai_service` / `_ai_action_assembler` 字段替换点
- `scripts/systems/battle/runtime/battle_runtime_module.gd:490-500` — callback 重 bind 路径，确认替换后立刻生效
- `tests/battle_runtime/benchmarks/run_battle_6v40_headless_benchmark.gd` — large 档 fixture 装填、SceneTree 启动样式、`Time.get_ticks_usec()` 用法
- `tests/battle_runtime/benchmarks/run_mixed_6v12_mirror_analysis.gd:431-453, 414-420` — medium 档 fixture、JSON 文件输出 `_write_json_file`

## 验证方案（端到端）

1. **冒烟**：先只跑 `small_2v4`，确认 probe 注入生效（每层 `call_count > 0`）、JSON 写盘成功、字段齐全。
2. **计时可靠性**：连续跑 3 次 small 档，比较 `avg_usec` 的方差应 < 10%；若不达标把 `BASELINE_REPEAT_COUNT` 提到 5 并取中位数。
3. **时序自洽断言**：`choose_command.total_inclusive_usec ≥ build_skill_score_input.total_usec + build_action_score_input.total_usec`，否则 probe 装配有问题（fail with exit 2）。
4. **回归触发演练**：临时在 `battle_ai_score_service.gd::_populate_hit_metrics` 加 `for i in 1000: pass`，跑一遍，确认 `[REGRESSION]` 行精确命中 `build_skill_score_input` 层而不是错位到 `choose_command.self`。验证完撤销改动。
5. **决策一致性**：probe 不能改变 AI 决策结果。复用 6v40 脚本的 `ai_turns / battle_ended / winner_faction_id` 字段，断言 probe 运行与原 `BattleAiService` 运行结果一致（同 seed）。
6. **三档曲线 sanity check**：检查 `choose_command.avg_usec` 是否随单位数增长（small < medium < large），且 large/small 的 ratio 与 ai_turns 数量 ratio 大致同阶或更高（如果超线性增长太多，说明 AI 评分本身就有 O(n²) 问题，是真实信号）。

## 风险与降级

- **风险 1**：runtime 在 setup 阶段已用旧 `_ai_action_assembler` 预生成动作计划（`_ai_action_plans_by_unit_id`），替换 assembler 后该缓存未刷新 → 验证步骤 1 会暴露（assembler 层 `call_count = 0`）；降级方案：替换 assembler 后手动重跑 `runtime._rebuild_ai_action_plans()` 或类似入口，或仅替换 `_ai_service` 跳过 assembler 层度量（first iteration 可接受）。
- **风险 2**：μs 计时噪声过大 → 验证步骤 2 兜底，靠重复次数 + 取中位压噪声。
- **风险 3**：probe 自身 wrapping 开销污染测量 → 单次 `Time.get_ticks_usec()` ≈ 0.1μs，相对 AI 层最小 ~30μs 是 0.3% 噪声，可接受。

## 审查关注点（给 Codex）

1. **子类化方案是否真能拦截到所有调用路径**？是否存在 `BattleAiService` 内部跳过 callback 直接调 `_score_service` 的旁路（绕过我重写的 `build_skill_score_input` wrapper）？请重点核对 `battle_ai_service.gd` 内 `_resolve_brain` / `_evaluate_action` 等内部函数。
2. **`runtime._ai_action_assembler` 的实际使用时机**：`_ai_action_plans_by_unit_id` 是否真的只在初始化时构建一次？战斗中是否会重新调用 `build_unit_action_plan`（如新单位加入、brain 切换）？若每回合都调，assembler probe 才有信号。
3. **三档场景的代表性**：`mixed_pressure` 与 `ground_skill_heavy` 哪个更适合做 large 档基线？后者会触发 `_populate_special_profile_metrics`（meteor swarm 路径），评分压力可能更大。
4. **阈值 +20% 是否合理**：μs 级测量在 Windows 上的噪声水位实际有多高？是否需要场景独立的阈值（small 档允许更大波动）？
5. **是否应该额外测一个 worst-case ground 场景**：之前用户拒绝了"额外加 ground worst-case"，但用户也许低估了 ground skill 的评分代价；codex 视角是否建议加？
