# 首次晋升闭环审计

日期：2026-09-13。状态：已完成代码审计及定向诊断，未修复生产实现。

审计对象是当前共享工作区，基线 HEAD 为 `6cafa279dcd62d9d9286c1bc53167e590a73caea`，包含其他任务的未提交修改。证据不等价于该提交的干净检出结果、全量回归或 CI。

## 结论与优先级

应先修通首次晋升，再测成长速度。当前至少有三处独立阻断：新角色没有正常的核心技能／升级触发选择入口；实际生成的晋升选项在提交时被拒绝；待确认晋升读档后不能恢复展示。另有领域候选到运行时提示的刷新缺口。

“成长是否太慢”也存在具体的规则原因：已用于晋升的技能会扩展上限，但后续职业条件仍要求它达到新的上限，导致旧资格失效、重复补练。此项属于需要明确取舍的成长设计，不应混同于上述功能缺陷。

| 编号 | 优先级 | 发现 | 证据性质 |
| --- | --- | --- | --- |
| F1 | P1 | 新角色无法通过正常入口完成核心化和选择升级触发技能 | 调用链审计＋新档诊断 |
| F2 | P1 | 已有领域候选不会自行转成晋升提示，需要额外奖励触发 | 定向诊断复现 |
| F3 | P1 | 实际提示复制丢失“字段未指定”语义，合法职业无法提交 | 实际生成提示＋正式文本命令复现 |
| F4 | P1 | 待确认晋升读档后领域候选仍在，提示和确认入口消失 | 磁盘保存／加载诊断复现 |
| F5 | P2 | 晋升后扩展技能上限会撤销其后续晋升资格 | 当前规则＋下一阶候选诊断 |

这里的 P1 指阻断核心成长路径、应优先修复；F5 是设计问题，当前代码按既有判定执行。

## F1：正常玩家路径无法满足晋升的前置状态

新角色等级为 0，初始技能为已学、非核心，也没有激活的升级触发技能。熟练度入账能够升级技能，但不会自动核心化。

当前晋升链要求先具备一个已激活、未锁定、达到上限的核心技能。`SetActiveLevelTriggerCoreSkillTyped` 会拒绝非核心技能。`SetSkillCore` 虽可直接修改领域状态，但当前生产调用搜索只找到 BattleSim 使用，未找到玩家 UI／文本命令入口。既有“非核心转职业核心”服务还要求已有职业等级和核心槽位，无法引导等级 0 的角色完成首次晋升。

代码锚点：

- [GameSession.CharacterCreation.cs:356](E:/game/magic/scripts/systems/persistence/GameSession.CharacterCreation.cs:356)：等级 0；385 行起初始技能 `is_core = false`。
- [ProgressionService.cs:250](E:/game/magic/scripts/systems/progression/ProgressionService.cs:250)：核心开关；312 行和 1275 行要求 ready active trigger。
- [LevelGrowthEvaluationService.cs:21](E:/game/magic/scripts/systems/progression/LevelGrowthEvaluationService.cs:21)：非核心技能返回 `skill_not_core`。
- [ProfessionAssignmentService.cs:114](E:/game/magic/scripts/systems/progression/ProfessionAssignmentService.cs:114)：核心槽位／已有职业限制。

诊断在新档中补齐三个已学近战技能，再通过真实待领奖励流程给重击 900 熟练度。结果：重击 3 级、人物 0 级、非核心、无激活技能、候选数 0。正式 `promotion choose warrior` 返回“当前没有待确认的职业晋升选择”。直接调用 manager 的触发选择接口也返回 `skill_not_core`。

这证明在排除技能获取和练习时间后，入口仍然不通；不代表诊断已覆盖三个技能的实际获取过程。

## F2：领域候选与运行时提示未形成统一刷新链

诊断显式调用现有服务，将重击设为核心并激活。此时 `PendingProfessionChoicesTyped.Count = 1`，但 modal 仍为 None，提示为空，`PresentPendingRewardIfReady()` 返回 false。再领取 1 点熟练度奖励后，提示才出现。

[CharacterManagementModule.cs:1327](E:/game/magic/scripts/systems/progression/CharacterManagementModule.cs:1327) 的选择接口只刷新领域运行状态，未通过成长 delta／运行时桥更新提示。[GameRuntimeRewardFlowHandler.cs:349](E:/game/magic/scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs:349) 只展示已经构造的提示或待领奖励，不从人物当前资格重建晋升选项。

因此，仅在 UI 上加一个调用现有 setter 的按钮仍不能闭环。选择、熟练度变化、学习、读档和奖励确认后的候选刷新必须走同一套领域查询及提示投影。

## F3：提示的默认选择在复制时变成显式空选择

这是本次诊断中复现的提交阻断：领域 `CanPromoteProfession(warrior) = true`，实际提示也展示战士 Rank 1，但确认被拒绝，等级维持 0。

具体链条：

1. [BattleSessionFacade.cs:782](E:/game/magic/scripts/systems/game_runtime/BattleSessionFacade.cs:782) 构造选择项时传入 `PromotionSelectionData.Empty`。
2. [GameRuntimePromotionPromptContext.cs:61](E:/game/magic/scripts/systems/game_runtime/GameRuntimePromotionPromptContext.cs:61) 复制时总是传入三个非 null 的集合。
3. [PromotionSelectionData.cs:34](E:/game/magic/scripts/systems/game_runtime/PromotionSelectionData.cs:34) 用“显式标志或集合非 null”判定字段存在。于是“未指定、允许领域解析”被转换成“三个字段均明确指定为空”。
4. [ProgressionService.cs:725](E:/game/magic/scripts/systems/progression/ProgressionService.cs:725) 进入显式选择校验，空集合不满足触发技能和职业条件。
5. [GameRuntimeRewardFlowHandler.cs:194](E:/game/magic/scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs:194) 正式提交失败，返回“晋升提交无效，当前选择仍需确认”。

实际提示包含：

```json
{"selected_assigned_core_skill_ids":[],"selected_qualifier_skill_ids":[],"trigger_skill_ids":[]}
```

诊断用合法的显式技能选择替换这一提示后，同一条正式文本命令成功完成晋升。这是用于隔离故障的测试干预，不是生产修复。

[PromotionChoiceWindow.cs:242](E:/game/magic/scripts/ui/PromotionChoiceWindow.cs:242) 同样提交提示中的 selection，因此 UI 共享此缺陷来源；本轮没有进行实际鼠标点击和窗口渲染 E2E。

此外，`PendingProfessionChoice` 保存的是候选池和要求数量，不是已经选定的完整技能组合；不能通过“把池中全部技能填入 selection”修复。`ProgressionService.cs:803` 的 `TriggerSkillIds` 是 qualifier 与 assigned 的并集，也不能直接当作唯一的本次升级触发技能。

## F4：读档恢复了领域候选，没有恢复确认入口

在第二次晋升条件满足时保存，日志显示候选数 1、modal Promotion、提示有内容。加载同一个磁盘存档后，候选数仍为 1，但 modal None、提示为空。调用提示调度仍返回 false，正式确认命令报告没有待确认选择。

代码链：

- [HeadlessGameTestSession.cs:181](E:/game/magic/scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs:181)：加载存档并重新建立 facade。
- [GameRuntimeFacade.cs:271](E:/game/magic/scripts/systems/game_runtime/GameRuntimeFacade.cs:271)：Setup 装配运行时；377 行重置 modal，没有从领域候选重建晋升提示。
- [GameRuntimeFacade.cs:505](E:/game/magic/scripts/systems/game_runtime/GameRuntimeFacade.cs:505)：旧 facade 的 Dispose 清除两类提示；新 facade 的提示初始为空。
- [GameRuntimeRewardFlowHandler.cs:349](E:/game/magic/scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs:349)：展示逻辑依赖已有提示。

应从持久化人物状态重新计算并投影提示，不应为 UI 字典新增一套存档。加载后还必须遵守死亡状态、战斗状态及现有 modal 调度边界。

## F5：已经完成的基础训练被后续上限重新计费

重击的成长配置为 `3 → 5`，曲线 `[100, 250, 550, 1000, 1600]`。第一次达到门槛需要 900；扩展后的两级还需要 2600。守御的基础门槛为 3 级，曲线前三项 `[300, 750, 1650]`，合计 2700。

诊断步骤：先用重击完成战士 Rank 1，再把守御练至 3 级、设核心并激活。此时重击变为 `3/5`，战士 Rank 2 不可晋升。给重击补足 2600，达到 `5/5` 后，Rank 2 候选出现。

| 本例阶段 | 当前规则所需熟练度 | 保留原资格的建议规则 |
| --- | ---: | ---: |
| 首次：重击达到基础门槛 | 900 | 900 |
| 第二次：守御达到门槛，并满足旧核心条件 | 2700 + 2600 = 5300 | 2700 |
| 两次累计 | 6200 | 3600 |

这只是已验证的战士技能组合，假设其他学习、标签、归属条件均已满足。它不是所有职业的最低成本，也不能直接换算成游戏时长。

原因位于 [SkillEffectiveMaxLevelRules.cs:24](E:/game/magic/scripts/systems/progression/SkillEffectiveMaxLevelRules.cs:24)：被锁定后解除 `non_core_max_level` 限制；而 [ProfessionRuleService.cs:485](E:/game/magic/scripts/systems/progression/ProfessionRuleService.cs:485)、[ProfessionAssignmentService.cs:39](E:/game/magic/scripts/systems/progression/ProfessionAssignmentService.cs:39)、[ProgressionService.cs:852](E:/game/magic/scripts/systems/progression/ProgressionService.cs:852) 继续用当前有效满级判定资格。

建议分离“技能还能练到几级”与“是否已经取得晋升资格”。扩展等级保留战斗价值，已完成晋升的同一技能不因上限变化失去支持后续职业成长的资格。这一取舍尚未实现。

## 已验证有效的下游部分

在上述显式干预之后，正式命令能够完成职业晋升，人物等级由 0 变为 1；本次样本最大 HP 从 6 到 7；重击记为已锁定且已领取属性成长，力量进度 +40、体质进度 +20；再次选它作为升级触发技能被拒绝。

保存并加载后，等级、HP、锁定和属性成长标志保持。本例 HP 增量只代表一次掷骰结果，不是固定升级公式。后续日志 HP 变为 12 是另一次成就奖励确认的结果，不应算进首次晋升收益。

这说明已有结算服务可复用，当前工作不需要先重写人物成长模块。

## 验证范围与复现材料

运行环境为 Windows、Godot `4.6.2.stable.mono.official.71f334935`。测试使用项目 runner 的 `prepare_user_data_env` 创建独立临时 APPDATA／LOCALAPPDATA／XDG 目录。

本轮构建成功；以下 6 个现有定向回归通过，生命周期报告无失败：

- `run_skill_effective_max_level_rules_regression.cs`
- `run_promotion_selection_typed_regression.cs`
- `run_character_management_quest_materializer_regression.cs`
- `run_text_command_reward_flow_regression.cs`
- `run_game_runtime_reward_flow_handler_regression.cs`
- `run_promotion_choice_window_schema_regression.cs`

前两个结果来自本轮终端执行记录；后四个原始输出归档在 [evidence 目录](E:/game/magic/docs/reviews/evidence/2026-09-13-first-promotion)。这些测试覆盖局部规则或注入后的流程，未覆盖“新角色产生真实提示再确认”的完整路径，因此它们通过与 F1—F4 并不矛盾。

诊断材料：

- [最终诊断输出](E:/game/magic/docs/reviews/evidence/2026-09-13-first-promotion/probe-final.txt)
- [诊断源代码归档](E:/game/magic/docs/reviews/evidence/2026-09-13-first-promotion/run_first_promotion_audit_probe.cs.txt)
- [移除临时诊断后的构建输出](E:/game/magic/docs/reviews/evidence/2026-09-13-first-promotion/build-final.txt)

归档源文件 SHA-256：`ff0350e0bf9283f1c18c1f78553685ee0e29c8c6dbae576d1b51c5ab14b972a8`。临时 runner 已从 `tests/` 移除，避免把“当前缺陷能够复现”混入长期正确性回归。诊断 PASS 表示观察断言成立，其中包含预期失败；不是首次晋升端到端 PASS。读档提示状态记录为观察输出，没有单独的恢复失败断言。

复现时临时将归档复制回 `tests/progression/core/run_first_promotion_audit_probe.cs`，先 `dotnet build magic.csproj`，再在隔离用户目录的子进程运行：

```text
python tests/run_regression_suite.py --pattern run_first_promotion_audit_probe.cs --fail-on-output-error --test-timeout-seconds 60
```

串行 runner 的用户目录隔离应通过 `prepare_user_data_env` 设置子进程环境，不能仅依赖并行执行用的 `--user-data-root` 参数。完成后移除临时 runner 并重新构建。

本轮没有运行全量回归、数值 BattleSim、真实战斗节奏测试、冷进程存档恢复或实际 UI 操作。熟练度通过真实待领奖励结算路径加速注入，技能学习使用 manager 接口；不能据此给出“多少分钟升一级”的实测结论。

## 下一步

按 [首次晋升闭环方案](E:/game/magic/docs/proposals/progression/first_promotion_closed_loop.md) 先完成提交语义和恢复修复，再落地玩家选择入口及资格保留规则。完成正常新档至两次晋升的验收后，再以实际战斗、探索、技能获取时间校准熟练度曲线。
