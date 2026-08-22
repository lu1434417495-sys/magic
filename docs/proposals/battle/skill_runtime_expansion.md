# 战斗技能系统规则扩展提案（Phase 5+）

> 状态：`Proposal / Partially landed`
> 更新日期：`2026-08-16`

## 定位

本文**只记录尚未落地的规则扩展**，是当前实现之外的后续方案。

战斗技能系统的当前实现真相（资源链路、命中/豁免/伤害结算、状态语义、范围计算、AI 评分、所有权边界）以 [`../../design/battle/skill_runtime.md`](../../design/battle/skill_runtime.md) 为准；本文中的任何现状描述都不构成当前合同。上下文单元见 [`../../design/project_context_units.md`](../../design/project_context_units.md) 的 CU-13 / CU-15 / CU-16。

设计红线（沿用且仍然有效）：

- 技能定义以 `SkillDef -> CombatSkillDef -> CombatEffectDef` 为唯一真相源，不另起平行系统
- 新规则必须落在通用 service/table 层，不得按技能 id、装备 id 或职业 tag 分支
- 命中/暴击/豁免真相源收敛后再加新字段，避免双入口
- 命中模型扩展不升 `SAVE_VERSION`，不重命名 `hit_rate / evasion` 属性

## 历史阶段回顾（已关闭）

| 阶段 | 结果 | 备注 |
| --- | --- | --- |
| Phase 0–3 规则底座与技能池 | ✅ 已落地并超额 | 技能资源已扩展到 700+ `.tres`，内容侧复用同一 effect/status/shape 模板 |
| Phase 4 优势/劣势 | ✅ 已落地（实现路径与本文件原计划不同） | 双骰取高/取低在 `scripts/systems/battle/rules/BattleHitResolver.cs`（`_roll_attack_die` + `NormalizeAdvantageState`）；来源合成在 `BattleAttackCheckPolicyService` 的 modifier bundle，由状态（`AttackRollAdvantage`）与装备能力驱动，而非新建 `battle_roll_disposition_resolver` 文件；回归为 `tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs`、`tests/battle_runtime/ai/run_battle_ai_advantage_behavior_regression.cs`、`tests/battle_runtime/fate/run_fate_attack_formula_regression.cs`；HUD 劣势文案见 `BattleHudAdapter` |
| Phase 6 前半：Saving Throw | ✅ 已提前落地 | `scripts/systems/battle/rules/BattleSaveResolver.cs`（法术 DC 基数 8 + 属性修正 + save advantage state）+ `BattleDamageResolver.SaveBranch.cs`；回归为 `tests/battle_runtime/runtime/run_battle_save_resolver_regression.cs`、`tests/progression/schema/run_battle_save_skill_schema_regression.cs` |
| 边墙（authored edge feature） | ✅ 已移除并完成残留清理（2026-08-16） | 格上静态边特征（墙/门/闸门）、接缝墙地图模板与 `edge_clear` 效果已删除。残留清理同日完成：`BattleUnitLineOfSightRules`（恒 true）整个文件删除、4 个退化调用点收敛；`BattleCellState.edge_feature_east/south` 从 state 与存档 schema 移除（SaveVersion 18→19）；`blocks_los` / `feature_blocks_los` 虽已无消费者但**保留**——移除 `ApplyEdgeFeatureActionPayloadDef` 的该 `[Export]` 会让 `run_resource_validation_regression` 以约 80% 概率在 GC finalizer 崩溃（`Handle is not initialized`；实测与 `.tres` 引用无关，清 `.godot` 缓存也无效，恢复该 export 后 5/5 通过），改由 validator fail-closed 拒绝内容配置；`BattleDirectionalPiercingPlan.BlockedBeforeCoord` 不可达链与 `BattleEdgeService.HasFeatureBetween` 孤儿方法删除。边界阻断唯一语义是虹光法球系的 layered barrier。虚空斧 `apply_edge_feature` 临时边特征保留，是 runtime feature face 的唯一来源。**不要顺手删 `CombatSkillDef.requires_los`**：它不属于这条链路，仍在门禁地面技能的 barrier 穿越校验 |
| `mage_passwall` 效果体 | ⚠️ 空壳待接回（2026-08-16） | 移除边墙时该技能曾被一并删除，导致虹光法球绿色层 `breaker_skill_ids` 变空、永久无法破解（破层唯一提交点 `BattleBarrierService._BreakActiveLayer` 的两个调用方都以 `_SkillBreaksLayer` 为门，`passage_outcomes` 不破层）。已恢复技能与 green.tres 绑定，但 `edge_clear` 无运行时、cast variant 的 `effect_defs` 目前为空。**空效果体不会真正破层**：ground 目标走 `BattleBarrierService` 的地面效果裁剪路径，该路径要求技能自身的单位/地形效果产生跨界地格，零效果时直接 `continue`。待规划中的 R4 格级障碍物落地后，用「移除格级障碍物」效果接回该技能；在此之前绿色层实战仍不可破。另注：当前没有任何职业/书籍/任务授予 `mage_passwall`，接回时需一并补授予渠道 |

原计划中技能级 `roll_disposition` 导出字段**未实现且已被替代**：优势/劣势经状态与装备能力进入攻击检定（内容示例：`warrior_one_inch_advantage.tres`、`weapon_sword_cowardice_scurry.tres`），`CombatSkillDef` 没有也不计划保留该字段，除非下节决策项另有结论。

## 剩余工作

### R1. 战场情境表（原 Phase 5，主要待办）

目标：把战场位置因素统一注入攻击检定，让玩家感到"数值差距在棋盘上可感知"。

注入通道**已存在**：`AttackCheckInput.SituationalAttackBonus / SituationalAttackPenalty`（`scripts/systems/battle/core/AttackCheckInput.cs`）已被 `BattleHitResolver` 消费，当前由 `flat_bonus / flat_penalty` 与状态攻击加值 delta 喂入。缺的是四个战场来源的计算：

| 来源 | 语义 | 建议归属 |
| --- | --- | --- |
| 高地 | 攻击方格高于目标格时获得 attack bonus | `BattleGridService` 提供高度差查询，`BattleAttackCheckPolicyService` 合成为 situational bonus |
| 掩体 | 防御方站在树（`base_terrain = forest`）格时获得 AC 加成 | `BattleTerrainRules` / `BattleGridService` 查询目标格地形，命中侧归入 `target_armor_class` 分量 |
| 贴身远程 | 远程技能/武器在被敌方贴身时受 attack penalty | `BattleHitResolver` / policy service 按 `projectile_kind` 与邻接敌情判定，不从技能 id 推断 |
| 包夹 | 多个友军毗邻目标时提供 attack bonus | policy service 按目标邻接友军计数 |

配套要求：

- 掩体载体已定为**树（森林地形格）**：`BattleTerrainKind.Forest` 已存在且由 `BattleTerrainGenerator` 正常生成，无需新增内容标注。原候选的边特征 `low_wall` 已从 `BattleEdgeFeatureKind` 移除（2026-08-16），掩体不走边特征路线。数值惯例参考 [`dnd35e_combat_vision.md`](dnd35e_combat_vision.md) §12（硬掩体 +4 AC、高地 +1/-1），落地时再定是否原样采用
- `BattlePreview.hit_preview` 增加 `situational_sources[]` 结构化明细，HUD 展示来源文案
- `BattleAiScoreService` 把情境 bonus 纳入预期命中率；AI 走位已有的 `high_ground` 模式（`BattleAiMoveToAdvantageActionEvaluator`）目前只影响 AI 评分、不影响真实命中，落地后两者必须同源
- 回归：覆盖四来源各自独立与叠加、与天然 1/20 及优势/劣势的交互、preview 与 execution 同结果

### R2. 通用暴击资源化（原 Phase 6 剩余，远期）

- 前置：收敛 `BattleHitResolver` 与 `BattleDamageResolver` 的暴击真相源（当前暴击走命运骰 `FateAttackFormula` crit gate，与幸运/劣势交互）
- 收敛后才评估新增通用字段：`crit_multiplier`（CombatSkillDef / 武器层）、武器威胁范围（天然命中扩 crit 区间）
- 不引入与命运骰并行的第二套暴击判定

### R3. Phase 4 收尾决策项（小）

- **技能级 `roll_disposition` 字段是否需要**：当前状态/装备能力驱动已覆盖设计意图，默认结论是不需要；若未来出现"技能固有优势"内容（不依赖状态），再评估新增字段
- **攻击预览的优势标签**：HUD 已有劣势文案与 save 的 `save_advantage_state`，普通攻击预览的"需 X+ · 优势"展示尚未补；如补齐，与 R1 的 `situational_sources[]` 合并展示

### R4. 格级障碍物（已拍板要做，独立于 R1）

**设计决定（2026-08-16）**：

- 战场需要“石头”这类**占一整格**的障碍物；玩家心智单位是格，边特征（墙）不承载这个语义
- 障碍物**只阻断站位与通行，不阻断视线**——远程/法术可以越过石头攻击；游戏已无任何 LOS 阻断机制（边墙已于 2026-08-16 移除）
- 掩体不走障碍物路线（已由 R1 的森林格承载），障碍物不提供 AC 加成

**实现边界**：

- 障碍物不是单位、不是边特征：`BattleCellState` 需要新增 typed 障碍字段（现有 `occupant_unit_id` 只认单位，`prop_ids` 只是渲染标记，规则层不得读 `prop_ids`）
- 必须接入的通行/站位判定面：`BattleGridService` / `BattleEdgeService` 寻路与 traversable、多格 footprint 放置、强制位移（击退/拉拽/风推）、冲锋路径、传送/交换/跳斩落点校验、AI 路径树与移动评分——障碍物格对这些入口视同不可站
- 展示走现有 prop 链路（`battle_board_prop.tscn`），由障碍字段同步渲染，不反向驱动规则
- 内容由 `BattleTerrainGenerator` 在地图模板中摆放；技能造/拆障碍物留待后续评估，不在首期范围

**验收**：障碍格不可进入/不可落点/不可穿越；视线与命中完全不受障碍物影响；AI 路径绕开障碍格；存档 schema 兼容（不升 `SAVE_VERSION`，缺失字段按无障碍处理）

## 集成边界

- 可改：`BattleHitResolver`、`BattleAttackCheckPolicyService`、`BattleGridService`、`BattlePreview` / HUD adapter、`BattleAiScoreService`、新增情境规则文件与对应 `tests/battle_runtime/rules/` 回归
- 不碰：`SaveSerializer`（不升版本）、`AttributeService` 的 `HIT_RATE / EVASION` 命名、`HeadlessGameTestSession` 核心结构、world map 战斗启动链路
- 命中公式变更后按 `skill_runtime.md` 的代表性回归清单重跑相关 runner
