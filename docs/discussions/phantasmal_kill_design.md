# 怪影杀戮（Phantasmal Kill）落地实现方案

状态：仓库语境校正版，用户决策已确认，进入实现准备。本文只更新讨论文档；本次不修改运行时代码，也不修改 `docs/design/project_context_units.md`。真正代码落地后，若 runtime 关系、owner 边界或推荐 read set 改变，再同步更新 context units。

## 目标

`怪影杀戮` 是一个幻术 / 恐惧 / 精神系大范围处决法术。它不按 D&D 原版逐字复刻，而是用当前项目的 typed battle rules 表达：

| 项 | 规格 |
| --- | --- |
| 技能 ID | `mage_phantasmal_kill` |
| 目标模式 | 地面选点，影响范围内单位 |
| 射程 | 12 格 |
| 范围 | 7x7，使用现有 `square` 区域，`area_value = 3` |
| 目标队伍 | `any`，影响敌我双方可受幻术影响的单位 |
| 豁免 | `willpower`，`save_tag = &"illusion"`，DC 使用施法者法术 DC |
| 伤害标签 | `psychic` |
| 核心效果 | 四级豁免，低生命阈值处决，高生命造成精神伤害和控制 |

不添加旧 ID、旧字段、兼容别名或旧 payload/schema 支持，除非用户另行确认。

## 当前仓库事实

本方案已按当前 C# 主线重新校正，旧 `.gd` 路径不再作为实现目标。

- effect type 的闭合集合由 `scripts/systems/battle/_interop/BattleTypedEnums.cs` 中的 `BattleEffectKind` / `BattleTypedNames` 负责，`CombatEffectDef.EffectKind` 从这里解析。`SkillContentRegistry` 不再维护单独的 `VALID_EFFECT_TYPES` 字符串列表。
- unit / ground / AI offensive effect 分类也主要在 `BattleTypedNames`，但 `BattleSkillExecutionOrchestrator._is_unit_effect()` 仍有本地 switch，需要同步扩展或收口。
- `BattleSkillResolutionRules.IsUnitEffect()` 已委托 `BattleTypedNames.IsUnitPayloadEffect(...)`。
- `BattleSaveResolver.ResolveSaveResult(...)` 已返回 `BattleSaveResult.Degree`，类型为 `BattleSaveDegreeKind`：`CriticalFailure` / `Failure` / `Success` / `CriticalSuccess`。运行时分级不需要重新用 natural roll 手写一套；AI 概率估算才需要枚举分级分布。
- `BattleSaveResolver` 已支持 `save_advantage_tags` 中的 `illusion_immunity` / `illusion_disadvantage` / `illusion_advantage` 后缀语义；免疫结果 `Immune = true`，同时 natural roll 为 0，必须先判断免疫。
- `BattleUnitState` 已有 unit-level `save_advantage_tags`，但 `EnemyTemplateDef` 当前没有对应导出字段，`EncounterRosterBuilder` 也没有把敌人模板豁免标签投影到战斗单位。
- `BattleAiScoreInput` 已有 `estimated_friendly_fire_target_count`、`estimated_friendly_lethal_target_count`、`friendly_fire_reject_reason` 等友伤字段，`UseGroundSkillAction.PassesFriendlyFireLimits(...)` 已消费这些字段。
- `BattlePreview` 已有 `save_branch_preview` 与 typed damage preview 边界；本技能应扩展现有 preview surface，不新增特殊 profile。
- 当前技能体系已支持 9 级甚至 10 级技能：`SkillContentRegistry` 不设 5 级上限，普通静态技能只要求 `mastery_curve.Length == max_level`；现有资源中已有大量 `max_level = 9` 技能。`level_description_configs` 的通用校验要求声明区间连续且不超过 `max_level`，不是全局强制 0..max。

## 用户已确认决策

以下实现口径已确认：

1. 技能定位：按“9 级高阶处决法术”落地。
   - 当前技能体系支持 `max_level = 9`，本技能直接按 9 级技能配置。
   - 内容表达使用 `max_level = 9`、`growth_tier = &"ultimate"`、`tags` 含 `ultimate`，资源消耗、mastery curve、成长预算按 9 级高阶处决技能处理。
   - 不新增 `spell_rank` 字段；本次 9 级由现有 `max_level` 体系表达，独立“法术环阶 / spell rank”不属于本技能的落地范围。

2. 状态口径：新增正式状态 ID `frightened`。
   - `frightened` 是新内容使用的规范恐惧状态。
   - 现有强力攻击劣势列表里的 `fear` / `feared` / `terrified` 只视为旧式状态名，不作为 `怪影杀戮` 的新资源 ID。
   - `aftershock`、`reaction_lock`、`frightened`、`stunned` 的差异见“状态语义”一节。

3. 守护反应封锁：新增 typed `lock_guard` 字段。
   - 增加 `CombatEffectDef.lock_guard` 与 `BattleStatusEffectState.lock_guard`。
   - `BattleRuntimeModule.IsUnitGuardLocked(...)` 改为读取状态集合中的 typed `lock_guard`。
   - 不复用 `guard_block`；它当前表示伤害减免/格挡数值，不是守护反应封锁。

4. 敌人“无心智 / 幻术免疫”表达：复用 `save_advantage_tags`。
   - 在 `EnemyTemplateDef` 增加导出字段 `save_advantage_tags: Array[StringName]`。
   - `EncounterRosterBuilder` 构建 `BattleUnitState` 时复制该字段。
   - 内容侧用 `illusion_immunity` 表示不受 `怪影杀戮` 影响的无心智或幻术免疫敌人。

## 最终技术路线

采用“标准地面技能 + 新 typed unit effect”的路线：

1. 新增 `BattleEffectKind.GradedSaveExecute`，在 `BattleTypedNames` 中映射 `effect_type = &"graded_save_execute"`。
2. 将 `GradedSaveExecute` 纳入 `BattleTypedNames.IsUnitPayloadEffect(...)` 与 `BattleTypedNames.IsAiOffensiveEffect(...)`。
3. 同步扩展仍未收口到 `BattleTypedNames` 的本地分类 switch，至少包括 `BattleSkillExecutionOrchestrator._is_unit_effect()`、AI intent / affordance / hostile threat 相关分类。
4. `mage_phantasmal_kill.tres` 走现有地面技能流程：目标校验、消耗、范围收集、预览、AI 候选枚举继续使用 ground skill 管线。
5. `BattleDamageResolver` 增加 `GradedSaveExecute` 解析分支，负责四级豁免、条件处决、精神伤害和状态附加。
6. 不新增 `special_resolution_profile_id`，不在 `BattleSkillExecutionOrchestrator` 增加 `phantasmal_kill` 专用命令分支。

选择该路线的原因：

- 7x7 地面范围已经由 `BattleGridService` / `BattleGroundEffectService` 覆盖。
- 复用 `BattleGroundEffectService` 可以保留击杀后的掉落、成就、评分、战斗结束检查和清场链路。
- `怪影杀戮` 的复杂点在单个 per-target effect 内，不需要 Meteor Swarm 级别的多阶段 special profile。
- `effect_defs` 非空后，预览、AI、资源校验和内容验证都有可读输入，避免“空 effect_defs 但运行时硬编码”的不可见行为。

## 明确不采用的方案

| 方案 | 结论 | 原因 |
| --- | --- | --- |
| 直接在 orchestrator 中硬编码 `phantasmal_kill` | 不采用 | 会绕开通用地面效果和击杀提交链，容易漏掉掉落、成就、评分、战斗结束检查 |
| 直接把目标 `current_hp = 0` | 不采用 | 会绕开 `death_ward`、last stand、fatal trait、伤害事件和清场路径 |
| 空 `effect_defs` + special profile | 不采用 | 本技能不需要多阶段 profile，且会扩大 AI/预览适配面 |
| 新增 `mental` 豁免标签 | 不采用 | 当前 `BattleSaveContentRules` 已有 `illusion`、`charm`、`frightened` 等心智/控制标签 |
| 为旧技能 ID 增加 alias | 不采用 | 兼容策略禁止未确认的 legacy alias |

## 行为规格

### 有心智单位判定

最小实现不新增“mind”生物学字段。目标是否受影响由现有 save tag 体系表达：

- 若目标通过 unit-level `save_advantage_tags` 或状态 typed fields 获得 `illusion_immunity`，`怪影杀戮` 完全无效。
- 敌人模板需要新增导出字段 `save_advantage_tags: Array[StringName]`，并在 `EncounterRosterBuilder` 构建 `BattleUnitState` 时复制到 unit-level `save_advantage_tags`。
- 内容作者表达“无心智”单位时，给敌人模板配置 `illusion_immunity`；如需要服务其他技能，可同时配置 `charm_immunity`、`frightened_immunity`。

注意：`BattleSaveResolver.ResolveSaveResult(...)` 对免疫目标会返回 `Immune = true`、`Success = true`、`NaturalRoll = 0`、`Degree = CriticalSuccess`。`graded_save_execute` 必须先判断 `Immune`，不能把 natural roll 0 当成大失败。

### 豁免分级

每个目标只解析一次豁免，运行时分级优先复用 `BattleSaveResult.Degree`：

| 分级 | 判定 |
| --- | --- |
| `immune` | `saveResult.Immune == true` |
| `critical_success` | 非免疫且 `saveResult.Degree == BattleSaveDegreeKind.CriticalSuccess` |
| `success` | 非免疫且 `saveResult.Degree == BattleSaveDegreeKind.Success` |
| `failure` | 非免疫且 `saveResult.Degree == BattleSaveDegreeKind.Failure` |
| `critical_failure` | 非免疫且 `saveResult.Degree == BattleSaveDegreeKind.CriticalFailure` |

AI 估算不能只用 `EstimateSaveSuccessProbabilityResult(...)` 的二分概率；需要新增分级分布估算，枚举 normal / advantage / disadvantage / override rolls 下各分级 basis points。

### 分级效果

| 分级 | 效果 |
| --- | --- |
| `immune` | 无效果，不造成伤害，不附加状态 |
| `critical_success` | 完全无效 |
| `success` | 附加 `aftershock` 1 轮：不能反击，也不能使用守护反应 |
| `failure` | 若当前 HP <= `max(50, floor(max_hp * 25%))`，执行处决；否则造成 `6d6 psychic`，附加 `frightened` 2 轮和 `reaction_lock` 1 轮 |
| `critical_failure` | 若当前 HP <= `floor(max_hp * 35%)`，执行处决；否则造成 `10d6 psychic`，附加 `frightened` 3 轮和 `stunned` 1 轮 |

处决不是直接写 HP，而是通过 `BattleDamageResolver` 内部正式伤害提交路径造成“当前 HP 数值”的致命精神伤害：

- `damage_tag = &"psychic"`
- `bypass_shield = true`
- `min_hp_after_damage = 0`
- `bypass_death_prevention = false`
- 保留 `death_ward`、last stand、fatal trait、伤害事件记录和后续 `BattleGroundEffectService` 击杀提交链

非处决伤害必须走现有 damage outcome / mitigation 路径，保留精神抗性、免疫、护盾吸收、伤害事件和预览统计。

## 数据配置

新增文件：

- `data/configs/skills/mage_phantasmal_kill.tres`

资源格式按现有 C# `SkillDef` / `CombatSkillDef` / `CombatEffectDef` `.tres` 样式编写。9 级技能的 `max_level` / `mastery_curve` 参考现有 `max_level = 9` 主动技能；`mage_power_word_kill.tres` 只作为 execute 语义和高资源消耗参考；`mage_fireball.tres` 只作为 ground AoE 与 `level_description_configs` 样式参考。

核心字段：

```gdscript
skill_id = &"mage_phantasmal_kill"
display_name = "怪影杀戮"
icon_id = &"mage_phantasmal_kill"
skill_type = &"active"
max_level = 9
non_core_max_level = 7
mastery_curve = PackedInt32Array(360, 900, 1980, 3600, 5760, 8600, 12000, 16000, 21000)
tags = Array[StringName]([&"mage", &"magic", &"illusion", &"fear", &"psychic", &"execute", &"output", &"control", &"ultimate"])
learn_source = &"book"
growth_tier = &"ultimate"
attribute_growth_progress = {
	"intelligence": 160,
	"willpower": 80
}

combat_profile.target_mode = &"ground"
combat_profile.target_team_filter = &"any"
combat_profile.target_selection_mode = &"single_coord"
combat_profile.range_value = 12
combat_profile.area_pattern = &"square"
combat_profile.area_value = 3
combat_profile.ap_cost = 3
combat_profile.mp_cost = 2000
combat_profile.aura_cost = 2
combat_profile.cooldown_tu = 600
combat_profile.ai_tags = Array[StringName]([&"large_aoe", &"ultimate", &"execute", &"friendly_fire_risk"])
combat_profile.delivery_categories = Array[StringName]([&"spell", &"illusion", &"fear", &"psychic"])
combat_profile.effect_defs = Array[ExtResource("...CombatEffectDef...")]([SubResource("graded_save_execute")])
```

不要写 `type`、`class_id`、`required_level`、`spell_rank` 或 `cooldown_turns`。当前 schema 使用 `skill_type`、`max_level` 与 `cooldown_tu`；本技能的 9 级定位直接落在 `max_level = 9`。职业/学习门槛若后续需要，走现有 `tags`、`learn_requirements`、`knowledge_requirements`、职业授予或技能前置系统。

`level_description_configs` 的通用 schema 只要求已声明等级连续且不超过 `max_level`，不强制从 0 覆盖到 max。但本技能作为新增正式主动技能，专用内容验证应要求 `"0"` 到 `"9"` 全覆盖，描述文案明确这是 9 级/ultimate 处决法术，并列出范围、豁免、处决阈值、失败伤害、状态和友伤风险。

`graded_save_execute` effect：

```gdscript
effect_type = &"graded_save_execute"
effect_target_team_filter = &"any"
damage_tag = &"psychic"
save_dc_mode = &"caster_spell"
save_dc = 0
save_dc_source_ability = &"intelligence"
save_ability = &"willpower"
save_tag = &"illusion"
params = {
	"profile_id": "phantasmal_kill",
	"failure_execute_threshold_fixed": 50,
	"failure_execute_threshold_max_hp_percent": 25,
	"failure_damage_dice_count": 6,
	"failure_damage_dice_sides": 6,
	"failure_frightened_duration_tu": 120,
	"failure_reaction_lock_duration_tu": 60,
	"critical_failure_execute_threshold_max_hp_percent": 35,
	"critical_failure_damage_dice_count": 10,
	"critical_failure_damage_dice_sides": 6,
	"critical_failure_frightened_duration_tu": 180,
	"critical_failure_stunned_duration_tu": 60,
	"success_aftershock_duration_tu": 60
}
```

字段校验要求：

- 在 `BattleTypedNames` / `BattleEffectKind` 增加 `GradedSaveExecute`，使 `CombatEffectDef.EffectKind` 不再返回 `Unknown`。
- `SkillContentRegistry.AppendEffectValidationErrors(...)` 增加 `AppendGradedSaveExecuteValidationErrors(...)` 专用校验。
- `profile_id` 必须为 `"phantasmal_kill"`；不要允许空 profile 静默通过。
- `params` 必须使用白名单，禁止错拼或多余 key 静默通过。
- 必需 key：`failure_execute_threshold_fixed`、`failure_execute_threshold_max_hp_percent`、`failure_damage_dice_count`、`failure_damage_dice_sides`、`failure_frightened_duration_tu`、`failure_reaction_lock_duration_tu`、`critical_failure_execute_threshold_max_hp_percent`、`critical_failure_damage_dice_count`、`critical_failure_damage_dice_sides`、`critical_failure_frightened_duration_tu`、`critical_failure_stunned_duration_tu`、`success_aftershock_duration_tu`。
- 骰子字段必须是正整数；百分比必须是 `1..100`；固定阈值必须 `>= 0`；所有 `*_duration_tu` 必须是 `SkillContentRegistry` TU 粒度的倍数。
- `effect_target_team_filter` 必须是 `any`，`damage_tag` 必须是 `psychic`，`save_dc_mode` 必须是 `caster_spell`，`save_ability` 必须是 `willpower`，`save_tag` 必须是 `illusion`。

## 状态语义

这里的状态 ID 是运行时状态效果 ID，不等同于技能顶层 `tags`，也不等同于 `save_tag`。`怪影杀戮` 的恐惧主题可以继续用顶层 `&"fear"` tag 辅助分类，但真正写入单位状态时使用下面的正式 ID。

旧式恐惧名的处理：

| ID | 当前处理 | 本技能是否使用 |
| --- | --- | --- |
| `fear` | 已出现在强力攻击劣势列表和部分 save tag 测试中，可视为历史通用恐惧名 | 不作为新状态写入 |
| `feared` | 已出现在强力攻击劣势列表中，语义像 `fear` 的旧式过去分词写法 | 不作为新状态写入 |
| `terrified` | 已出现在强力攻击劣势列表中，语义上像更强恐惧，但当前没有独立 typed 规则层级 | 不作为大失败专用状态写入 |
| `frightened` | 新内容使用的规范恐惧状态，也已有 save tag 语义基础 | 作为本技能唯一恐惧状态写入 |

不要把 `fear` / `feared` / `terrified` 做成本技能的兼容 alias。落地时只需要把 `frightened` 加入强力攻击劣势列表，保留旧名不动，避免破坏已有内容。

本技能使用的状态差异如下：

| 状态 | ID | 来源 | 核心语义 | 不承担的语义 |
| --- | --- | --- | --- | --- |
| 余悸 | `aftershock` | 豁免成功 60 TU | 轻度幻术余波；有害、可驱散、刷新持续时间；锁反击与守护反应；用于表示“你看穿了幻象，但精神仍短暂失衡” | 不造成攻击劣势，不清 AP，不代表恐惧 |
| 反应封锁 | `reaction_lock` | 豁免失败且未处决 60 TU | 明确关闭短反应窗口；有害、可驱散、刷新持续时间；锁反击与守护反应；可与 `frightened` 并存 | 不造成强力攻击劣势，不清 AP，不代表更深恐惧 |
| 恐惧 | `frightened` | 失败 120 TU / 大失败 180 TU | 主要战斗 debuff；有害、可驱散、刷新持续时间；强力攻击判定视为劣势来源；表达目标被幻象追猎、攻击动作失准 | 不直接锁反击/守护，不直接清 AP；这些由 `reaction_lock` 或 `stunned` 负责 |
| 震慑 | `stunned` | 大失败且未处决 60 TU | 硬控；有害、可驱散；对本技能施加的临时效果应清空本轮行动点，并显式锁反击与守护反应 | 不作为恐惧状态，不替代 `frightened` 的持续攻击劣势 |

强弱关系不要理解成 `aftershock -> reaction_lock -> frightened -> stunned` 的同一条升级链。它们分别对应不同轴线：

- `aftershock`：成功后的小惩罚，主要限制 reaction。
- `reaction_lock`：失败后的 reaction 关闭，解决“被幻象震住但仍能立刻替队友挡刀/反击”的不合理表现。
- `frightened`：持续恐惧，影响主动攻击质量。
- `stunned`：大失败硬控，短时间剥夺行动与反应。

状态落点：

- `BattleStatusSemanticTable` 增加上述状态常量、显示名、harmful / dispellable 归类和刷新语义。
- `BattleRuntimeSkillTurnResolver.DebuffStatusIds` 增加这些状态，或依靠 `counts_as_debuff_override = true` 的 typed 字段并补测试。
- `BattleState.StrongAttackDisadvantageStatusIdOrder` 增加 `frightened`；旧式 `fear` / `feared` / `terrified` 保留原状，不新增 alias 映射。
- `BattleRuntimeSkillTurnResolver.HasCounterattackLockStatus(...)` 已读取 typed `BattleStatusEffectState.lock_counterattack`；`aftershock` / `reaction_lock` 应设置 `CombatEffectDef.lock_counterattack = true`，不要只把 `"lock_counterattack": true` 塞进 residual params。
- 新增 typed `lock_guard` 字段：`CombatEffectDef.lock_guard`、`BattleStatusEffectState.lock_guard`、schema roundtrip、projection、merge 逻辑都要同步。`BattleRuntimeSkillTurnResolver` 增加 `HasGuardLockStatus(...)`，`BattleRuntimeModule.IsUnitGuardLocked(...)` 改为保留黑星烙印硬编码并额外读取 typed guard lock。
- `aftershock` / `reaction_lock` 应同时设置 `lock_counterattack = true` 与 `lock_guard = true`。
- `frightened` 不设置 reaction lock 字段；它只通过强力攻击劣势表达恐惧压力。
- `怪影杀戮` 施加的 `stunned` 临时 effect 应在命中结算中立刻加到目标身上，并在同一次 resolver 流程里清空目标当前 AP 与移动点；同时设置 `lock_counterattack = true` 与 `lock_guard = true`。若未来要让所有来源的 `stunned` 都统一锁反应或统一清 AP，需要另做影响面评估。
- `BattleDamageResolver` 附加这些状态时，构造临时 `CombatEffectDef` 并走现有 `ApplyStatusEffect(...)` 路径，复用 `BattleStatusSemanticTable.MergeStatus(...)` 的刷新、持续时间和 typed fields 合并语义。

## 运行时实现

### 1. 规则层

新增：

- `scripts/systems/battle/rules/BattleGradedSaveExecutionRules.cs`

职责：

- 从 `BattleSaveResult` 计算分级，先处理 `Immune`。
- 从 `CombatEffectDef` 解析 `PhantasmalKill` 专用 typed params，避免规则层反复散读 `Dictionary`。
- 计算失败分支处决阈值：`max(fixed, floor(max_hp * percent / 100.0))`。
- 计算大失败分支处决阈值：`floor(max_hp * percent / 100.0)`。
- 提供平均伤害估算给 AI 复用。
- 提供 `EstimateGradeDistribution(...)`：枚举 normal / advantage / disadvantage / override rolls 的自然骰分布，返回 `Immune`、`CriticalSuccess`、`Success`、`Failure`、`CriticalFailure` basis points。

### 2. 伤害解析

修改：

- `scripts/systems/battle/rules/BattleDamageResolver.cs`
- `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- 如需要复用非处决伤害构建，也同步调整 `BattleDamageResolver.Dice.cs` / `BattleDamageResolver.Mitigation.cs` 中的 helper 可见性。

新增 `GradedSaveExecute` 分支：

1. 调用 `BattleSaveResolver.ResolveSaveResult(source, target, effectDef, context.ToBattleSaveContext())`。
2. 把 save result 写入 `AttackEffectResolutionResult.SaveResults`。
3. 若分级是 `immune` 或 `critical_success`，返回 no-op 结果。
4. 若分级是 `success`，附加 `aftershock`。
5. 若分级是 `failure`：
   - 当前 HP 在阈值内：提交处决伤害。
   - 否则：提交 `6d6 psychic` 伤害，附加 `frightened` 和 `reaction_lock`。
6. 若分级是 `critical_failure`：
   - 当前 HP 在阈值内：提交处决伤害。
   - 否则：提交 `10d6 psychic` 伤害，附加 `frightened` 和 `stunned`。
7. 返回结果必须填充现有调用方依赖的 typed fields：`Applied`、`Damage`、`HpDamage`、`ShieldAbsorbed`、`DamageEvents`、`StatusEffectIds`、`SaveResults`、`SkillId`。

实现约束：

- 不直接修改 `target.current_hp`。
- 不在 `BattleGroundEffectService` 之外清理死亡单位。
- 不把免疫目标计入失败或大失败。
- 处决可参考现有 `BuildFatalExecuteDamageInput(...)` / `ApplyDamageToTargetResult(...)` 路径，但 death context 应标记为 phantasmal kill / psychic execution，而不是伪装成 Power Word Kill。
- 非处决的 `6d6` / `10d6` 必须走现有伤害结算路径，保留精神抗性、免疫、护盾和伤害事件。

### 3. 地面效果服务

需要改 effect 分类，优先不改击杀提交：

- `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- `scripts/systems/battle/rules/BattleSkillResolutionRules.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- `scripts/systems/battle/runtime/BattleGroundEffectService.cs`

`BattleTypedNames.IsUnitPayloadEffect(GradedSaveExecute)` 必须返回 true。`BattleSkillExecutionOrchestrator._is_unit_effect()` 若仍保留本地 switch，也必须同步。否则 ground preview 和 `_apply_ground_unit_effects` 会收集不到该 effect。

只要 `BattleDamageResolver.ResolveEffects(...)` 返回的结果字段与现有 damage/status/execute effect 一致，`BattleGroundEffectService` 会继续负责日志、死亡提交、掉落和评分。只有在日志需要显示“处决分级”时，才扩展 report/event 展示；这不是首批阻塞项。

### 4. AI 和预览

修改：

- `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- `scripts/systems/battle/ai/BattleAiActionIntent.cs`
- `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
- `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
- `scripts/systems/battle/ai/BattleAiScoreService.cs`
- `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`
- `scripts/systems/battle/ai/BattleAiScoreInput.cs`（仅当现有字段不足时）
- `scripts/enemies/EnemyAiAction.cs`
- `scripts/enemies/actions/UseGroundSkillAction.cs`
- `scripts/systems/battle/core/BattlePreview.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`

要求：

- `graded_save_execute` 被视为进攻 / 伤害 / 处决 / hostile threat。
- AI 评分使用 `BattleGradedSaveExecutionRules.EstimateGradeDistribution(...)`、平均伤害和处决阈值。
- 低 HP 敌人处于处决阈值内时，评分要显著高于普通 6d6/10d6 伤害。
- 免疫目标估算为 no-op，不应吸引 AI。
- 友方目标走软性配置：对非免疫友方目标写入 `estimated_friendly_fire_target_count`；对处决阈值内或预计致死友方写入 `estimated_friendly_lethal_target_count`；不要因为普通友方受影响或友方处决风险直接写 `friendly_fire_reject_reason`，避免绕过 AI action 的友伤配置。
- `UseGroundSkillAction.PassesFriendlyFireLimits(...)` 已依赖这些字段；默认 `maximum_friendly_fire_target_count = 0` 和 `allow_friendly_lethal = false` 下，任意可受影响友方或可致死友方仍会被拒绝；显式配置可以放宽普通友伤或友方致死风险。`friendly_fire_reject_reason` 只保留给非配置化硬阻断。
- 玩家预览是首批阻塞项：`target_team_filter = any` 且 7x7 处决/控制技能，不能只显示单位数。必须在 `BattlePreview.save_branch_preview` 或新的 typed preview facts 中暴露友方受影响数、友方处决风险数、免疫/no-op 数、保存分级风险摘要，并由 `BattleHudAdapter` tooltip / warning 文案展示。

### 5. 敌人模板投影

必做修改：

- `scripts/enemies/EnemyTemplateDef.cs`
- `scripts/systems/world/EncounterRosterBuilder.cs`
- 按需补 `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`
- 按需补 `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`

要求：

- `EnemyTemplateDef` 增加导出字段 `save_advantage_tags: Array[StringName]`。
- `EnemyTemplateDef.ValidateSchemaTyped(...)` 校验非空项，并校验基础 tag 是 `BattleSaveContentRules` 支持的 save tag；允许后缀 `_advantage`、`_disadvantage`、`_immunity`。
- `EncounterRosterBuilder` 构建 `BattleUnitState` 时复制模板的 `save_advantage_tags`。
- 内容侧使用 `illusion_immunity` 表达不受 `怪影杀戮` 影响的无心智 / 幻术免疫敌人。

## 预计修改文件清单

核心实现：

- `data/configs/skills/mage_phantasmal_kill.tres`
- `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- `scripts/player/progression/SkillContentRegistry.cs`
- `scripts/systems/battle/rules/BattleGradedSaveExecutionRules.cs`
- `scripts/systems/battle/rules/BattleSkillResolutionRules.cs`
- `scripts/systems/battle/rules/BattleDamageResolver.cs`
- `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- `scripts/systems/battle/core/BattlePreview.cs`
- `scripts/systems/battle/core/BattleState.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- `scripts/systems/battle/ai/BattleAiActionIntent.cs`
- `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
- `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
- `scripts/systems/battle/ai/BattleAiScoreService.cs`
- `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`
- `scripts/enemies/EnemyAiAction.cs`
- `scripts/enemies/actions/UseGroundSkillAction.cs`
- `scripts/enemies/EnemyTemplateDef.cs`
- `scripts/systems/world/EncounterRosterBuilder.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`

测试：

- `tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs`
- `tests/battle_runtime/skills/run_phantasmal_kill_regression.cs`
- `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`
- `tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs`
- `tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs`
- `tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs`
- `tests/progression/schema/run_phantasmal_kill_schema_regression.cs`
- `tests/runtime/validation/run_resource_validation_regression.cs`（官方资源校验覆盖新增技能）
- `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`
- `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`

文档：

- 本文档已校正为当前 C# 主线。
- 代码落地后必须检查 `docs/design/project_context_units.md`，重点是 CU-13 / CU-14 / CU-15 / CU-16 / CU-18 / CU-19 / CU-20。只有实际新增 owner、runtime chain 或推荐 read set 改变时才更新。

## 回归测试

新增 `tests/battle_runtime/skills/run_phantasmal_kill_regression.cs` 覆盖：

1. `CriticalSuccess`：无伤害、无状态。
2. 免疫目标：`Immune = true` 且 natural roll 为 0 时仍然 no-op，不能进入大失败。
3. 普通成功：只获得 `aftershock`，不能反击，也不能守护。
4. 普通失败且低 HP：触发处决，击杀提交链保留掉落/评分/战斗状态。
5. 普通失败且高 HP：造成 `6d6 psychic`，附加 `frightened` 2 轮和 `reaction_lock` 1 轮。
6. 大失败且低 HP：35% 阈值处决。
7. 大失败且高 HP：造成 `10d6 psychic`，附加 `frightened` 3 轮和 `stunned` 1 轮。
8. `death_ward` / last stand：处决伤害不能绕过现有保命逻辑。
9. 精神伤害抗性/免疫：影响非处决伤害，不影响豁免分级本身。
10. 7x7 多目标：范围内敌我可受影响，范围外不受影响。
11. 状态附加走 `BattleStatusSemanticTable.MergeStatus(...)`：重复附加刷新时长且保留 typed fields。

扩展现有测试：

- 状态语义：`frightened` 进入强力攻击劣势；`aftershock` / `reaction_lock` 的 typed `lock_counterattack` 生效。
- 守护锁：带 `lock_guard` typed field 的状态会阻断守护类技能。
- 规则层：`EstimateGradeDistribution(...)` 在 normal / advantage / disadvantage / override rolls 下给出正确分级概率，且免疫返回 `immune = 10000`。
- AI：低 HP 敌人评分升高；任意可受影响友方写入 friendly-fire count 并由默认 soft limit 拒绝；友军处决风险写入 lethal count 并由 `allow_friendly_lethal` 控制；免疫目标不吸引 AI；advantage/disadvantage 影响评分方向。
- 预览：玩家选点时能看到友方受影响数、友方处决风险、免疫/no-op 目标统计。
- 敌人模板：`EnemyTemplateDef.save_advantage_tags = [&"illusion_immunity"]` 能投影到 `BattleUnitState.save_advantage_tags` 并让技能 no-op。
- 内容验证：`graded_save_execute` effect、`mage_phantasmal_kill.tres`、保存标签、白名单 params 和骰子参数通过 schema 校验；错拼 key 应被拒绝。
- 法师技能内容：新增技能满足 `max_level = 9`、9 项 ultimate `mastery_curve`、`growth_tier`、240 成长预算、0..9 级描述配置；当前没有集中覆盖 mage alignment 的 runner，若要集中覆盖，可新增 C# runner。

建议执行顺序：

```bash
godot --headless -s res://tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_phantasmal_kill_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs
godot --headless -s res://tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
python tests/run_regression_suite.py
dotnet build magic.csproj
```

不要把数值模拟 / 平衡模拟 runner 加入常规全量回归，除非用户明确要求。

## 实现顺序

1. 以已确认决策作为 baseline：9 级 ultimate 定位、规范状态 `frightened`、新增 typed `lock_guard`、敌人免疫复用 `save_advantage_tags`。
2. 增加 `BattleEffectKind.GradedSaveExecute`、`BattleTypedNames` 映射和分类。
3. 增加 `BattleGradedSaveExecutionRules` 与纯规则回归测试。
4. 扩展 `SkillContentRegistry` 的 `graded_save_execute` 白名单 params 校验。
5. 扩展 unit effect 分类，确保 ground preview / 执行能收集该 effect。
6. 扩展 `BattleDamageResolver`，让单目标分级效果跑通；状态附加走 `ApplyStatusEffect(...)`，处决走正式伤害提交，非处决走 damage outcome。
7. 增加状态语义和反应锁 / 守护锁接入。
8. 增加 `mage_phantasmal_kill.tres`，补齐 `max_level = 9`、9 项 ultimate mastery curve、240 成长预算、0..9 级描述配置。
9. 增加敌人模板 `save_advantage_tags` 投影和 schema 校验，补免疫目标测试。
10. 扩展 AI 识别、威胁判断、评分、friendly fire 软性配置字段写入和默认拒绝。
11. 扩展玩家预览 payload 与 HUD warning。
12. 跑窄回归，再跑常规全量回归。
13. 根据实际新增 runtime relationships、所有权边界和 read set 更新 `docs/design/project_context_units.md`。

## 验收标准

- 技能配置通过资源验证。
- 技能在 7x7 范围内对敌我单位按同一规则逐目标解析。
- 免疫目标不会因为 natural roll 0 被当成大失败。
- 所有死亡都通过现有击杀提交链结算。
- 反击锁、守护锁、恐惧劣势和本技能震慑的即时 AP / 移动点清空在运行时生效。
- AI 不会默认选择会影响或处决友军的落点；对应 score input 字段可被测试断言。
- 玩家预览能显示友方受影响、友方处决风险和免疫/no-op 目标统计。
- 新技能满足 9 级 ultimate 定位下的 `max_level` / mastery / growth / description 规则。
- 新增测试和常规回归通过。

## 对抗审查记录

首版执行方案已吸收原讨论稿中的主要风险：免疫 natural roll、空 `effect_defs`、直接写 HP、击杀链绕过、状态语义缺失、AI friendly fire、敌人模板免疫投影和兼容性边界。

本次仓库语境校正新增以下修正：

- 旧 `.gd` 实现路径全部改为当前 C# 文件。
- effect type 注册点从不存在的 `VALID_EFFECT_TYPES` 改为 `BattleEffectKind` / `BattleTypedNames`。
- 分级规则改为复用 `BattleSaveResult.Degree`，AI 估算另建分级分布。
- 敌人模板投影保持“当前缺失，需要新增”，但明确 runtime 已有 `BattleUnitState.save_advantage_tags` 和 `illusion_immunity` 后缀语义。
- 守护锁不再建议用 residual params `"lock_guard"`，改为用户已确认新增的 typed field。
- 测试清单从不存在的 `.gd` runner 改为当前 C# runner 风格，并修正 Godot headless 命令格式。
