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
- Round: {1} / {2}
- You are: {Claude}
- Counterpart: {Codex}
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
{### Current Position
- `单体野怪` 继续复用 `EncounterAnchorData`，沿用当前“战后移除锚点”的链路；只补一个最小分类字段，例如 `encounter_kind`，区分普通遭遇和聚落。
- `聚落类野怪` 不建议把成长、清空、恢复都塞进 `EncounterAnchorData`。更合适的是：`EncounterAnchorData` 仍表示世界对象实例，新增独立的 `WildSettlementState` 放在 `scripts/systems/`，保存 `site_id / def_id / growth_level / threat_level / last_advanced_step / suppressed_until_step` 这类状态。
- 世界时间推进应采用“全局推进 + 每个聚落持有状态”的组合：不要每个聚落自己跑 timer；由统一系统按 world tick/step 推进所有 settlement state。
- “同种类敌人 + 特殊职业敌人”应挂在新的 encounter/roster 配置层，而不是 `EnemyTemplateDef`。`EnemyTemplateDef` 继续只描述单个单位模板；聚落配置只描述族群构成、人数成长档位、specialist 解锁阈值。
- 聚落战后不应直接移除世界对象。更稳妥的是保留对象并降低威胁等级；降到 0 后标记 `suppressed/cleared`，后续再按时间重建。
- 逻辑归位：`scripts/systems/` 放聚落状态、时间推进、战后结算；`scripts/enemies/` 只放敌人模板与注册；`data/configs/` 放聚落定义；`scripts/utils/` 只放通用 helper，不放领域规则。

### Critique Or Risks
- 终端在本轮无法继续展开源码细读，以下结论是基于你列出的真实文件链路收敛；如果仓库里已经有现成的 world time 分发点，应直接复用，不要新造总线。
- 如果 `encounter_roster_builder.gd` 当前强依赖 `EnemyTemplateDef` 内的基础人数，第一步要先把“最终出战人数决定权”上提到 roster builder，否则聚落成长会被模板层绑死。
- 如果普通遭遇“战后移除 anchor”的逻辑现在写死在 `BattleRuntimeModule` 或其直接调用方，聚落链路必须先抽一个结算分支，否则后面还会继续膨胀大模块。

### Revised Plan
1. 先定数据分层，不先重写大入口。  
   在 `scripts/systems/` 新增 `wild_settlement_state.gd` 和 `wild_settlement_def.gd`；`EncounterAnchorData` 只新增最小字段，如 `encounter_kind`、`site_state_id` 或 `site_def_id`。单体野怪仍只靠 anchor 跑完整链路。

2. 把聚落编队规则放进新配置层。  
   在 `data/configs/` 增一份最小 `wolf_camp` 配置：普通族群 template ids、人数成长档位、后续 specialist 阈值表。`scripts/enemies/` 只补必要的敌人模板，例如狼头目、狼祭司，但不承载成长规则。

3. 第一实现切片做“一个聚落 + 人数成长 + 战后保留”，先不做特殊职业。  
   这是最快验证方案正确的闭环，不建议只做数据结构，也不建议第一次就补齐全部玩法。具体做法是：`world_map_spawn_system.gd` 生成一个 `encounter_kind = settlement` 的 anchor；`encounter_roster_builder.gd` 根据 `WildSettlementState.growth_level` 把狼群人数从 2 提到 3、4；战斗胜利后不删 anchor，而是降低 threat/growth。

4. 再补统一推进系统，而不是给每个据点挂独立增长逻辑。  
   新增 `wild_settlement_progression_system.gd`，由现有的 world tick/step 入口驱动，按 step 推进所有 settlement state，并记录 `last_advanced_step` 防止重复推进。`WorldMapSystem` 只负责转发 step，不负责算成长公式。

5. 第二阶段再接 specialist。  
   扩展 `encounter_roster_builder.gd`：当 `growth_level` 或 `threat_tier` 达阈值时，从 `WildSettlementDef` 的 specialist 配置中插入 `wolf_alpha` / `wolf_shaman` 之类模板。状态层只存 tier，不存具体编队明细。

6. 战后结算单独收口，避免继续往大模块里塞。  
   最好在 `scripts/systems/` 增一个轻量的世界遭遇结算职责点：普通遭遇执行“移除 anchor”，聚落执行“降低威胁/标记 suppressed”。`GameRuntimeFacade` 只做编排，`BattleRuntimeModule` 只负责 battle 生命周期。

7. 需要修改的核心文件列表。  
   `scripts/systems/encounter_anchor_data.gd`  
   `scripts/systems/world_map_spawn_system.gd`  
   `scripts/systems/encounter_roster_builder.gd`  
   `scripts/systems/world_map_system.gd` 或现有 world time 入口  
   `BattleRuntimeModule` 的战后回写衔接点  
   新增 `scripts/systems/wild_settlement_state.gd`  
   新增 `scripts/systems/wild_settlement_def.gd`  
   新增 `scripts/systems/wild_settlement_progression_system.gd`  
   新增 `data/configs/` 下的聚落配置资源

8. 最值得先补的测试。  
   `EncounterRosterBuilder`：同一 `wolf_camp` 在 growth 0/1/2 时分别产出 2/3/4 只狼。  
   世界推进：同一 step 不重复增长，跨 step 正确 catch-up。  
   战后结算：普通遭遇胜利后 anchor 被移除；聚落胜利后 anchor 保留且 threat 降低。  
   阈值解锁：第二阶段再测达到阈值后 roster 中出现 specialist template。  
   持久化：`WildSettlementState` 存档/读档后 growth 与 suppressed 状态不丢。

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
