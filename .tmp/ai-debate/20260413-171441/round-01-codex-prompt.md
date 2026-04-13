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
{# 大地图野怪系统

请基于当前仓库真实结构，给出一个可落地的设计。

目标：
- 野怪分两类：`单体野怪`、`聚落类野怪`
- 单体野怪：普通遭遇点，进战斗后敌人数固定或轻微波动，打完可直接清除
- 聚落类野怪：敌方营地/巢穴，世界时间推进时会增长战斗内敌人数
- 聚落类野怪成长到阈值后，会出现同种族的特殊职业敌人
  - 例：狼营地前期只有普通狼，后期人数从 2 增到 3/4/5，再出现狼头目或狼祭司

请重点回答：
1. 单体野怪和聚落类野怪的世界层数据分别挂在哪里
2. 世界时间推进怎么设计，避免把逻辑塞进 `GameRuntimeFacade` / `WorldMapSystem`
3. 聚落类野怪打完后，世界对象是移除、降级、抑制后重建，还是别的方案
4. “同种类敌人 + 特殊职业敌人”该挂在 `EnemyTemplateDef`、新的 roster 配置层，还是世界状态层
5. 最小实现切片怎么切
6. 需要修改的核心文件和测试列表

仓库事实：
- 世界遭遇生成：`scripts/systems/world_map_spawn_system.gd`
- 世界遭遇对象：`scripts/systems/encounter_anchor_data.gd`
- 当前普通遭遇打完后会移除对应遭遇锚点
- 敌方编队主要走：
  - `scripts/systems/encounter_roster_builder.gd`
  - `scripts/enemies/enemy_template_def.gd`
  - `scripts/enemies/enemy_content_registry.gd`
- `EnemyTemplateDef` 当前更像“单个敌方单位模板/族群模板”，还没有聚落成长状态
- `WildSpawnRule` 当前主要是区域/模板/密度规则，还没有聚落成长状态

约束：
- 尽量贴合当前仓库，不重写整套 world runtime
- 保持两类野怪链路清晰
- 优先小步落地}

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
