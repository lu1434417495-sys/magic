# Structured Design Debate

You are `{Codex}` participating in a structured technical debate for the repository at `{D:\game\magic}`.

## Mission
Answer the user's question by converging on the strongest practical implementation plan for this specific codebase.
Use the same language as the user's question when obvious. Otherwise use concise Chinese.

## Guardrails
- This is discussion only. Do not modify files.
- You may inspect the repository to ground your answer.
- Prefer concrete references to this repo's actual structure over generic advice.
- Do not repeat settled points.
- If the counterpart is correct on a point, explicitly absorb it.
- Keep the answer concise and technical.

## Debate Context
- Round: {1} / {1}
- You are: {Codex}
- Counterpart: {Claude}
- Extra context paths: {(none)}

## User Question
{# 大地图野怪系统方案讨论

请基于当前仓库的真实结构，讨论并收敛一个可实施的大地图野怪系统设计。

## 目标

我希望大地图野怪分成两类：

1. `单体野怪`
   - 表现为普通世界遭遇点。
   - 进入战斗时生成固定或轻度波动的敌人数量。
   - 战斗胜利后可以直接清除该遭遇点。

2. `聚落类野怪`
   - 表现为一种“敌方据点/巢穴/营地”式的世界对象，不是一次性普通遭遇点。
   - 会随着世界时间轴推进而增长“战斗内敌人数量”。
   - 当数量成长到某个阈值后，会出现“同种族但带特殊职业”的敌人。
   - 例子：
     - 狼群营地早期只有普通狼。
     - 时间推进后，战斗中敌人数从 2 增长到 3、4、5。
     - 达到阈值后，出现“荒狼群头目/荒狼群祭司”之类的特殊职业单位，但仍属于同一怪物族群。

## 讨论重点

请结合当前仓库已有系统，重点回答下面问题：

1. 这两类野怪的世界层数据应分别挂在哪里：
   - 继续复用 `EncounterAnchorData`
   - 还是新增独立的“野怪聚落状态”数据对象
2. 世界时间推进应如何影响聚落类野怪：
   - 是给每个聚落类野怪维护 growth 状态
   - 还是由全局系统按 tick/step 统一推进
3. 聚落类野怪被玩家反复挑战后，世界层对象是否应保留：
   - 直接移除
   - 标记清空后重建
   - 保留但降低威胁等级
4. “同种类敌人 + 特殊职业敌人”最适合挂在哪一层：
   - `EnemyTemplateDef`
   - 新的 encounter/roster 配置层
   - 世界对象状态层
5. 如何避免把过多逻辑直接塞进：
   - `GameRuntimeFacade`
   - `WorldMapSystem`
   - `BattleRuntimeModule`
6. 首个最小实现切片应该是什么，才能最快验证方案正确：
   - 只做数据结构
   - 只做一种聚落类野怪
   - 只做敌人数增长，不先做特殊职业
   - 还是一次补齐最小可玩闭环

## 已知仓库事实

- 世界遭遇生成入口在 `scripts/systems/world_map_spawn_system.gd`
- 世界层遭遇对象当前是 `scripts/systems/encounter_anchor_data.gd`
- 进入战斗后，遭遇对象会传入 `BattleRuntimeModule.start_battle(...)`
- 当前普通遭遇打完后，运行时会移除对应遭遇锚点
- 敌方编队当前主要通过：
  - `scripts/systems/encounter_roster_builder.gd`
  - `scripts/enemies/enemy_template_def.gd`
  - `scripts/enemies/enemy_content_registry.gd`
- 当前 `EnemyTemplateDef` 只有单个模板的基础敌人数、技能、属性、AI brain 等信息
- 当前 `WildSpawnRule` 主要描述区域、模板 id、密度、视野、chunk 分布，还没有聚落类野怪成长状态

## 我希望得到的结论格式

请务必给出：

1. 推荐的数据分层方案
2. 推荐的运行时推进方案
3. 推荐的最小实现顺序
4. 需要修改的核心文件列表
5. 最值得先补的测试列表

## 额外约束

- 方案要尽量贴合当前仓库，不要重写整个 world runtime。
- 保持“单体野怪”和“聚落类野怪”两条链路都清晰。
- 优先小步落地，避免一次改太多系统。
- 请指出哪些逻辑应放在 `scripts/systems/`，哪些应放在 `scripts/enemies/`，哪些应放在 `scripts/utils/` 或 `data/configs/`。}

## Your Previous Position
{No prior position yet.}

## Counterpart Latest Position
{No counterpart position yet.}

## Required Output
Use exactly these sections:

### Current Position
- 3 to 6 bullets

### Critique Or Risks
- 0 to 4 bullets
- If none, write `- none`

### Revised Plan
1. Ordered steps grounded in this repo
2. Keep it practical
3. Mention the first implementation slice

### Remaining Disagreements
- bullets
- If none, write `- none`
