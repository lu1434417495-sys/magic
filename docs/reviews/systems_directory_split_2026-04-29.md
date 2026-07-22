# `scripts/systems/` 目录拆分检视剩余观察

原始检视日期：`2026-04-29`  
当前代码复核：`2026-07-20`

原报告的数据源是 `scripts/systems/**/*.gd` 的 `preload(...)` 边。项目已迁移到 C#，原 preload 数量、热力图、行数和目录体量已不能表示当前依赖，因此已移除。复核时不是按语言直接判定过时，而是追踪了当前 C# 的同职责 owner。

## 已由当前结构覆盖

- 外层 `runtime/` 已改为 `scripts/systems/game_runtime/`，与 `scripts/systems/battle/runtime/` 的命名碰撞已消除。
- 战斗专用 fate 规则已进入 `scripts/systems/battle/fate/`，原报告中基于多条 GDScript preload 的大规模 `fate ↔ battle` 循环不再存在。
- `AttributeService` 已迁到 `scripts/systems/attributes/AttributeService.cs`，不再伪装成 progression owner。
- battle simulation 已通过 `BattleSimContentProvider` / `ContentSnapshot` 消费内容，当前 `scripts/systems/battle/sim/` 没有对 `GameSession` 的直接引用。
- 原顶层 `text_runtime/` 已收编到 `scripts/systems/game_runtime/headless/`。

## 剩余架构观察（非正确性 bug）

### 顶层 fate 仍保留一条 battle-specific 反向边

- `scripts/systems/fate/LowLuckRelicRules.cs` 仍直接读取 `BattleUnitState`，而 battle rules 又消费 `LowLuckRelicRules`。
- 因此原报告描述的大规模 GDScript preload 循环已消失，但“顶层 fate 包含 battle-specific 规则”的同类分层问题尚有一个残留。
- 当前全局 C# assembly 不会因这条边编译失败；它是目录/owner 债务，不是现时 correctness bug。

### Persistence 仍直接调用 character creation service

- 当前 owner：`scripts/systems/persistence/GameSession.CharacterCreation.cs`。
- `GameSession` 仍直接调用 `CharacterCreationService`来应用角色创建 payload 和计算初始属性。
- 这是有意的新游戏组装边界，目前没有发现由此导致的运行时缺陷。若未来要收紧 persistence 分层，可将新游戏组装迁到更高层 coordinator；不应将该观察作为当前 bug 修复。

## Project Context Units Impact

本次只清理过时检视数据，没有改变 runtime owner 或推荐读集，不需要修改 `docs/design/project_context_units.md`。
