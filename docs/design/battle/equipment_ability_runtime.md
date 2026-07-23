# 装备能力系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-24`

## 定位

本文记录当前已经落地的装备能力内容 ABI、内容快照、战斗投影、技能入口和 typed action 执行链。原始 V1/V2/V3 方案仍在 [`../../proposals/battle/equipment_ability/`](../../proposals/battle/equipment_ability/)；其中未实现机制不属于当前能力。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `EquipmentAbilityContentPackDef`、`EquipmentAbilityBindingDef`、reaction/condition/action/state 子资源 | 声明装备来源、触发、条件、typed payload、状态和授予技能 |
| 校验与投影 | `EquipmentAbilityContentRegistry`（编排/索引）、`EquipmentAbilityBindingValidator`（binding/reaction/condition 校验）、`EquipmentAbilityPayloadValidators`（payload 校验）、`EquipmentAbilityDefinitionProjection`（Resource→Definition 投影）、`EquipmentAbilityRuntimeDefinitions.cs` | 校验 handler/consumer/source/item/skill 引用并投影 immutable definitions |
| 共享属性契约 | `AttributeContentRules` | 定义五种 AC component 的 typed kind、稳定 id、只读顺序和 membership，供 authoring 校验与 attribute/world/battle 共同消费 |
| 进程内容 | `ContentSnapshotBuilder`、`ContentSnapshot`、`GameContentCatalog` | 发布 binding/pack definition 索引，session 与 battle 只借用 |
| 战斗投影 | `BattleEquipmentAbilityProjectionService`、`BattleUnitFactory`、`EncounterRosterBuilder` | 将玩家装备或敌方配置投影为 `BattleUnitState.equipment_ability_sources` |
| 技能入口 | `BattleSkillAvailabilityService`、`BattleSkillEntryIds` | 将装备授予技能与已学技能合成为稳定、带来源的 battle entry |
| 执行 | `BattleEquipmentAbilityRuntimeService` | 按 trigger/condition/fact 执行 typed action，并写入 battle state/event batch |
| 通用桥接 | `BattleDamageResolver`、`BattleAttackCheckPolicyService`、`BattleSkillExecutionOrchestrator`、`BattleRuntimeSkillTurnResolver` | 复用命中、伤害、技能、耐久、时间线和状态规则 |

## 主链

```text
EquipmentAbilityContentPackDef
  -> EquipmentAbilityContentRegistry validation/projection
  -> ContentSnapshot / GameContentCatalog
  -> BattleEquipmentAbilityProjectionService
  -> BattleUnitState.equipment_ability_sources
  -> BattleSkillAvailabilityService and/or trigger dispatch
  -> BattleEquipmentAbilityRuntimeService
  -> canonical battle services + BattleEventBatch
```

## 当前能力边界

- Binding、reaction、condition、fact query、action payload 和 state schema 都在加载期转为 typed definition；未知或 consumer 不支持的 handler 必须由 registry 拒绝，运行时不做字符串 fallback。
- `ignored_ac_components` / `ac_component_multipliers` 的合法 id 由 `AttributeContentRules` 校验；同一规则也驱动属性汇总、敌方属性投影、战斗单位构建和命中时 AC component 调整，不从 `AttributeService` 反向读取或复制白名单。
- 装备授予主动技能不写入角色 `known_active_skill_ids`。`SkillEntryId` 同时贯穿 HUD、选择态、命令、preview、execution、AI 与 scoped auto-cast。
- 玩家装备能力通过当前 battle-local equipment view 投影；敌方装备能力由 `EncounterRosterBuilder` 生成 battle-only source。战斗规则只读 `BattleUnitState.equipment_ability_sources`。
- `BattleEquipmentAbilityRuntimeService` 负责 reaction 时序、fact 查询、target mark、状态栈、召唤、立即武器攻击、内部技能、伤害/治疗、AP、临时边特征和能力状态；其中召唤职责拆分为其持有的 `BattleEquipmentSummonResolver`、target mark 生命周期职责拆分为 `BattleEquipmentTargetMarkResolver`、条件/fact 求值拆分为 `BattleEquipmentAbilityConditionEvaluator`、反应动作执行拆分为 `BattleEquipmentStatusActionResolver` / `BattleEquipmentSkillTriggerActionResolver` / `BattleEquipmentAreaActionResolver` / `BattleEquipmentDirectEffectActionResolver`、能力状态机拆分为 `BattleEquipmentAbilityStateResolver`、攻击修正收集拆分为 `BattleEquipmentAttackModifierResolver`（Setup 时相互接线，主服务保留事件入口、`ResolveActions` 编排、roll gate 与对外 internal 委托入口）。具体伤害、命中、技能、位移与死亡规则仍交给 canonical service。
- `BattleEquipmentAttackModifierResolver` 显式实现只读的 `IBattleEquipmentAttackCheckQuery` 与 `IBattleEquipmentDamageQuery`；`BattleEquipmentAbilityRuntimeService` 显式实现写侧 `IBattleEquipmentCombatReactionSink`。`BattleAttackCheckPolicyService` 只注入 attack-check query，`BattleDamageResolver` 分别注入 damage query 与 reaction sink，不再经 `BattleRuntimeModule` 或 12-member 聚合接口定位能力。
- `BattleRuntimeModule.BindEquipmentRulePorts()` 是三个端口的唯一正式装配点：先完成装备 runtime/child resolver 接线，再原子绑定 policy 与 damage resolver。`GetAttackCheckPolicyService()` / `GetEquipmentAbilityRuntimeService()` 是无副作用 getter；测试替换 damage/hit resolver 必须走显式 configure 入口，不能靠 getter 隐式重新 `Setup`。
- `BattleAttackCheckPolicyContext.battle_state` 与 `DamageResolutionContext.BattleState` 是装备 query/reaction 的显式状态来源；屏障直伤、坠落伤害与免死后的递归 effect 也由调用方继续传递同一 state。端口不提供 `GetBattleState()`，rules 也不保存 runtime owner；preview 未携带正式 state 时不会从全局 runtime 补回。
- 装备耐久使用 selector 与 `BattleDamageResolver.ApplyEquipmentDurabilityDamageToSelection(...)` 的 selected-target commit，不能在能力 handler 中二次随机或直接改 `EquipmentInstanceState.current_durability`。
- 来源消失、换装或装备摧毁后，`BattleUnitFactory.RefreshEquipmentProjection(...)` 重建 source；runtime service 清理声明了 source-missing 语义的 target mark，并同步 changed unit/event facts。
- Preview 与 commit 共用只读规则收集；preview 不写真实 store。AI mutation guard 必须检测装备能力来源、状态、mark、召唤和相关计数的非法变化并立即失败，不承担状态回滚。

## 代表性回归

- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- `tests/battle_runtime/rules/run_equipment_durability_selected_target_regression.cs`
- `tests/battle_runtime/rules/run_attack_policy_parity_regression.cs`
- `tests/battle_runtime/rules/run_damage_context_typed_regression.cs`
- `tests/battle_runtime/runtime/run_executioner_axe_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_gorgon_crossbow_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_scorpion_bow_weapon_ability_regression.cs`

技能共用合同见 [`skill_runtime.md`](skill_runtime.md)，武器投影见 [`weapon_dice_and_equipment.md`](weapon_dice_and_equipment.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-10、CU-13、CU-15 和 CU-16。
