# 战斗技能系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-17`

## 定位

本文记录技能从 `.tres` 内容到战斗可用性、preview、execution、状态语义和 AI 消费的当前主链。未来规则扩展与旧阶段计划位于 [`../../proposals/battle/skill_runtime_expansion.md`](../../proposals/battle/skill_runtime_expansion.md)，不能作为当前合同。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `SkillDef`、`CombatSkillDef`、`CombatEffectDef`、`CastVariantDef`、`data/configs/skills/*.tres` | 声明技能、目标、范围、消耗、检定和 typed effects |
| 校验与投影 | `SkillContentRegistry`（加载/索引/编排 + 技能级校验）、`SkillCombatProfileValidator` / `SkillDamageEffectValidator` / `SkillExecuteEffectValidator`（加载期分区校验器）、`SkillDefinition`、`CombatEffectDefinition` | 加载期校验并发布 immutable definition graph |
| 战斗可用性 | `BattleSkillAvailabilityService`、`BattleSkillEntryRef`、`BattleSkillEntryIds` | 合并已学技能、装备授予技能和 scoped auto-cast 入口 |
| 命令与预览 | `BattleCommand`、`BattleRuntimeModule.PreviewCommand(...)` | 校验 entry、资源、目标和当前 battle state，返回只读 preview |
| 执行编排 | `BattleSkillExecutionOrchestrator` | 解析目标、读条、范围、屏障裁剪、效果和特殊入口 |
| 通用规则 | `BattleRangeService`、`BattleHitResolver`、`BattleSaveResolver`、`BattleDamageResolver`、`BattleStatusSemanticTable` | 拥有射程、命中、豁免、伤害和状态语义 |
| AI | `BattleAi*ActionEvaluator`、`BattleAiScoreService` | 使用同一可用技能入口、canonical preview 和 typed score input |

## 主链

```text
SkillDef Resource
  -> SkillContentRegistry
  -> SkillDefinition in ContentSnapshot
  -> BattleSkillAvailabilityService
  -> BattleCommand(skill_entry_id + skill_id)
  -> PreviewCommand / IssueCommand
  -> BattleSkillExecutionOrchestrator
  -> hit/save/damage/status/movement/barrier services
  -> BattleEventBatch + BattlePresentationDelta
```

## 实现约束

- Authoring Resource 只在内容构建边界存在；battle runtime、AI 和 UI 消费 `SkillDefinition` 与 battle-local state。
- `skill_entry_id` 标识本次技能来源，`skill_id` 标识技能定义。旧 entry 失效时必须拒绝或清空，不能按同名 `skill_id` 静默切换到另一来源。
- HUD、手动选择、文本命令、preview、execution 和 AI 必须通过 `BattleSkillAvailabilityService` 看到同一组技能入口。
- Preview 不消费正式 RNG、状态 charge 或资源；commit 必须重新校验当前状态，不能信任旧 preview。
- Pending cast 是 battle-only runtime state。手动施法、auto-cast 和 pending cast 最终进入同一 orchestrator 规则链。
- 范围、命中、豁免、伤害、死亡、屏障和状态阻断属于通用 service/table；不得在具体技能 id、装备 id 或 UI 中复制规则。
- AI 快速评估可以使用专用 typed evaluator，但遇到会改变合法目标集合的 canonical 规则时必须委托正式 preview，而不是近似复制。

## 代表性回归

- `tests/runtime/validation/run_barrier_skill_content_validation_regression.cs`
- `tests/battle_runtime/rules/run_battle_range_service_contract_regression.cs`
- `tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs`
- `tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs`
- `tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs`

模块生命周期见 [`runtime_module.md`](runtime_module.md)，AI 参数见 [`ai_score_parameters.md`](ai_score_parameters.md)，装备来源技能见 [`equipment_ability_runtime.md`](equipment_ability_runtime.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-13、CU-15、CU-16、CU-18、CU-20 和 CU-21。
