# 装备能力系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-19`

## 定位

本文记录当前已经落地的装备能力内容 ABI、内容快照、战斗投影、技能入口和 typed action 执行链。原始 V1/V2/V3 方案仍在 [`../../proposals/battle/equipment_ability/`](../../proposals/battle/equipment_ability/)；其中未实现机制不属于当前能力。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `data/configs/json/equipment_abilities/equipment_abilities.json`、`EquipmentAbilityJsonDtos` | 以 55 packs / 170 bindings 声明装备来源、状态激活、动态减伤光环、移动足迹、触发、条件、typed payload、认知上限、致死拦截、授予技能和附伤替换组 |
| 校验与投影 | `EquipmentAbilityContentJsonAuthoringDomain`、`EquipmentAbilityImportGraphMapper`、`EquipmentAbilityImportVocabularyValidator`、`EquipmentAbilityClosedVocabulary`、`EquipmentAbilityPayloadKindCatalog`、`EquipmentAbilityDefinitionProjection`、`EquipmentAbilityContentRegistry` | strict DTO → plain ImportModel → domain/cross-domain validation → immutable Definition；production 不构造或加载 payload Resource |
| 共享属性契约 | `AttributeContentRules` | 定义五种 AC component 的 typed kind、稳定 id、只读顺序和 membership，供 authoring 校验与 attribute/world/battle 共同消费 |
| 进程内容 | `ContentSnapshotBuilder`、`ContentSnapshot`、`GameContentCatalog` | 发布 binding/pack definition 索引，session 与 battle 只借用 |
| 战斗投影 | `BattleEquipmentAbilityProjectionService`、`BattleUnitEquipmentAbilityProjectionState`、`BattleUnitFactory`、`EncounterRosterBuilder` | 纯计算玩家/敌方 sources、temporal modifiers 与 cognition ceiling modifiers，并由 battle-unit owner 原子安装不可变读视图 |
| 技能入口 | `BattleSkillAvailabilityService`、`BattleSkillEntryIds` | 将装备授予技能与已学技能合成为稳定、带来源的 battle entry |
| 执行与 usage | `BattleEquipmentAbilityRuntimeService`、`BattleEquipmentAttackModifierResolver`、`EquipmentAbilityUsageRuntime` | 按 trigger/condition/fact 执行 typed action，收集只读 aura/附伤规则，提供 fatal-intercept 仲裁，并提交 per-battle/per-world usage 与 battle state/event batch |
| 附伤互斥 | `BattleEquipmentAbilityBonusDamageReplacementRules` | 以 action bundle 为单位仲裁同一 replacement group，并在 query/after-hit 两条结果链保持一致 |
| 移动足迹 | `BattleMovementService`、`BattleChargeResolver`、`BattleTerrainEffectSystem`、`BattleBoardRenderProfile` | 在正式移动/冲锋提交后按离开格生成同源时限地形，由 canonical contact-damage 链结算，并把已登记的 `render_overlay_id` 投影为对应高度的棋盘 overlay source |
| 通用桥接 | `BattleDamageResolver`、`BattleAttackCheckPolicyService`、`BattleSkillExecutionOrchestrator`、`BattleRuntimeSkillTurnResolver` | 复用命中、伤害、技能、耐久、时间线和状态规则 |

## 主链

```text
equipment ability JSON
  -> strict DTO / EquipmentAbilityImportGraphMapper
  -> EquipmentAbilityImportVocabularyValidator
  -> EquipmentAbilityDefinitionProjection
  -> EquipmentAbilityContentRegistry indexes
  -> ContentSnapshot / GameContentCatalog
  -> BattleEquipmentAbilityProjectionService
  -> BattleUnitEquipmentAbilityProjectionState
       (sources + temporal progress modifiers + cognition ceiling modifiers)
  -> BattleSkillAvailabilityService and/or trigger dispatch
  -> BattleEquipmentAbilityRuntimeService
  -> canonical battle services + BattleEventBatch
```

## 当前能力边界

- Binding、reaction、condition、fact query、action payload 和 state schema 都在加载期转为 typed definition；29 个有效 handler kind（3 condition + 26 action）、35 个 fact ID、10 个 trigger 与 10 个 timing 由共享 closed vocabulary/spec 驱动。未知、ghost 或 consumer 不支持的 handler 必须在 import/registry 阶段拒绝，运行时不做字符串 fallback。
- 正式 55 个 `.tres`、49 个 equipment authoring Resource C#、TRES adapter/converter、loader constructor、ResourcePath provenance 与 Resource→Definition 兼容桥均已删除。runtime 只保留 immutable definitions、handler metadata/spec 和 battle consumers。
- `EquipmentAbilityContentValidationContext` 的 trait、skill、外部 status 三个 open-content catalog 都是必填合同；`null` 表示生产构建缺失依赖并返回 `EQA_VALIDATION_CONTEXT_INCOMPLETE`，非 null 空集合表示目录权威为空，不能用于关闭校验。damage tag 与 equipment slot 属于 closed domain，分别直接调用 `DamageTagContentRules` 与 `EquipmentRules`，不在 context 里复制可选白名单。
- Status 采用声明/引用两阶段校验。外部声明来自 `StatusContentRules` 的系统状态、技能 effect 和 trait passive status；battle 的 `BattleStatusSemanticTable` 消费系统状态声明并附加运行时语义，不反向拥有内容 ID。装备 pack 内的 `apply_status`（包括 fatal-intercept `success_actions`）、下一回合 AP 归零标记、target mark 镜像状态和区域接触状态先由 `EquipmentAbilityStatusDeclarationCatalog` 汇总，随后 `activation_status_id` 与所有 condition/fact/clear/consume 等引用再统一做 membership 校验。因此 pack 顺序不影响合法引用，未声明拼写不能进入 process snapshot。
- Binding 的 `activation_status_id` 不负责创建状态；它只在单位已经持有对应已声明状态时，投影稳定的 `battle_status:<unit_id>:<binding_id>` 装备能力来源。状态来源与原装备来源解耦，因此已施加到受益者的状态可在原装备卸下后继续提供自己的 reaction/fatal-intercept，直到状态正常到期或被移除。
- Binding 级 `mitigation_auras` 由 `BattleEquipmentAttackModifierResolver` 在每次伤害查询时从当前存活来源、阵营过滤和单位 footprint 格距动态收集，再经 `IBattleEquipmentDamageQuery` 合入 `BattleDamageResolver` 的 canonical immune/half/double/bypass 仲裁。光环不写受益者状态；来源死亡、来源投影移除或目标离开范围会在下一次查询立即反映，多个同层 `half` 只形成同一减伤层而不会连续折半。
- Binding 级 `movement_trails` 声明 trail id、替换组/优先级、可选 required skill、持续 TU、阵营、单一固定骰与伤害 tag。普通移动和 charge 共用同一选择规则：每个离开格生成同一 `field_instance_id` 的时限地形；接触伤害按每个单位、每个 field、每次移动命令最多一次，并标记 `BattleEffectOrigin.EquipmentAbility()`，因此不会被再次视为外部伤害触发装备递归。required skill 的高优先级轨迹只替换本次对应技能移动的低优先级足迹。战斗快照只携带 active terrain effect 的 `render_overlay_id`；是否可画由 `BattleBoardRenderProfile` 的 source spec 决定，未登记 id 保持不绘制，已登记但暂无专用贴图的 source 使用通用可见 generated fallback。
- `ignored_ac_components` / `ac_component_multipliers` 的合法 id 由 `AttributeContentRules` 校验；同一规则也驱动属性汇总、敌方属性投影、战斗单位构建和命中时 AC component 调整，不从 `AttributeService` 反向读取或复制白名单。
- 装备授予主动技能不写入角色 `known_active_skill_ids`。`SkillEntryId` 同时贯穿 HUD、选择态、命令、preview、execution、AI 与 scoped auto-cast。
- 套装阈值能力不伪装成普通固定装备 trait。`GearSetEvaluationService` 产生 `gear_set_threshold` 有效 trait，`BattleEquipmentAbilityProjectionService` 将其投影为 `PlayerPersistentGearSetThreshold` source，并把套装定义选出的真实 usage-anchor 装备实例用于 per-battle/per-world usage。换装后整份有效 trait 与能力来源原子重算。
- 玩家装备能力通过当前 battle-local equipment view 投影；敌方装备能力由 `EncounterRosterBuilder` 生成 battle-only source。`BattleEquipmentAbilityProjectionService` 只返回纯 `BattleEquipmentAbilityProjectionResult`，不回写单位；`BattleUnitEquipmentAbilityProjectionState` 深拷贝并一次替换 sources、binding 级 temporal modifiers 与 cognition ceiling modifiers，异常时保留旧投影。正式 encounter roster 启动由 `BattleRuntimeModule` 直接消费 builder 返回的 typed `BattleUnitState` 列表，不借道 canonical Godot payload，因而三个 runtime-only 组件保持同一 owner 生命周期。plain/programmatic BattleSim definition 也可从已投影单位捕获私有的规范化 seed，每局重建 canonical unit 后重新原子安装，并经 fresh typed `BattleStartUnitRoster` 一次性交给 runtime；该 seed 不进入 70-key codec，也不复用 AI mutation 的 raw-exact diagnostic snapshot。formal fixture 的 hostile 同样经 enemy-only typed roster 移交，不再走 canonical `enemy_units`；两个实际 formal benchmark已通过 process snapshot 注入 trait/equipment-binding catalog，但当前 authored `BattleSimUnitSpec` 不生成非空 seed，默认 loadout 本身也不保证产生 temporal 或 cognition modifier，因此不能推定每次默认模拟都存在非空装备能力投影。context-only `battle_party` / `enemy_units` 继续是 canonical 70-key 合同，只携带持久的单位基础认知与既有装备能力来源，不携带两个 runtime-only modifier 组件；需要保留完整投影的进程内调用方必须使用 typed roster。需要 Godot collection 的同步调用方才使用 projection lease。规则只消费 owner 的不可变 scalar read view；timeline/casting 读取 owner 在替换时按 `ModifierId` ordinal 预选的 action/cast 项，同 ID 仍由投影顺序中的第一项获胜，掷骰和属性读取仍按每次进度结算执行。
- Binding 级 `cognition_ceiling_modifiers` 只表达“这件装备处于投影来源中时，有效认知不能高于某级”。加载期要求 `modifier_id` 在 binding 内唯一、`ceiling` 为已知封闭认知值，并投影来源装备 instance id 与 binding id。多个装备、状态与基础认知同时存在时，`BattleCognitionRules` 取最低上限；换装、摧毁或来源刷新后由完整 equipment projection 原子重算，不把派生有效认知写入单位存档，也不靠状态镜像模拟“装备中”。
- `BattleEquipmentAbilityRuntimeService` 负责 reaction 时序、fact 查询、target mark、状态栈、召唤、立即武器攻击、内部技能、伤害/治疗、AP、临时边特征和能力状态；其中召唤职责拆分为其持有的 `BattleEquipmentSummonResolver`、target mark 生命周期职责拆分为 `BattleEquipmentTargetMarkResolver`、条件/fact 求值拆分为 `BattleEquipmentAbilityConditionEvaluator`、反应动作执行拆分为 `BattleEquipmentStatusActionResolver` / `BattleEquipmentSkillTriggerActionResolver` / `BattleEquipmentAreaActionResolver` / `BattleEquipmentDirectEffectActionResolver`、能力状态机拆分为 `BattleEquipmentAbilityStateResolver`、攻击修正收集拆分为 `BattleEquipmentAttackModifierResolver`（Setup 时相互接线，主服务保留事件入口、`ResolveActions` 编排、roll gate 与对外 internal 委托入口）。具体伤害、命中、技能、位移与死亡规则仍交给 canonical service。
- `BattleEquipmentAttackModifierResolver` 显式实现只读的 `IBattleEquipmentAttackCheckQuery` 与 `IBattleEquipmentDamageQuery`；`BattleEquipmentAbilityRuntimeService` 显式实现写侧 `IBattleEquipmentCombatReactionSink`。`BattleAttackCheckPolicyService` 只注入 attack-check query，`BattleDamageResolver` 分别注入 damage query 与 reaction sink，不再经 `BattleRuntimeModule` 或 12-member 聚合接口定位能力。
- `BattleRuntimeModule.BindEquipmentRulePorts()` 是三个端口的唯一正式装配点：先完成装备 runtime/child resolver 接线，再原子绑定 policy 与 damage resolver。`GetAttackCheckPolicyService()` / `GetEquipmentAbilityRuntimeService()` 是无副作用 getter；测试替换 damage/hit resolver 必须走显式 configure 入口，不能靠 getter 隐式重新 `Setup`。
- `BattleAttackCheckPolicyContext.battle_state` 与 `DamageResolutionContext.BattleState` 是装备 query/reaction 的显式状态来源；屏障直伤、坠落伤害与免死后的递归 effect 也由调用方继续传递同一 state。端口不提供 `GetBattleState()`，rules 也不保存 runtime owner；preview 未携带正式 state 时不会从全局 runtime 补回。
- `EquipmentAbilityTriggerKind.OnDamageTakenFinalized` 是 defender 侧正式伤害后触发点，在致死 trait / Last Stand / equipment fatal intercept 全部处理完且目标仍存活时读取最终生命。fact 同时提供 `raw_damage`、`damage_tag`、`hp_before`、`hp_before_percent_bp`、`hp_percent_bp`、`is_equipment_generated` 与 `is_self_damage`；最终 HP 伤害为 0 但正原始伤害存在时，只有 condition/action 显式引用 `raw_damage` 的反应才允许执行，既有只依赖最终伤害的内容不会被免疫伤害误触。Preview 在当前规则所需的 detached battle 子集上计算离散 roll-gate 概率，不消费正式随机队列；确定且受支持的动作只写 detached state，条件动作只报告概率，`modify_ability_state`、ground/mixed trigger skill 等无法安全投影的动作以 typed unsupported reason 返回。
- `ApplyStatusActionPayloadDefinition.ArmorClassBonusPerStack` 进入 typed status；`BattleHitResolver` 只汇总仍有效且 stack count 为正的状态层数，并做整数溢出保护。该字段表达状态 AC，不回写持久属性快照。
- `EquipmentAbilityBindingDefinition.RequiredEffectiveTraitIds` 是能力源投影前置门槛。`BattleEquipmentAbilityProjectionService` 用单位本次换装后重算的完整有效 trait 集合逐项复核；任一前置缺失时不投影该 binding 的 source、modifier 或技能入口。固定装备 binding 仍保留原装备实例作为 usage owner，因此可用套装阈值 trait 解锁单件能力而不把账本迁移到 gear-set usage anchor。
- `fatal_intercepts` 投影为不可变 `EquipmentFatalInterceptDefinition` 候选。`BattleDamageResolver` 先按 normal protection priority 复核旧式 fatal trait，再处理 Last Stand 和 `IBattleFatalInterceptArbiter`；因此高优先级死亡来源不会在装备仲裁前被普通致死特性截走。候选按 `resolution_order` 与稳定来源键排序，逐个复核 protection priority、来源、usage 和 roll gate。`consume_on_attempt` 可让失败掷骰消耗次数并继续后序候选；首个成功候选先恢复生命，再按声明顺序执行仅允许为 `apply_status` / `trigger_skill` 的 `success_actions`，随后终止本次仲裁。Preview 为每个候选返回 reach/success/contribution 概率、期望恢复和成功动作摘要；只有 branch-local 100% 且处于受支持安全子集的动作会应用到对应 detached branch，对外概率动作仍保持 conditional，内部技能递归预览有显式深度上限。带 saving throw 的伤害分别预览成功/失败分支，再按分支概率合并 fatal 候选与动作，不能用平均伤害决定是否丢弃致死摘要。多段伤害沿 `BattleDamagePreviewWorkingSet` 的 continuation frontier 延续每个分支的 HP/status/usage，`consume_on_attempt` 与 consume-on-success 分别按正式时点提交到 branch-local clone；即使分支已经死亡，只要后续段仍可能重新触发未消耗候选就继续保留。整个过程不推进正式 RNG、usage 或真实单位状态。`per_world_month` usage 直接使用 `WorldTimeSystem.StepToMonth(world_step)`，当前世界历法每月为 15 × 30 = `450 world steps`。
- 装备耐久使用 selector 与 `BattleDamageResolver.ApplyEquipmentDurabilityDamageToSelection(...)` 的 selected-target commit，不能在能力 handler 中二次随机或直接改 `EquipmentInstanceState.current_durability`。
- `AddDamageDiceActionPayloadDefinition.RequireWeaponDamage` 默认 true，保持既有武器能力只对含武器伤害的攻击生效。内容只有显式设为 false 时，才允许徒手或法术攻击的主直接伤害段取得该装备附伤；miss、DOT、地形、反射和装备产生的额外段仍由既有伤害段规则排除。
- `AddDamageDiceActionPayloadDefinition.ReplacementGroupId` 非空时，`BattleEquipmentAbilityBonusDamageReplacementRules` 对同组 action bundle 只保留最高 `ReplacementPriority`；同优先级按 binding id、action id ordinal 稳定决胜。不同组与未分组附伤仍可叠加，获胜动作的全部骰项作为一包保留；高优先级来源消失后，低优先级候选在下次只读收集时自动恢复。
- 来源消失、换装或装备摧毁后，`BattleUnitFactory.RefreshEquipmentProjection(...)` 先原子提交完整 equipment-ability projection，再由 runtime service 清理声明了 source-missing 语义的 target mark，并同步 changed unit/event facts。
- Preview 与 commit 共用只读规则收集；`BattleDetachedPreviewState` 深复制当前装备/致死规则需要的 units、cells、environment 与 world step，并由显式 required source/target clone 覆盖同 ID 的通用副本。objective、barrier、target mark、temporary edge、backpack/report 等尚未进入该 detached owner，因此新增依赖这些事实的预览规则前必须先扩展 clone 合同。装备授予技能在基础 canonical `PreviewCommand` 通过后，于新的 detached state 复用正式 `ResolveGrantedSkillUsed` 后置动作，向预览公开 reaction action summaries 与 `source_preview_after`，但不提交 usage 或真实 store；该来源快照不合并基础技能自身造成的 HP/status 变化，后者仍由基础 skill preview 字段单独表达。AI mutation guard 通过共同 owner 的 raw exact snapshot 保留 owner 缺失、三个组件各自的 null/empty、null entry、嵌套字段 null 和原始顺序，并输出 `equipment_ability_sources` / `temporal_progress_modifiers` / `cognition_ceiling_modifiers` 三个 stable key；它必须检测装备能力来源、认知上限、状态、mark、召唤、cells、environment、world step 和相关计数的非法变化并立即失败，不承担状态回滚。
- Continuation branch 只在全部 unit detached plain snapshot、全部 per-battle charge 与 world step 的 future-observable signature 一致时合并并累加概率；finalized reaction 中 branch-local 确定的 `modify_ability_state` 会写入 branch，从而阻止后续伤害段重复触发一次性能力。当前 frontier 没有硬上限；本地 roll gate 处于 `(0,10000)` 的 finalized 状态动作只生成条件摘要，不再把其状态分裂继续传给后续段。正式凤凰内容的 finalized 动作本地概率均为 `10000`，因此本套的连续伤害预览走精确分支；新增本地概率状态动作或大量 fatal 来源前必须先补 bounded/truncated 合同。

## 代表性回归

- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- `tests/runtime/validation/run_equipment_ability_json_content_regression.cs`
- `tests/battle_runtime/rules/run_equipment_durability_selected_target_regression.cs`
- `tests/battle_runtime/rules/run_attack_policy_parity_regression.cs`
- `tests/battle_runtime/rules/run_damage_context_typed_regression.cs`
- `tests/battle_runtime/runtime/run_executioner_axe_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_sands_time_weapon_ability_regression.cs`
- `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_owner_api_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`
- `tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_fatal_intercept_runtime_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_mitigation_aura_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_bonus_damage_replacement_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_movement_trail_regression.cs`
- `tests/battle_runtime/rendering/run_equipment_movement_trail_overlay_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_finalized_damage_status_ac_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_ability_preview_integrity_regression.cs`
- `tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_equipment_granted_skill_regression.cs`
- `tests/battle_runtime/runtime/run_gorgon_crossbow_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_scorpion_bow_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_ravenplume_weapon_ability_regression.cs`
- `tests/battle_runtime/runtime/run_wolf_bow_weapon_ability_regression.cs`
- `tests/battle_runtime/skills/run_warrior_taunt_cognition_regression.cs`

技能共用合同见 [`skill_runtime.md`](skill_runtime.md)，套装阈值来源见 [`../progression/equipment_sets.md`](../progression/equipment_sets.md)，武器投影见 [`weapon_dice_and_equipment.md`](weapon_dice_and_equipment.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-10、CU-13、CU-15 和 CU-16。
