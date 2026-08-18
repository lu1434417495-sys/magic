# 战斗技能系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-13`

## 定位

本文记录技能从 `.tres` 内容到战斗可用性、preview、execution、状态语义和 AI 消费的当前主链。未来规则扩展与旧阶段计划位于 [`../../proposals/battle/skill_runtime_expansion.md`](../../proposals/battle/skill_runtime_expansion.md)，不能作为当前合同。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `SkillDef`、`CombatSkillDef`、`CombatEffectDef`、`CombatWeightedStatusOutcomeDef`、`CombatCastVariantDef`、`CombatWindupDef`、`CombatSpellReactionDef`、`CombatRangedWeaponReactionDef`、`CombatDirectionalPiercingDef`、`CombatLineThroughAttackDef`、`data/configs/skills/*.tres` | 声明技能、目标、目标生物类型排除、范围、地面布置合法性、基础/逐目标槽位消耗、单位目标解析模式、攻击所对抗的 AC 模式、typed effects、豁免失败加权状态池、投射物种类、可选蓄力曲线、主动施法反应、远程武器受击反应、方向贯穿、穿身攻击与地形接触参数 |
| 校验与投影 | `SkillContentRegistry`（加载/索引/编排 + 技能级校验）、`SkillCombatProfileValidator` / `SkillDamageEffectValidator` / `SkillExecuteEffectValidator`（加载期分区校验器）、`CombatSkillContentRules`、`CombatUnitTargetResolutionContentRules`、`CombatLineThroughAttackContentRules`、`CombatTerrainContactModeRules`、`CombatProjectileContentRules`、`SkillDefinition`、`CombatEffectDefinition`、`CombatWeightedStatusOutcomeDefinition`、`CombatSpellReactionDefinition`、`CombatRangedWeaponReactionDefinition`、`CombatDirectionalPiercingDefinition`、`CombatLineThroughAttackDefinition`、`BattleAttackRollModifierSpec` | 加载期校验并发布 immutable definition graph；单位目标解析模式与逐槽位费用、地面布置与移动接触、攻击防御模式、投射物种类、攻击检定修正、豁免失败加权状态池、主动施法反应、远程武器受击反应、方向贯穿、穿身攻击和效果目标最低认知均以 typed content-definition 契约随投影使用 |
| 战斗可用性 | `BattleSkillAvailabilityService`、`BattleSkillEntryRef`、`BattleSkillEntryIds` | 合并已学技能、装备授予技能和 scoped auto-cast 入口 |
| 成长与建卡资源支持 | `ProgressionService`、`RandomStartingSkillResourceSupportService`、`EncounterRosterBuilder`、`GameSession.CharacterCreation` | 以 `BattleTargetSlotCostRules` 的最低一槽费用识别逐槽位耗蓝/耗体力技能，负责资源解锁、随机起始法力配套、遭遇单位资源投影与起始技能强度分层；随机书技能候选排除起始等级实际 MP 消耗超过 40 的技能，成功选中后法力池不得低于该消耗 |
| 命令与预览 | `BattleCommand`、`BattlePreview` / `BattleForcedMovePreviewData` / `BattleStatusContributionPreviewData` / `BattleRangedWeaponReactionPreviewData`、`BattleRuntimeModule.PreviewCommand(...)`、`BattleGroundSkillValidationService`、`BattleSkillCreatureTypeTargetRules`、`BattleWindPushRules`、`BattleWindupRules`、`BattleSourceRetreatRules`、`BattleApproachAttackRules`、`BattleDirectionalPiercingRules`、`BattleLineThroughAttackRules` | 校验 entry、资源、目标阵营与目标生物类型、完整空地/通行/视线布置、蓄力挡位、强风逐目标失败分支、主动后撤、踏步攻击路径、贯穿方向、穿身路径和当前 battle state，并返回只读 preview |
| 执行编排 | `BattleSkillExecutionOrchestrator`、`BattleCastingTimeService`、`BattleSkillTargetValidationService`、`BattleGroundRelocationService`、`BattleVaultBehindTargetRules`、`BattleDirectionalPiercingRules`、`BattleLineThroughAttackRules`、`BattleMovementService`、`BattleSpecialSkillResolver`、`BattleTerrainEffectSystem`、`IBattleRangedWeaponAttackReactionSink` | 解析目标、读条/蓄力、范围、屏障裁剪、效果和特殊入口；地面强风按风向由远到近把范围内目标交给通用强制位移链；普通/技能/冲锋/强制位移通过正式移动入口提交地形接触 |
| 时序与状态计时 | `BattleTimelineDriver`、`BattleStaminaRecoveryRules`、`BattleUnitActionClockState`、`BattleTemporalStatusService`、`BattleUnitTurnState`、`BattleUnitRestState`、`BattleUnitCooldownState`、`BattleStatusEffectState`、`BattleRuntimeSkillTurnResolver` | timeline driver 拥有 timeline/TU/ready、action-threshold 校验/日志、stamina 与冻结/读条跳过编排；共享 stamina rules 唯一提供 tick 数、属性/百分比与休息倍率换算，供 timeline 和只读 AI 投影共同消费；action-clock owner 唯一持有 action progress/threshold/rate remainder，并原子累计余数与扣除全部 crossing；temporal service 决定倍率；turn owner 唯一持有行动、移动、锁定移动点授权与施法耗尽事实；rest owner 持有跨 activation 的 resting fact；cooldown owner 唯一持有 map 与 last-turn anchor 并原子判定/推进；source-scoped 状态在聚合状态内保存独立来源贡献；turn resolver 负责 current-TU、5 TU 粒度日志、静滞/turn-start 编排、turn timer、逐来源状态 tick/duration、护盾 duration 与 tick anchor 初始化 |
| Contingency auto-cast | `BattleContingencySystem`、`IBattleContingencyRuntimePort`、`BattleContingencyBridgeService` | 构造 reaction 请求，并在同一调用栈、同一 batch 与 auto-cast origin scope 内进入 orchestrator |
| 通用规则 | `BattleRangeService`、`BattleHitResolver`、`BattleSaveResolver`、`BattleDamageResolver`、`BattleWeightedStatusOutcomeRules`、`BattleDamageBonusConditionRules`、`BattleStatusSemanticTable`、`BattleCognitionRules`、`BattleEffectTargetRequirementRules` | 拥有射程、命中、豁免、豁免失败加权状态选择/预览、伤害条件、状态语义、有效认知与效果目标门禁 |
| AI | `BattleAiSkillAffordanceClassifier`、`BattleAi*ActionEvaluator`、`BattleAiScoreService`、`BattleAiScoreService.HealingSuppression`、`BattleAiScoreService.ForcedMove`、`BattleAiScoreService.Taunt`、`BattleAiScoreService.RangedWeaponReaction` | 按每个效果的正式目标过滤聚合 hostile/support/mixed affordance，使用同一可用技能入口、canonical preview 和 typed score input；治疗抑制只按目标阵营治疗能力、可恢复生命、命中率和独立状态时长的真实边际拒疗估值，不领取普通控制分；强风按逐目标豁免失败概率、实际可达位移、接战/地形/高度变化估值，空放或零位移目标不进入有效目标集合 |

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
- 地面机关的合法布置由 `CombatSkillDef.ground_effect_require_full_area/empty/traversable` 与 `requires_los` 显式声明；`BattleGroundSkillValidationService` 对 preview 和 commit 共用同一面积、占用、footprint、layered barrier 与阻挡 LOS 边校验。`area_direction_mode = target_vector_perpendicular` 只旋转通用 line area 的方向，不按技能 id 推断。
- `terrain_contact_mode = interrupt_movement_on_failed_save` 是通用 typed 地形接触合同。`BattleTerrainEffectState` 保存来源、目标过滤、豁免、剩余有效触发数、接地/格内重判和同源实例策略；同一 field 在一次移动命令内最多判定一次，豁免成功不消耗次数，失败才原子递减整组 field 并拦停。普通移动在失败时支付命令最初选定路径的全部成本并锁定；技能步进和冲锋停止剩余路径，强制位移停止剩余位移；飞行只在效果显式要求接地时忽略，blink/jump/传送/交换/生成/复活不进入该接触入口。新移动命令从 field 内起步是否重判由 typed 字段决定，同一目标可在后续命令再次消耗同一 field；来源倒下不清除 timed field，同源替换在新 field 写入前按来源和 effect id 原子移除旧实例。熟练度只在失败实际拦停后提交，数量继续服从技能的 typed mastery amount mode（如 `per_target_rank`），成功豁免不入账。
- `position_swap` 是通用 unit payload effect。`BattlePositionSwapRules` 在付费前同时验证非自身存活目标、双方完整 footprint、第三方占位、静滞、敌方强制位移免疫、阻挡 LOS 边与 layered-barrier boundary，并为 preview/execution 返回同一成对落点；正式提交不会经过中间格或地形接触。友军换位视为自愿且不豁免，敌方使用 effect 的正式 `BattleSaveResolver` 意志豁免，成功时已提交消耗与冷却但不移动、不结算 `effect_applied` 熟练度。`CombatSkillDef.mastery_base_amount` 经 immutable definition 投影后只缩放现有 mastery amount mode 的成功结算，不能绕过 trigger gate；canonical preview、HUD 与 AI 共同消费 `BattlePositionSwapPreviewData`，AI 以目标换位前后的威胁差、施法者换位后位置风险及敌方豁免失败概率评分，不按技能 id 分支。
- `forced_move_mode = wind_push` 要求正数距离、1—4 的显式目标体型上限和正式豁免。地面强风只收集效果范围内目标，按风向由远到近逐个进入 `BattleSpecialSkillResolver.ApplyForcedMoveEffect`；范围外单位只阻挡而不会被递归推动。每个目标只进行一次豁免，失败才尝试完整距离；每步都重验通行、占位与 layered barrier，提交后进入统一 terrain-contact 去重链。`BattleWindPushRules` 在 detached state 上以相同顺序投影失败分支落点、体型门禁和豁免概率；纯强风技能只把实际存在失败移动分支的目标暴露给 HUD/AI。`effect_applied + per_target_rank` 熟练度只记录每次施放实际移动的 distinct target；仅屏障裁剪生效且没有单位/地形效果时记录一次 `mastery_base_amount`，空放、成功豁免、体型/免疫拦截与零位移不记录。
- `skill_entry_id` 标识本次技能来源，`skill_id` 标识技能定义。旧 entry 失效时必须拒绝或清空，不能按同名 `skill_id` 静默切换到另一来源。
- HUD、手动选择、文本命令、preview、execution 和 AI 必须通过 `BattleSkillAvailabilityService` 看到同一组技能入口。
- `CombatSkillDef.excluded_target_creature_type_tags` 经校验后投影为 immutable `CombatSkillDefinition.ExcludedTargetCreatureTypeTags`；只要目标拥有任一排除 tag，mutable execution validation 与 read-only preview validation 都在资源支付前拒绝。AI 对含该约束的 unit skill 强制消费 canonical preview，不按技能 id、显示名或其他正向 creature tag 覆盖排除结果。
- status/apply-status effect 的 `heal_multiplier_percent < 100` 继续由 `BattleStatusModifierRules` 按各独立状态的最小倍率结算；aggregate refresh 只合并同一 `status_id`，不会延长其他减疗。canonical `BattleStatusContributionPreviewData` 只公开本次将施加的状态、倍率和合并后时长，HUD 不枚举目标已有的其他减疗。AI 将该效果投影为 `estimated_enemy_healing_denied` / `estimated_ally_healing_denied`，按各状态剩余时长分段计算边际收益，且不计入 `estimated_status_count` / `estimated_control_count`。
- `BattleSkillAvailabilityService` 是 caller-scoped 的无状态规则类型，不缓存进 `BattleRuntimeModule`。command preview 在当前时点校验 entry，execution 独立重新校验并解析 entry level；availability result 不跨 preview/commit 缓存。
- Preview 不消费正式 RNG、状态 charge、装备 usage 或资源；commit 必须重新校验当前状态，不能信任旧 preview。伤害预览对 saving throw 的成功/失败分支分别执行 detached 致死推演，再合并 `lethal_probability_basis_points`、fatal-intercept 候选/动作与期望存活生命；分支专属动作只能标记 conditional，不能写入确定的 `target_preview_after`。装备授予技能还会在基础 command preview 允许后，于当前规则所需的 detached battle 子集上执行同一 post-use reaction core，并以 `source_preview_after` 和 action previews 展示装备 reaction 而不提交真实 usage；该来源快照不合并技能自身的 HP/status 变化，基础技能效果仍由原 skill preview 单独表达。
- `CombatEffectDef.save_failure_status_outcomes` 只用于带正式豁免的 damage effect，并与单一 `save_failure_status_id` 互斥；每个 `CombatWeightedStatusOutcomeDef` 只承载唯一 id、正权重和一个 status/apply_status 子效果。`BattleDamageResolver` 只在真实豁免失败后调用一次 weighted roll，成功豁免不抽取；`BattleWeightedStatusOutcomeRules` 将各分支权重投影为条件概率，并乘以正式豁免失败率生成实际施加概率，canonical damage/skill preview、HUD 与 AI 共享这些事实且不消费 RNG。AI 把随机池计为期望控制概率而非必然控制次数，不按技能 id 建分支。
- `CombatEffectDef.save_dc_bonus` 是 `caster_spell` 动态豁免公式的非负 authored 加值，经 immutable `CombatEffectDefinition` 投影后由 `BattleSaveResolver` 统一叠加到 `8 + 施法属性调整值 + 法术熟练加值`；静态 DC 效果不得配置该字段。效果等级窗口负责表达技能等级成长，因此 execution、canonical preview、HUD 与 AI 概率估算自动消费同一个最终 DC，不得在具体技能 id 或评分代码中重复加值。
- status/apply-status effect 可用 typed `skip_turn`、`break_on_positive_damage` 与 `on_removed_status_*` 声明状态生命周期；这些字段经 immutable `CombatEffectDefinition` 投影进 `BattleStatusEffectState`，非状态效果、缺少 successor id、递归 successor 与非法豁免 tag 在加载期拒绝。`BattleUnitState.EraseStatusEffect` 是解除与 successor 转换的唯一 owner，因此到期、净化和正伤害解除共享规则；`BattleDamageResolver` 只在实际 HP 伤害或正护盾吸收后解除，0 伤害不触发，并把解除 id 同时写入 execution result 与 canonical detached preview。`BattleRuntimeSkillTurnResolver` 按状态字段跳过行动，`BattleTimelineDriver` 只在正常 activation 结束后消费 `consume_after_normal_turn`，被跳过的行动不消费。AI 的普通豁免控制收益必须乘正式豁免失败概率，免疫目标贡献 0，不按技能或状态 id 建评分特例。
- Repeat attack 的后续段伤害倍率与命中成长分别由 `CombatEffectDef.follow_up_damage_multiplier_percent`、`follow_up_attack_roll_bonus_curve` typed 字段声明并投影到 immutable `CombatEffectDefinition`；旧 `params.follow_up_damage_multiplier_percent` 不再接受。`fixed_repeat_attack` 与 `repeat_attack_until_fail` 都遵守 `stop_on_miss`，需要落空后继续的固定段技能必须显式写 `false`。canonical hit preview 逐段投影到达概率和伤害倍率，并给出相对单段攻击的条件伤害期望/总潜力；伤害范围预览展开全部可达段，AI 使用同一段数、倍率和条件概率评分，不按具体技能 id 分支。
- `CombatSkillDef.unit_target_resolution_mode = ordered_slots` 是通用单位多选逐槽结算合同，只能与 `target_mode=unit`、`target_selection_mode=multi_unit`、`allow_repeat_target=true`、`selection_order_mode=manual` 和即时施放组合。`mp_cost_per_target_slot` / `stamina_cost_per_target_slot` 可按等级覆盖，实际总费用严格为基础费用加“实际选择槽位数 × 单槽费用”；初始可用性、成长资源解锁、随机起始法力配套、遭遇单位资源投影与建卡强度分层按一槽检查，canonical preview、commit、HUD 和 AI 候选评分按命令中的实际槽位数检查与扣除。preview 与 execution 都保留重复目标和手动顺序，每槽独立进入屏障及正式效果链；既定目标提前失效时后续槽位落空且不重选。AI 同时枚举重复集火和轮转分散候选，效果收益也按有序槽位逐次累计，不能按单位 id 去重。该模式的 active-skill 目标熟练度事件在整次施放结束后折叠为一次最高合法奖励；auto-cast 支持同一有序槽位解析，pending cast 由 schema 拒绝。
- 多段伤害预览使用 continuation frontier 延续 branch-local HP/status/fatal usage；dead branch 若仍有未消耗致死候选也会进入后续段。等价分支以完整 unit detached snapshot、per-battle charge 与 world step 签名合并。当前没有 frontier 硬上限，本地概率型 finalized 状态动作只公开条件摘要、不把其状态继续分裂；正式内容新增这类规则前必须扩展 typed truncated/fail-closed 合同。
- `CombatSkillDef.attack_defense_mode` 是每次技能攻击检定所对抗 AC 的唯一语义声明，只接受 `normal`、`touch`、`flat_footed`。`touch` 忽略护甲、盾牌与天生护甲分量，保留敏捷、闪避和偏斜；`flat_footed` 移除受护甲敏捷上限约束后的正敏捷加值，并移除静态与状态闪避加值，但保留负敏捷调整。`BattleHitResolver` 在同一次 canonical check 中把该模式与装备能力的分量忽略/倍率规则合并，因此 execution、HUD preview、重复攻击和 AI preview 不得另建分支。
- `CombatSkillDef.spell_reaction_profile` 只声明由技能状态提供的主动施法反应：精确待机状态、触发投送类别、反应攻击技能、武器族门槛、维持豁免公式和逐级命中/DC 加值都经 `CombatSpellReactionDefinition` 投影；引用的反应技能必须存在，且是包含武器骰伤害的主动 unit skill。待机状态在使用者下一行动开始时到期；敌方手动或 AI 主动法术在消耗与冷却提交后、效果应用前触发，按 timeline 顺序让合法反应者逐一执行同一标准武器攻击链。未命中或没有造成 HP 伤害时法术继续；造成 HP 伤害后用正式 `BattleSaveResolver` 进行体质维持检定，失败或施法者被击倒时中断法术且不返还已提交消耗。自动施法、Contingency、装备反应和 direct auto-effect 不进入该触发点；AI 只在当前武器范围内存在已知主动法术威胁时为待机状态计入收益。
- `CombatSkillDef.ranged_weapon_reaction_profile` 声明由技能状态提供的远程武器受击反应：精确待机状态、允许武器族、伤害标签、独立攻击所对抗的 AC、逐级命中加值、命中/未命中触发开关、每次消耗层数与是否可暴击都经 `CombatRangedWeaponReactionDefinition` 投影。`BattleDamageResolver` 在原攻击检定及其全部伤害、装备反应和耐久刷新完成后，才把攻击前冻结的主武器基础骰与本次主 W 倍率交给同步 reaction sink；反应不会复制属性/固定加值、暴击额外骰、bonus W、装备附伤或其他伤害段，也不重新检查已成立攻击的距离/视线。每个独立攻击检定至多消费一次状态层数，反应自身继续走正式命中、伤害、护盾、减伤和死亡链且禁止递归触发。canonical preview/HUD 显示动态公式；AI 只估算持续窗口内可用敌对武器攻击，按法珠上限和现存状态计算边际期望伤害，不把待机状态计为普通控制或普通状态收益。
- Pending cast 是 battle-only runtime state。手动施法、auto-cast 和 pending cast 最终进入同一 orchestrator 规则链。
- 可选蓄力通过 `CombatWindupDef` → `CombatWindupDefinition` 声明曲线，由命令的 `windup_tier` 表达本次选择；`requires_heavy_weapon` 是与蓄力正交的技能资源配置，只有显式设为 `true` 才要求 heavy 近战武器，不能根据 `windup_profile` 推导。确认时把挡位、力量/体质调整值、总 TU、额外体力、武器骰倍率与完整武器签名冻结进 `BattleWindupSnapshot`。结算只消费该 snapshot，不因蓄力期间属性变化重新定价。玩家在确认前可以切换挡位或退出选择，pending 建立后不允许主动取消。
- 蓄力 pending 冻结单位 action progress，但不冻结非休息体力恢复；`time_slow` 继续缩放蓄力进度，`time_stasis` 同时冻结蓄力进度和该单位的常规体力恢复。单纯 HP 损失不触发蓄力维持检定；蓄力自己的硬控集合、施术者坐标变化、武器签名变化和 hard-anchor 目标失效会打断，已付资源不退并启动完整冷却。目标移动本身不打断，完成时由 canonical target validation 重验目标、距离、视线与屏障；失败按落空结算并启动完整冷却。
- 带蓄力配置的技能只允许显式 manual/AI unit-skill command 使用。AI 把每个合法挡位作为独立候选，先通过 canonical preview 校验挡位与当前资源，再由 `BattleWindupRules` 生成 `FinalStaminaCost`、`DelayedResolutionTu` 两项候选事实交给通用 scorer；最终体力进入既有资源权重与 reserve pressure，延迟按独立 profile 权重扣分，scorer 不重复计算蓄力规则。canonical preview 已允许但 quote 失败属于 invariant 破坏，候选以 `windup_quote_invariant_reject` fail closed。当前延迟只是确定性的行动机会成本，不模拟受击、逃离或打断概率；Contingency、装备即时攻击和 `trigger_skill` 自动路径在内容校验与运行时双重拒绝。
- `bonus_condition` 只在 authoring 边界保留 `StringName`，进入内容契约后必须解码为 `BattleDamageBonusConditionKind`，未知值由 schema validator 拒绝。正式伤害和 AI 估值统一委托 `BattleDamageBonusConditionRules`，不得各自维护条件分支；`damage_ratio_percent` 仅在条件成立时生效，其中 `target_has_shield` 按每个效果实际结算时的 `BattleUnitState.HasShield()` 判断。
- `CombatSkillDef.projectile_kind` 唯一声明技能投送是否为 `none`、`nonmagical`、`magical` 或 `current_weapon`；`CombatCastVariantDef.projectile_kind_override` 的空值表示继承，显式 `none` 可把投射技能的某个近战变体关闭。`BattleEffectCategoryResolver` 只从这两个 typed 字段派生 `projectile` + `magical_projectile` / `nonmagical_projectile`，不得从职业 tag、`magic`、射程、伤害、豁免或技能 id 推断。投射物种类也不得反向推断 `attack_defense_mode`：魔法投射可以对抗普通 AC 或接触 AC，必须由技能显式选择。派生类别不得写入 `delivery_categories` / `effect_categories`，旧 `magical_missile` / `nonmagical_missile` 名称不再接受。
- `current_weapon` 只在效果真实包含武器伤害、当前运行态与正式物品定义都确认主手为 ranged 时派生非魔法投射类别；基础攻击和弓手武器技能使用该模式。显式 `magical` 不会因为持有普通弓而再叠加 `nonmagical_projectile`。选中的 cast variant 必须沿 preview、commit、ground clip、repeat attack 和 chain damage 传到同一 resolver。
- 新状态的 timeline tick anchor 由 `BattleRuntimeSkillTurnResolver` 基于应用时的 current TU 初始化；`BattleRuntimeModule.MarkAppliedStatusesForTurnTiming(...)` 只保持“先初始化 anchor，再通知 Fate”的跨 owner 顺序，不保存状态计时规则。
- `BattleStatusSemanticTable` 可把状态声明为 source-definition scoped；来源身份由来源类型、来源单位与来源定义组成。同一来源按状态语义刷新/叠层，不同来源在同一聚合状态下独立保存层数、时长与 tick anchor，来源倒下不清除既有贡献；聚合状态只服务状态存在性、驱散、HUD 和通用查询，驱散该状态会移除全部来源。execution、canonical preview、HUD、AI 和 snapshot 必须消费同一来源贡献，不得按技能 id 另建燃烧特例。
- `BattleSkillMasteryService` 对 authored `skill_damage_dice_max` 触发器消费正式 `damage_dice_high_total_roll` 事实：常规伤害骰总和达到理论最大值的 4/5，或该伤害为暴击时满足骰面条件；目标仍必须实际受到伤害或由护盾吸收伤害。范围技能逐目标按等级累计，状态施加后的周期跳伤不回流为本次主动技能熟练度。typed result 与 Godot Dictionary snapshot 必须读取同一高伤骰字段。
- 单位基础认知是 `BattleCognitionKind` 的封闭序列 `mindless < instinctive < sapient`，由 `BattleUnitState` 持久拥有；有效认知由 `BattleCognitionRules` 取基础认知、状态语义认知上限与装备来源认知上限中的最低值。`mindless` 表示没有可理解语义和意图的自主心智，不是“智力属性较低”；技能不得从 INT 阈值、creature tag、敌人 id 或描述文本推断认知。
- `CombatEffectDef.required_target_min_cognition` 是 effect-local typed 目标门禁，加载期只接受上述封闭值；`BattleEffectTargetRequirementRules` 同时供 ground preview、正式执行和 AI 目标/收益计算消费。只覆盖不合格单位的地面技能在支付 AP、体力和冷却前拒绝；范围内混合目标则只对合格单位应用效果。
- `CombatEffectDef.heal_to_hp_percent_floor` 只用于 heal effect，治疗量为 `max(ceil(hp_max * percent / 100) - current_hp, 0)`；`heal_missing_hp_percent` 同样只用于 heal effect，治疗量为 `floor(max(hp_max - current_hp, 0) * percent / 100)`。两种百分比治疗彼此互斥，也都不得与 power、固定骰或属性缩放骰治疗混配；正式执行后仍统一应用 typed 治疗倍率。`max_affected_targets`、`exclude_source` 与 closed `target_order = lowest_hp_percent_then_unit_id` 在 team/status/range 过滤后冻结候选，按 `hp_percent_bp ASC, unit_id ordinal ASC` 排序再截断。普通 unit、ground、auto-cast、pending、preview 和 AI 必须消费同一 `BattleCombatEffectTargetRules`，前序效果导致的生命变化不能重选后序效果的冻结候选。
- Shield effect 的家族、属性调整值与投骰粒度分别由 `CombatEffectDef.shield_family`、`shield_attribute_modifier_id`、`shield_roll_per_target` typed 字段声明，并投影到 immutable `CombatEffectDefinition`；旧 `params.shield_family` 不再接受。属性字段只允许六项基础属性对应的 `*_modifier`，逐目标投骰必须同时存在骰池，非 shield effect 不得携带这些字段。`BattleShieldPreviewRules` 是 preview、HUD、AI 共用的护盾区间、期望值、属性调整值和现有护盾边际收益 owner；正式执行由 `BattleShieldService` 使用同一 typed 家族与属性规则，默认维持一次效果共享投骰，显式 `shield_roll_per_target = true` 时按目标单位各自投骰并在同一目标的重复结算内复用。护盾仅在实际建立、增强或延长时产生成功 effect application，因此 `effect_applied/per_target_rank` 熟练度按真实改善单位数累计，无收益目标不计数。
- `equipment_durability_damage` 的装备候选、稀有度豁免加值、预期耐久损失与摧毁概率统一由 `BattleEquipmentDurabilityResolver` 计算。canonical preview 与 HUD 使用只读 `BattleEquipmentDurabilityPreviewData`，不消费随机数或改写装备；目标没有匹配装备时技能仍可合法攻击，预览明确显示耐久收益为零。正式执行只在效果门禁满足后结算豁免，耐久归零立即清除装备入口并触发属性/装备能力投影刷新；AI 消费同一预览算法产生的 typed 期望损失和摧毁概率，因此以评分规避无甲低收益目标，不增加目标合法性特例，也不按技能 id 分支。
- Unit target validation 会在付费前应用 effect 级 `exclude_source`；若对自身所有可作用效果都排除 source，则该目标非法。死者默认不可选，只有显式 `heal_fatal` 效果才提供复活目标语义；普通 `heal` 不再被推断成可复活。
- `CombatDamageSegmentDef.double_dice_on_critical` 默认 false；只有显式开启且段本身具有合法骰池时，extra damage segment 才在 critical 时追加同组骰，flat bonus 不重复。该开关不让额外段继承武器骰、装备附伤或其他主段暴击规则。
- AI 技能分类逐个读取 effect 经 `BattleTargetTeamRules.ResolveEffectTargetFilter(...)` 解析后的正式目标阵营，再聚合 hostile/support affordance；同一技能同时含敌对伤害与友方治疗时保留 `mixed`，不得由第一个效果或 profile 级 `any` 提前决定整项技能意图。装备授予技能沿同一 action-plan、动态 `IsSelectable`、canonical preview 和 mutation guard 进入 AI，不建立具体装备或技能 id 分支。
- `madness` 的状态语义只把有效认知上限压到 `instinctive`，不会把单位变成 `mindless`，也不会由认知规则自行随机选目标或接管 AI。需要随机攻击、敌我混淆等行为时仍由疯狂机制自己的规则拥有。
- `taunted` 只在被影响单位的有效认知达到 `sapient` 时提供攻击劣势和 AI 强制目标。认知临时降低时，既有状态及剩余时长继续按 timeline 保存和递减但语义暂停；认知在到期前恢复后自动重新生效。挑衅 AI 不再领取通用状态/控制数量分，而按目标下一行动窗口内攻击其他友军时，命中概率从 `p` 降为 `p²` 所减少的预期伤害估值；候选攻击先投影届时冷却、AP、体力与状态，再经 `IBattleAiScoreContext` 携带的 canonical cast-block callback 检查。已处于攻击劣势、`direct_effect`、规则层 force-hit-no-crit、届时仍不可施放或没有其他友军可保护时不产生增量收益。
- 带 `upkeep_*` typed 字段的持续状态由 `BattleRuntimeSkillTurnResolver` 按 `next_tick_at_tu` 扣除战斗资源，并用 `upkeep_elapsed_tu` 保存已维持时长；费用升档按下一次结算时点计算。资源不足或 `break_on_hard_control` 命中硬控集合时，resolver 原子移除持续状态、施加 typed 攻击检定惩罚状态，并从终止时点写入技能冷却。状态快照必须保留维持配置与已维持时长。
- 状态提供的 `combo_attack_bonus_status_id` / `combo_attack_bonus_stack_divisor` 由 `BattleHitResolver` 只对真实包含武器伤害的近战武器攻击结算；`melee_combo_stack_gain_bonus` 由 `BattleDamageResolver` 在近战武器命中后叠加到通用的每击 1 层规则。两条规则都读取独立的 `melee_combo_stack`，不回退到旧 `combo_stack`。
- 护盾 duration 与普通状态 duration 共用非静滞单位的 timeline status phase；实际递减和到期六字段清理由 `BattleUnitShieldState` 原子执行。step 开始时已有 `time_stasis` 的单位在该 step 内冻结护盾 duration，静滞解除后的下一 step 才恢复。
- Contingency system 只弱借用 `IBattleContingencyRuntimePort`，不反向依赖 `BattleRuntimeModule`。bridge 实现该端口，并保留同步递归 reaction 顺序、调用方 `BattleEventBatch`、执行前玩家已学来源复核和覆盖嵌套反应的 effect-origin scope。
- 范围、命中、豁免、伤害、死亡、屏障和状态阻断属于通用 service/table；不得在具体技能 id、装备 id 或 UI 中复制规则。
- `vault_behind_target` 是 unit-skill typed effect：目标必须与使用者正交相邻，落点为目标沿攻击方向的下一格；落点占用、两段 edge 通行或 layered barrier 边界任一不合法时，canonical preview 与 execution 都拒绝。只有本次攻击检定命中后才移动使用者，目标被伤害击倒不取消已合法的落位。
- `source_retreat` 是必须放在基础 `effect_defs` 且只出现一次的 single-unit typed effect，距离只读 `CombatEffectDef.source_retreat_distance`。命令必须携带精确的单位正交方向，第一步必须增加使用者与本次攻击目标的曼哈顿距离；缺失、斜向、非单位向量、靠近目标或移动锁定状态都会在支付资源前拒绝整个技能。目标坐标在攻击结算前冻结，之后无论命中、未命中或目标被击倒都尝试后撤；第一步受阻时只完成攻击，第二步受阻时只移动一格。后撤不扣移动力、不记录第二次行动，实际经过的地格仍走正式 grid move、terrain contact、changed coord 和一次 position-changed 事件，layered barrier 边界视为路径阻挡。玩家输入必须先选目标再选方向；AI 把每个远离目标的正交方向作为独立候选并分别走 canonical preview，不增加专用评分权重。读条、蓄力、cast variant、special/random-chain 与 Contingency 等无人工选向的自动路径在内容和运行时 fail closed。
- `forced_move_mode = airborne_pull` 是敌方 single-unit 的两阶段纯控制效果。目标必须满足 authored 状态前置、体型上限且不具有时间静滞或强制位移免疫；命令先固定目标，再用 `forced_move_destination_coord` 只选择一次最终落点。`BattleAirbornePullRules` 对 mutable/read view 共用曼哈顿位移上限、完整 footprint、空落点、“比原位置更接近施法者”和 layered barrier 边界校验；高低地双向均可，不检查普通移动高差，也不逐格经过中间单位。正式执行以空中迁移一次提交最终坐标，中间格不触发接触，落地格只进入一次 terrain contact；`forced_move_applied` 后置效果只有实际移动成功才执行。canonical preview 通过 `BattleForcedMovePreviewData` 公开起点、终点、距离、体型上限和接触语义；玩家必须完成目标/落点两阶段，AI 枚举每个合法目标-落点组合并分别预览，不允许运行时自动挑落点或按技能 id 分支。
- `approach_attack_profile` 是先沿直线推进、再进行一次标准武器攻击的 single-unit typed profile。目标必须在当前武器射程外、与使用者 anchor 正交对齐且不超过“当前武器射程 + 等级化 `range_value`”；`BattleApproachAttackRules` 选择让目标首次进入武器射程的最短路径，不允许转弯或绕路。每一步先校验完整 footprint 的通行、占用与 layered barrier，再把所有占用格高度与起始 anchor 高度做绝对差比较；profile 为 0 时，任何中间格或最终格上/下坡以及多格 footprint 局部异高都在付费前拒绝。推进不读写普通移动点，但移动限制状态仍拒绝；正式提交继续逐步触发 terrain contact、changed coord 与 position-changed。付费后发生动态中断时保留 AP、体力和冷却并取消攻击；到位后重新校验目标存活、正交关系、武器类型和真实武器射程，再复用标准命中、暴击、护甲、护盾、抗性、减伤与装备能力链。canonical preview 通过 `source_advance_path`、`resolved_anchor_coord` 和 `move_cost = 0` 公开最终状态；AI unit-skill 快速路径遇到该 profile 必须委托正式 preview，以同一最终落点参与既有位置风险、伤害与资源评分，不增加技能 id 分支或专用权重。
- `line_through_attack_profile` 是“选择直线终点敌人、按顺序攻击途中敌人、集中攻击终点并在命中后落到敌后”的 single-unit typed profile。`range_value` 是终点目标的配置射程，不与武器射程相加；profile 另行限制允许的最大武器攻击距离。`BattleLineThroughAttackRules` 对完整 footprint 逐步检查地图、地形高度边、阻挡边与 layered barrier：友方单位阻断，途中敌人只记录一次，选定敌人之后的首个完整空落点才是 destination；路径、落点或移动限制在付费前不合法即拒绝。途中和终点都通过同一标准武器攻击链独立结算，未命中不中止；只有途中成功攻击检定按等级化 cap 增加终点武器骰组数与非负攻击检定加值，终点始终只生成一次伤害/减伤包。途中结果不记录该技能熟练度，终点结果正常记录。终点命中后由 `BattleMovementService` 重新验证冻结路径与落点，再作为穿越式位移直接提交 destination、movement trail、changed coord 与 position-changed；终点未命中或落点动态失效时留在原地。canonical preview 以有序 `target_unit_ids`、`source_advance_path`、`resolved_anchor_coord` 和 `AttackPreviewStage` 的途中命中率/终点 capped-success DP 状态公开同一计划；AI 必须使用该 preview 枚举终点，并按阶段命中概率估计途中收益和终点不同武器骰状态，不按技能 id 分支。
- `directional_piercing_profile` 是直线贯穿武器技的 typed 内容契约。命令只选择相邻单位正交方向，`BattleDirectionalPiercingRules` 以当前武器完整有效射程构造唯一有序路径；阻挡 LOS 的边先截断路径，单位不阻挡，所有阵营的存活单位都可受击，多格单位只按路径首次相交格处理一次。高度以施术者格为基准：同层不锁定，首个高度差为 `+1` 或 `-1` 的合法单位自动锁定该通道，之后跳过相反通道，绝对高度差超过 profile 上限也跳过；跳过不进行攻击检定且不推进衰减。每个纳入目标独立走标准武器攻击，只有成功攻击检定推进后续伤害衰减，倍率在减伤前应用。体力由完整有效射程与力量调整值按 profile 的平方参数动态计算，preview、HUD、执行与 AI 共用同一公式；没有合法路径目标时在支付 AP、体力和冷却前拒绝。AI 只枚举四个方向并消费 canonical plan，按 profile 自动扩展的 prior-hit 状态 DP 估计当前目标的期望衰减（当前 20%/40% 参数对应 `0/1/2/3+` 四状态），友军同样进入顺序与状态转移。
- `sequential_line_hit_profile` 是“选择正交射线首敌、命中才继续”的 typed 法术攻击契约。`range_value` 与 profile 的等级化最短距离共同限制首目标；`BattleSequentialLineHitRules` 在付费前验证所选单位确为该方向首个存活单位，并按每段等级化续行距离构造有序敌方目标，友方/非敌对单位、阻挡 LOS 的边、墙体、战场边界或 layered barrier 都会拒绝首段或截停后续。正式执行对每个已到达阶段独立复用标准法术攻击、接触 AC、暴击、护盾、抗性与减伤链，后续阶段按 profile 施加累计攻击检定减值，任一攻击检定落空即停止。canonical preview 通过有序 `target_unit_ids`、射线路径与 `AttackPreviewStage.ReachProbabilityBasisPoints` 公开逐段命中率和到达率；AI 强制使用该 preview，并用“到达概率 × 本段命中率”缩放每个目标的期望伤害，不按技能 id 分支。
- AI 快速评估可以使用专用 typed evaluator，但遇到会改变合法目标集合的 canonical 规则时必须委托正式 preview，而不是近似复制。

## 代表性回归

- `tests/runtime/validation/run_barrier_skill_content_validation_regression.cs`
- `tests/battle_runtime/rules/run_battle_range_service_contract_regression.cs`
- `tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs`
- `tests/battle_runtime/runtime/run_equipment_ability_preview_integrity_regression.cs`
- `tests/progression/schema/run_skill_attack_defense_mode_schema_regression.cs`
- `tests/battle_runtime/rules/run_skill_attack_defense_mode_regression.cs`
- `tests/progression/schema/run_combat_projectile_kind_schema_regression.cs`
- `tests/battle_runtime/rules/run_battle_effect_category_resolver_contract_regression.cs`
- `tests/battle_runtime/skills/run_archer_disrupting_arrow_regression.cs`
- `tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs`
- `tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`
- `tests/battle_runtime/runtime/run_combat_effect_heal_floor_target_limiter_regression.cs`
- `tests/battle_runtime/skills/run_mage_bone_chill_regression.cs`
- `tests/runtime/persistence/run_game_session_random_start_skill_regression.cs`
- `tests/battle_runtime/skills/run_archer_double_nock_regression.cs`
- `tests/battle_runtime/runtime/run_fixed_repeat_attack_execution_regression.cs`
- `tests/battle_runtime/runtime/run_repeat_attack_decay_multiplier_regression.cs`
- `tests/progression/schema/run_combat_effect_shield_schema_regression.cs`
- `tests/battle_runtime/runtime/run_battle_shield_service_typed_context_regression.cs`
- `tests/battle_runtime/skills/run_priest_aid_regression.cs`
- `tests/battle_runtime/skills/run_warrior_guard_break_regression.cs`
- `tests/battle_runtime/skills/run_spell_disjunction_equipment_durability_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs`
- `tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs`
- `tests/battle_runtime/skills/run_warrior_over_shoulder_regression.cs`
- `tests/battle_runtime/skills/run_warrior_perfect_rhythm_regression.cs`
- `tests/battle_runtime/skills/run_warrior_heavy_blow_windup_regression.cs`
- `tests/battle_runtime/skills/run_warrior_taunt_cognition_regression.cs`
- `tests/battle_runtime/skills/run_archer_backstep_shot_regression.cs`
- `tests/battle_runtime/skills/run_mage_voltage_hook_regression.cs`
- `tests/battle_runtime/skills/run_archer_breach_barrage_regression.cs`
- `tests/battle_runtime/skills/run_warrior_piercing_thrust_regression.cs`
- `tests/battle_runtime/ai/run_battle_ai_score_input_metrics_regression.cs`

模块生命周期见 [`runtime_module.md`](runtime_module.md)，AI 参数见 [`ai_score_parameters.md`](ai_score_parameters.md)，装备来源技能见 [`equipment_ability_runtime.md`](equipment_ability_runtime.md)。架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-13、CU-15、CU-16、CU-18、CU-20 和 CU-21。
