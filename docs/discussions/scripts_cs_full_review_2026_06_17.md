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

截至 2026-06-19 typed-boundary 修复计划 Task17，本节原先列出的 3 项当前 findings 均已处理；本轮最终弱边界扫描未新增需要在本计划内阻断完成的非边界弱类型 owner。

### 2026-06-19 typed-boundary 修复核对

- `scripts/ui/BattleMapPanel.cs` 时间轴 tooltip literal `"/n"` 已在 Task6 修复，并由 `tests/battle_runtime/ui/run_battle_board_ui_small_regression.cs` 覆盖。
- `scripts/systems/battle/sim/BattleSimRunReport.cs` 的 metrics owner 已在 Task16 改为 `BattleSimMetricsSnapshot` / `BattleSimUnitMetricsSnapshot`，`BattleSimReportBuilder` 与 `BattleSimTraceSummaryBuilder` 直接消费 typed snapshot；`Metrics` 属性只保留为 final report payload 边界投影。`FinalUnits` 仍属于 battle-sim final report/export payload，不作为 gameplay runtime state owner。
- `scripts/systems/battle/core/meteor_swarm/MeteorSwarmCommitResult.cs` 的 meteor 战报条目已在 Task17 审计修复中改为 `MeteorSwarmReportEntry` typed DTO；`BattleSkillOutcomeCommitter` 只在 final battle report payload 边界通过 `MeteorSwarmProjection.Project(...)` 输出 `GDictionary`。
- Task17 弱边界扫描命令：`rg -n "\\bGDictionary\\b|Godot\\.Collections\\.Dictionary|\\bVariant\\b|\\.Call\\(|\\.Get\\(" scripts --glob '!*.uid'`。扫描仍覆盖大量 save/content/UI/projection/final report 边界；动态 `.Get` 复核后主要为 typed collection `Get(...)`，剩余 `SubmapEntryWindow` 与 `BattleSimOverrideApplier` 分别属于 UI 属性绑定和 battle-sim override 配置边界。
- 本轮仍允许的 report/export 边界包括 `BattleCommonSkillOutcome.report_entries` 与 `BattleEventBatch.ReportEntriesTyped`，它们承载最终战斗报告 payload，不拥有玩法状态；meteor special-profile 正式链已不再通过 `MeteorSwarmCommitResult.report_entries` 暴露可绕过 schema 的 raw list。

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
