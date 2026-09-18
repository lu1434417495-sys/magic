# 来源状态伤害提前结算

当前实现：`advance_status_ticks`。生产使用者为熔爆术；内容参数见[技能输入卡](../../content/skills/mage_molten_burst.md)。

## 内容与所有权

| 边界 | 当前 owner |
|---|---|
| 来源分组语义 | `CombatStatusSourceContentRules` 拥有 `BattleStatusStackingScope`；燃烧为 SourceDefinition，`BattleStatusSemanticTable` 消费同一规则 |
| 正式内容 | JSON `effect_type=advance_status_ticks`，闭合 payload `max_ticks/max_sources/required_source_tag`；`SkillFullCombatEffectClosedSpec`、严格 DTO/normalizer、canonical schema、projector 投影成 immutable `AdvanceStatusTicksEffectPayloadDefinition` |
| 诊断 Resource | `CombatEffectDef.status_tick_limit/status_source_limit/status_source_tag`；只作为 fixture 输入，不能放入 `params` |
| 计划与消费 | `BattleStatusTickAdvanceRules.BuildPlan/ConsumeTick`；不按技能 ID 分支 |
| 执行与伤害 | `BattleDamageResolver.StatusTickAdvance`、普通 `ResolveEffects`；每跳复用 `BuildTimelineDamageInput` 和正式 damage application |
| 付费前门禁 | `BattleSkillTargetValidationService` 的 state/read-view 入口调用同一计划；空计划拒绝命令 |
| 预览 | `BattleSkillPreviewService` → `IBattleSkillPreviewRuntimePort` → `BattleSkillPreviewBridgeService`；resolver 使用完整 detached working set |
| AI | affordance / offensive kind / hostile threat 分类；`BattleAiScoreService.Effects` 消费同一预览，来源 `NextTickAtTu` 已在目标缓存指纹中 |

内容 validator 要求 root active、single enemy unit、single area、即时 direct_effect、每等级恰好一个有效 effect；禁止混合效果、地面、变体、特殊 profile 和重复目标。payload 次数为 1..32、来源数为 1..8，状态必须具有来源贡献语义。效果不得另配基础伤害、伤害标签/比例、豁免、武器骰或新持续时间；目标状态门禁必须为本状态的一层，效果过滤为 enemy。能力边界由导入错误明确表达。

## 计划与状态提交

只选择存活目标的有效有限来源：`DurationTu > 0`、`TickIntervalTu > 0`，并至少还有一跳落在到期时刻以内。技能来源通过 immutable Definition 的 tag 判定；非技能来源通过贡献自身的 damage tag 判定。缺失来源定义不合格。故带 fire tag 的技能燃烧可用，而借用 burning 模拟流血的非火系技能不会成为火种。

先按剩余到期时间、稳定来源键选定至多 `max_sources` 个来源，再按下一跳时间、来源键排序，取**合计**至多 `max_ticks` 次。多个来源不分别领取完整次数上限。自然调度器包含恰好位于到期时刻的一跳；计划沿用该规则。下一跳尚未初始化或不在未来时，按当前 TU + interval 归一化。

每跳先将该来源 `NextTickAtTu` 前移一个 interval，再执行伤害，防止同步反应重入重放；不修改来源 power、层数和剩余时长。死亡后停止，未执行的跳数不消耗。重施状态沿用现有 merge：可以补层/延长持续时间，不能把已前移的 tick anchor 重置。原快照字段已包含 next tick，无新存档字段、版本或迁移。

## 伤害与预览

每跳保持原来源的 flat/dice 数值，分开处理防护取整、易伤、护盾、伤害解除状态、死亡与装备反应。不合并再减伤，不进行第二次豁免，不乘施法者法术/武器/暴击增幅。免疫也消耗已提前执行的跳数，但不产生有效伤害熟练度。

已有已知元素标签的跳数沿用自然 tagged timeline damage：抵抗减半取整、免疫归零、double 在既有豁免阶段之后一次翻倍，然后消耗护盾。没有标签或未知标签的来源沿用自然 untagged 结算；本功能不批量修复旧火系来源缺标签问题。来源资格和伤害标签是两个不同判断。

即时伤害/击杀归本次施法者，未来自然 tick 仍归原施加者。`DamageOriginKind=TimelineUpkeep`，不会变成新的直接攻击来领取武器附伤；普通编排器接收逐跳 `DamageEvents`、汇总 HP/护盾变化和移除状态，沿用既有结果提交与日志。

预览最小、平均、最大骰模式不调用正式 RNG；不同免死分支各自保有已消费的时序，生存分支继续结算剩余跳数，死亡分支停止。HUD 展示次数、来源数、预计 HP/护盾消耗及未来扣除提示。存在概率免死时，骰模式输出是该骰情形下的分支加权估计，不是所有随机分支的绝对伤害上下界。

AI 保留即时 HP、破盾和保守致死事实；只有最小骰预览的全部免死分支均死亡才记为确定致死。普通输出分减去这部分已经安排的 HP 伤害，避免将来源技能和熔爆重复估成两份新增持续输出；未新增固定控制分或技能专属权重。此估值侧重收割/破盾，未把未来驱散、治疗或抗性改变估成额外收益。

自动 unit cast 经同一目标校验和效果链，仍服从既有反应 root 及预存释放支付规则；不另收普通主动费用或发放主动熟练度。新增效果本身不为熔爆开放预存资格。读条/地面/特殊执行组合被内容契约拒绝。

## 验证入口

`tests/battle_runtime/skills/run_mage_molten_burst_regression.cs` 覆盖真实火种命令、无效来源付费门禁、来源/次数共享、时间轴扣减、重施、生命周期总量、防护逐跳、护盾与熟练度、骰预览无 RNG、AI、概率免死分支和自动执行。共享回归及当前 checkout 结果见[落地记录](../../reviews/2026-09-15-mage-molten-burst-landing.md)。
