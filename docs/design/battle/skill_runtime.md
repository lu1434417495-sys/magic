# 战斗技能系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-25`

## 定位

本文记录技能从 `.tres` 内容到战斗可用性、preview、execution、状态语义和 AI 消费的当前主链。未来规则扩展与旧阶段计划位于 [`../../proposals/battle/skill_runtime_expansion.md`](../../proposals/battle/skill_runtime_expansion.md)，不能作为当前合同。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `SkillDef`、`CombatSkillDef`、`CombatEffectDef`、`CombatCastVariantDef`、`data/configs/skills/*.tres` | 声明技能、目标、范围、消耗、检定、typed effects 与投射物种类 |
| 校验与投影 | `SkillContentRegistry`（加载/索引/编排 + 技能级校验）、`SkillCombatProfileValidator` / `SkillDamageEffectValidator` / `SkillExecuteEffectValidator`（加载期分区校验器）、`CombatProjectileContentRules`、`SkillDefinition`、`CombatEffectDefinition`、`BattleAttackRollModifierSpec` | 加载期校验并发布 immutable definition graph；投射物种类和攻击检定修正均以 typed content-definition 契约随投影使用 |
| 战斗可用性 | `BattleSkillAvailabilityService`、`BattleSkillEntryRef`、`BattleSkillEntryIds` | 合并已学技能、装备授予技能和 scoped auto-cast 入口 |
| 命令与预览 | `BattleCommand`、`BattleRuntimeModule.PreviewCommand(...)` | 校验 entry、资源、目标和当前 battle state，返回只读 preview |
| 执行编排 | `BattleSkillExecutionOrchestrator`、`BattleSkillTargetValidationService`、`BattleVaultBehindTargetRules`、`BattleSpecialSkillResolver` | 解析目标、读条、范围、屏障裁剪、效果和特殊入口；越肩类效果共享落点/路径校验并在命中后提交移动 |
| 时序与状态计时 | `BattleTimelineDriver`、`BattleUnitActionClockState`、`BattleTemporalStatusService`、`BattleUnitTurnState`、`BattleUnitRestState`、`BattleUnitCooldownState`、`BattleRuntimeSkillTurnResolver` | timeline driver 拥有 timeline/TU/ready、action-threshold 校验/日志、stamina 与冻结/读条跳过编排；action-clock owner 唯一持有 action progress/threshold/rate remainder，并原子累计余数与扣除全部 crossing；temporal service 决定倍率；turn owner 唯一持有行动、移动、锁定移动点授权与施法耗尽事实；rest owner 持有跨 activation 的 resting fact；cooldown owner 唯一持有 map 与 last-turn anchor 并原子判定/推进；turn resolver 负责 current-TU、5 TU 粒度日志、静滞/turn-start 编排、turn timer、状态 tick/duration、护盾 duration 与 `next_tick_at_tu` 初始化 |
| Contingency auto-cast | `BattleContingencySystem`、`IBattleContingencyRuntimePort`、`BattleContingencyBridgeService` | 构造 reaction 请求，并在同一调用栈、同一 batch 与 auto-cast origin scope 内进入 orchestrator |
| 通用规则 | `BattleRangeService`、`BattleHitResolver`、`BattleSaveResolver`、`BattleDamageResolver`、`BattleDamageBonusConditionRules`、`BattleStatusSemanticTable` | 拥有射程、命中、豁免、伤害条件和状态语义 |
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
- `BattleAttackRollModifierSpec` 归 `scripts/systems/content/skills/`，只提供字段、typed 枚举映射、克隆和字典编解码；筛选、叠加与最终生效仍归 battle rules/runtime，不回灌进内容契约。
- `skill_entry_id` 标识本次技能来源，`skill_id` 标识技能定义。旧 entry 失效时必须拒绝或清空，不能按同名 `skill_id` 静默切换到另一来源。
- HUD、手动选择、文本命令、preview、execution 和 AI 必须通过 `BattleSkillAvailabilityService` 看到同一组技能入口。
- `BattleSkillAvailabilityService` 是 caller-scoped 的无状态规则类型，不缓存进 `BattleRuntimeModule`。command preview 在当前时点校验 entry，execution 独立重新校验并解析 entry level；availability result 不跨 preview/commit 缓存。
- Preview 不消费正式 RNG、状态 charge 或资源；commit 必须重新校验当前状态，不能信任旧 preview。
- Pending cast 是 battle-only runtime state。手动施法、auto-cast 和 pending cast 最终进入同一 orchestrator 规则链。
- `bonus_condition` 只在 authoring 边界保留 `StringName`，进入内容契约后必须解码为 `BattleDamageBonusConditionKind`，未知值由 schema validator 拒绝。正式伤害和 AI 估值统一委托 `BattleDamageBonusConditionRules`，不得各自维护条件分支；`damage_ratio_percent` 仅在条件成立时生效，其中 `target_has_shield` 按每个效果实际结算时的 `BattleUnitState.HasShield()` 判断。
- `CombatSkillDef.projectile_kind` 唯一声明技能投送是否为 `none`、`nonmagical`、`magical` 或 `current_weapon`；`CombatCastVariantDef.projectile_kind_override` 的空值表示继承，显式 `none` 可把投射技能的某个近战变体关闭。`BattleEffectCategoryResolver` 只从这两个 typed 字段派生 `projectile` + `magical_projectile` / `nonmagical_projectile`，不得从职业 tag、`magic`、射程、伤害、豁免或技能 id 推断。派生类别不得写入 `delivery_categories` / `effect_categories`，旧 `magical_missile` / `nonmagical_missile` 名称不再接受。
- `current_weapon` 只在效果真实包含武器伤害、当前运行态与正式物品定义都确认主手为 ranged 时派生非魔法投射类别；基础攻击和弓手武器技能使用该模式。显式 `magical` 不会因为持有普通弓而再叠加 `nonmagical_projectile`。选中的 cast variant 必须沿 preview、commit、ground clip、repeat attack 和 chain damage 传到同一 resolver。
- 新状态的 timeline tick anchor 由 `BattleRuntimeSkillTurnResolver` 基于应用时的 current TU 初始化；`BattleRuntimeModule.MarkAppliedStatusesForTurnTiming(...)` 只保持“先初始化 anchor，再通知 Fate”的跨 owner 顺序，不保存状态计时规则。
- 带 `upkeep_*` typed 字段的持续状态由 `BattleRuntimeSkillTurnResolver` 按 `next_tick_at_tu` 扣除战斗资源，并用 `upkeep_elapsed_tu` 保存已维持时长；费用升档按下一次结算时点计算。资源不足或 `break_on_hard_control` 命中硬控集合时，resolver 原子移除持续状态、施加 typed 攻击检定惩罚状态，并从终止时点写入技能冷却。状态快照必须保留维持配置与已维持时长。
- 状态提供的 `combo_attack_bonus_status_id` / `combo_attack_bonus_stack_divisor` 由 `BattleHitResolver` 只对真实包含武器伤害的近战武器攻击结算；`melee_combo_stack_gain_bonus` 由 `BattleDamageResolver` 在近战武器命中后叠加到通用的每击 1 层规则。两条规则都读取独立的 `melee_combo_stack`，不回退到旧 `combo_stack`。
- 护盾 duration 与普通状态 duration 共用非静滞单位的 timeline status phase；实际递减和到期六字段清理由 `BattleUnitShieldState` 原子执行。step 开始时已有 `time_stasis` 的单位在该 step 内冻结护盾 duration，静滞解除后的下一 step 才恢复。
- Contingency system 只弱借用 `IBattleContingencyRuntimePort`，不反向依赖 `BattleRuntimeModule`。bridge 实现该端口，并保留同步递归 reaction 顺序、调用方 `BattleEventBatch`、执行前玩家已学来源复核和覆盖嵌套反应的 effect-origin scope。
- 范围、命中、豁免、伤害、死亡、屏障和状态阻断属于通用 service/table；不得在具体技能 id、装备 id 或 UI 中复制规则。
- `vault_behind_target` 是 unit-skill typed effect：目标必须与使用者正交相邻，落点为目标沿攻击方向的下一格；落点占用、两段 edge 通行或 layered barrier 边界任一不合法时，canonical preview 与 execution 都拒绝。只有本次攻击检定命中后才移动使用者，目标被伤害击倒不取消已合法的落位。
- AI 快速评估可以使用专用 typed evaluator，但遇到会改变合法目标集合的 canonical 规则时必须委托正式 preview，而不是近似复制。

## 代表性回归

- `tests/runtime/validation/run_barrier_skill_content_validation_regression.cs`
- `tests/battle_runtime/rules/run_battle_range_service_contract_regression.cs`
- `tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs`
- `tests/progression/schema/run_combat_projectile_kind_schema_regression.cs`
- `tests/battle_runtime/rules/run_battle_effect_category_resolver_contract_regression.cs`
- `tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs`
- `tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs`
- `tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs`
- `tests/battle_runtime/skills/run_warrior_over_shoulder_regression.cs`
- `tests/battle_runtime/skills/run_warrior_perfect_rhythm_regression.cs`

模块生命周期见 [`runtime_module.md`](runtime_module.md)，AI 参数见 [`ai_score_parameters.md`](ai_score_parameters.md)，装备来源技能见 [`equipment_ability_runtime.md`](equipment_ability_runtime.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-13、CU-15、CU-16、CU-18、CU-20 和 CU-21。
