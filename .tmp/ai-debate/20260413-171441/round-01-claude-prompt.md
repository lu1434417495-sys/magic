# Structured Design Debate

You are `{Claude}` participating in a structured technical debate for the repository at `{D:\game\magic}`.

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
- You are: {Claude}
- Counterpart: {Codex}
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
{### Current Position
- `单体野怪` 的世界层数据直接挂在 `scripts/systems/encounter_anchor_data.gd`；保留 `encounter_kind=single`、规则/模板引用、seed 等最小字段，胜利后沿用现有“移除锚点”链路。
- `聚落类野怪` 也以 `EncounterAnchorData` 为世界对象，但在其上挂专用 `settlement_state`，只存 `site_id`、`growth_level`、`suppressed_until`、`last_growth_time` 这类运行时字段，别把成长状态塞进 `WildSpawnRule` 或 `EnemyTemplateDef`。
- 世界时间推进应落到新 `scripts/systems/wild_encounter_progression_system.gd`；`GameRuntimeFacade` / `WorldMapSystem` 只转发 `advance_world_time()` 与战斗结算结果，不承载成长规则。
- “同种族普通敌人 + 特殊职业敌人”应放到新的 roster 配置层；`EnemyTemplateDef` 继续表示单体敌人模板，世界状态只记录当前成长阶段，不记录编队规则。
- 聚落打完采用“抑制 + 降级 + 定时恢复”：不永久删世界对象，也不做纯降级常驻；同一 anchor 进入 `suppressed`，回到基线层级，冷却后恢复并继续增长。

### Critique Or Risks
- 如果当前存档只序列化扁平 anchor 字段，`settlement_state` 需要做成可序列化资源；否则第一版先用 `EncounterAnchorData` 平铺字段更稳。
- `WildSpawnRule` 最多新增 `encounter_kind`、`roster_profile_id`、初始成长参数，不要承担 live state，否则生成规则和世界状态会缠死。
- `encounter_roster_builder.gd` 如果现在只有“模板直出”路径，第一版应加分支，不要顺手重写整套 builder。

### Revised Plan
1. 第一实现切片先做数据分流：在 `scripts/systems/encounter_anchor_data.gd` 增加 `encounter_kind`、`roster_profile_id`、`site_id`，并为聚落增加 `growth_level`、`suppressed_until`、`last_growth_time` 这些字段或一个 `settlement_state` 子资源。这样单体和聚落先在世界层分家，但不改现有 runtime 骨架。
2. 在 `WildSpawnRule` 对应定义里只补“生成哪一类锚点 + 用哪个 roster 配置 + 初始成长值”。`scripts/systems/world_map_spawn_system.gd` 负责按规则创建两类 `EncounterAnchorData`，其中单体仍是一次性遭遇，聚落则是持久锚点。
3. 新增一层 encounter 级配置，例如 `data/configs/` 下的 `wild_settlement_roster_def`。它描述基础族群模板、人数成长曲线如 `2/3/4/5`，以及职业解锁阈值如 `growth>=3` 出 `wolf_alpha`。`EnemyTemplateDef` 不承载这些成长规则；如果当前模板缺少“种族/职业”标识，只在 `scripts/enemies/enemy_template_def.gd` 做最小字段补充。
4. 修改 `scripts/systems/encounter_roster_builder.gd`：`single` 继续走当前模板/轻微波动逻辑；`settlement` 则读取 `roster_profile_id + growth_level`，先铺同种类普通敌人，再按阈值追加特殊职业敌人。`scripts/enemies/enemy_content_registry.gd` 继续做模板查找，不建议把聚落成长塞进去。
5. 新增 `scripts/systems/wild_encounter_progression_system.gd`，专门处理 `advance_world_time(delta)` 和 `apply_battle_result(anchor_id, result)`。世界时间前进时它只扫聚落锚点并推进成长；战斗胜利时，单体按现状移除锚点，聚落则写入 `suppressed_until`、回退到基线成长、暂时失活，冷却结束后重新激活同一个世界对象。
6. 最低测试清单应覆盖：单体胜利后锚点被移除；聚落胜利后锚点保留但进入 suppressed；世界时间推进只影响聚落成长；成长阈值正确解锁特殊职业敌人；聚落 suppression 结束后重新可战；存档读档后 `growth_level` / `suppressed_until` 不丢。核心改动文件至少是 `scripts/systems/encounter_anchor_data.gd`、`scripts/systems/world_map_spawn_system.gd`、`scripts/systems/encounter_roster_builder.gd`、`WildSpawnRule` 定义文件，以及新增的 `scripts/systems/wild_encounter_progression_system.gd` 和对应 roster def 文件；`GameRuntimeFacade` / `WorldMapSystem` 只做薄转发。

### Remaining Disagreements
- none}

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
