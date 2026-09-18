# 承受攻击劣势防护与熟练度

状态：Current / Implemented。核对日期：2026-09-15。

本文记录当前通用机制。幻身术的作者参数、60–100 场培养目标及算术前提见[内容设计记录](../../proposals/battle/skills/mage_blur.md)；验证结果见[本次回归记录](../../reviews/2026-09-15-mage-blur-landing.md)。

## 内容与状态合同

`incoming_attack_roll_disadvantage` 是 `status` / `apply_status` 效果的布尔字段，要求正持续时间。生产 JSON 经 `SkillCombatEffectJsonDto`、import model、`SkillDefinitionProjector` 进入不可变 `CombatEffectDefinition.IncomingAttackRollDisadvantage`；schema、normalizer、诊断 fixture 与 definition validator 同步处理。禁止放入无类型 payload 或用于 damage 效果。

`BattleStatusSemanticTable` 把效果投影为 `BattleStatusEffectState`。`blurred_form` 注册显示名“幻身”，为可驱散的有益状态；同状态按通用刷新规则取剩余时间与新持续时间的最大值，不累加、不叠加强度。命中与施法者正常行动不会消费此状态。通用驱散由 `BattleDamageResolver.ApplyDispelMagicEffect` 执行，时长由 `BattleRuntimeSkillTurnResolver` 推进。

该字段随 `DuplicateState`、preview clone、mutation snapshot 与 `BattleStatusReadView` 保真。strict 状态快照只在值为 true 时输出此可选字段，读取时拒绝错误类型。AI mutation stable projection 已反射覆盖公共状态字段，无第二份手写状态清单。

## 命中与预览

[`BattleIncomingAttackDisadvantageRules`](../../../scripts/systems/battle/rules/BattleIncomingAttackDisadvantageRules.cs) 是防守方贡献的共同判定入口；[`BattleHitResolver`](../../../scripts/systems/battle/rules/BattleHitResolver.cs) 的正式 metadata 和 fate-aware preview 都在既有来源劣势之后调用它，再统一处理优势抵消。

- 只作用于普通攻击检定，包括近战、远程、法术及接触攻击；不按距离、投射物或技能 ID 分支。
- 攻击者已有劣势时不再增加贡献。攻击者有优势时，新贡献参与抵消；多份防护不产生多重劣势。
- `AttackContext.HasIsDisadvantage` 仍只覆盖来源侧劣势。明确覆盖为 false 不会跳过防守方贡献。
- 无效检定、死亡目标、已到期状态、`ForceHitNoCrit` 与 `ForceHitAllowCrit` 不获得此贡献；后者的暴击门也不受其影响。
- 豁免与直接效果没有进入普通攻击检定，因此不经过此规则。其余幸运、天然点数、暴击规则继续由原命中 owner 拥有。

多个状态同时提供能力时，按状态 ID 的既有稳定排序选择首个贡献者，写入 `AttackResolutionMetadata.IncomingDisadvantageStatusId`，用于唯一归因。

## 直接熟练度事件

闭合触发值 `incoming_attack_disadvantage` 对应 `CombatSkillMasteryTriggerMode.IncomingAttackDisadvantage`。主动施放结果处理明确排除此触发，施加、刷新、失败与 preview 均不直接授予熟练度。

当前链路：

```text
BattleHitResolver -> 实际攻击 metadata 的贡献状态 ID
  -> BattleDamageResolver 捕获奖励事实
  -> IBattleIncomingAttackDisadvantageSink
  -> BattleSkillExecutionOrchestrator.IncomingDefense
  -> BattleSkillMasteryService 校验来源并计算数量
  -> 本次攻击效果及反应结算后提交
  -> BattleRuntimeModule.ApplySkillMasteryGrantTyped
  -> character gateway / ProgressionService
```

每次真实独立攻击检定最多授予一次。同一检定的多个伤害段不重复计量；攻击最后命中或未命中均可获得，依据是防护实际加入了新的攻击干扰，无法据此断言该次伤害原本必定发生。

来源必须是活着的角色单位，拥有状态记录的已学主动技能，且该技能声明此触发；攻击者必须与来源敌对。奖励按状态的 `source_unit_id` / `source_skill_id` 归属，不按防守者当前选中技能归属。捕获阶段只生成 typed reward，不升级角色；提交发生在该次伤害与相关反应处理之后，防止熟练度升级改变正在结算的伤害。

数量复用现有 `mastery_amount_mode`。幻身术配置 `per_target_rank` 和 `mastery_base_amount = 1`：普通攻击者 1 点、精英 2 点、首领 3 点。冗余劣势、强制命中、友军攻击、无有效来源技能或已失效保护均为 0。没有每场上限、重复敌人递减或新的存档累计器。

`BattleRuntimeModule` 随 damage resolver 的初始化/替换/释放绑定并清除此 sink。奖励结构不跨战斗生命周期保存。

## 与战后评级及等级上限的关系

`BattleRatingSystem.RecordSkillSuccess` 记录成功施放次数；`BattleSkillMasteryService.BuildBattleRatingMasteryRewardEntries` 对本场用过的每项技能生成固定奖励项。评分达到 2 / 4 / 6 时分别为 2 / 4 / 6 点，低于 2 为 0；不会乘施放次数。奖励经过正常待领取/应用链路入账，独立于上面的直接事件。

因此“施放不直接加点”不等于“空放绝不可能获得任何评级奖励”。当前全局评级语义保留。训练与任务等允许来源也照常生效。

`ProgressionService.LearnSkill` 使普通新学技能从 0 级开始。`GrantSkillMastery` 按 `GetMasteryRequiredForLevel(currentLevel)` 逐级扣除，达到有效上限后清零 `current_mastery`，继续记录累计已获数量。幻身术当前逐级需求为 `[60,100,150,210,280,380,500]`，0→5 累计 800；额外熟练度不能绕过 `SkillEffectiveMaxLevelRules` 的晋升历史门槛。此变更没有增加存档字段、版本或历史进度重算/迁移。

## AI 评分

[`BattleAiScoreService.IncomingDefense`](../../../scripts/systems/battle/ai/BattleAiScoreService.IncomingDefense.cs) 对防守者建立只读副本，应用候选保护并分别推进已有/新保护到敌人下次行动。仅估计下一次敌方 activation，要求其在保护到期前、技能冷却与资源允许、未被施法阻止、处于现有威胁射程内，且攻击走普通 fate attack。

每个敌人保留一个候选攻击的最大边际防护预算，使用 canonical 命中概率差乘既有 AI 的 `IncomingBudgetDamage`。汇总到 `estimated_incoming_attack_damage_relief`，复用 `DamageWeight`；不增加普通 status/control 分数，也不按持续 TU 乘虚构的未来攻击次数。字段随 score input、fingerprint 和 detached decision trace 保真。

这是局部威胁估值：沿用当前射程/可支付预测，不预测转火、未来移动及完整多轮策略，也没有完整重演候选攻击的所有屏障/装备修正及暴击伤害分布。不能将此值标为精确预期减伤或战斗平衡证据。

## 聚焦回归

[`run_mage_blur_regression.cs`](../../../tests/battle_runtime/skills/run_mage_blur_regression.cs) 从生产 JSON 读取技能，覆盖 0–7 级、真实付费施法/刷新/到期/驱散、typed 状态往返、命中概率、防护熟练度去重和排除项、799/800 点升级边界、评级独立入账、AI 新旧保护边际及不可用威胁。公共命中、状态、幸运公式及 mutation guard 结果见本次回归记录。
