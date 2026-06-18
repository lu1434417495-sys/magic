# scripts 目录 C# 全量审查记录

日期：`2026-06-17`

范围：`scripts/**/*.cs`

文件总数：`469`

## 审查原则

- 逐文件检查，优先记录会导致运行时错误、GodotSharp 互操作问题、状态/序列化风险、性能热点、场景契约漂移和缺失回归的问题。
- 普通风格问题不记录，除非它掩盖了明确的正确性或维护风险。
- 发现问题后先记录到本文档，再继续后续文件。
- 兼容逻辑、旧 schema 支持、fallback 迁移只作为风险指出，不在未确认前建议直接添加。

## 覆盖进度

下表为原始全量审查覆盖记录；发现数是修复前的原始发现数。当前待处理项见下方 `Findings`。

| 分片 | 范围 | 状态 |
| --- | --- | --- |
| enemies | `scripts/enemies/**/*.cs`（32） | 已完成，发现 3 项 |
| player | `scripts/player/**/*.cs`（89） | 已完成，发现 5 项 |
| battle-ai-rules | `scripts/systems/battle/{ai,rules,terrain,fate,_interop,presentation}/**/*.cs`（80） | 已完成，发现 5 项 |
| battle-core-runtime | `scripts/systems/battle/{core,runtime,sim}/**/*.cs`（121） | 已完成，发现 5 项 |
| runtime-systems | `scripts/systems/{content,game_runtime,inventory,persistence,progression,settlement,world,attributes,fate}/**/*.cs`（96） | 已完成，发现 2 项 |
| ui-utils | `scripts/ui/**/*.cs`, `scripts/utils/**/*.cs`, `scripts/dev_tools/**/*.cs`（51） | 已完成，发现 5 项 |

## Findings

### 当前仍需处理

`[low] scripts/ui/BattleMapPanel.cs:2121` - 时间轴 tooltip 仍把换行写成 `"/n"`，运行时会显示字面 `/n` 而不是换行。建议修成 `\n`，并扩展 `tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs` 覆盖 timeline tooltip/detail 文本不包含 `/n`。

`[medium] scripts/systems/battle/sim/BattleSimRunReport.cs:6` - formal sim run report 仍把 `Metrics` 存成 `Godot.Collections.Dictionary`，`FinalUnits` 存成 raw `Godot.Collections.Array`。当前 getter/setter 已做 deep copy，能缓解外部 mutation，但 owner 仍不是 typed metrics/final-unit snapshot，`BattleSimReportBuilder` 仍依赖 `"skill_attempt_counts"`、`"skill_success_counts"`、`"factions"` 等字符串 key 回读。建议让 run report 持有 typed metrics/final-unit snapshot，`ToDictionary()` 只作导出，并补 mutation/schema-boundary regression。

`[medium] scripts/systems/battle/core/meteor_swarm/MeteorSwarmCommitResult.cs:11` - meteor commit result 的 `report_entries` 仍是 `List<GDictionary>`，`BattleSkillOutcomeCommitter` 仍直接 duplicate 后塞入 common outcome/batch。当前 `AddReportEntry(...)` 增加了 schema key 检查，但字段本身仍 public 且测试直接 `result.report_entries.Add(reportEntry)` 绕过检查；`tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.cs` 还在注入缺少 `"text"`、`"component_breakdown"`、`"target_summaries"`、`"terrain_summary"` 的 raw report。建议引入 typed meteor report entry DTO，在 batch/export 边界投影字典，并扩展回归覆盖不能绕过 schema。

### 子代理覆盖记录

- enemies：已审查 32/32 个文件；未覆盖文件：无。
- player：已审查 89/89 个文件；未覆盖文件：无。
- ui-utils：已审查 51/51 个文件；未覆盖文件：无。
- battle-core-runtime：已审查 121/121 个文件；未覆盖文件：无。
- battle-ai-rules：已审查 80/80 个文件；未覆盖文件：无。
- runtime-systems：已审查 96/96 个文件；未覆盖文件：无。

## 完成状态

- 6 个 GPT-5.5 xhigh 子代理分片均已完成。
- 所有子代理 findings 已按文件/行号做主线程复核；已确认修复项已从上方 `Findings` 移除。
- `scripts/**/*.cs` 覆盖总数为 469/469，无遗漏文件。

## 验证记录

- `dotnet build magic.csproj`：通过，0 warnings / 0 errors。
- 分片覆盖核对：无遗漏文件、无重复分配。

## 当前核对记录

日期：`2026-06-18`

- 已确认修复项已从上方 `Findings` 删除。
- 当前仅保留 3 项未完成或部分完成项。
- 本次核对验证：`dotnet build magic.csproj` 通过，0 warnings / 0 errors。
