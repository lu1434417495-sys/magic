# 装备能力系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-07-25`

## 定位

本文记录当前已经落地的装备能力内容 ABI、内容快照、战斗投影、技能入口和 typed action 执行链。原始 V1/V2/V3 方案仍在 [`../../proposals/battle/equipment_ability/`](../../proposals/battle/equipment_ability/)；其中未实现机制不属于当前能力。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `EquipmentAbilityContentPackDef`、`EquipmentAbilityBindingDef`、reaction/condition/action/state 子资源 | 声明装备来源、触发、条件、typed payload、状态和授予技能 |
| 校验与投影 | `EquipmentAbilityContentRegistry`（编排/索引）、`EquipmentAbilityStatusDeclarationCatalog`（状态声明目录）、`EquipmentAbilityBindingValidator`（binding/reaction/condition 校验）、`EquipmentAbilityPayloadValidators`（payload 校验）、`EquipmentAbilityDefinitionProjection`（Resource→Definition 投影）、`EquipmentAbilityRuntimeDefinitions.cs` | fail-closed 校验 handler/consumer/trait/skill/status 引用与 closed damage/slot domain，并投影 immutable definitions |
| 共享属性契约 | `AttributeContentRules` | 定义五种 AC component 的 typed kind、稳定 id、只读顺序和 membership，供 authoring 校验与 attribute/world/battle 共同消费 |
| 进程内容 | `ContentSnapshotBuilder`、`ContentSnapshot`、`GameContentCatalog` | 发布 binding/pack definition 索引，session 与 battle 只借用 |
| 战斗投影 | `BattleEquipmentAbilityProjectionService`、`BattleUnitEquipmentAbilityProjectionState`、`BattleUnitFactory`、`EncounterRosterBuilder` | 纯计算玩家/敌方 sources 与 temporal modifiers，并由 battle-unit owner 原子安装不可变读视图 |
| 技能入口 | `BattleSkillAvailabilityService`、`BattleSkillEntryIds` | 将装备授予技能与已学技能合成为稳定、带来源的 battle entry |
| 执行 | `BattleEquipmentAbilityRuntimeService` | 按 trigger/condition/fact 执行 typed action，并写入 battle state/event batch |
| 通用桥接 | `BattleDamageResolver`、`BattleAttackCheckPolicyService`、`BattleSkillExecutionOrchestrator`、`BattleRuntimeSkillTurnResolver` | 复用命中、伤害、技能、耐久、时间线和状态规则 |

## 主链

```text
EquipmentAbilityContentPackDef
  -> EquipmentAbilityContentRegistry validation/projection
  -> ContentSnapshot / GameContentCatalog
  -> BattleEquipmentAbilityProjectionService
  -> BattleUnitEquipmentAbilityProjectionState
       (sources + temporal progress modifiers)
  -> BattleSkillAvailabilityService and/or trigger dispatch
  -> BattleEquipmentAbilityRuntimeService
  -> canonical battle services + BattleEventBatch
```

## 当前能力边界

- Binding、reaction、condition、fact query、action payload 和 state schema 都在加载期转为 typed definition；未知或 consumer 不支持的 handler 必须由 registry 拒绝，运行时不做字符串 fallback。
- `EquipmentAbilityContentValidationContext` 的 trait、skill、外部 status 三个 open-content catalog 都是必填合同；`null` 表示生产构建缺失依赖并返回 `EQA_VALIDATION_CONTEXT_INCOMPLETE`，非 null 空集合表示目录权威为空，不能用于关闭校验。damage tag 与 equipment slot 属于 closed domain，分别直接调用 `DamageTagContentRules` 与 `EquipmentRules`，不在 context 里复制可选白名单。
- Status 采用声明/引用两阶段校验。外部声明来自 `StatusContentRules` 的系统状态、技能 effect 和 trait passive status；battle 的 `BattleStatusSemanticTable` 消费系统状态声明并附加运行时语义，不反向拥有内容 ID。装备 pack 内的 `apply_status`、下一回合 AP 归零标记、target mark 镜像状态和区域接触状态先由 `EquipmentAbilityStatusDeclarationCatalog` 汇总，随后所有 condition/fact/clear/consume 等引用再统一做 membership 校验。因此 pack 顺序不影响合法引用，未声明拼写不能进入 process snapshot。
- `ignored_ac_components` / `ac_component_multipliers` 的合法 id 由 `AttributeContentRules` 校验；同一规则也驱动属性汇总、敌方属性投影、战斗单位构建和命中时 AC component 调整，不从 `AttributeService` 反向读取或复制白名单。
- 装备授予主动技能不写入角色 `known_active_skill_ids`。`SkillEntryId` 同时贯穿 HUD、选择态、命令、preview、execution、AI 与 scoped auto-cast。
- 玩家装备能力通过当前 battle-local equipment view 投影；敌方装备能力由 `EncounterRosterBuilder` 生成 battle-only source。`BattleEquipmentAbilityProjectionService` 只返回纯 `BattleEquipmentAbilityProjectionResult`，不回写单位；`BattleUnitEquipmentAbilityProjectionState` 深拷贝并一次替换 sources 与 binding 级 temporal modifiers，异常时保留旧投影。正式 encounter roster 启动由 `BattleRuntimeModule` 直接消费 builder 返回的 typed `BattleUnitState` 列表，不借道 canonical Godot payload，因而 sources 与 runtime-only temporal 组件保持同一 owner 生命周期。plain/programmatic BattleSim definition 也可从已投影单位捕获私有的规范化 seed，每局重建 canonical unit 后重新原子安装，并经 fresh typed `BattleStartUnitRoster` 一次性交给 runtime；该 seed 不进入 69-key codec，也不复用 AI mutation 的 raw-exact diagnostic snapshot。formal fixture 的 hostile 同样经 enemy-only typed roster 移交，不再走 canonical `enemy_units`；当前 authored `BattleSimUnitSpec` 不生成非空 seed，且两个实际 formal benchmark 尚未注入完整 trait/equipment-binding catalog，因此这条保留能力不能解释为默认模拟内容已产生非空 temporal 投影。需要 Godot collection 的同步调用方才使用 projection lease。规则只消费 owner 的不可变 scalar read view；timeline/casting 读取 owner 在替换时按 `ModifierId` ordinal 预选的 action/cast 项，同 ID 仍由投影顺序中的第一项获胜，掷骰和属性读取仍按每次进度结算执行。
- `BattleEquipmentAbilityRuntimeService` 负责 reaction 时序、fact 查询、target mark、状态栈、召唤、立即武器攻击、内部技能、伤害/治疗、AP、临时边特征和能力状态；其中召唤职责拆分为其持有的 `BattleEquipmentSummonResolver`、target mark 生命周期职责拆分为 `BattleEquipmentTargetMarkResolver`、条件/fact 求值拆分为 `BattleEquipmentAbilityConditionEvaluator`、反应动作执行拆分为 `BattleEquipmentStatusActionResolver` / `BattleEquipmentSkillTriggerActionResolver` / `BattleEquipmentAreaActionResolver` / `BattleEquipmentDirectEffectActionResolver`、能力状态机拆分为 `BattleEquipmentAbilityStateResolver`、攻击修正收集拆分为 `BattleEquipmentAttackModifierResolver`（Setup 时相互接线，主服务保留事件入口、`ResolveActions` 编排、roll gate 与对外 internal 委托入口）。具体伤害、命中、技能、位移与死亡规则仍交给 canonical service。
- `BattleEquipmentAttackModifierResolver` 显式实现只读的 `IBattleEquipmentAttackCheckQuery` 与 `IBattleEquipmentDamageQuery`；`BattleEquipmentAbilityRuntimeService` 显式实现写侧 `IBattleEquipmentCombatReactionSink`。`BattleAttackCheckPolicyService` 只注入 attack-check query，`BattleDamageResolver` 分别注入 damage query 与 reaction sink，不再经 `BattleRuntimeModule` 或 12-member 聚合接口定位能力。
- `BattleRuntimeModule.BindEquipmentRulePorts()` 是三个端口的唯一正式装配点：先完成装备 runtime/child resolver 接线，再原子绑定 policy 与 damage resolver。`GetAttackCheckPolicyService()` / `GetEquipmentAbilityRuntimeService()` 是无副作用 getter；测试替换 damage/hit resolver 必须走显式 configure 入口，不能靠 getter 隐式重新 `Setup`。
- `BattleAttackCheckPolicyContext.battle_state` 与 `DamageResolutionContext.BattleState` 是装备 query/reaction 的显式状态来源；屏障直伤、坠落伤害与免死后的递归 effect 也由调用方继续传递同一 state。端口不提供 `GetBattleState()`，rules 也不保存 runtime owner；preview 未携带正式 state 时不会从全局 runtime 补回。
- 装备耐久使用 selector 与 `BattleDamageResolver.ApplyEquipmentDurabilityDamageToSelection(...)` 的 selected-target commit，不能在能力 handler 中二次随机或直接改 `EquipmentInstanceState.current_durability`。
- 来源消失、换装或装备摧毁后，`BattleUnitFactory.RefreshEquipmentProjection(...)` 先原子提交完整 equipment-ability projection，再由 runtime service 清理声明了 source-missing 语义的 target mark，并同步 changed unit/event facts。
- Preview 与 commit 共用只读规则收集；preview 不写真实 store。AI mutation guard 通过共同 owner 的 raw exact snapshot 保留 owner 缺失、两个组件各自的 null/empty、null entry、嵌套 ability-id null 和原始顺序，并继续输出既有 `equipment_ability_sources` / `temporal_progress_modifiers` 两个 stable key；它必须检测装备能力来源、状态、mark、召唤和相关计数的非法变化并立即失败，不承担状态回滚。

## 代表性回归

- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- `tests/battle_runtime/rules/run_equipment_durability_selected_target_regression.cs`
- `tests/battle_runtime/rules/run_attack_policy_parity_regression.cs`
- `tests/battle_runtime/rules/run_damage_context_typed_regression.cs`
- `tests/battle_runtime/runtime/run_executioner_axe_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_sands_time_weapon_ability_regression.cs`
- `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_owner_api_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`
- `tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_gorgon_crossbow_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_scorpion_bow_weapon_ability_regression.cs`

技能共用合同见 [`skill_runtime.md`](skill_runtime.md)，武器投影见 [`weapon_dice_and_equipment.md`](weapon_dice_and_equipment.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-10、CU-13、CU-15 和 CU-16。
