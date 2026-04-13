# Final Synthesis

You are `{Claude}`, the final synthesizer for a Codex vs Claude design debate on the repository at `{D:\game\magic}`.

## Mission
Produce the best actionable conclusion for the user's question using the debate transcript below.
Use the same language as the user's question when obvious. Otherwise use concise Chinese.

## Guardrails
- This is a recommendation, not implementation.
- Ground the recommendation in this repository's actual structure.
- Prefer decisions that reduce risk and keep the next coding slice small.
- If the debate left real uncertainty, name it clearly instead of pretending consensus.

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

## Debate Transcript
{## Round 1

### Codex
### Current Position
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
- none

### Claude
Now I have enough concrete detail to give a grounded position.

### Current Position

- 与 Codex 整体对齐。补充基于实际代码的细节：
  - `EncounterAnchorData` 是 `RefCounted`，已有平铺的 `to_dict`/`from_dict` 模式（`encounter_anchor_data.gd:28-51`）。**第一切片用平铺字段**（`encounter_kind`, `growth_level`, `suppressed_until`），不用子资源，序列化零改动。
  - **`_remove_active_battle_encounter_anchor()`（`game_runtime_facade.gd:1689`）是唯一的战后移除锚点入口**，也是 single vs settlement 分支的精确切入点——single 继续走当前删除路径，settlement 在此改写 `suppressed_until` 和 `growth_level` 而不删。
  - `EncounterRosterBuilder._build_template_enemy_units()` 已在 line 65 支持 `build_context["enemy_unit_count"]` 覆盖。**聚落成长路径只需把 `growth_level` 换算成 count 注入 context**，不需要改 `EnemyTemplateDef.enemy_count`。特殊职业敌人追加第二批单位即可，复用现有 `_build_template_enemy_units` 调用。
  - Line 1123-1125 过滤 `is_cleared` 控制渲染可见性。抑制态的聚落需要同步加 `is_suppressed` 检查，否则玩家仍能看见、触发已"抑制"锚点的遭遇。
  - `WildSpawnRule` 是 `extends Resource` 带 `@export`，直接新增 `@export var encounter_kind: String = "single"` 和 `@export var roster_profile_id: StringName`，Godot 编辑器和 `.tres` 都能直接序列化，无需迁移旧配置。

### Critique Or Risks

- Codex 未指定 `advance_world_time()` 的**触发时机**。当前 `GameRuntimeFacade` 没有显式世界时间概念；第一切片最简单的方案是**玩家每移动 N 格触发一次**，比实时计时器更易测试，比"明确休息"更自然。需在设计里定下，否则 `WildEncounterProgressionSystem` 接口不好写。
- `_encounter_anchor_by_coord` 是坐标键查找表（`game_runtime_facade.gd:69`）。抑制期的聚落锚点若留在坐标表里，玩家踩上去仍会触发遭遇检查。**需要在 `is_suppressed` 时从坐标表排除，抑制结束后重新插入**——这个重新激活路径要在 `WildEncounterProgressionSystem` 里包含，而不是仅仅改 `is_suppressed` 字段。

### Revised Plan

1. **`WildSpawnRule`** 新增两个 `@export` 字段：`encounter_kind: String = "single"` 和 `roster_profile_id: StringName`（聚落类才填）。`.tres` 配置现有规则不需改，默认值回退到 single 行为。

2. **`EncounterAnchorData`** 平铺新增字段：`encounter_kind: StringName`、`growth_level: int`、`suppressed_until: float`（世界时间戳）、`roster_profile_id: StringName`。`to_dict`/`from_dict` 各加四行，存档向前兼容靠 `data.get("...", default)` 覆盖。

3. **`WorldMapSpawnSystem._build_encounter_anchor()`（line 578）** 接收 `WildSpawnRule` 而不只是零散参数，把 `encounter_kind` 和 `roster_profile_id` 写进新锚点。调用处 `_generate_encounter_anchors` 里 line 419 传入 rule 对象。

4. **新增 `data/configs/wild_settlement_roster_def.gd`（Resource）**，字段：`roster_profile_id`, `base_template_id`（普通族群），`growth_counts: Array[int]`（例 `[2,3,4,5]` 对应 level 0-3），`elite_unlock_level: int`, `elite_template_id: StringName`。不放进 `EnemyTemplateDef`，不放进 `WildSpawnRule`。对应 `.tres` 放 `data/configs/wild_settlement/`。

5. **`EncounterRosterBuilder`** 新增 `build_settlement_enemy_units(anchor, roster_def, skill_defs, …)` 方法：根据 `anchor.growth_level` 查 `roster_def.growth_counts` 算出普通兵数，调现有 `_build_template_enemy_units` 带 count 覆盖；若 `growth_level >= elite_unlock_level` 再取 `elite_template_id` 追加一个单位。`build_enemy_units()` 顶层按 `anchor.encounter_kind` 分支，settlement 走新方法。

6. **新增 `scripts/systems/wild_encounter_progression_system.gd`**，两个公开方法：
   - `advance_world_time(anchors: Array, world_time: float)` — 遍历 `encounter_kind == "settlement"` 且 `suppressed_until <= world_time` 的锚点，按 roster_def 配置推进 `growth_level`（有上限）。
   - `apply_battle_result(anchor, outcome: String, world_time: float)` — 胜利时写 `suppressed_until = world_time + cooldown`，`growth_level = max(0, growth_level - 1)`；锚点不删。
   
   `GameRuntimeFacade` 只做：（a）胜利时调 `_progression_system.apply_battle_result(anchor, "victory", _world_time)` 替换原有 `_remove_active_battle_encounter_anchor()`（single 继续删，settlement 走新路径）；（b）玩家每移动 N 格调 `advance_world_time()`；（c）抑制判定时同步更新 `_encounter_anchor_by_coord`。

7. **最小测试切片**（`tests/` 新增 `run_wild_encounter_progression_tests.gd`）：
   - single 胜利 → anchor 从 `_world_data` 删除
   - settlement 胜利 → anchor 保留，`is_suppressed` 为真，从坐标表移除
   - `advance_world_time` 只影响 settlement 锚点 growth
   - growth 到阈值 → 编队包含 elite 单位
   - suppression 结束后锚点重回坐标表
   - 存档 round-trip 后 `growth_level`/`suppressed_until` 不丢

**核心修改文件**：`encounter_anchor_data.gd`、`wild_spawn_rule.gd`、`world_map_spawn_system.gd`（`_build_encounter_anchor` 签名）、`encounter_roster_builder.gd`（分支 + settlement 方法）、`game_runtime_facade.gd`（`_remove_active_battle_encounter_anchor` 分支 + 坐标表刷新）；**新增**：`wild_encounter_progression_system.gd`、`wild_settlement_roster_def.gd`、对应 `.tres` 配置、测试脚本。

### Remaining Disagreements

- none}

## Required Output
Use exactly these sections:

### Recommended Plan
Short paragraph.

### Why This Plan
- 2 to 5 bullets

### Open Risks
- bullets
- If none, write `- none`

### First Build Slice
1. 3 to 6 concrete steps
