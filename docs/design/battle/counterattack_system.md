# 反击系统

> 状态：当前实现真相
> 更新日期：2026-09-14
> 关联上下文：CU-12、CU-14、CU-15、CU-16、CU-18、CU-19

## 范围

当前实现提供反击的运行时闭环、battle-local 状态、真实即时武器攻击和武器精通成长。它不安装任何生产 capability 内容，也不决定默认能力来源、最终数值、属性派生或 AI 权重。

生产内容只有显式安装 `BattleCounterattackCapability` 后才会启用反击。运行时不按技能 ID、武器 ID、日志文本或状态参数推导反击能力。

## 运行时主链

```text
mutation root
  -> BattleAttackActionCoordinator.BeginReactionBoundary(batch)
  -> BattleEffectExecutionContextService.Push(origin)
  -> BeginLogicalAttack(deliveryKind)
  -> BattleDamageResolver.ResolveAttackEffects(..., AttackContext.Action)
  -> IBattleAttackResolutionSink.OnAttackResolved(fact, same batch)
  -> BattleCounterattackSystem 捕获、去重、FIFO 入队
  -> 原 producer 完成装备反应、耐久、死亡、贡献、评分与 metrics
  -> 最外层 boundary.Complete()
  -> FIFO drain
  -> eligibility 复核
  -> 原子扣 reaction charge + stamina
  -> chance roll
  -> BattleImmediateWeaponAttackService.Execute(...)
```

`BattleAttackActionCoordinator` 是 root batch、root/action ID、logical-action stack、边界深度和 work guard 的唯一 owner。一个 active root 只允许一个 `BattleEventBatch` 实例。nested boundary、AutoCast、repeat、charge、ground、equipment reaction 和 random-chain 都复用该 batch。

`BattleEffectExecutionContextService` 是 effect origin stack 的唯一 owner。普通 command、timeline 和 AutoCast origin 可以触发反应；`BattleEffectOrigin.Counterattack(...)` 保留原攻击 action ID，但关闭直接反击递归。反击仍可同步触发现有 contingency 和装备反应。

## 攻击事实与 delivery

`BattleAttackActionContext` 冻结：

- `BattleAttackActionId`
- `RootBoundaryId`
- `BattleEffectOrigin`
- `BattleAttackDeliveryKind`

`BattleDamageResolver` 只有 required-context 的五参 `ResolveAttackEffects(...)` 正式入口。miss 和 hit 都在完成各自同步后处理后，通过同一 finalizer 发布 immutable `BattleAttackResolutionFact`；fact 不持有 live unit 或 battle state。

`BattleAttackDeliveryRules` 是 delivery 分类唯一 owner：

- 不含武器伤害：`NonWeapon`
- 武器伤害 + `melee` 投影：`MeleeWeapon`
- 武器伤害 + `ranged` 投影：`RangedWeapon`
- missing owner、无效射程或未知 range type：`Unknown`

production logical attack 遇到 `Unknown` 立即失败，不能降级为非武器攻击。物品 JSON 导入由 `ItemImportValueRules` 校验 `{melee, ranged}` 闭集，`BattleWeaponRangeTypeNames` 复用同一 token。敌人 `EnemyTemplateProjectionRules` 将 natural/unarmed 武器默认投影为 melee，`EnemyContentRegistry` 对最终武器投影的空值或未知 range type 失败关闭；不恢复已删除的 enemy Resource authoring 类型。

当前主线的 ordered slots、directional piercing、sequential line hit、line-through attack、spell reaction 和 ranged-weapon reaction 都显式创建 logical action；多目标／多段 producer 在原结算完成后才释放该作用域，反击仍在 root 完成时统一排空。

## 捕获、去重与执行

当前 trigger 闭集只有：

- `MeleeHitReceived`
- `MeleeAttackEvaded`

事实发布时按 capability 的稳定优先级选择候选，并以 `(ActionId, attacker, defender)` 去重。同一逻辑攻击的多段只产生一次机会；独立 follow-up action 可再次产生机会。

排空时按以下顺序 fail closed：

1. fact/action/origin 有效
2. 捕获的 capability 实例仍存在且完整值未改变
3. defender 与 attacker 仍存在、存活且 HP 为正
4. 双方不同且敌对
5. trigger 仍匹配
6. 未被 `lock_counterattack` 或硬控阻止
7. 有 reaction charge
8. weapon action definition 可用
9. 当前装备满足 weapon family、精确 weapon type、重型武器、盾牌、当前武器、近战武器和排除武器约束
10. 射程、屏障、stamina 合法

第 9 步与正式施法共同调用 `BattleSkillWeaponRequirementRules`，并保持 `required family → required type → heavy weapon → shield → current weapon → melee weapon → excluded family → excluded type` 的唯一判定顺序。反击只把失败折叠为既有 `AttackUnavailable`，不扩展公开 block-reason ABI；失败发生在 attempt 成本和 chance RNG 之前。

attempt 成本由 `BattleUnitState.TryCommitCounterattackAttemptCostTyped(...)` 原子提交：charge 与 stamina 要么同时扣除，要么都不变。chance 失败仍消耗 attempt 成本；0%/100% 不调用共享 chance RNG，1%–99% 恰好调用一次。

反击队列同步排空到空。damage hook 或 equipment reaction 产生的 nested work 按实际发布时间进入同一 FIFO。depth/work guard 超限会使整个 root 失败并清空 transient queue/dedupe/action 状态；旧 generation 的 scope handle 在 battle reset 后只做 no-op。

所有 production root 和 logical-attack owner 都在自身 `using` scope 内捕获异常、先调用 `AbortActiveReactionBoundary()` 再原样重抛。这样 resolver、producer、drain、depth/work guard 的原始异常不会被作用域退出时的 `"disposed without Complete()"` 合同异常覆盖；清理后的下一 root 从空队列开始。

`BattleChargeResolver` 的正常出口只有一个 `logicalAttack.Complete()`：实际移动、起步即受阻和无效果返回都先汇总 `commandHandled` 再完成 scope。起步即受阻仍是已结算命令，费用与冷却保留，但不产生移动熟练度、攻击事实或反击机会。

## 真实即时武器攻击

`BattleImmediateWeaponAttackService` 是 counterattack 和 equipment immediate weapon attack 的共享执行 owner。prepared plan 冻结 source/target/state、definition/effects、delivery、stamina、attack bonus、来源归因和 counterattack 的 weapon-training skill ID。

query 与 execute 使用同一 plan，并按 definition → current weapon requirements → range → barrier → stamina 的顺序检查。跨 battle 使用旧 plan 会在读取或提交业务状态前失败。

`BattleWeaponAttackOutcomeCommitter` 共享四个顺序阶段：

1. resolver surface
2. post-producer hooks
3. applied/unapplied result surface
4. terminal outcome

标准武器技能、反击与装备追击使用 `WeaponAttackOutcomeKind` 区分策略。反击复用正式武器伤害、装备附伤/反应、耐久、combo、contingency、死亡、贡献、rating、metrics 和 kill provenance，但不消费 AP、不设置 cooldown/cast，也不获得 action definition 自身的熟练度。

即时攻击显式标记伤害来源：counterattack 为 `MainDirectEffect`，equipment immediate 为 `EquipmentTriggeredSkill`，保留现有装备直接伤害加骰的来源门槛。装备能力 JSON registry 的 validation context 持有 immutable skill definitions；`immediate_weapon_attack` 必须引用包含武器伤害的定义，并保留自动路径禁止 windup 的校验。

## 反应预算与状态

`BattleUnitReactionState` 持有 capacity、remaining charge、recharge interval 和 `NextRechargeAtTu`。`BattleUnitCounterattackCapabilityState` 持有当前 battle-local capability 列表。两者都显式区分 owner missing、present-empty 和 present-zero。

`BattleReactionBudgetRules.InitializeUnitForAdmission` 在单位进入 `BattleState` 前初始化两个 owner。该规则只读取传入 state 的当前 TU 并写入新单位，因此生产 admission 与召唤 preview 的克隆 state 共享规则，不要求 preview state 绑定到 live runtime。它们进入：

- gameplay clone
- strict codec
- canonical/detached snapshot
- AI mutation-exact snapshot

它们不进入持久存档。

时间线、反应预算、体力恢复、状态、遭遇与地形校验共同消费纯规则 `BattleTimeRules.TuGranularity`；离线 JSON 校验器链接同一源文件。跨多个 interval 可一次补满且 anchor 不右漂。`time_stasis` 整步平移未来 recharge anchor，不补 charge；即使 stasis 在本 step 末到期，也从下一 step 才恢复正常推进。

## 武器精通与职业升级

反击在 `Applied` 且满足由 runtime setup 显式注入基础攻击角色 ID 所对应 definition 的 `weapon_attack_quality` 条件时，按攻击前冻结的武器投影发放：

- sword → `sword_training`
- bow → `bow_training`
- unarmed/natural → `unarmed_training`

当前生产基础攻击角色为 `basic_attack`；数量使用该 definition 的 `per_target_rank` 规则：normal/elite/boss 分别为 1/2/3。反击 action definition 的 mastery policy 不参与该判断，也不获得 action mastery。

`SkillProfessionPromotionRules` 统一认定带 `weapon_training` 标签的技能不能作为职业升级 trigger。`PromotionEligibilityRules.IsReadyTrigger` 消费此规则，晋升候选发现与 `ProgressionService.PreparePromotion` 共用该资格入口。武器训练仍可学习、设为 core、增长熟练度和技能等级，并保留核心资格，但不得作为本次晋升的成长触发技能。`CharacterManagementModule` 只过滤本次武器训练 mastery delta 的 pending choice/modal；其他普通技能的晋升候选仍按正式规则计算。

## Preview 与 HUD

反击风险不是 preview contract。`BattleCommandPreviewService`、`BattleSkillPreviewService` 和所有 producer preview 都不查询反击 capability/readiness，不计算反击概率、期望次数或伤害，也不为了反击去解释多段攻击的 continuation policy。

`BattlePreview`、`BattlePreviewProjection`、`BattleHudAdapter` 与 `BattleHudSnapshot` 均不得携带或投影反击风险字段。真实反击只能由正式攻击提交后发布的 `BattleAttackResolutionFact` 触发，并在最外层 boundary 完成时按当前状态复核；因此 preview 结果不能被误认为执行承诺，也不会泄露敌方 capability、block reason、stamina、reaction budget 或潜在反击信息。

HUD 可以显示当前 party-backed focus unit 自己的 detached reaction budget；这属于己方既有状态展示，不是对当前预览命令的反击风险推导。非 party-backed focus unit 的精确反应预算固定隐藏。

## 生命周期与验证

runtime 保留当前 capability bridge 边界；charge/timeline 通过各自 runtime port 获取 boundary/action/origin 操作，CounterattackQuery 在 CommandPreview 前绑定，teardown 逆序断开并保留异常汇总。reaction coordinator/counterattack system 在 borrower teardown 前停止接单、解绑 sink 并清空 transient state。

主要回归入口：

- `tests/battle_runtime/runtime/run_battle_counterattack_action_contract_regression.cs`
- `tests/battle_runtime/runtime/run_battle_counterattack_queue_regression.cs`
- `tests/battle_runtime/runtime/run_battle_counterattack_execution_parity_regression.cs`
- `tests/battle_runtime/state_schema/run_battle_counterattack_state_regression.cs`
- `tests/battle_runtime/rules/run_battle_skill_weapon_requirement_rules_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_melee_charge_behavior_regression.cs`
- `tests/progression/core/run_weapon_training_promotion_policy_regression.cs`
- `tests/static_analysis/run_battle_reaction_contract_static_regression.cs`

完整设计推导、逐调用点迁移清单与未来内容阶段仍保留在 `docs/proposals/battle/counterattack_system.md`。
