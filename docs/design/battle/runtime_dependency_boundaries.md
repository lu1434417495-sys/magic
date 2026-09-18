# 战斗预览与运行时依赖边界

本文描述当前代码中的依赖方向，不包含未来拆分计划。

## 技能预览、校验与执行

`BattleSkillExecutionOrchestrator` 仍拥有技能执行期子服务及其 teardown。`BattleSkillPreviewService` 通过 `IBattleSkillPreviewRuntimePort` 借用运行时查询能力，通过 `BattleSkillTargetValidationService` 校验目标；它不再持有执行器。直线续击、穿行攻击、方向贯穿及地面屏障裁剪的预览实现位于 `BattleSkillPreviewService.Targeting.cs`。

目标校验拥有随机链候选池的构建，随机链执行只消费候选池并按原顺序抽样、结算。校验器仅注入一个当前命令技能等级查询委托，以保留装备授予技能等入口的 scoped level；不借用整个执行器或随机链执行服务。销毁时清除此委托与 runtime 引用。

共享规则的归属如下：

| 规则或契约 | 当前 owner | 消费者 |
| --- | --- | --- |
| 效果目标筛选、稳定目标顺序、目标计划 | `BattleSkillTargetPlanRules` | 目标校验、执行、预览、地面效果 |
| 连锁伤害准备计划 | `BattleChainDamagePreparationRules` | 连锁执行、预览 bridge |
| 屏障裁剪结果合并 | `BattleGroundEffectClipRules` | 地面执行、地面预览 |
| 连击段效果及整数衰减 | `BattleRepeatAttackEffectRules` | 连击执行、预览、AI |
| 方向贯穿伤害倍率 | `BattleDirectionalPiercingRules` | 贯穿执行、预览、AI |
| 武器训练技能识别 | `BattleWeaponTrainingRules` | 熟练度结算、反击契约校验 |
| 伤害范围 | `core/BattleDamagePreviewContracts.cs` | preview、规则、HUD、AI、投影 codec |
| 地面屏障裁剪事实 | `core/BattleGroundEffectBarrierClipContext.cs` | 执行、预览、裁剪规则 |
| 连锁准备结果 | `core/BattlePreparedChainDamage.cs` | 执行、预览 |

预览端口的 `GetStateForReadOnlyRules()` 仍借出当前 `BattleState`，供尚未采用 `BattleStateReadView` 的现有规则读取。该能力不得用于提交状态；需要模拟变化的预览继续使用 detached working set。预览不消费正式 RNG、资源、状态次数或装备 usage。同步伤害与装备反应的嵌套调用顺序没有改为异步事件。

执行期伤害日志直接调用 `BattleReportFormatter`，不通过预览服务转发。技能、目标、状态的投影字段及伤害范围文案保持现有合同。

## 模块绑定与查询

`BattleRuntimeModule.BindRuntimeSidecars()` 集中绑定模块级服务，在构造完成、`FinishSetup` 和显式更换 damage resolver 时调用。构造后的模块即拥有可用服务图；内容索引由同一模块持有，内容同步更新索引并重建 AI 决策借用。

普通查询与命令入口调用 `AssertRuntimeAvailable()`，只检查模块生命周期和是否完成组合，不重新调用整组 `Setup`。`RuntimeSidecarBindingGeneration` 用于观察显式重新组合；反复移动查询不会增加该值。

战斗状态切换仍由 `BindRuntimeBattleState` 关闭上一战斗的反应边界、清理 AI 决策上下文并开始新的 cache epoch；状态绑定入口明确连接战斗 sidecar。`Dispose` 仍按 borrower 顺序解除引用，查询不得使已销毁的服务恢复可用。

## 编译期约束

`tools/architecture/layer_rules.json` 在原有约束上增加：

- `battle_runtime_isolated` 不得引用 `battle_skill_execution`。技能执行器及两个连锁执行服务归入后者。
- `PartyManagementWindow` 的两个 partial 文件归入 `presentation_isolated`，不得引用 `composition`，包括 `CharacterManagementModule`、`GameSession` 和 runtime facade。
- 新层继承原 application / presentation 的既有禁用方向；没有新增 baseline 豁免。

主项目构建执行这些语义检查。行为验证仍由正式 runner 负责，依赖方向通过不能替代运行时回归。
