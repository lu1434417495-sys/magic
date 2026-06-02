# `scripts/systems/` 目录拆分检视

更新日期：`2026-04-29`

## 目的

对照 `docs/design/project_context_units.md` 的 CU 划分，扫一遍 `scripts/systems/` 各顶层目录之间的真实依赖（preload 边），定位拆分上的问题与不一致。

## 数据口径

- 扫描范围：`scripts/systems/**/*.gd` 中所有 `preload("res://scripts/systems/...")`。
- 总 preload 数：290。
- 跨顶层目录边数：76（约 26%）。
- 自引用（同顶层目录内的 preload）不计入下述边表。

## 顶层依赖热力（src → dst 跨顶层 preload 数）

```
runtime      → battle        16
battle       → progression   12
fate         → battle        10
battle       → fate           7
runtime      → world          5
persistence  → world          3
runtime      → inventory      3
runtime      → fate           3
battle       → world          2
persistence  → progression    2
runtime      → progression    2
runtime      → settlement     2
battle       → inventory      1
battle       → persistence    1
progression  → inventory      1
progression  → fate           1
text_runtime → persistence    1
text_runtime → battle         1
text_runtime → world          1
world        → battle         1
world        → progression    1
```

## 真正的问题

### 1. 命名碰撞：`systems/runtime/` vs `systems/battle/runtime/`

- 外层 `runtime/`：`game_runtime_facade.gd`（3007 行）、`battle_session_facade.gd` 等世界/战斗总编排。
- 内层 `battle/runtime/`：`battle_runtime_module.gd`（4143 行）等战斗执行编排。
- 看 import 路径 `scripts/systems/runtime/...` 时无法直觉判断是哪一层。

**建议**：外层重命名为 `game_runtime/` 或 `world_runtime/`，让两个 runtime 在路径上自带区分。改动是一次性的；现在的代价是每次跨层调试都要重新核对位置。

### 2. `fate/` ↔ `battle/` 双向循环

- `fate/` 引 `battle/core`（10 条）；
- `battle/rules`、`battle/runtime` 反过来引 `fate/`（7 条）。

`fate/` 下的几个文件名已经在自首：

- `battle_fate_attack_rules.gd`
- `battle_fate_event_bus.gd`
- `fate_attack_formula.gd`

这些都是战斗专用的 fate 子层，错放在了 `fate/` 顶层。

**建议**：把上述战斗专用的 fate 文件迁到 `battle/rules/`（或新建 `battle/fate/` 子目录）。让 `fate/` 只留与战斗外可复用的命运/低运/信仰/相位规则。这样依赖就单向：`battle → fate`，不再循环。

### 3. `progression/attribute_service.gd` 被当成共享库

被 `battle/core`、`battle/rules`、`battle/runtime`、`battle/sim`、`world/encounter_roster_builder` 多处 preload。它叫 service，但实际被当作数据/属性查找层用。后果是 `progression/` 事实上变成了二级共享层，CU 划分失真。

**建议**：

- 要么把它降级为纯数据/计算工具，搬到 `scripts/utils/`（或新建 `scripts/systems/attributes/`）；
- 要么在调用方改用 DTO，不再直接 preload 该 service。

### 4. `battle/sim` → `persistence/game_session.gd`

模拟器/回归 runner 直接 preload 了存档单例。如果只是为了拿内容注册表，应改走显式注入或经 facade 暴露，避免把 sim 与 live save 状态绑死。

### 5. `text_runtime/` 体量与位置

`text_runtime/` 只有 3 个文件（headless session + command runner + result），却要依赖 5 个其他顶层目录。本质是 `runtime/` 的 headless 驱动同胞，但摆在和 `progression/`（14 文件）、`world/`（11 文件）、`battle/`（多子目录）并列的层级上，权重不匹配。

**建议**：

- 收编到 `runtime/headless/`；或
- 保留顶层但在文档与文件注释里明确「driver / harness」语义。

### 6. `persistence/` → `progression/character_creation_service.gd`

存档层调用了角色创建服务。restore 时补默认值是合理的，但意味着 `persistence/` 也兼了「初始世界引导」职责。这一点目前没在 `project_context_units.md` 明说，建议要么补文档，要么把引导逻辑迁出。

## 不需要动的

- `battle/{core,rules,runtime,terrain,ai,sim}` 内部分层（数据 → 规则 → 执行 → 变体）干净，无循环。
- `world/`、`settlement/`、`inventory/`、`progression/` 的内部聚合度正常。
- `runtime → battle/world/inventory/settlement/...` 高扇出符合 facade 模式，是预期的。
- `runtime/` 内部 16 个文件按 facade + 各 command handler / writeback / loot commit / snapshot 拆分，结构清晰。

## 优先级

| 等级 | 项目 |
| --- | --- |
| 高 | 1（rename `runtime/`）、2（拆解 fate ↔ battle 循环）|
| 中 | 3（`attribute_service` 重新定位）|
| 低 | 4（sim 触 persistence）、5（text_runtime 位置）、6（persistence 触角色创建）|

## 数据来源

依赖图通过遍历 `scripts/systems/**/*.gd`、解析 `preload("res://scripts/systems/<top>/...")`、按顶层目录聚合得到。如需复跑，可临时写一个一次性脚本统计；本次扫描的原始 290 条 preload 中，64 条为同顶层目录内引用，76 条跨顶层目录，剩余 150 条为同文件内 / 子目录内自引用形式。
