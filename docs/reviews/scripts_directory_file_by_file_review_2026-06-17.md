# scripts 目录逐文件代码审查报告

- 审查日期：2026-06-17
- 审查范围：`scripts/` 下全部文件（用户写作 `scripr`，本报告按仓库实际目录 `scripts/` 处理）。
- 文件总数：466
- 审查方式：先阅读 `AGENTS.md` 与 `docs/design/project_context_units.md`，再对 `scripts/` 每个文件逐一做静态审查记录；重点关注 Godot 场景/信号耦合、typed 边界、存档/序列化、运行时状态、性能热点、测试缺口。
- 注意：这是全目录静态审查索引，不等同于已逐个场景启动验证；需要对“需跟进”项再做对应 headless/runtime 复现。

## 总体结论

本次逐文件审查没有直接修改运行时代码。`scripts/` 目录规模较大，许多文件处于 Godot/C# 边界、战斗 AI、内容注册表、存档序列化和 UI 场景适配层。报告中每个文件均列出“关注点”和“审查结论”。标记为“需跟进”的文件不是一定存在缺陷，而是包含高风险耦合或需要对应场景/回归验证的接口。

## 高优先级横向风险

1. **Godot 字典/数组边界仍然广泛存在**：这些文件需要确保字典只停留在 Godot 资源、UI payload 或测试边界，不要回流为正式业务态。
2. **UI 与场景节点路径/信号高度耦合**：含 `GetNode*`、`Connect`、`SignalName`、`Callable` 的文件应在场景调整时同步检查 `.tscn`。
3. **战斗 AI 与预览/执行一致性风险**：战斗相关文件数量大，且包含 preview、commit、score、trace 多条链路；后续修复应优先使用窄回归而不是全量模拟。
4. **存档/序列化触点高风险**：包含 `ToDictionary` / `FromDictionary` / save / JSON 的文件应遵循兼容性策略，不能擅自加入旧 schema 兼容逻辑。



## Godot code-review skill 复审结论（findings-first）

本节按 `.codex/skills/godot-code-review/SKILL.md` 和 `references/review-checklist.md` 重新整理，优先报告会导致运行时错误、状态错写、预览/执行不一致、场景脚本契约漂移、持久化风险或缺失回归的发现；不再把普通模式命中当作问题本身。

[高] `scripts/ui/PromotionChoiceWindow.cs:241` - 晋升确认信号在 `HideWindow()` 之后发出，`HideWindow()` 已在 `scripts/ui/PromotionChoiceWindow.cs:109` 清空 `_memberId`，所以 `WorldMapSystem._on_promotion_choice_submitted()` 收到的 `member_id` 会是空值。失败路径是玩家点击确认后 runtime 侧无法定位触发晋升的角色，轻则拒绝晋升，重则如果下游有默认角色兜底会把晋升选择应用到错误角色。现有 `tests/world_map/ui/run_promotion_choice_window_schema_regression.cs` 只验证 show/render，不验证 confirm signal payload，因此会漏掉这个回归。建议缓存 `memberId` 后再 `HideWindow()`，并补一个连接 `choice_submitted` 的 headless UI 测试。

[中] `scripts/ui/PromotionChoiceWindow.cs:209` - `_detailsLabel.Text` 直接拼入 `display_name`、`description`、`selection_hint`，而 `scenes/ui/promotion_choice_window.tscn:110` 开启 `bbcode_enabled = true`。任何职业/技能内容中出现 BBCode 方括号都会被解释为 UI 标记而非普通文本，导致晋升说明被伪造颜色、隐藏、截断或布局污染。建议只让代码插入受控 BBCode 标签，所有内容字段统一走 BBCode escape helper，或关闭该 label 的 BBCode。

[中] `scripts/systems/battle/ai/BattleAiService.cs:71` - AI trace 的 `Enter("choose:impl")` / `Exit("choose:impl")` 没有用 `try/finally` 包住；`ChooseCommandImpl()` 或 mutation guard capture/validate 中任意异常都会跳过对应 `Exit`，而外层 `finally` 只调用 `_scoreService.EndDecisionScope()`，不会修复 trace recorder 栈。失败后 `AiTraceRecorder.AssertBalanced()` 会失败，后续父子耗时也会被污染。该文件已有外层 try/finally，说明生命周期清理是预期要求；建议给每个 trace span 使用 helper/`try/finally`。

[中] `scripts/systems/battle/ai/BattleAiScoreService.cs:237` - `BuildSkillScoreInput()` 里多段 `AiTraceRecorder.Enter/Exit` 同样不是 exception-safe；`FilterEffectDefsForContext()`、`PopulateHitMetrics()`、`PopulateSpecialProfileMetrics()` 等评分构建任意一步抛错都会让 `build_skill_score_input` 或子 span 残留在 trace 栈中。这个函数处在战斗 AI 热路径，诊断模式一旦打开，异常后的性能报告会变成不可信。建议将 trace span 改成 `using`/scope guard 或显式 `try/finally`。

[中] `scripts/dev_tools/AiTraceRecorder.cs:130` - trace 栈帧把 `Time.GetTicksUsec()` 的 `ulong` 和 `(ulong)0` 存入 `Godot.Collections.Dictionary`，随后在 `scripts/dev_tools/AiTraceRecorder.cs:186` / `scripts/dev_tools/AiTraceRecorder.cs:190` / `scripts/dev_tools/AiTraceRecorder.cs:238` 通过 `long`/`ulong` 显式强转从 Variant 取回。Godot Variant 的正式整数形态是有符号整型，诊断模式下这些混合转换存在 InvalidCast/溢出/平台差异风险。建议内部计时统一为 `long`，写入 Dictionary 前显式转换，读取时使用一致 helper。

[中] `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs:123` - “丢弃全部”先用 `CountItem(itemId)` 计算同类总数，再把 `totalQuantity` 传入 `RemoveWarehouseItemOrInstance()`；但该 helper 在 `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs:533` 对装备完全忽略 quantity，改为只删除 `instanceId` 指定的单个装备实例。UI 当前会传选中实例，所以按钮路径大概率正常；但命令名/API 语义对文本命令、测试和未来调用者是陷阱：`CommandDiscardAllTyped(equipmentId)` 并不会删除全部同类装备。建议把装备无 instance 的路径在 command 层显式拒绝并改文案，或实现真正的多实例删除。

[中] `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs:164` - `RollDamageDieVirtual()` 通过 `Call("_roll_damage_die", diceSides)` 调用本类已有的 `public virtual int _roll_damage_die(...)`。这绕过 C# 编译期重命名检查，也绕过正常虚方法调用约束；若方法名调整、签名变化或 Godot binding 行为变化，伤害随机掷骰会在运行时才失败。测试替身已经通过 override `_roll_damage_die` 工作，直接调用虚方法即可保留可测性并降低 GodotSharp 字符串调用风险。

[低] `scripts/ui/PartyWarehouseWindow.cs:366` - 仓库详情对任意非空 `icon_path` 直接 `GD.Load<Texture2D>()`。内容路径拼错或指向非 Texture2D 时，每次选择条目都会产生 Godot load error/log noise；如果仓库列表频繁刷新，还会重复触发失败加载。建议内容注册时校验 icon path，UI 层用 `ResourceLoader.Exists`/类型检查和失败缓存，失败时显示占位图。

[低] `tests/world_map/ui/run_promotion_choice_window_schema_regression.cs:34` - 晋升窗口回归只覆盖 payload schema 和卡片渲染，没有覆盖确认按钮、取消按钮、信号载荷、BBCode 文本边界。它无法捕捉本次最高优先级的 `_memberId` 清空 bug。建议补 `choice_submitted` 参数断言和 BBCode escape 边界用例。

Open questions / assumptions:

- `scripts/systems/persistence/SaveSerializer.cs:1006` 的 `NormalizeEncounterAnchors()` 在判断 `Variant.Type.Dictionary` 前先调用 `anchorValue.AsGodotObject()`；如果 Godot C# 对非 Object Variant 的 `AsGodotObject()` 不是安全返回 null，而是抛异常，那么已序列化为 Dictionary 的 encounter anchors 在 normalize/load 时会崩。当前缺少 Godot runtime 无法确认该 binding 行为，建议在有 Godot 环境时补一个最小 headless 用例。
- `AiTraceRecorder` 默认未启用，相关 trace 问题主要影响 benchmark、battle simulation、AI profiling 和异常后的诊断可信度；如果团队不把 trace dump 作为 CI gate，严重度可下调，但仍应修复以免性能排查时被工具自身误导。
- 仓库“丢弃全部装备”的正确产品语义需要确认：是禁止批量删除装备，还是删除所有同类实例。当前实现和命令名不一致。

Residual risks / test gaps:

- 当前容器缺少 `godot` 与 `dotnet`，无法运行 `dotnet build magic.csproj`、`python tests/run_regression_suite.py` 或针对 UI/AI 的 headless runner，因此以上发现是源码级复审结论，尚未 runtime 复现。
- 战斗 preview-vs-execution、一整套 battle AI action evaluator、save schema 细节数量很大；本轮优先抓了跨文件高风险链路，没有声称覆盖所有数值平衡或 AI 决策质量问题。
- 旧的逐文件索引仍保留在后文，用于定位文件和静态热点；真正应优先处理的是本 findings-first 节列出的具体问题。

## 逐文件审查明细


### 1. `scripts/dev_tools/AiTraceRecorder.cs`

- 状态：**需跟进**
- 规模：330 行；扩展名 `.cs`
- 文件职责线索：类型：AiTraceRecorder；public 成员/声明约 14 处。
- 静态关注点：Godot Dictionary/Array boundary 20处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 11处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 2. `scripts/enemies/AiActionTrace.cs`

- 状态：**关注**
- 规模：193 行；扩展名 `.cs`
- 文件职责线索：类型：AiActionTrace；public 成员/声明约 24 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 21处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 3. `scripts/enemies/AiCandidateSummary.cs`

- 状态：**关注**
- 规模：122 行；扩展名 `.cs`
- 文件职责线索：类型：AiCandidateSummary；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 17处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 4. `scripts/enemies/AiCommandSummary.cs`

- 状态：**关注**
- 规模：121 行；扩展名 `.cs`
- 文件职责线索：类型：AiCommandSummary；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 15处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 5. `scripts/enemies/DropEntryDef.cs`

- 状态：**通过**
- 规模：20 行；扩展名 `.cs`
- 文件职责线索：类型：DropEntryDef；public 成员/声明约 6 处；导出字段 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 6. `scripts/enemies/EnemyAiAction.cs`

- 状态：**需跟进**
- 规模：1547 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiAction, struct；public 成员/声明约 6 处；导出字段 3 处。
- 静态关注点：Godot Dictionary/Array boundary 29处；runtime mutation/state touchpoint 66处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 7. `scripts/enemies/EnemyAiActionHelper.cs`

- 状态：**关注**
- 规模：231 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiActionHelper。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 8. `scripts/enemies/EnemyAiBrainDef.cs`

- 状态：**需跟进**
- 规模：133 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiBrainDef；public 成员/声明约 6 处；导出字段 4 处。
- 静态关注点：Godot Dictionary/Array boundary 10处；runtime mutation/state touchpoint 20处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 9. `scripts/enemies/EnemyAiDistanceReference.cs`

- 状态：**通过**
- 规模：46 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiDistanceReference, EnemyAiDistanceReferences。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 10. `scripts/enemies/EnemyAiGenerationSlotDef.cs`

- 状态：**关注**
- 规模：369 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiGenerationSlotRole, EnemyAiSkillAffordance, EnemyAiActionFamily, EnemyAiGenerationSuppressionPolicy, EnemyAiGenerationSlotDef；public 成员/声明约 14 处；导出字段 12 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 16处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 11. `scripts/enemies/EnemyAiStateDef.cs`

- 状态：**关注**
- 规模：179 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiStateDef；public 成员/声明约 5 处；导出字段 3 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；runtime mutation/state touchpoint 29处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 12. `scripts/enemies/EnemyAiTargetSelectorRules.cs`

- 状态：**通过**
- 规模：73 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiTargetSelector, EnemyAiTargetSelectorRules。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 13. `scripts/enemies/EnemyAiTransitionConditionDef.cs`

- 状态：**关注**
- 规模：176 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiTransitionPredicate, EnemyAiTransitionConditionDef；public 成员/声明约 6 处；导出字段 5 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 14处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 14. `scripts/enemies/EnemyAiTransitionRuleDef.cs`

- 状态：**关注**
- 规模：103 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyAiTransitionRuleDef；public 成员/声明约 7 处；导出字段 5 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 17处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 15. `scripts/enemies/EnemyContentRegistry.cs`

- 状态：**需跟进**
- 规模：496 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyContentRegistry；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 12处；resource/path load coupling 8处；runtime mutation/state touchpoint 61处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 16. `scripts/enemies/EnemyContentSeed.cs`

- 状态：**关注**
- 规模：14 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyContentSeed；public 成员/声明约 4 处；导出字段 3 处。
- 静态关注点：Godot Dictionary/Array boundary 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 17. `scripts/enemies/EnemyTemplateDef.cs`

- 状态：**需跟进**
- 规模：762 行；扩展名 `.cs`
- 文件职责线索：类型：EnemyTemplateDef；public 成员/声明约 19 处；导出字段 18 处。
- 静态关注点：Godot Dictionary/Array boundary 27处；runtime mutation/state touchpoint 86处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 18. `scripts/enemies/TraceDictionaryProjection.cs`

- 状态：**需跟进**
- 规模：305 行；扩展名 `.cs`
- 文件职责线索：类型：TraceDictionaryProjection。
- 静态关注点：Godot Dictionary/Array boundary 16处；serialization/save/schema touchpoint 28处；runtime mutation/state touchpoint 37处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 19. `scripts/enemies/WildEncounterRosterDef.cs`

- 状态：**关注**
- 规模：164 行；扩展名 `.cs`
- 文件职责线索：类型：WildEncounterRosterDef；public 成员/声明约 7 处；导出字段 6 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；runtime mutation/state touchpoint 20处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 20. `scripts/enemies/WildEncounterRosterStageDef.cs`

- 状态：**关注**
- 规模：11 行；扩展名 `.cs`
- 文件职责线索：类型：WildEncounterRosterStageDef；public 成员/声明约 3 处；导出字段 2 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 21. `scripts/enemies/WildEncounterRosterUnitEntryDef.cs`

- 状态：**关注**
- 规模：15 行；扩展名 `.cs`
- 文件职责线索：类型：WildEncounterRosterUnitEntryDef；public 成员/声明约 4 处；导出字段 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 22. `scripts/enemies/actions/MoveToAdvantagePositionAction.cs`

- 状态：**关注**
- 规模：583 行；扩展名 `.cs`
- 文件职责线索：类型：MoveToAdvantagePositionAction, MoveToAdvantagePositioningMode, MoveCandidate；public 成员/声明约 19 处；导出字段 11 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 25处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 23. `scripts/enemies/actions/MoveToMultiUnitSkillPositionAction.cs`

- 状态：**关注**
- 规模：365 行；扩展名 `.cs`
- 文件职责线索：类型：MoveToMultiUnitSkillPositionAction；public 成员/声明约 3 处；导出字段 1 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 24处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 24. `scripts/enemies/actions/MoveToRangeAction.cs`

- 状态：**需跟进**
- 规模：2482 行；扩展名 `.cs`
- 文件职责线索：类型：MoveToRangeAction, MoveToRangeAiEvaluationMode, MoveToRangeScreeningMode, ScreeningContext, struct, ScreeningThreatEntry；public 成员/声明约 83 处；导出字段 16 处。
- 静态关注点：Godot Dictionary/Array boundary 20处；runtime mutation/state touchpoint 105处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 25. `scripts/enemies/actions/RetreatAction.cs`

- 状态：**关注**
- 规模：206 行；扩展名 `.cs`
- 文件职责线索：类型：RetreatAction；public 成员/声明约 6 处；导出字段 4 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 26. `scripts/enemies/actions/UseChargeAction.cs`

- 状态：**关注**
- 规模：535 行；扩展名 `.cs`
- 文件职责线索：类型：UseChargeAction, ChargeTargetInfo, ChargeDistanceBreakpoint；public 成员/声明约 13 处；导出字段 3 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；runtime mutation/state touchpoint 13处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 27. `scripts/enemies/actions/UseChargePathAoeAction.cs`

- 状态：**关注**
- 规模：495 行；扩展名 `.cs`
- 文件职责线索：类型：UseChargePathAoeAction, ChargeTargetInfo, PathStepHitMetrics；public 成员/声明约 18 处；导出字段 5 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 24处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 28. `scripts/enemies/actions/UseGroundRepositionSkillAction.cs`

- 状态：**关注**
- 规模：256 行；扩展名 `.cs`
- 文件职责线索：类型：UseGroundRepositionSkillAction；public 成员/声明约 8 处；导出字段 6 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 29. `scripts/enemies/actions/UseGroundSkillAction.cs`

- 状态：**需跟进**
- 规模：1122 行；扩展名 `.cs`
- 文件职责线索：类型：UseGroundSkillAction, GroundCandidatePrefilter, GroundTargetCoordSet, GroundSkillEffectSet；public 成员/声明约 30 处；导出字段 13 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 62处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 30. `scripts/enemies/actions/UseMultiUnitSkillAction.cs`

- 状态：**关注**
- 规模：419 行；扩展名 `.cs`
- 文件职责线索：类型：UseMultiUnitSkillAction；public 成员/声明约 9 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 44处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 31. `scripts/enemies/actions/UseRandomChainSkillAction.cs`

- 状态：**关注**
- 规模：479 行；扩展名 `.cs`
- 文件职责线索：类型：UseRandomChainSkillAction, RandomChainDistanceContract, RandomChainScoreMetadata；public 成员/声明约 27 处；导出字段 6 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 36处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 32. `scripts/enemies/actions/UseUnitSkillAction.cs`

- 状态：**关注**
- 规模：80 行；扩展名 `.cs`
- 文件职责线索：类型：UseUnitSkillAction；public 成员/声明约 10 处；导出字段 8 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 33. `scripts/enemies/actions/WaitAction.cs`

- 状态：**关注**
- 规模：286 行；扩展名 `.cs`
- 文件职责线索：类型：WaitAction, ActiveRestProfile；public 成员/声明约 10 处；导出字段 2 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 34. `scripts/player/equipment/EquipmentDurabilityRules.cs`

- 状态：**关注**
- 规模：33 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentDurabilityRules。
- 静态关注点：serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 35. `scripts/player/equipment/EquipmentEntryState.cs`

- 状态：**关注**
- 规模：95 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentEntryState；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 36. `scripts/player/equipment/EquipmentRequirement.cs`

- 状态：**关注**
- 规模：64 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentRequirementCheckResult, EquipmentRequirement；public 成员/声明约 10 处；导出字段 1 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 37. `scripts/player/equipment/EquipmentRules.cs`

- 状态：**关注**
- 规模：167 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentSlotKind, EquipmentRules；public 成员/声明约 6 处。
- 静态关注点：runtime mutation/state touchpoint 13处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 38. `scripts/player/equipment/EquipmentState.cs`

- 状态：**关注**
- 规模：302 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentState；public 成员/声明约 18 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 26处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 39. `scripts/player/progression/AchievementDef.cs`

- 状态：**需跟进**
- 规模：130 行；扩展名 `.cs`
- 文件职责线索：类型：AchievementDef；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 10处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 2处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 40. `scripts/player/progression/AchievementProgressState.cs`

- 状态：**关注**
- 规模：82 行；扩展名 `.cs`
- 文件职责线索：类型：AchievementProgressState；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 41. `scripts/player/progression/AchievementRewardDef.cs`

- 状态：**关注**
- 规模：113 行；扩展名 `.cs`
- 文件职责线索：类型：AchievementRewardDef；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 42. `scripts/player/progression/AgeContentRegistry.cs`

- 状态：**需跟进**
- 规模：240 行；扩展名 `.cs`
- 文件职责线索：类型：AgeContentRegistry；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 12处；resource/path load coupling 2处；runtime mutation/state touchpoint 20处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 43. `scripts/player/progression/AgeProfileDef.cs`

- 状态：**关注**
- 规模：44 行；扩展名 `.cs`
- 文件职责线索：类型：AgeProfileDef；public 成员/声明约 14 处；导出字段 13 处。
- 静态关注点：Godot Dictionary/Array boundary 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 44. `scripts/player/progression/AgeStageRule.cs`

- 状态：**关注**
- 规模：29 行；扩展名 `.cs`
- 文件职责线索：类型：AgeStageRule；public 成员/声明约 9 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 45. `scripts/player/progression/AscensionContentRegistry.cs`

- 状态：**需跟进**
- 规模：246 行；扩展名 `.cs`
- 文件职责线索：类型：AscensionContentRegistry；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 9处；resource/path load coupling 2处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 46. `scripts/player/progression/AscensionDef.cs`

- 状态：**关注**
- 规模：41 行；扩展名 `.cs`
- 文件职责线索：类型：AscensionDef；public 成员/声明约 13 处；导出字段 11 处。
- 静态关注点：Godot Dictionary/Array boundary 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 47. `scripts/player/progression/AscensionStageDef.cs`

- 状态：**关注**
- 规模：32 行；扩展名 `.cs`
- 文件职责线索：类型：AscensionStageDef；public 成员/声明约 10 处；导出字段 8 处。
- 静态关注点：Godot Dictionary/Array boundary 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 48. `scripts/player/progression/AttributeGrowthContentRules.cs`

- 状态：**通过**
- 规模：47 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeGrowthTierKind, AttributeGrowthContentRules；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 49. `scripts/player/progression/AttributeModifier.cs`

- 状态：**通过**
- 规模：75 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeModifierMode, AttributeModifier；public 成员/声明约 11 处；导出字段 6 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 50. `scripts/player/progression/AttributeRequirement.cs`

- 状态：**通过**
- 规模：17 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeRequirement；public 成员/声明约 5 处；导出字段 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 51. `scripts/player/progression/AttributeSnapshot.cs`

- 状态：**关注**
- 规模：129 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeSnapshotIdKind, AttributeSnapshot, struct；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 52. `scripts/player/progression/BarrierContentRegistry.cs`

- 状态：**关注**
- 规模：210 行；扩展名 `.cs`
- 文件职责线索：类型：BarrierContentRegistry；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；resource/path load coupling 2处；runtime mutation/state touchpoint 23处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 53. `scripts/player/progression/BarrierLayerDef.cs`

- 状态：**关注**
- 规模：57 行；扩展名 `.cs`
- 文件职责线索：类型：BarrierLayerDef；public 成员/声明约 8 处；导出字段 6 处。
- 静态关注点：serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 54. `scripts/player/progression/BarrierOutcomeDef.cs`

- 状态：**需跟进**
- 规模：107 行；扩展名 `.cs`
- 文件职责线索：类型：BarrierOutcomeKind, BarrierOutcomeDef；public 成员/声明约 13 处；导出字段 11 处。
- 静态关注点：serialization/save/schema touchpoint 15处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 55. `scripts/player/progression/BarrierProfileDef.cs`

- 状态：**关注**
- 规模：76 行；扩展名 `.cs`
- 文件职责线索：类型：BarrierAnchorMode, BarrierProfileDef；public 成员/声明约 10 处；导出字段 8 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 56. `scripts/player/progression/BattleSaveContentRules.cs`

- 状态：**需跟进**
- 规模：237 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSaveDcMode, BattleSaveAdvantageStateKind, BattleSaveTagKind, BattleSaveAbilityKind, BattleSaveContentRules。
- 静态关注点：serialization/save/schema touchpoint 148处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 57. `scripts/player/progression/BloodlineContentRegistry.cs`

- 状态：**需跟进**
- 规模：215 行；扩展名 `.cs`
- 文件职责线索：类型：BloodlineContentRegistry；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 9处；resource/path load coupling 2处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 58. `scripts/player/progression/BloodlineDef.cs`

- 状态：**关注**
- 规模：29 行；扩展名 `.cs`
- 文件职责线索：类型：BloodlineDef；public 成员/声明约 9 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 59. `scripts/player/progression/BloodlineStageDef.cs`

- 状态：**关注**
- 规模：29 行；扩展名 `.cs`
- 文件职责线索：类型：BloodlineStageDef；public 成员/声明约 9 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 60. `scripts/player/progression/BodySizeContentRules.cs`

- 状态：**通过**
- 规模：118 行；扩展名 `.cs`
- 文件职责线索：类型：BodySizeCategoryKind, BodySizeContentRules。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 61. `scripts/player/progression/CombatCastVariantDef.cs`

- 状态：**关注**
- 规模：84 行；扩展名 `.cs`
- 文件职责线索：类型：CombatCastFootprintPattern, CombatCastVariantDef；public 成员/声明约 11 处；导出字段 9 处。
- 静态关注点：Godot Dictionary/Array boundary 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 62. `scripts/player/progression/CombatEffectDef.cs`

- 状态：**需跟进**
- 规模：659 行；扩展名 `.cs`
- 文件职责线索：类型：CombatEffectTriggerEvent, CombatEffectTriggerCondition, CombatEffectLifetimePolicy, CombatEffectDef；public 成员/声明约 130 处；导出字段 125 处。
- 静态关注点：Godot Dictionary/Array boundary 10处；serialization/save/schema touchpoint 20处；runtime mutation/state touchpoint 10处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 63. `scripts/player/progression/CombatResourceIds.cs`

- 状态：**关注**
- 规模：47 行；扩展名 `.cs`
- 文件职责线索：类型：CombatResourceIdKind, CombatResourceIds。
- 静态关注点：runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 64. `scripts/player/progression/CombatSkillDef.cs`

- 状态：**需跟进**
- 规模：628 行；扩展名 `.cs`
- 文件职责线索：类型：CombatSpellFateMode, CombatSpellCriticalMode, CombatSkillBacklashMode, CombatAreaOriginMode, CombatAreaDirectionMode, CombatSkillDef；public 成员/声明约 67 处；导出字段 49 处。
- 静态关注点：Godot Dictionary/Array boundary 27处；runtime mutation/state touchpoint 8处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 65. `scripts/player/progression/CombatSkillResourceCosts.cs`

- 状态：**关注**
- 规模：23 行；扩展名 `.cs`
- 文件职责线索：类型：struct；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 66. `scripts/player/progression/CombatSkillTargetingContentRules.cs`

- 状态：**通过**
- 规模：87 行；扩展名 `.cs`
- 文件职责线索：类型：CombatSkillTargetingContentRules。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 67. `scripts/player/progression/CombatTargetTeamContentRules.cs`

- 状态：**通过**
- 规模：79 行；扩展名 `.cs`
- 文件职责线索：类型：CombatTargetTeamFilterKind, CombatTargetTeamContentRules；public 成员/声明约 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 68. `scripts/player/progression/DamageTagContentRules.cs`

- 状态：**通过**
- 规模：119 行；扩展名 `.cs`
- 文件职责线索：类型：DamageTagKind, DamageMitigationTierKind, DamageCategoryKind, DamageTagContentRules；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 69. `scripts/player/progression/DerivedAttributeRule.cs`

- 状态：**关注**
- 规模：75 行；扩展名 `.cs`
- 文件职责线索：类型：DerivedAttributeRule；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 70. `scripts/player/progression/FaithDeityDef.cs`

- 状态：**关注**
- 规模：104 行；扩展名 `.cs`
- 文件职责线索：类型：FaithDeityDef；public 成员/声明约 11 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 71. `scripts/player/progression/FaithRankDef.cs`

- 状态：**需跟进**
- 规模：172 行；扩展名 `.cs`
- 文件职责线索：类型：struct, FaithRankDef；public 成员/声明约 15 处；导出字段 8 处。
- 静态关注点：Godot Dictionary/Array boundary 9处；runtime mutation/state touchpoint 13处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 72. `scripts/player/progression/IdentityContentRegistryBase.cs`

- 状态：**需跟进**
- 规模：468 行；扩展名 `.cs`
- 文件职责线索：类型：IdentityContentRegistryBase；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 25处；runtime mutation/state touchpoint 28处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 73. `scripts/player/progression/PartyMemberState.cs`

- 状态：**需跟进**
- 规模：633 行；扩展名 `.cs`
- 文件职责线索：类型：PartyMemberState；public 成员/声明约 42 处。
- 静态关注点：Godot Dictionary/Array boundary 34处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 4处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 74. `scripts/player/progression/PartyState.cs`

- 状态：**需跟进**
- 规模：899 行；扩展名 `.cs`
- 文件职责线索：类型：PartyState；public 成员/声明约 62 处。
- 静态关注点：Godot Dictionary/Array boundary 53处；serialization/save/schema touchpoint 11处；runtime mutation/state touchpoint 66处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 75. `scripts/player/progression/PendingCharacterRewardContentRules.cs`

- 状态：**关注**
- 规模：87 行；扩展名 `.cs`
- 文件职责线索：类型：PendingCharacterRewardEntryKind, PendingCharacterRewardContentRules。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 76. `scripts/player/progression/PendingProfessionChoice.cs`

- 状态：**关注**
- 规模：302 行；扩展名 `.cs`
- 文件职责线索：类型：PendingProfessionChoice；public 成员/声明约 21 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 27处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 77. `scripts/player/progression/ProfessionActiveCondition.cs`

- 状态：**通过**
- 规模：57 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionActiveConditionKind, ProfessionActiveCondition；public 成员/声明约 7 处；导出字段 5 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 78. `scripts/player/progression/ProfessionContentRegistry.cs`

- 状态：**关注**
- 规模：709 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionContentRegistry, struct, struct；public 成员/声明约 10 处。
- 静态关注点：resource/path load coupling 2处；runtime mutation/state touchpoint 61处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 79. `scripts/player/progression/ProfessionDef.cs`

- 状态：**关注**
- 规模：159 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionReactivationMode, ProfessionDependencyVisibilityMode, ProfessionBaseAttackProgression, ProfessionDef；public 成员/声明约 19 处；导出字段 14 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 80. `scripts/player/progression/ProfessionGrantedSkill.cs`

- 状态：**通过**
- 规模：14 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionGrantedSkill；public 成员/声明约 4 处；导出字段 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 81. `scripts/player/progression/ProfessionPromotionRecord.cs`

- 状态：**关注**
- 规模：168 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionPromotionRecord；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 82. `scripts/player/progression/ProfessionPromotionRequirement.cs`

- 状态：**通过**
- 规模：33 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionPromotionRequirement；public 成员/声明约 8 处；导出字段 6 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 83. `scripts/player/progression/ProfessionRankGate.cs`

- 状态：**通过**
- 规模：45 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionGateCheckMode, ProfessionRankGate；public 成员/声明约 4 处；导出字段 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 84. `scripts/player/progression/ProfessionRankRequirement.cs`

- 状态：**关注**
- 规模：26 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionRankRequirement；public 成员/声明约 7 处；导出字段 5 处。
- 静态关注点：Godot Dictionary/Array boundary 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 85. `scripts/player/progression/ProgressionContentRegistry.cs`

- 状态：**需跟进**
- 规模：2421 行；扩展名 `.cs`
- 文件职责线索：类型：ProgressionContentRegistry；public 成员/声明约 39 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 237处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 86. `scripts/player/progression/ProgressionDataUtils.cs`

- 状态：**需跟进**
- 规模：216 行；扩展名 `.cs`
- 文件职责线索：类型：ProgressionDataUtils。
- 静态关注点：Godot Dictionary/Array boundary 10处；runtime mutation/state touchpoint 13处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 87. `scripts/player/progression/QuestContentValidator.cs`

- 状态：**关注**
- 规模：249 行；扩展名 `.cs`
- 文件职责线索：类型：QuestContentValidator；public 成员/声明约 2 处。
- 静态关注点：runtime mutation/state touchpoint 38处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 88. `scripts/player/progression/QuestDef.cs`

- 状态：**需跟进**
- 规模：806 行；扩展名 `.cs`
- 文件职责线索：类型：QuestObjectiveKind, QuestRewardKind, QuestDef, ObjectiveEntryData, RewardEntryData, PendingRewardEntryData；public 成员/声明约 35 处；导出字段 8 处。
- 静态关注点：Godot Dictionary/Array boundary 48处；serialization/save/schema touchpoint 12处；runtime mutation/state touchpoint 42处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 89. `scripts/player/progression/QuestProviderContentRules.cs`

- 状态：**关注**
- 规模：45 行；扩展名 `.cs`
- 文件职责线索：类型：QuestProviderKind, QuestProviderContentRules；public 成员/声明约 4 处。
- 静态关注点：runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 90. `scripts/player/progression/QuestState.cs`

- 状态：**需跟进**
- 规模：344 行；扩展名 `.cs`
- 文件职责线索：类型：QuestStatusKind, QuestState；public 成员/声明约 24 处。
- 静态关注点：Godot Dictionary/Array boundary 12处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 6处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 91. `scripts/player/progression/RaceContentRegistry.cs`

- 状态：**关注**
- 规模：181 行；扩展名 `.cs`
- 文件职责线索：类型：RaceContentRegistry；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；resource/path load coupling 2处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 13处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 92. `scripts/player/progression/RaceDef.cs`

- 状态：**需跟进**
- 规模：56 行；扩展名 `.cs`
- 文件职责线索：类型：RaceDef；public 成员/声明约 18 处；导出字段 16 处。
- 静态关注点：Godot Dictionary/Array boundary 10处；serialization/save/schema touchpoint 1处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 93. `scripts/player/progression/RaceTraitContentRegistry.cs`

- 状态：**关注**
- 规模：112 行；扩展名 `.cs`
- 文件职责线索：类型：RaceTraitContentRegistry；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；resource/path load coupling 2处；runtime mutation/state touchpoint 15处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 94. `scripts/player/progression/RaceTraitDef.cs`

- 状态：**关注**
- 规模：307 行；扩展名 `.cs`
- 文件职责线索：类型：RaceTraitEffectKind, RaceTraitDef；public 成员/声明约 7 处；导出字段 5 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 95. `scripts/player/progression/RacialGrantedSkill.cs`

- 状态：**通过**
- 规模：57 行；扩展名 `.cs`
- 文件职责线索：类型：RacialSkillChargeKind, RacialGrantedSkill；public 成员/声明约 5 处；导出字段 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 96. `scripts/player/progression/ReputationRequirement.cs`

- 状态：**通过**
- 规模：17 行；扩展名 `.cs`
- 文件职责线索：类型：ReputationRequirement；public 成员/声明约 5 处；导出字段 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 97. `scripts/player/progression/SkillContentRegistry.cs`

- 状态：**需跟进**
- 规模：2218 行；扩展名 `.cs`
- 文件职责线索：类型：SkillContentRegistry, struct；public 成员/声明约 11 处。
- 静态关注点：resource/path load coupling 2处；serialization/save/schema touchpoint 111处；runtime mutation/state touchpoint 235处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 98. `scripts/player/progression/SkillDef.cs`

- 状态：**需跟进**
- 规模：921 行；扩展名 `.cs`
- 文件职责线索：类型：SkillTypeKind, SkillLearnSourceKind, SkillPracticeTierKind, SkillUnlockMode, CoreSkillTransitionMode, SkillDef；public 成员/声明约 66 处；导出字段 28 处。
- 静态关注点：Godot Dictionary/Array boundary 41处；runtime mutation/state touchpoint 96处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 99. `scripts/player/progression/SkillLevelDescriptionContentRules.cs`

- 状态：**关注**
- 规模：84 行；扩展名 `.cs`
- 文件职责线索：类型：SkillLevelDescriptionContentRules；public 成员/声明约 2 处。
- 静态关注点：runtime mutation/state touchpoint 13处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 100. `scripts/player/progression/StageAdvancementContentRegistry.cs`

- 状态：**关注**
- 规模：172 行；扩展名 `.cs`
- 文件职责线索：类型：StageAdvancementContentRegistry；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；resource/path load coupling 2处；runtime mutation/state touchpoint 15处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 101. `scripts/player/progression/StageAdvancementModifier.cs`

- 状态：**关注**
- 规模：98 行；扩展名 `.cs`
- 文件职责线索：类型：StageAdvancementTargetAxis, StageAdvancementModifier；public 成员/声明约 13 处；导出字段 12 处。
- 静态关注点：Godot Dictionary/Array boundary 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 102. `scripts/player/progression/SubraceContentRegistry.cs`

- 状态：**关注**
- 规模：175 行；扩展名 `.cs`
- 文件职责线索：类型：SubraceContentRegistry；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；resource/path load coupling 2处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 103. `scripts/player/progression/SubraceDef.cs`

- 状态：**关注**
- 规模：51 行；扩展名 `.cs`
- 文件职责线索：类型：SubraceDef；public 成员/声明约 16 处；导出字段 14 处。
- 静态关注点：serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 104. `scripts/player/progression/TagRequirement.cs`

- 状态：**通过**
- 规模：130 行；扩展名 `.cs`
- 文件职责线索：类型：TagRequirementSkillState, TagRequirementOriginFilter, TagRequirementSelectionRole, TagRequirement；public 成员/声明约 6 处；导出字段 5 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 105. `scripts/player/progression/TraitTriggerContentRules.cs`

- 状态：**关注**
- 规模：160 行；扩展名 `.cs`
- 文件职责线索：类型：TraitTriggerKind, TraitTriggerContentRules；public 成员/声明约 4 处。
- 静态关注点：runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 106. `scripts/player/progression/UnitBaseAttributes.cs`

- 状态：**关注**
- 规模：304 行；扩展名 `.cs`
- 文件职责线索：类型：UnitBaseAttributeKind, UnitBaseAttributes；public 成员/声明约 20 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 107. `scripts/player/progression/UnitProfessionProgress.cs`

- 状态：**需跟进**
- 规模：233 行；扩展名 `.cs`
- 文件职责线索：类型：UnitProfessionProgress, in；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 18处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 10处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 108. `scripts/player/progression/UnitProgress.cs`

- 状态：**需跟进**
- 规模：1412 行；扩展名 `.cs`
- 文件职责线索：类型：UnitProgress；public 成员/声明约 56 处。
- 静态关注点：Godot Dictionary/Array boundary 92处；serialization/save/schema touchpoint 14处；runtime mutation/state touchpoint 107处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 109. `scripts/player/progression/UnitReputationState.cs`

- 状态：**需跟进**
- 规模：105 行；扩展名 `.cs`
- 文件职责线索：类型：UnitReputationKind, UnitReputationState；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 11处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 2处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 110. `scripts/player/progression/UnitSkillProgress.cs`

- 状态：**需跟进**
- 规模：409 行；扩展名 `.cs`
- 文件职责线索：类型：UnitSkillGrantSourceType, UnitSkillProgress；public 成员/声明约 23 处。
- 静态关注点：Godot Dictionary/Array boundary 13处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 6处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 111. `scripts/player/warehouse/EquipmentInstanceState.cs`

- 状态：**关注**
- 规模：187 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentInstanceState, RarityTier；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 112. `scripts/player/warehouse/ItemContentRegistry.cs`

- 状态：**需跟进**
- 规模：845 行；扩展名 `.cs`
- 文件职责线索：类型：ItemContentRegistry；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 30处；resource/path load coupling 4处；runtime mutation/state touchpoint 103处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 113. `scripts/player/warehouse/ItemDef.cs`

- 状态：**关注**
- 规模：354 行；扩展名 `.cs`
- 文件职责线索：类型：ItemCategoryKind, ItemEquipmentTypeKind, WeaponPhysicalDamageTagKind, ItemDef；public 成员/声明约 48 处；导出字段 17 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；runtime mutation/state touchpoint 15处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 114. `scripts/player/warehouse/RecipeContentRegistry.cs`

- 状态：**关注**
- 规模：246 行；扩展名 `.cs`
- 文件职责线索：类型：RecipeContentRegistry；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；resource/path load coupling 2处；runtime mutation/state touchpoint 39处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 115. `scripts/player/warehouse/RecipeDef.cs`

- 状态：**通过**
- 规模：33 行；扩展名 `.cs`
- 文件职责线索：类型：RecipeDef；public 成员/声明约 10 处；导出字段 7 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 116. `scripts/player/warehouse/SkillBookItemContentValidator.cs`

- 状态：**关注**
- 规模：108 行；扩展名 `.cs`
- 文件职责线索：类型：SkillBookItemContentValidator；public 成员/声明约 2 处。
- 静态关注点：runtime mutation/state touchpoint 19处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 117. `scripts/player/warehouse/SkillBookItemFactory.cs`

- 状态：**关注**
- 规模：64 行；扩展名 `.cs`
- 文件职责线索：类型：SkillBookItemFactory；public 成员/声明约 3 处。
- 静态关注点：resource/path load coupling 1处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 118. `scripts/player/warehouse/WarehouseStackState.cs`

- 状态：**关注**
- 规模：43 行；扩展名 `.cs`
- 文件职责线索：类型：WarehouseStackState；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 119. `scripts/player/warehouse/WarehouseState.cs`

- 状态：**需跟进**
- 规模：182 行；扩展名 `.cs`
- 文件职责线索：类型：WarehouseState；public 成员/声明约 18 处。
- 静态关注点：Godot Dictionary/Array boundary 13处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 120. `scripts/player/warehouse/WarehouseStateItemValidator.cs`

- 状态：**关注**
- 规模：137 行；扩展名 `.cs`
- 文件职责线索：类型：WarehouseStateItemValidator；public 成员/声明约 2 处。
- 静态关注点：runtime mutation/state touchpoint 20处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 121. `scripts/player/warehouse/WeaponDamageDiceDef.cs`

- 状态：**关注**
- 规模：85 行；扩展名 `.cs`
- 文件职责线索：类型：WeaponDamageDiceDef；public 成员/声明约 8 处；导出字段 3 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 122. `scripts/player/warehouse/WeaponProfileDef.cs`

- 状态：**需跟进**
- 规模：281 行；扩展名 `.cs`
- 文件职责线索：类型：WeaponProfileDef, PropertyMergeMode；public 成员/声明约 19 处；导出字段 10 处。
- 静态关注点：Godot Dictionary/Array boundary 20处；runtime mutation/state touchpoint 10处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 123. `scripts/systems/attributes/AttributeService.cs`

- 状态：**需跟进**
- 规模：1126 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeIdKind, AttributeSourceKind, AttributeServiceKeyKind, AttributeService, struct, struct；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 57处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 124. `scripts/systems/attributes/AttributeSourceContext.cs`

- 状态：**关注**
- 规模：41 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeSourceContext；public 成员/声明约 19 处。
- 静态关注点：runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 125. `scripts/systems/battle/_interop/BattleTypedEnums.cs`

- 状态：**需跟进**
- 规模：937 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiActionKind, BattleCommandKind, PendingCastBindingModeKind, PendingCastRefundPolicy, BattleEquipmentOperationKind, BattlePhaseKind；public 成员/声明约 1 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 126. `scripts/systems/battle/ai/BattleAiActionAssembler.cs`

- 状态：**需跟进**
- 规模：1281 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiActionAssembler, in, in；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 49处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 127. `scripts/systems/battle/ai/BattleAiActionIntent.cs`

- 状态：**通过**
- 规模：69 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiIntent, BattleAiActionIntent。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 128. `scripts/systems/battle/ai/BattleAiCandidateEvaluationService.cs`

- 状态：**关注**
- 规模：160 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiCandidateEvaluationService；public 成员/声明约 1 处。
- 静态关注点：runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 129. `scripts/systems/battle/ai/BattleAiCandidateRequest.cs`

- 状态：**关注**
- 规模：303 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiCandidateRequest, MoveToRangePathSearchBudget, MoveToRangeTacticalParams, MoveToRangeRuntimeMetadata；public 成员/声明约 32 处。
- 静态关注点：runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 130. `scripts/systems/battle/ai/BattleAiContext.cs`

- 状态：**需跟进**
- 规模：1197 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiContext, struct, RuntimeActionMetadata；public 成员/声明约 30 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 84处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 131. `scripts/systems/battle/ai/BattleAiDecision.cs`

- 状态：**通过**
- 规模：17 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiDecision；public 成员/声明约 10 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 132. `scripts/systems/battle/ai/BattleAiDecisionCommitter.cs`

- 状态：**通过**
- 规模：186 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiDecisionCommitter, DecisionStatePatch；public 成员/声明约 2 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 133. `scripts/systems/battle/ai/BattleAiDecisionEngine.cs`

- 状态：**关注**
- 规模：823 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiDecisionEngine, ScoreInputFacts, RuntimeActionResolution；public 成员/声明约 33 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 134. `scripts/systems/battle/ai/BattleAiFailurePolicy.cs`

- 状态：**关注**
- 规模：178 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiFailureEvent, BattleAiFailurePolicy；public 成员/声明约 5 处。
- 静态关注点：runtime mutation/state touchpoint 13处；per-frame/async/lifecycle touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 135. `scripts/systems/battle/ai/BattleAiMoveToRangeCandidateEvaluator.cs`

- 状态：**需跟进**
- 规模：1131 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiMoveToRangeCandidateEvaluator, GroundAoeSetupMetrics, struct, UnitAreaIndex；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 16处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 136. `scripts/systems/battle/ai/BattleAiMutationGuard.cs`

- 状态：**需跟进**
- 规模：3584 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiMutationGuard, BattleAiMutationSnapshot, BattleUnitSnapshot, BattleStateFieldsSnapshot, BattleUnitFieldsSnapshot, KnownFieldSnapshot；public 成员/声明约 90 处。
- 静态关注点：Godot Dictionary/Array boundary 21处；serialization/save/schema touchpoint 70处；runtime mutation/state touchpoint 242处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 137. `scripts/systems/battle/ai/BattleAiMutationViolation.cs`

- 状态：**关注**
- 规模：113 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiMutationViolationException, BattleAiMutationViolationReport。
- 静态关注点：runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 138. `scripts/systems/battle/ai/BattleAiPayloadGuard.cs`

- 状态：**关注**
- 规模：520 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiPayloadGuard。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 12处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 139. `scripts/systems/battle/ai/BattleAiQueryService.cs`

- 状态：**关注**
- 规模：595 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiQueryService, struct, struct, SkillRecord；public 成员/声明约 20 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 48处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 140. `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs`

- 状态：**关注**
- 规模：632 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiRuntimeActionPlan, RuntimeActionMetadata, RuntimeActionExportMetadata；public 成员/声明约 39 处。
- 静态关注点：runtime mutation/state touchpoint 61处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 141. `scripts/systems/battle/ai/BattleAiSafetyGate.cs`

- 状态：**通过**
- 规模：75 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiSafetyGate。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 142. `scripts/systems/battle/ai/BattleAiScoreContextAdapter.cs`

- 状态：**关注**
- 规模：278 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreContextAdapter。
- 静态关注点：runtime mutation/state touchpoint 11处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 143. `scripts/systems/battle/ai/BattleAiScoreInput.cs`

- 状态：**需跟进**
- 规模：1455 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreInput；public 成员/声明约 98 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 36处；runtime mutation/state touchpoint 96处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 144. `scripts/systems/battle/ai/BattleAiScoreOrdering.cs`

- 状态：**通过**
- 规模：60 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreOrdering；public 成员/声明约 2 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 145. `scripts/systems/battle/ai/BattleAiScoreProfile.cs`

- 状态：**关注**
- 规模：321 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiMeteorFriendlyFireProfile, BattleAiScoreProfile；public 成员/声明约 44 处；导出字段 38 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 146. `scripts/systems/battle/ai/BattleAiScoreRuntimeMetadata.cs`

- 状态：**关注**
- 规模：486 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreRuntimeMetadata；public 成员/声明约 37 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 20处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 147. `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`

- 状态：**需跟进**
- 规模：1378 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreService, TargetEffectMetrics, TargetRoleSummary, struct；public 成员/声明约 20 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 19处；runtime mutation/state touchpoint 58处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 148. `scripts/systems/battle/ai/BattleAiScoreService.Helpers.cs`

- 状态：**关注**
- 规模：409 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreService；public 成员/声明约 1 处。
- 静态关注点：runtime mutation/state touchpoint 22处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 149. `scripts/systems/battle/ai/BattleAiScoreService.Position.cs`

- 状态：**关注**
- 规模：675 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreService, HitRatePreviewEstimate；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 16处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 150. `scripts/systems/battle/ai/BattleAiScoreService.Projection.cs`

- 状态：**关注**
- 规模：486 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreService, ThreatProjection, ThreatSkillEntry, ThreatProfile；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 24处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 151. `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`

- 状态：**需跟进**
- 规模：1384 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreService, DamageEstimateResult, DamageSaveEstimate, DamageEstimateBreakdown, DamagePreviewSnapshot, PathStepHitCountEntry；public 成员/声明约 63 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 300处；runtime mutation/state touchpoint 118处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 152. `scripts/systems/battle/ai/BattleAiScoreService.cs`

- 状态：**关注**
- 规模：859 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiScoreService, struct, struct, struct, ScoreBuildMetadata, ScoreRandomChainMetadata；public 成员/声明约 31 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 55处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 153. `scripts/systems/battle/ai/BattleAiService.cs`

- 状态：**关注**
- 规模：231 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiService。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 154. `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`

- 状态：**关注**
- 规模：395 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiSkillAffordanceClassifier。
- 静态关注点：serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 155. `scripts/systems/battle/ai/BattleAiSkillAffordanceRecord.cs`

- 状态：**关注**
- 规模：87 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiSkillAffordanceRecord；public 成员/声明约 21 处。
- 静态关注点：runtime mutation/state touchpoint 11处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 156. `scripts/systems/battle/ai/BattleAiStateResolver.cs`

- 状态：**关注**
- 规模：444 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiStateResolver, TransitionResult, TransitionConditionTrace；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 30处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 157. `scripts/systems/battle/ai/BattleAiTurnTraceProjection.cs`

- 状态：**关注**
- 规模：340 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiTurnTraceProjection, BattleAiTraceTransitionProjection, BattleAiTraceTransitionConditionProjection, BattleAiTraceUnitSnapshotProjection, BattleAiTraceUnitResultProjection, BattleAiTraceExecutionResultProjection；public 成员/声明约 6 处。
- 静态关注点：runtime mutation/state touchpoint 66处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 158. `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`

- 状态：**关注**
- 规模：608 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiTypedActionHelper；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 26处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 159. `scripts/systems/battle/ai/BattleAiUnitSkillCandidateEvaluator.cs`

- 状态：**关注**
- 规模：532 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiUnitSkillCandidateEvaluator。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 23处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 160. `scripts/systems/battle/ai/BattleAiUnitSnapshot.cs`

- 状态：**需跟进**
- 规模：274 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiUnitBlackboardSnapshot, BattleAiUnitSnapshot；public 成员/声明约 33 处。
- 静态关注点：Godot Dictionary/Array boundary 16处；runtime mutation/state touchpoint 18处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 161. `scripts/systems/battle/ai/IBattleAiScoreContext.cs`

- 状态：**关注**
- 规模：11 行；扩展名 `.cs`
- 文件职责线索：脚本/辅助定义文件。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 162. `scripts/systems/battle/ai/MoveToRangeScoreOrdering.cs`

- 状态：**通过**
- 规模：347 行；扩展名 `.cs`
- 文件职责线索：类型：MoveToRangeScoreOrdering, FactIndex。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 163. `scripts/systems/battle/core/AttackCheckInput.cs`

- 状态：**通过**
- 规模：100 行；扩展名 `.cs`
- 文件职责线索：类型：AttackCheckInput；public 成员/声明约 30 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 164. `scripts/systems/battle/core/AttackContext.cs`

- 状态：**关注**
- 规模：51 行；扩展名 `.cs`
- 文件职责线索：类型：AttackContext；public 成员/声明约 11 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 165. `scripts/systems/battle/core/AttackEffectResolutionResult.cs`

- 状态：**需跟进**
- 规模：1175 行；扩展名 `.cs`
- 文件职责线索：类型：AttackResolutionKind, CriticalSourceKind, ExecuteOutcomeKind, MitigationTierKind, DamageDiceMaxReasonKind, ReportEntryKind；public 成员/声明约 134 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 56处；runtime mutation/state touchpoint 35处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 166. `scripts/systems/battle/core/AttackPreviewData.cs`

- 状态：**关注**
- 规模：199 行；扩展名 `.cs`
- 文件职责线索：类型：struct, AttackPreviewData；public 成员/声明约 31 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 14处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 167. `scripts/systems/battle/core/AttackResolutionMetadata.cs`

- 状态：**关注**
- 规模：29 行；扩展名 `.cs`
- 文件职责线索：类型：AttackResolutionMetadata；public 成员/声明约 24 处。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 168. `scripts/systems/battle/core/AttackRollResult.cs`

- 状态：**通过**
- 规模：35 行；扩展名 `.cs`
- 文件职责线索：类型：AttackRollResult；public 成员/声明约 10 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 169. `scripts/systems/battle/core/AttackTraitTriggerResult.cs`

- 状态：**通过**
- 规模：55 行；扩展名 `.cs`
- 文件职责线索：类型：AttackTraitTriggerResult；public 成员/声明约 17 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 170. `scripts/systems/battle/core/BattleAiBlackboard.cs`

- 状态：**关注**
- 规模：291 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAiBlackboard；public 成员/声明约 33 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 171. `scripts/systems/battle/core/BattleAttackCheckPolicyContext.cs`

- 状态：**通过**
- 规模：19 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAttackCheckPolicyContext；public 成员/声明约 15 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 172. `scripts/systems/battle/core/BattleAttackRollModifierBundle.cs`

- 状态：**关注**
- 规模：54 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAttackRollModifierBundle；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 173. `scripts/systems/battle/core/BattleAttackRollModifierSpec.cs`

- 状态：**需跟进**
- 规模：501 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAttackRollModifierStackMode, BattleAttackRollModifierEndpointMode, BattleAttackRollModifierFootprintMode, BattleAttackRollModifierApplyTarget, BattleAttackRollModifierSpec；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 15处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 4处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 174. `scripts/systems/battle/core/BattleBarrierInstanceState.cs`

- 状态：**关注**
- 规模：231 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBarrierInstanceState；public 成员/声明约 17 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 5处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 175. `scripts/systems/battle/core/BattleBarrierLayerState.cs`

- 状态：**需跟进**
- 规模：250 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBarrierLayerState；public 成员/声明约 14 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 9处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 176. `scripts/systems/battle/core/BattleBarrierOutcomeState.cs`

- 状态：**需跟进**
- 规模：93 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBarrierOutcomeState；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 15处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 177. `scripts/systems/battle/core/BattleCellState.cs`

- 状态：**需跟进**
- 规模：709 行；扩展名 `.cs`
- 文件职责线索：类型：BattleCellState；public 成员/声明约 24 处。
- 静态关注点：Godot Dictionary/Array boundary 27处；serialization/save/schema touchpoint 7处；runtime mutation/state touchpoint 16处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 178. `scripts/systems/battle/core/BattleCommand.cs`

- 状态：**关注**
- 规模：121 行；扩展名 `.cs`
- 文件职责线索：类型：BattleCommand；public 成员/声明约 20 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；runtime mutation/state touchpoint 16处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 179. `scripts/systems/battle/core/BattleCommonSkillOutcome.cs`

- 状态：**关注**
- 规模：90 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleCommonSkillOutcome；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 180. `scripts/systems/battle/core/BattleEdgeFaceState.cs`

- 状态：**关注**
- 规模：59 行；扩展名 `.cs`
- 文件职责线索：类型：BattleEdgeFaceState；public 成员/声明约 22 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 181. `scripts/systems/battle/core/BattleEdgeFeatureState.cs`

- 状态：**关注**
- 规模：372 行；扩展名 `.cs`
- 文件职责线索：类型：BattleEdgeFeatureKind, BattleEdgeRenderKind, BattleEdgeInteractionKind, BattleEdgeFeatureState；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 182. `scripts/systems/battle/core/BattleEventBatch.cs`

- 状态：**需跟进**
- 规模：263 行；扩展名 `.cs`
- 文件职责线索：类型：BattleEventBatch；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 11处；runtime mutation/state touchpoint 35处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 183. `scripts/systems/battle/core/BattleLootConstants.cs`

- 状态：**通过**
- 规模：135 行；扩展名 `.cs`
- 文件职责线索：类型：BattleLootDropKind, BattleLootSourceKind, BattleLootSourceIdKind, BattleLootSpecialItemKind, BattleLootIds。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 184. `scripts/systems/battle/core/BattlePendingCastState.cs`

- 状态：**关注**
- 规模：78 行；扩展名 `.cs`
- 文件职责线索：类型：BattlePendingCastState。
- 静态关注点：runtime mutation/state touchpoint 11处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 185. `scripts/systems/battle/core/BattlePreview.cs`

- 状态：**关注**
- 规模：212 行；扩展名 `.cs`
- 文件职责线索：类型：BattlePreview；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 26处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 186. `scripts/systems/battle/core/BattleRepeatAttackStageSpec.cs`

- 状态：**关注**
- 规模：277 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRepeatAttackStageSpec；public 成员/声明约 21 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 187. `scripts/systems/battle/core/BattleResolutionResult.cs`

- 状态：**关注**
- 规模：564 行；扩展名 `.cs`
- 文件职责线索：类型：BattleResolutionResult。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 7处；runtime mutation/state touchpoint 16处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 188. `scripts/systems/battle/core/BattleSpecialProfileGateResult.cs`

- 状态：**关注**
- 规模：50 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpecialProfileGateResult；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 189. `scripts/systems/battle/core/BattleSpecialProfilePreviewFacts.cs`

- 状态：**关注**
- 规模：174 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpecialProfilePreviewFacts；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 23处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 190. `scripts/systems/battle/core/BattleState.cs`

- 状态：**需跟进**
- 规模：489 行；扩展名 `.cs`
- 文件职责线索：类型：BattleState, BattleCellEntry, BattleUnitEntry；public 成员/声明约 49 处。
- 静态关注点：Godot Dictionary/Array boundary 18处；runtime mutation/state touchpoint 22处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 191. `scripts/systems/battle/core/BattleStatusEffectState.cs`

- 状态：**需跟进**
- 规模：1032 行；扩展名 `.cs`
- 文件职责线索：类型：BattleStatusEffectState；public 成员/声明约 58 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 101处；runtime mutation/state touchpoint 31处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 192. `scripts/systems/battle/core/BattleTimelineState.cs`

- 状态：**需跟进**
- 规模：187 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTimelineState；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 15处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 4处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 193. `scripts/systems/battle/core/BattleUnitState.cs`

- 状态：**需跟进**
- 规模：2314 行；扩展名 `.cs`
- 文件职责线索：类型：BattleWeaponProfileKind, BattleWeaponGripKind, BattleUnitState；public 成员/声明约 104 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 32处；runtime mutation/state touchpoint 79处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 194. `scripts/systems/battle/core/CombatResourceKind.cs`

- 状态：**通过**
- 规模：89 行；扩展名 `.cs`
- 文件职责线索：类型：CombatResourceKind, CombatResourceKindUtils；public 成员/声明约 1 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 195. `scripts/systems/battle/core/SkillCostTransaction.cs`

- 状态：**关注**
- 规模：44 行；扩展名 `.cs`
- 文件职责线索：类型：SkillCostTransaction。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 196. `scripts/systems/battle/core/WeaponDice.cs`

- 状态：**关注**
- 规模：74 行；扩展名 `.cs`
- 文件职责线索：类型：WeaponDice；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 197. `scripts/systems/battle/core/WeaponProjection.cs`

- 状态：**关注**
- 规模：59 行；扩展名 `.cs`
- 文件职责线索：类型：WeaponProjection；public 成员/声明约 15 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 198. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmCastContext.cs`

- 状态：**通过**
- 规模：21 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmCastContext；public 成员/声明约 9 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 199. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmCommitResult.cs`

- 状态：**关注**
- 规模：39 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmCommitResult；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 200. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmComponentFact.cs`

- 状态：**关注**
- 规模：66 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmComponentFact；public 成员/声明约 15 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 201. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmImpactComponent.cs`

- 状态：**关注**
- 规模：84 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmImpactComponent；public 成员/声明约 18 处；导出字段 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 202. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmNumericSummary.cs`

- 状态：**需跟进**
- 规模：726 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmHostileTerrainConsequence, MeteorSwarmComponentBreakdownEntry, MeteorSwarmNumericSummary；public 成员/声明约 51 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 124处；runtime mutation/state touchpoint 65处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 203. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmPreviewFacts.cs`

- 状态：**关注**
- 规模：87 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmPreviewFacts；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 11处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 204. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmProfile.cs`

- 状态：**关注**
- 规模：72 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmProfile；public 成员/声明约 10 处；导出字段 9 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 205. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTargetOutcome.cs`

- 状态：**关注**
- 规模：61 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmTargetOutcome；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 206. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTargetPlan.cs`

- 状态：**关注**
- 规模：78 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmTargetPlan；public 成员/声明约 18 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 207. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTerrainEffectFact.cs`

- 状态：**通过**
- 规模：12 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmTerrainEffectFact；public 成员/声明约 8 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 208. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTerrainSummaryFact.cs`

- 状态：**关注**
- 规模：42 行；扩展名 `.cs`
- 文件职责线索：类型：MeteorSwarmTerrainSummaryFact；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 209. `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifest.cs`

- 状态：**通过**
- 规模：38 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpecialProfileManifest；public 成员/声明约 12 处；导出字段 11 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 210. `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifestValidator.cs`

- 状态：**需跟进**
- 规模：576 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpecialProfileManifestValidator；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 26处；resource/path load coupling 2处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 61处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 211. `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs`

- 状态：**需跟进**
- 规模：305 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpecialProfileRegistry；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 14处；resource/path load coupling 2处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 36处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 212. `scripts/systems/battle/fate/BattleFateAttackRules.cs`

- 状态：**通过**
- 规模：64 行；扩展名 `.cs`
- 文件职责线索：类型：BattleFateAttackRules；public 成员/声明约 5 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 213. `scripts/systems/battle/fate/BattleFateEventBus.cs`

- 状态：**需跟进**
- 规模：98 行；扩展名 `.cs`
- 文件职责线索：类型：BattleFateEventBus；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 13处；string/callable/node path coupling 2处；runtime mutation/state touchpoint 2处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 214. `scripts/systems/battle/fate/FateAttackFormula.cs`

- 状态：**通过**
- 规模：96 行；扩展名 `.cs`
- 文件职责线索：类型：FateAttackFormula, GodotRandomRollSource；public 成员/声明约 11 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 215. `scripts/systems/battle/fate/FateRuntimeModule.cs`

- 状态：**需跟进**
- 规模：717 行；扩展名 `.cs`
- 文件职责线索：类型：FateRuntimeModule。
- 静态关注点：Godot Dictionary/Array boundary 13处；serialization/save/schema touchpoint 7处；runtime mutation/state touchpoint 24处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 216. `scripts/systems/battle/fate/FortunaGuidanceService.cs`

- 状态：**关注**
- 规模：265 行；扩展名 `.cs`
- 文件职责线索：类型：FortunaGuidanceEventInput, FortunaChapterCompletionInput, FortunaGuidanceService；public 成员/声明约 12 处。
- 静态关注点：runtime mutation/state touchpoint 14处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 217. `scripts/systems/battle/fate/FortuneService.cs`

- 状态：**通过**
- 规模：149 行；扩展名 `.cs`
- 文件职责线索：类型：FortuneMarkEventInput, FortuneService, SeededGodotRollSource；public 成员/声明约 13 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 218. `scripts/systems/battle/fate/LowLuckEventService.cs`

- 状态：**关注**
- 规模：823 行；扩展名 `.cs`
- 文件职责线索：类型：LowLuckFateEventPayload, LowLuckSettlementActionInput, struct, LowLuckBattleResolutionInput, struct, LowLuckEventResult；public 成员/声明约 33 处。
- 静态关注点：runtime mutation/state touchpoint 48处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 219. `scripts/systems/battle/fate/MisfortuneGuidanceService.cs`

- 状态：**关注**
- 规模：467 行；扩展名 `.cs`
- 文件职责线索：类型：MisfortuneForgeGuidanceItemEntry, MisfortuneForgeGuidanceInput, MisfortuneGuidanceService；public 成员/声明约 14 处。
- 静态关注点：runtime mutation/state touchpoint 24处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 220. `scripts/systems/battle/fate/MisfortuneService.cs`

- 状态：**关注**
- 规模：862 行；扩展名 `.cs`
- 文件职责线索：类型：MisfortuneSkillKind, MisfortuneService, struct；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；runtime mutation/state touchpoint 43处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 221. `scripts/systems/battle/fate/MisfortuneSkillCastResult.cs`

- 状态：**通过**
- 规模：28 行；扩展名 `.cs`
- 文件职责线索：类型：struct；public 成员/声明约 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 222. `scripts/systems/battle/presentation/BattleHudAdapter.cs`

- 状态：**需跟进**
- 规模：2425 行；扩展名 `.cs`
- 文件职责线索：类型：BattleHudAdapter, EquipmentPreviewRule；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 86处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 223. `scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs`

- 状态：**关注**
- 规模：814 行；扩展名 `.cs`
- 文件职责线索：类型：BattleAttackCheckPolicyService；public 成员/声明约 10 处。
- 静态关注点：runtime mutation/state touchpoint 37处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 224. `scripts/systems/battle/rules/BattleDamagePreviewRangeService.cs`

- 状态：**关注**
- 规模：257 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamagePreviewRangeService, struct, struct, struct, struct, struct；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 225. `scripts/systems/battle/rules/BattleDamagePreviewResult.cs`

- 状态：**需跟进**
- 规模：469 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamagePreviewSaveEstimate, BattleDamagePreviewResult；public 成员/声明约 49 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 180处；runtime mutation/state touchpoint 51处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 226. `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs`

- 状态：**关注**
- 规模：358 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamageResolver；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 227. `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`

- 状态：**需跟进**
- 规模：1363 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamageResolver, struct；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 77处；runtime mutation/state touchpoint 33处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 228. `scripts/systems/battle/rules/BattleDamageResolver.Events.cs`

- 状态：**关注**
- 规模：397 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamageResolver；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 229. `scripts/systems/battle/rules/BattleDamageResolver.Helpers.cs`

- 状态：**关注**
- 规模：368 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamageResolver；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 230. `scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs`

- 状态：**关注**
- 规模：657 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamageResolver；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 17处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 231. `scripts/systems/battle/rules/BattleDamageResolver.cs`

- 状态：**需跟进**
- 规模：2709 行；扩展名 `.cs`
- 文件职责线索：类型：BattleDamagePreviewRollMode, BattleDamagePreviewSaveMode, BattleDamagePreviewOptions, BattleDamageResolver, struct, struct；public 成员/声明约 25 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 427处；runtime mutation/state touchpoint 52处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 232. `scripts/systems/battle/rules/BattleDeathResolutionRules.cs`

- 状态：**通过**
- 规模：32 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleDeathResolutionRules；public 成员/声明约 6 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 233. `scripts/systems/battle/rules/BattleEffectCategoryResolver.cs`

- 状态：**关注**
- 规模：64 行；扩展名 `.cs`
- 文件职责线索：类型：BattleEffectCategoryResolver；public 成员/声明约 2 处。
- 静态关注点：runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 234. `scripts/systems/battle/rules/BattleEquipmentRequirementRules.cs`

- 状态：**关注**
- 规模：70 行；扩展名 `.cs`
- 文件职责线索：类型：BattleEquipmentRequirementRules；public 成员/声明约 4 处。
- 静态关注点：runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 235. `scripts/systems/battle/rules/BattleExecutionRules.cs`

- 状态：**关注**
- 规模：256 行；扩展名 `.cs`
- 文件职责线索：类型：struct, struct, struct, BattleExecutionRules；public 成员/声明约 14 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 236. `scripts/systems/battle/rules/BattleHitResolver.cs`

- 状态：**需跟进**
- 规模：1717 行；扩展名 `.cs`
- 文件职责线索：类型：BattleHitResolver；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；runtime mutation/state touchpoint 14处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 237. `scripts/systems/battle/rules/BattleRangeService.cs`

- 状态：**关注**
- 规模：505 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRangeService, UnitRangeInfo, struct；public 成员/声明约 24 处。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 238. `scripts/systems/battle/rules/BattleReportFormatter.cs`

- 状态：**需跟进**
- 规模：899 行；扩展名 `.cs`
- 文件职责线索：类型：BattleReportFormatter, DamageResultSummary；public 成员/声明约 17 处。
- 静态关注点：Godot Dictionary/Array boundary 19处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 25处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 239. `scripts/systems/battle/rules/BattleSaveResolver.cs`

- 状态：**需跟进**
- 规模：905 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSaveDegreeKind, struct, BattleSaveContext, struct, struct, BattleSaveResolver；public 成员/声明约 24 处。
- 静态关注点：Godot Dictionary/Array boundary 12处；serialization/save/schema touchpoint 245处；runtime mutation/state touchpoint 17处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 240. `scripts/systems/battle/rules/BattleSkillResolutionRules.cs`

- 状态：**关注**
- 规模：738 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSkillResolutionPolicy, BattleSkillResolutionRules；public 成员/声明约 35 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 38处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 241. `scripts/systems/battle/rules/BattleStatusModifierRules.cs`

- 状态：**关注**
- 规模：168 行；扩展名 `.cs`
- 文件职责线索：类型：BattleStatusModifierRules, struct；public 成员/声明约 5 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 242. `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`

- 状态：**需跟进**
- 规模：612 行；扩展名 `.cs`
- 文件职责线索：类型：struct, struct, BattleStatusSemanticTable；public 成员/声明约 23 处。
- 静态关注点：serialization/save/schema touchpoint 15处；runtime mutation/state touchpoint 7处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 243. `scripts/systems/battle/rules/BattleTargetTeamRules.cs`

- 状态：**通过**
- 规模：83 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTargetTeamRules, struct；public 成员/声明约 7 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 244. `scripts/systems/battle/rules/BattleTemporalStatusService.cs`

- 状态：**关注**
- 规模：260 行；扩展名 `.cs`
- 文件职责线索：类型：TemporalStatusReleaseKind, BattleTemporalStatusService。
- 静态关注点：serialization/save/schema touchpoint 5处；runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 245. `scripts/systems/battle/runtime/AscensionTraitResolver.cs`

- 状态：**关注**
- 规模：151 行；扩展名 `.cs`
- 文件职责线索：类型：AscensionTraitResolver；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 246. `scripts/systems/battle/runtime/BattleBarrierGeometryService.cs`

- 状态：**关注**
- 规模：125 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleBarrierGeometryService；public 成员/声明约 5 处。
- 静态关注点：runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 247. `scripts/systems/battle/runtime/BattleBarrierOutcomeResolver.cs`

- 状态：**需跟进**
- 规模：581 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleBarrierOutcomeResolver, struct, struct, struct；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 85处；runtime mutation/state touchpoint 5处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 248. `scripts/systems/battle/runtime/BattleBarrierService.cs`

- 状态：**需跟进**
- 规模：684 行；扩展名 `.cs`
- 文件职责线索：类型：struct, struct, struct, BattleBarrierService, struct；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 9处；serialization/save/schema touchpoint 25处；runtime mutation/state touchpoint 19处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 249. `scripts/systems/battle/runtime/BattleCastingTimeService.cs`

- 状态：**关注**
- 规模：720 行；扩展名 `.cs`
- 文件职责线索：类型：BattleCastingTimeService, struct。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 250. `scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs`

- 状态：**需跟进**
- 规模：1271 行；扩展名 `.cs`
- 文件职责线索：类型：BattleChangeEquipmentResult, ChangeEquipmentRuleResult, BattleChangeEquipmentResolver。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 40处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 251. `scripts/systems/battle/runtime/BattleChargeResolver.cs`

- 状态：**需跟进**
- 规模：1971 行；扩展名 `.cs`
- 文件职责线索：类型：BattleChargeResolver, struct, PathStepResult, ChargeBlockerResult, SidePushResult, TrapResult；public 成员/声明约 23 处。
- 静态关注点：Godot Dictionary/Array boundary 12处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 65处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 252. `scripts/systems/battle/runtime/BattleContributionEvent.cs`

- 状态：**关注**
- 规模：82 行；扩展名 `.cs`
- 文件职责线索：类型：BattleContributionRelation, BattleContributionOriginKind, BattleContributionEvent；public 成员/声明约 15 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 253. `scripts/systems/battle/runtime/BattleContributionEventBuilder.cs`

- 状态：**关注**
- 规模：161 行；扩展名 `.cs`
- 文件职责线索：类型：BattleContributionEventBuilder。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 254. `scripts/systems/battle/runtime/BattleContributionLedger.cs`

- 状态：**关注**
- 规模：24 行；扩展名 `.cs`
- 文件职责线索：类型：BattleContributionLedger；public 成员/声明约 3 处。
- 静态关注点：runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 255. `scripts/systems/battle/runtime/BattleForcedMoveContext.cs`

- 状态：**关注**
- 规模：32 行；扩展名 `.cs`
- 文件职责线索：类型：struct；public 成员/声明约 5 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 256. `scripts/systems/battle/runtime/BattleGroundEffectApplicationResult.cs`

- 状态：**关注**
- 规模：42 行；扩展名 `.cs`
- 文件职责线索：类型：struct, struct, struct；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 257. `scripts/systems/battle/runtime/BattleGroundEffectService.cs`

- 状态：**需跟进**
- 规模：2664 行；扩展名 `.cs`
- 文件职责线索：类型：BattleGroundEffectService, struct, GroundUnitEffectResolution, EdgeAuthoringReference。
- 静态关注点：Godot Dictionary/Array boundary 16处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 151处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 258. `scripts/systems/battle/runtime/BattleGroundSkillValidationResult.cs`

- 状态：**需跟进**
- 规模：176 行；扩展名 `.cs`
- 文件职责线索：类型：struct；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 13处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 10处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 259. `scripts/systems/battle/runtime/BattleLayeredBarrierService.cs`

- 状态：**通过**
- 规模：1 行；扩展名 `.cs`
- 文件职责线索：类型：BattleLayeredBarrierService。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 260. `scripts/systems/battle/runtime/BattleMagicBacklashResolver.cs`

- 状态：**关注**
- 规模：339 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMagicBacklashResolver。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 261. `scripts/systems/battle/runtime/BattleMagicBacklashResult.cs`

- 状态：**需跟进**
- 规模：282 行；扩展名 `.cs`
- 文件职责线索：类型：class, struct, struct；public 成员/声明约 26 处。
- 静态关注点：Godot Dictionary/Array boundary 23处；serialization/save/schema touchpoint 8处；runtime mutation/state touchpoint 6处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 262. `scripts/systems/battle/runtime/BattleMeteorSwarmResolver.cs`

- 状态：**需跟进**
- 规模：1715 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMeteorSwarmResolver。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 58处；runtime mutation/state touchpoint 89处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 263. `scripts/systems/battle/runtime/BattleMetricsCollector.cs`

- 状态：**关注**
- 规模：383 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMetricEntry, BattleMetricsState, BattleMetricsCollector；public 成员/声明约 30 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 8处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 264. `scripts/systems/battle/runtime/BattleMovePathResult.cs`

- 状态：**需跟进**
- 规模：111 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMovePathResult, BattleMovePathTreeResult, BattleValidatedMoveExecutionResult；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 15处；serialization/save/schema touchpoint 8处；runtime mutation/state touchpoint 9处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 265. `scripts/systems/battle/runtime/BattleMovementQueryService.cs`

- 状态：**需跟进**
- 规模：2370 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMovementQueryService, struct, struct, CellInfo, UnitInfo, EdgeInfo；public 成员/声明约 52 处。
- 静态关注点：Godot Dictionary/Array boundary 20处；runtime mutation/state touchpoint 100处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 266. `scripts/systems/battle/runtime/BattleMovementService.cs`

- 状态：**关注**
- 规模：541 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMovementService, struct。
- 静态关注点：runtime mutation/state touchpoint 30处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 267. `scripts/systems/battle/runtime/BattleRatingMemberStats.cs`

- 状态：**关注**
- 规模：100 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRatingMemberStats；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 268. `scripts/systems/battle/runtime/BattleRatingSystem.cs`

- 状态：**关注**
- 规模：512 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRatingSystem。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 269. `scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs`

- 状态：**需跟进**
- 规模：928 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRepeatAttackResolver, struct；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 12处；runtime mutation/state touchpoint 6处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 270. `scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs`

- 状态：**需跟进**
- 规模：596 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRuntimeLootResolver, ParsedDropDefinition；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 13处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 9处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 271. `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

- 状态：**需跟进**
- 规模：5121 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRuntimeDictionaryOptions, BattleDefeatHandlingOptions, BattleStartOptions, BattleEndOptions, BattleStartFailureSnapshot, BattleRuntimeModule；public 成员/声明约 41 处。
- 静态关注点：Godot Dictionary/Array boundary 11处；serialization/save/schema touchpoint 34处；runtime mutation/state touchpoint 197处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 272. `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`

- 状态：**需跟进**
- 规模：1802 行；扩展名 `.cs`
- 文件职责线索：类型：struct, struct, BattleRuntimeSkillTurnResolver。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 32处；runtime mutation/state touchpoint 19处；per-frame/async/lifecycle touchpoint 2处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 273. `scripts/systems/battle/runtime/BattleShieldService.cs`

- 状态：**需跟进**
- 规模：536 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleShieldService。
- 静态关注点：Godot Dictionary/Array boundary 10处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 274. `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`

- 状态：**需跟进**
- 规模：3986 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSkillExecutionOrchestrator, struct, UnitSkillEffectResolution；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 114处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 275. `scripts/systems/battle/runtime/BattleSkillMasteryGrant.cs`

- 状态：**关注**
- 规模：75 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSkillMasteryGrant；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 276. `scripts/systems/battle/runtime/BattleSkillMasteryService.cs`

- 状态：**需跟进**
- 规模：951 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSkillMasteryService, SkillMasteryResultSnapshot, SkillMasteryResolutionEvent, SkillMasteryDamageEventSnapshot；public 成员/声明约 37 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 20处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 277. `scripts/systems/battle/runtime/BattleSkillOutcomeCommitter.cs`

- 状态：**关注**
- 规模：245 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSkillOutcomeCommitter。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 278. `scripts/systems/battle/runtime/BattleSpawnReachabilityService.cs`

- 状态：**需跟进**
- 规模：988 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpawnReachabilityOptions, BattleSpawnReachabilityResult, BattleSpawnReachabilityUnitResult, BattleSpawnReachabilityService, BattleSpawnReachabilityAttackSkill, BattleSpawnReachabilityAttackTarget；public 成员/声明约 17 处。
- 静态关注点：Godot Dictionary/Array boundary 9处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 66处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 279. `scripts/systems/battle/runtime/BattleSpecialProfileGate.cs`

- 状态：**需跟进**
- 规模：289 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSpecialProfileGate, SpecialProfileGateSnapshot, struct；public 成员/声明约 5 处。
- 静态关注点：Godot Dictionary/Array boundary 11处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 11处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 280. `scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs`

- 状态：**需跟进**
- 规模：1468 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleSpecialSkillResolver, struct, struct；public 成员/声明约 37 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 44处；runtime mutation/state touchpoint 53处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 281. `scripts/systems/battle/runtime/BattleTargetCollectionResult.cs`

- 状态：**关注**
- 规模：53 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTargetCollectionResult；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 282. `scripts/systems/battle/runtime/BattleTargetCollectionService.cs`

- 状态：**关注**
- 规模：257 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTargetCollectionService。
- 静态关注点：runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 283. `scripts/systems/battle/runtime/BattleTimelineDriver.cs`

- 状态：**关注**
- 规模：823 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTimelineDriver。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 18处；per-frame/async/lifecycle touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 284. `scripts/systems/battle/runtime/BattleUnitFactory.cs`

- 状态：**需跟进**
- 规模：1103 行；扩展名 `.cs`
- 文件职责线索：类型：BattleUnitFactory, AllyUnitDefaults, EnemyUnitDefaults, EnemyWeaponDefaults；public 成员/声明约 33 处。
- 静态关注点：Godot Dictionary/Array boundary 53处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 37处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 285. `scripts/systems/battle/runtime/BattleUnitSkillValidationResult.cs`

- 状态：**需跟进**
- 规模：109 行；扩展名 `.cs`
- 文件职责线索：类型：struct；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 11处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 14处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 286. `scripts/systems/battle/runtime/IBattleRatingCharacterGateway.cs`

- 状态：**关注**
- 规模：91 行；扩展名 `.cs`
- 文件职责线索：public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 287. `scripts/systems/battle/runtime/PassiveStatusOrchestrator.cs`

- 状态：**关注**
- 规模：43 行；扩展名 `.cs`
- 文件职责线索：类型：PassiveStatusOrchestrator；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 288. `scripts/systems/battle/runtime/RaceTraitResolver.cs`

- 状态：**关注**
- 规模：156 行；扩展名 `.cs`
- 文件职责线索：类型：RaceTraitResolver；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 289. `scripts/systems/battle/runtime/SkillPassiveResolver.cs`

- 状态：**关注**
- 规模：350 行；扩展名 `.cs`
- 文件职责线索：类型：SkillPassiveResolver；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 5处；runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 290. `scripts/systems/battle/runtime/TestCsBase.cs`

- 状态：**通过**
- 规模：6 行；扩展名 `.cs`
- 文件职责线索：类型：TestCsBase；public 成员/声明约 2 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 291. `scripts/systems/battle/runtime/TraitTriggerHooks.cs`

- 状态：**关注**
- 规模：560 行；扩展名 `.cs`
- 文件职责线索：类型：TraitDispatchResult, TraitTriggerHooks, struct；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 292. `scripts/systems/battle/sim/BattleSimContentProvider.cs`

- 状态：**关注**
- 规模：32 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimContentProvider；public 成员/声明约 2 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 293. `scripts/systems/battle/sim/BattleSimExecutionLoop.cs`

- 状态：**关注**
- 规模：271 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimExecutionLoop, BattleSimExecutionLoopResult；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 294. `scripts/systems/battle/sim/BattleSimFactionMetricSummary.cs`

- 状态：**关注**
- 规模：58 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimFactionMetricSummary；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 295. `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`

- 状态：**需跟进**
- 规模：1403 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimFormalRosterOptionsData, BattleSimFormalCombatFixture, in；public 成员/声明约 38 处。
- 静态关注点：Godot Dictionary/Array boundary 85处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 56处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 296. `scripts/systems/battle/sim/BattleSimOutputFiles.cs`

- 状态：**关注**
- 规模：22 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimOutputFiles；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 297. `scripts/systems/battle/sim/BattleSimOverrideApplier.cs`

- 状态：**关注**
- 规模：495 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimOverrideApplier；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 24处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 298. `scripts/systems/battle/sim/BattleSimOverrideApplyResult.cs`

- 状态：**关注**
- 规模：26 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimOverrideApplyResult。
- 静态关注点：runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 299. `scripts/systems/battle/sim/BattleSimProfileComparison.cs`

- 状态：**关注**
- 规模：59 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimProfileComparison；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 300. `scripts/systems/battle/sim/BattleSimProfileDef.cs`

- 状态：**关注**
- 规模：37 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimProfileDef；public 成员/声明约 6 处；导出字段 4 处。
- 静态关注点：serialization/save/schema touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 301. `scripts/systems/battle/sim/BattleSimProfileReportEntry.cs`

- 状态：**关注**
- 规模：24 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimProfileReportEntry；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 302. `scripts/systems/battle/sim/BattleSimProfileSummary.cs`

- 状态：**关注**
- 规模：86 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimProfileSummary；public 成员/声明约 14 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 303. `scripts/systems/battle/sim/BattleSimReportBuilder.cs`

- 状态：**需跟进**
- 规模：351 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimReportBuilder；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 13处；runtime mutation/state touchpoint 29处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 304. `scripts/systems/battle/sim/BattleSimRunReport.cs`

- 状态：**关注**
- 规模：66 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimRunReport；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 305. `scripts/systems/battle/sim/BattleSimRunner.cs`

- 状态：**需跟进**
- 规模：467 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimRunner；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 17处；serialization/save/schema touchpoint 15处；runtime mutation/state touchpoint 23处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 306. `scripts/systems/battle/sim/BattleSimScenarioDef.cs`

- 状态：**需跟进**
- 规模：231 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimScenarioDef；public 成员/声明约 18 处；导出字段 15 处。
- 静态关注点：Godot Dictionary/Array boundary 20处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 9处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 307. `scripts/systems/battle/sim/BattleSimScenarioReport.cs`

- 状态：**关注**
- 规模：34 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimScenarioReport；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 5处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 308. `scripts/systems/battle/sim/BattleSimTerrainGenerator.cs`

- 状态：**关注**
- 规模：125 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimTerrainGenerator；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 309. `scripts/systems/battle/sim/BattleSimTraceSummaryBuilder.cs`

- 状态：**需跟进**
- 规模：1535 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimTraceSummaryBuilder, TraceSummaryOptionsData, CompactRunTraceData, CompactTurnTraceData, CompactActionTraceData, CompactTopCandidateData；public 成员/声明约 158 处。
- 静态关注点：Godot Dictionary/Array boundary 27处；serialization/save/schema touchpoint 109处；runtime mutation/state touchpoint 117处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 310. `scripts/systems/battle/sim/BattleSimUnitSpec.cs`

- 状态：**关注**
- 规模：480 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSimUnitSpec；public 成员/声明约 26 处；导出字段 24 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 311. `scripts/systems/battle/terrain/BattleEdgeService.cs`

- 状态：**关注**
- 规模：416 行；扩展名 `.cs`
- 文件职责线索：类型：BattleEdgeService, EdgeLookup；public 成员/声明约 5 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 312. `scripts/systems/battle/terrain/BattleGridDistanceService.cs`

- 状态：**关注**
- 规模：52 行；扩展名 `.cs`
- 文件职责线索：类型：BattleGridDistanceService；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 313. `scripts/systems/battle/terrain/BattleGridService.cs`

- 状态：**需跟进**
- 规模：2209 行；扩展名 `.cs`
- 文件职责线索：类型：struct, BattleGridService, MovePathNode, struct；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 56处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 314. `scripts/systems/battle/terrain/BattleTerrainEffectState.cs`

- 状态：**需跟进**
- 规模：631 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTerrainEffectState；public 成员/声明约 24 处。
- 静态关注点：Godot Dictionary/Array boundary 18处；serialization/save/schema touchpoint 17处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 315. `scripts/systems/battle/terrain/BattleTerrainEffectSystem.cs`

- 状态：**关注**
- 规模：615 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTerrainEffectSystem；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 316. `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`

- 状态：**需跟进**
- 规模：1776 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTerrainProfileKind, BattleTerrainGenerator, TerrainQualityResult；public 成员/声明约 5 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；runtime mutation/state touchpoint 76处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 317. `scripts/systems/battle/terrain/BattleTerrainRules.cs`

- 状态：**关注**
- 规模：282 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTerrainKind, BattleTerrainRules；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 318. `scripts/systems/battle/terrain/BattleTerrainTopologyService.cs`

- 状态：**关注**
- 规模：367 行；扩展名 `.cs`
- 文件职责线索：类型：BattleTerrainTopologyChange, BattleTerrainTopologyService；public 成员/声明约 10 处。
- 静态关注点：runtime mutation/state touchpoint 34处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 319. `scripts/systems/battle/terrain/BattleVirtualBoardOverlay.cs`

- 状态：**关注**
- 规模：157 行；扩展名 `.cs`
- 文件职责线索：类型：BattleVirtualBoardOverlay；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 320. `scripts/systems/content/GameContentCatalog.cs`

- 状态：**关注**
- 规模：194 行；扩展名 `.cs`
- 文件职责线索：类型：GameContentCatalog；public 成员/声明约 20 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 26处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 321. `scripts/systems/content/GameRoot.cs`

- 状态：**通过**
- 规模：47 行；扩展名 `.cs`
- 文件职责线索：类型：GameRoot；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 322. `scripts/systems/content/IValidatableRegistry.cs`

- 状态：**关注**
- 规模：6 行；扩展名 `.cs`
- 文件职责线索：public 成员/声明约 1 处。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 323. `scripts/systems/content/skills/ISkillCatalog.cs`

- 状态：**关注**
- 规模：46 行；扩展名 `.cs`
- 文件职责线索：public 成员/声明约 1 处。
- 静态关注点：runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 324. `scripts/systems/content/skills/SkillCatalog.cs`

- 状态：**关注**
- 规模：151 行；扩展名 `.cs`
- 文件职责线索：类型：SkillCatalog, EffectiveCombatProfileCacheKey；public 成员/声明约 20 处。
- 静态关注点：runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 325. `scripts/systems/content/skills/SkillEffectiveCombatProfile.cs`

- 状态：**关注**
- 规模：57 行；扩展名 `.cs`
- 文件职责线索：类型：SkillEffectiveCombatProfile；public 成员/声明约 13 处。
- 静态关注点：runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 326. `scripts/systems/content/skills/SkillEffectiveCombatProfileResolver.cs`

- 状态：**关注**
- 规模：189 行；扩展名 `.cs`
- 文件职责线索：类型：SkillEffectiveCombatProfileResolver。
- 静态关注点：Godot Dictionary/Array boundary 8处；runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 327. `scripts/systems/fate/LowLuckRelicRules.cs`

- 状态：**关注**
- 规模：217 行；扩展名 `.cs`
- 文件职责线索：类型：LowLuckRelicItemKind, LowLuckRelicAttributeKind, LowLuckRelicStatusKind, LowLuckPathTagKind, LowLuckRelicRules。
- 静态关注点：runtime mutation/state touchpoint 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 328. `scripts/systems/game_runtime/BattleRefreshMode.cs`

- 状态：**通过**
- 规模：19 行；扩展名 `.cs`
- 文件职责线索：类型：BattleRefreshMode, BattleRefreshModes。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 329. `scripts/systems/game_runtime/BattleSessionFacade.cs`

- 状态：**需跟进**
- 规模：1138 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSessionFacade；public 成员/声明约 32 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；runtime mutation/state touchpoint 16处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 330. `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`

- 状态：**关注**
- 规模：899 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeBattleLootCommitService, ItemCommitResult, BattleLootCommitResult。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 16处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 331. `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`

- 状态：**需跟进**
- 规模：2094 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeBattleSelection；public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 73处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 332. `scripts/systems/game_runtime/GameRuntimeBattleSelectionState.cs`

- 状态：**关注**
- 规模：70 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeBattleSelectionState；public 成员/声明约 12 处。
- 静态关注点：runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 333. `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`

- 状态：**关注**
- 规模：548 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeBattleWritebackService, BattleLocalWritebackResult, BattleLocalCandidateValidationResult。
- 静态关注点：serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 334. `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`

- 状态：**需跟进**
- 规模：487 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeCharacterInfoBuilder。
- 静态关注点：Godot Dictionary/Array boundary 22处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 17处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 335. `scripts/systems/game_runtime/GameRuntimeCommandLogger.cs`

- 状态：**需跟进**
- 规模：599 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeCommandLogger, CommandLogScope；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 19处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 23处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 336. `scripts/systems/game_runtime/GameRuntimeFacade.cs`

- 状态：**需跟进**
- 规模：3882 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeFacade, RuntimeCommandCode, RuntimeCommandResult；public 成员/声明约 96 处。
- 静态关注点：Godot Dictionary/Array boundary 10处；serialization/save/schema touchpoint 42处；runtime mutation/state touchpoint 131处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 337. `scripts/systems/game_runtime/GameRuntimePartyCommandHandler.cs`

- 状态：**关注**
- 规模：742 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimePartyCommandHandler；public 成员/声明约 9 处。
- 静态关注点：runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 338. `scripts/systems/game_runtime/GameRuntimePendingBattleGenerationRequest.cs`

- 状态：**关注**
- 规模：32 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimePendingBattleGenerationRequest；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 339. `scripts/systems/game_runtime/GameRuntimePendingSubmapPrompt.cs`

- 状态：**关注**
- 规模：63 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimePendingSubmapPrompt。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 340. `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`

- 状态：**关注**
- 规模：834 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeQuestCommandHandler, QuestCommandDefData, QuestProgressCommandPayloadData, QuestCommandDataReader；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 341. `scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs`

- 状态：**关注**
- 规模：779 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeRewardFlowHandler；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 342. `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`

- 状态：**需跟进**
- 规模：3740 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeSettlementCommandHandler, SettlementActionValidationResult, ContractBoardQuestData, SettlementServiceEntryResolution, StagecoachDestinationData, SettlementPersistResult；public 成员/声明约 27 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 9处；runtime mutation/state touchpoint 89处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 343. `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`

- 状态：**需跟进**
- 规模：977 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeSnapshotBuilder；public 成员/声明约 2 处。
- 静态关注点：Godot Dictionary/Array boundary 46处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 43处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 344. `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`

- 状态：**关注**
- 规模：806 行；扩展名 `.cs`
- 文件职责线索：类型：GameRuntimeWarehouseHandler, WarehouseTransactionSnapshot；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 345. `scripts/systems/game_runtime/IGameRuntimeSnapshotSource.cs`

- 状态：**关注**
- 规模：68 行；扩展名 `.cs`
- 文件职责线索：public 成员/声明约 1 处。
- 静态关注点：Godot Dictionary/Array boundary 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 346. `scripts/systems/game_runtime/RuntimeModalKind.cs`

- 状态：**通过**
- 规模：48 行；扩展名 `.cs`
- 文件职责线索：类型：RuntimeModalKind, RuntimeModalKinds；public 成员/声明约 1 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 347. `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`

- 状态：**关注**
- 规模：605 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapRuntimeProxy；public 成员/声明约 52 处。
- 静态关注点：serialization/save/schema touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 348. `scripts/systems/game_runtime/WorldMapSystem.cs`

- 状态：**需跟进**
- 规模：1075 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapSystem；public 成员/声明约 89 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；string/callable/node path coupling 27处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 4处；per-frame/async/lifecycle touchpoint 2处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 349. `scripts/systems/game_runtime/headless/GameTextCommandResult.cs`

- 状态：**关注**
- 规模：189 行；扩展名 `.cs`
- 文件职责线索：类型：GameTextCommandResult, AssertionEntry；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 36处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 350. `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`

- 状态：**需跟进**
- 规模：1389 行；扩展名 `.cs`
- 文件职责线索：类型：GameTextCommandRunner, IntParseResult, CoordParseResult, ExpectationResult, CommandOutcome；public 成员/声明约 22 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 10处；runtime mutation/state touchpoint 61处；diagnostic markers 3处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 351. `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`

- 状态：**需跟进**
- 规模：1449 行；扩展名 `.cs`
- 文件职责线索：类型：HeadlessGameTestSession, SessionCommandOutcome, BattleEquipmentInstanceSelection, ChangeEquipmentReportSummary；public 成员/声明约 19 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；string/callable/node path coupling 1处；serialization/save/schema touchpoint 19处；runtime mutation/state touchpoint 89处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 352. `scripts/systems/inventory/EquipmentDropService.cs`

- 状态：**关注**
- 规模：122 行；扩展名 `.cs`
- 文件职责线索：类型：EquipmentDropService；public 成员/声明约 7 处。
- 静态关注点：runtime mutation/state touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 353. `scripts/systems/inventory/PartyEquipmentService.cs`

- 状态：**关注**
- 规模：630 行；扩展名 `.cs`
- 文件职责线索：类型：PartyEquipmentService, EquipmentDisplacedEntry, EquipmentEquipPreviewResult, EquipmentActionResult, EquipmentViewEntry；public 成员/声明约 37 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 47处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 354. `scripts/systems/inventory/PartyItemUseService.cs`

- 状态：**关注**
- 规模：201 行；扩展名 `.cs`
- 文件职责线索：类型：PartyItemUseService, PartyItemUseOptions, PartyItemUseResult；public 成员/声明约 19 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 355. `scripts/systems/inventory/PartyWarehouseService.cs`

- 状态：**需跟进**
- 规模：1203 行；扩展名 `.cs`
- 文件职责线索：类型：PartyWarehouseService, WarehouseBatchItemEntry, WarehouseBatchSwapResult, WarehouseAddItemResult, WarehouseRemoveItemResult；public 成员/声明约 50 处。
- 静态关注点：Godot Dictionary/Array boundary 23处；serialization/save/schema touchpoint 5处；runtime mutation/state touchpoint 57处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 356. `scripts/systems/inventory/WarehouseInventoryEntry.cs`

- 状态：**关注**
- 规模：88 行；扩展名 `.cs`
- 文件职责线索：类型：WarehouseInventoryEntry；public 成员/声明约 20 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 357. `scripts/systems/persistence/FileIOCoordinator.cs`

- 状态：**关注**
- 规模：336 行；扩展名 `.cs`
- 文件职责线索：类型：FileIOCoordinator；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 358. `scripts/systems/persistence/GameLogService.cs`

- 状态：**关注**
- 规模：299 行；扩展名 `.cs`
- 文件职责线索：类型：GameLogService, GameLogEntry；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；string/callable/node path coupling 2处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 359. `scripts/systems/persistence/GameSession.cs`

- 状态：**需跟进**
- 规模：3323 行；扩展名 `.cs`
- 文件职责线索：类型：GameSession, ContentValidationDomainSnapshotData, ContentValidationSnapshotData；public 成员/声明约 123 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；resource/path load coupling 1处；serialization/save/schema touchpoint 554处；runtime mutation/state touchpoint 94处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 360. `scripts/systems/persistence/SaveSerializer.cs`

- 状态：**需跟进**
- 规模：1508 行；扩展名 `.cs`
- 文件职责线索：类型：SaveSerializer；public 成员/声明约 34 处。
- 静态关注点：Godot Dictionary/Array boundary 8处；serialization/save/schema touchpoint 236处；runtime mutation/state touchpoint 44处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 361. `scripts/systems/progression/AgeStageResolution.cs`

- 状态：**通过**
- 规模：19 行；扩展名 `.cs`
- 文件职责线索：类型：AgeStageResolution；public 成员/声明约 5 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 362. `scripts/systems/progression/AgeStageResolver.cs`

- 状态：**关注**
- 规模：188 行；扩展名 `.cs`
- 文件职责线索：类型：AgeStageResolver, StageCandidate；public 成员/声明约 5 处。
- 静态关注点：runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 363. `scripts/systems/progression/AscensionApplyService.cs`

- 状态：**关注**
- 规模：110 行；扩展名 `.cs`
- 文件职责线索：类型：AscensionApplyService；public 成员/声明约 5 处。
- 静态关注点：runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 364. `scripts/systems/progression/AttributeGrowthResult.cs`

- 状态：**关注**
- 规模：89 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeGrowthResult；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 365. `scripts/systems/progression/AttributeGrowthService.cs`

- 状态：**通过**
- 规模：79 行；扩展名 `.cs`
- 文件职责线索：类型：AttributeGrowthService；public 成员/声明约 6 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 366. `scripts/systems/progression/BloodlineApplyService.cs`

- 状态：**关注**
- 规模：63 行；扩展名 `.cs`
- 文件职责线索：类型：BloodlineApplyService；public 成员/声明约 4 处。
- 静态关注点：runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 367. `scripts/systems/progression/CharacterAttributeChangeFact.cs`

- 状态：**关注**
- 规模：146 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterAttributeChangeFact；public 成员/声明约 15 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 368. `scripts/systems/progression/CharacterCreationIdentityOptionService.cs`

- 状态：**关注**
- 规模：269 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterCreationIdentityOptionService；public 成员/声明约 6 处。
- 静态关注点：runtime mutation/state touchpoint 50处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 369. `scripts/systems/progression/CharacterCreationService.cs`

- 状态：**需跟进**
- 规模：446 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterCreationOptions, CharacterCreationService, ProgressionContentSourceRef；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 17处；runtime mutation/state touchpoint 1处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 370. `scripts/systems/progression/CharacterKnowledgeChangeFact.cs`

- 状态：**关注**
- 规模：67 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterKnowledgeChangeFact；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 371. `scripts/systems/progression/CharacterManagementModule.cs`

- 状态：**需跟进**
- 规模：4298 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterManagementModule, LearnSkillOptionsData, AttributeGrowthEntryData, AchievementProgressSummaryEntry, DailyPracticeGrowthResult, PendingCharacterRewardEntryData；public 成员/声明约 155 处。
- 静态关注点：Godot Dictionary/Array boundary 33处；serialization/save/schema touchpoint 16处；runtime mutation/state touchpoint 205处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 372. `scripts/systems/progression/CharacterMasteryChangeFact.cs`

- 状态：**关注**
- 规模：89 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterMasteryChangeFact；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 373. `scripts/systems/progression/CharacterProgressionDelta.cs`

- 状态：**需跟进**
- 规模：523 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterProgressionDelta；public 成员/声明约 34 处。
- 静态关注点：Godot Dictionary/Array boundary 14处；serialization/save/schema touchpoint 6处；runtime mutation/state touchpoint 53处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 374. `scripts/systems/progression/FaithService.cs`

- 状态：**关注**
- 规模：485 行；扩展名 `.cs`
- 文件职责线索：类型：FaithDevotionResult, FaithService；public 成员/声明约 21 处。
- 静态关注点：resource/path load coupling 2处；runtime mutation/state touchpoint 23处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 375. `scripts/systems/progression/IdentityPayloadValidator.cs`

- 状态：**关注**
- 规模：426 行；扩展名 `.cs`
- 文件职责线索：类型：IdentityPayloadValidator；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 38处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 376. `scripts/systems/progression/LevelGrowthEvaluationService.cs`

- 状态：**关注**
- 规模：134 行；扩展名 `.cs`
- 文件职责线索：类型：LevelGrowthEvaluationService；public 成员/声明约 7 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 377. `scripts/systems/progression/LevelGrowthTriggerResult.cs`

- 状态：**关注**
- 规模：64 行；扩展名 `.cs`
- 文件职责线索：类型：LevelGrowthTriggerResult；public 成员/声明约 11 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 378. `scripts/systems/progression/MisfortuneBlackOmenService.cs`

- 状态：**关注**
- 规模：338 行；扩展名 `.cs`
- 文件职责线索：类型：MisfortuneBlackOmenHookPayload, MisfortuneBlackOmenResult, MisfortuneBlackOmenHookKind, MisfortuneBlackOmenService；public 成员/声明约 24 处。
- 静态关注点：runtime mutation/state touchpoint 8处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 379. `scripts/systems/progression/PassiveSourceContext.cs`

- 状态：**通过**
- 规模：14 行；扩展名 `.cs`
- 文件职责线索：类型：PassiveSourceContext；public 成员/声明约 9 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 380. `scripts/systems/progression/PendingCharacterReward.cs`

- 状态：**需跟进**
- 规模：192 行；扩展名 `.cs`
- 文件职责线索：类型：PendingCharacterReward；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 11处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 5处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 381. `scripts/systems/progression/PendingCharacterRewardEntry.cs`

- 状态：**关注**
- 规模：139 行；扩展名 `.cs`
- 文件职责线索：类型：PendingCharacterRewardEntry；public 成员/声明约 10 处。
- 静态关注点：Godot Dictionary/Array boundary 7处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 382. `scripts/systems/progression/PracticeGrowthService.cs`

- 状态：**关注**
- 规模：491 行；扩展名 `.cs`
- 文件职责线索：类型：PracticeTrackKind, PracticeGrowthService；public 成员/声明约 16 处。
- 静态关注点：runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 383. `scripts/systems/progression/PracticeSkillLearnStatus.cs`

- 状态：**关注**
- 规模：91 行；扩展名 `.cs`
- 文件职责线索：类型：PracticeSkillLearnStatus；public 成员/声明约 13 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 384. `scripts/systems/progression/ProfessionAssignmentService.cs`

- 状态：**关注**
- 规模：293 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionAssignmentService；public 成员/声明约 9 处。
- 静态关注点：runtime mutation/state touchpoint 18处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 385. `scripts/systems/progression/ProfessionRuleService.cs`

- 状态：**关注**
- 规模：709 行；扩展名 `.cs`
- 文件职责线索：类型：ProfessionRuleService；public 成员/声明约 13 处。
- 静态关注点：runtime mutation/state touchpoint 32处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 386. `scripts/systems/progression/ProgressionIdentityCatalogData.cs`

- 状态：**关注**
- 规模：58 行；扩展名 `.cs`
- 文件职责线索：类型：ProgressionIdentityCatalogData；public 成员/声明约 11 处。
- 静态关注点：runtime mutation/state touchpoint 39处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 387. `scripts/systems/progression/ProgressionService.cs`

- 状态：**需跟进**
- 规模：1604 行；扩展名 `.cs`
- 文件职责线索：类型：ProgressionService；public 成员/声明约 17 处。
- 静态关注点：Godot Dictionary/Array boundary 25处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 40处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 388. `scripts/systems/progression/QuestCommandResultData.cs`

- 状态：**关注**
- 规模：290 行；扩展名 `.cs`
- 文件职责线索：类型：QuestSubmitItemResultData, QuestClaimResultData；public 成员/声明约 19 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 37处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 389. `scripts/systems/progression/QuestProgressService.cs`

- 状态：**需跟进**
- 规模：1004 行；扩展名 `.cs`
- 文件职责线索：类型：QuestProgressEventKind, QuestProgressService, QuestActiveObjectiveMatch, QuestProgressEventData, QuestObjectiveDefData, QuestProgressDataReader；public 成员/声明约 63 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；serialization/save/schema touchpoint 9处；runtime mutation/state touchpoint 56处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 390. `scripts/systems/progression/RacialSkillGrantService.cs`

- 状态：**关注**
- 规模：280 行；扩展名 `.cs`
- 文件职责线索：类型：RacialSkillGrantService, RacialGrantEntry；public 成员/声明约 7 处。
- 静态关注点：runtime mutation/state touchpoint 22处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 391. `scripts/systems/progression/SkillEffectiveMaxLevelRules.cs`

- 状态：**通过**
- 规模：127 行；扩展名 `.cs`
- 文件职责线索：类型：SkillEffectiveMaxLevelRules；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 392. `scripts/systems/progression/SkillLevelDescriptionFormatter.cs`

- 状态：**需跟进**
- 规模：495 行；扩展名 `.cs`
- 文件职责线索：类型：SkillLevelDescriptionFormatter；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 17处；runtime mutation/state touchpoint 31处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 393. `scripts/systems/progression/SkillMergeService.cs`

- 状态：**关注**
- 规模：463 行；扩展名 `.cs`
- 文件职责线索：类型：SkillMergeService；public 成员/声明约 5 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 12处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 394. `scripts/systems/progression/StageAdvancementApplyService.cs`

- 状态：**关注**
- 规模：72 行；扩展名 `.cs`
- 文件职责线索：类型：StageAdvancementApplyService；public 成员/声明约 4 处。
- 静态关注点：runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 395. `scripts/systems/settlement/SettlementForgeService.cs`

- 状态：**关注**
- 规模：879 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementForgeService, RecipeItemValidationResult；public 成员/声明约 9 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 48处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 396. `scripts/systems/settlement/SettlementPanelKind.cs`

- 状态：**通过**
- 规模：47 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementPanelKind, SettlementPanelKinds。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 397. `scripts/systems/settlement/SettlementResearchService.cs`

- 状态：**关注**
- 规模：641 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementResearchService, ResearchMemberAvailability；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 19处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 398. `scripts/systems/settlement/SettlementServiceMetadata.cs`

- 状态：**关注**
- 规模：63 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementServiceMetadata；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 1处；runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 399. `scripts/systems/settlement/SettlementServiceResult.cs`

- 状态：**关注**
- 规模：171 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementServiceResult；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 29处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 400. `scripts/systems/settlement/SettlementShopService.cs`

- 状态：**需跟进**
- 规模：1115 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementShopService, ShopItemId, struct, ShopDefinition, ShopStockEntry；public 成员/声明约 5 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 22处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 401. `scripts/systems/settlement/SettlementShopTradeResult.cs`

- 状态：**通过**
- 规模：27 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementShopTradeResult；public 成员/声明约 8 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 402. `scripts/systems/settlement/SettlementSubmissionSource.cs`

- 状态：**通过**
- 规模：62 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementSubmissionSource, SettlementSubmissionSources。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 403. `scripts/systems/world/EncounterAnchorData.cs`

- 状态：**关注**
- 规模：245 行；扩展名 `.cs`
- 文件职责线索：类型：EncounterAnchorKind, EncounterAnchorData；public 成员/声明约 15 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；serialization/save/schema touchpoint 7处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 404. `scripts/systems/world/EncounterRosterBuilder.cs`

- 状态：**需跟进**
- 规模：1441 行；扩展名 `.cs`
- 文件职责线索：类型：EncounterRosterBuilder, ParsedDropDefinition, PreviewLootEntryData, EncounterBuildContextData；public 成员/声明约 25 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 84处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 405. `scripts/systems/world/WildEncounterGrowthSystem.cs`

- 状态：**关注**
- 规模：107 行；扩展名 `.cs`
- 文件职责线索：类型：WildEncounterGrowthSystem；public 成员/声明约 3 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 406. `scripts/systems/world/WorldMapDataContext.cs`

- 状态：**需跟进**
- 规模：1263 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapDataContext, WorldMapContextSyncResult, WorldMapSubmapEnterResult, WorldMapSubmapReturnResult, WorldMapSubmapReturnStackEntry, WorldMapMountedSubmapData；public 成员/声明约 100 处。
- 静态关注点：Godot Dictionary/Array boundary 20处；resource/path load coupling 1处；serialization/save/schema touchpoint 32处；runtime mutation/state touchpoint 39处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 407. `scripts/systems/world/WorldMapFogFactionState.cs`

- 状态：**关注**
- 规模：36 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapFogFactionState；public 成员/声明约 7 处。
- 静态关注点：runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 408. `scripts/systems/world/WorldMapFogSystem.cs`

- 状态：**关注**
- 规模：409 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapFogStateKind, WorldMapFogSystem, CoordParseResult；public 成员/声明约 14 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 3处；runtime mutation/state touchpoint 39处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 409. `scripts/systems/world/WorldMapFootprintState.cs`

- 状态：**通过**
- 规模：13 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapFootprintState；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 410. `scripts/systems/world/WorldMapGridSystem.cs`

- 状态：**关注**
- 规模：217 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapGridSystem；public 成员/声明约 13 处。
- 静态关注点：runtime mutation/state touchpoint 9处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 411. `scripts/systems/world/WorldMapOccupantState.cs`

- 状态：**通过**
- 规模：13 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapOccupantState；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 412. `scripts/systems/world/WorldMapSpawnSystem.cs`

- 状态：**需跟进**
- 规模：2042 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapSpawnSystem, WorldBuildData, SettlementInstanceData, FacilityInstanceData, ServiceNpcInstanceData, ServiceEntryData；public 成员/声明约 98 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；resource/path load coupling 10处；serialization/save/schema touchpoint 17处；runtime mutation/state touchpoint 158处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 413. `scripts/systems/world/WorldTimeSystem.cs`

- 状态：**通过**
- 规模：82 行；扩展名 `.cs`
- 文件职责线索：类型：WorldTimeSystem, WorldTimeAdvanceResult；public 成员/声明约 13 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 414. `scripts/tools/tile_baker.gd`

- 状态：**关注**
- 规模：275 行；扩展名 `.gd`
- 文件职责线索：脚本/辅助定义文件。
- 静态关注点：resource/path load coupling 5处；serialization/save/schema touchpoint 1处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 415. `scripts/tools/tree_baker.gd`

- 状态：**关注**
- 规模：157 行；扩展名 `.gd`
- 文件职责线索：脚本/辅助定义文件。
- 静态关注点：resource/path load coupling 5处；serialization/save/schema touchpoint 1处；per-frame/async/lifecycle touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 416. `scripts/ui/BattleBoard2D.cs`

- 状态：**关注**
- 规模：681 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBoard2D；public 成员/声明约 52 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；string/callable/node path coupling 11处；runtime mutation/state touchpoint 15处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 417. `scripts/ui/BattleBoardController.cs`

- 状态：**需跟进**
- 规模：1898 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBoardController；public 成员/声明约 44 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；resource/path load coupling 4处；runtime mutation/state touchpoint 45处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 418. `scripts/ui/BattleBoardProp.cs`

- 状态：**关注**
- 规模：174 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBoardProp；public 成员/声明约 7 处；导出字段 1 处。
- 静态关注点：string/callable/node path coupling 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 419. `scripts/ui/BattleBoardRenderProfile.cs`

- 状态：**关注**
- 规模：434 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBoardRenderProfile；public 成员/声明约 57 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；resource/path load coupling 1处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 420. `scripts/ui/BattleHoverPreviewOverlay.cs`

- 状态：**关注**
- 规模：425 行；扩展名 `.cs`
- 文件职责线索：类型：BattleHoverPreviewOverlay；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；runtime mutation/state touchpoint 1处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 421. `scripts/ui/BattleMapPanel.cs`

- 状态：**需跟进**
- 规模：2610 行；扩展名 `.cs`
- 文件职责线索：类型：BattleMapPanel；public 成员/声明约 78 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；string/callable/node path coupling 55处；resource/path load coupling 4处；runtime mutation/state touchpoint 52处；per-frame/async/lifecycle touchpoint 11处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 422. `scripts/ui/BattleSkillSlotButton.cs`

- 状态：**关注**
- 规模：147 行；扩展名 `.cs`
- 文件职责线索：类型：BattleSkillSlotButton；public 成员/声明约 8 处。
- 静态关注点：runtime mutation/state touchpoint 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 423. `scripts/ui/BattleUiTheme.cs`

- 状态：**通过**
- 规模：155 行；扩展名 `.cs`
- 文件职责线索：类型：BattleUiTheme；public 成员/声明约 52 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 424. `scripts/ui/CharacterCreationWindow.cs`

- 状态：**需跟进**
- 规模：1699 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterCreationWindow, AttributeModifierTotals, CharacterCreationWindowNodeExtensions；public 成员/声明约 80 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；string/callable/node path coupling 42处；runtime mutation/state touchpoint 70处；per-frame/async/lifecycle touchpoint 6处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 425. `scripts/ui/CharacterInfoWindow.cs`

- 状态：**关注**
- 规模：613 行；扩展名 `.cs`
- 文件职责线索：类型：CharacterInfoWindow, EntryKind, CharacterInfoPayload, CharacterInfoSection, CharacterInfoEntry；public 成员/声明约 27 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；string/callable/node path coupling 10处；runtime mutation/state touchpoint 37处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 426. `scripts/ui/DisplaySettingsWindow.cs`

- 状态：**关注**
- 规模：194 行；扩展名 `.cs`
- 文件职责线索：类型：DisplaySettingsWindow；public 成员/声明约 9 处。
- 静态关注点：string/callable/node path coupling 11处；runtime mutation/state touchpoint 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 427. `scripts/ui/LoginScreen.cs`

- 状态：**需跟进**
- 规模：440 行；扩展名 `.cs`
- 文件职责线索：类型：LoginScreen；public 成员/声明约 46 处。
- 静态关注点：Godot Dictionary/Array boundary 5处；string/callable/node path coupling 10处；serialization/save/schema touchpoint 35处；runtime mutation/state touchpoint 2处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 428. `scripts/ui/MasteryRewardWindow.cs`

- 状态：**关注**
- 规模：168 行；扩展名 `.cs`
- 文件职责线索：类型：MasteryRewardWindow；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；string/callable/node path coupling 7处；runtime mutation/state touchpoint 10处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 429. `scripts/ui/PartyManagementWindow.cs`

- 状态：**需跟进**
- 规模：1375 行；扩展名 `.cs`
- 文件职责线索：类型：PartyManagementWindow, struct；public 成员/声明约 54 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；string/callable/node path coupling 32处；runtime mutation/state touchpoint 149处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 430. `scripts/ui/PartyMemberOptionUtils.cs`

- 状态：**关注**
- 规模：259 行；扩展名 `.cs`
- 文件职责线索：类型：PartyMemberOptionUtils；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 4处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 431. `scripts/ui/PartyWarehouseWindow.cs`

- 状态：**需跟进**
- 规模：719 行；扩展名 `.cs`
- 文件职责线索：类型：PartyWarehouseWindow, WarehouseWindowData, WarehouseEntry, struct；public 成员/声明约 52 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；string/callable/node path coupling 22处；resource/path load coupling 1处；runtime mutation/state touchpoint 21处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 432. `scripts/ui/PromotionChoiceWindow.cs`

- 状态：**关注**
- 规模：432 行；扩展名 `.cs`
- 文件职责线索：类型：PromotionChoiceWindow；public 成员/声明约 6 处。
- 静态关注点：Godot Dictionary/Array boundary 6处；string/callable/node path coupling 11处；runtime mutation/state touchpoint 16处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 433. `scripts/ui/RuntimeLogDock.cs`

- 状态：**关注**
- 规模：502 行；扩展名 `.cs`
- 文件职责线索：类型：RuntimeLogDock, struct；public 成员/声明约 19 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；string/callable/node path coupling 11处；runtime mutation/state touchpoint 18处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 434. `scripts/ui/SaveListWindow.cs`

- 状态：**关注**
- 规模：102 行；扩展名 `.cs`
- 文件职责线索：类型：SaveListWindow；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；string/callable/node path coupling 2处；serialization/save/schema touchpoint 5处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 435. `scripts/ui/SettlementWindow.cs`

- 状态：**需跟进**
- 规模：1101 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementWindow, SettlementWindowData, struct, struct, struct, ServiceEntry；public 成员/声明约 78 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；string/callable/node path coupling 17处；runtime mutation/state touchpoint 57处；per-frame/async/lifecycle touchpoint 1处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 436. `scripts/ui/ShopWindow.cs`

- 状态：**需跟进**
- 规模：992 行；扩展名 `.cs`
- 文件职责线索：类型：ShopWindow, ShopWindowData, ShopEntry, MemberOption；public 成员/声明约 80 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；string/callable/node path coupling 23处；runtime mutation/state touchpoint 38处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 437. `scripts/ui/SubmapEntryWindow.cs`

- 状态：**关注**
- 规模：318 行；扩展名 `.cs`
- 文件职责线索：类型：SubmapEntryWindow；public 成员/声明约 16 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；string/callable/node path coupling 15处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 438. `scripts/ui/WorldMapView.cs`

- 状态：**关注**
- 规模：577 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapView；public 成员/声明约 33 处；导出字段 18 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；string/callable/node path coupling 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 439. `scripts/ui/WorldPresetPickerWindow.cs`

- 状态：**关注**
- 规模：56 行；扩展名 `.cs`
- 文件职责线索：类型：WorldPresetPickerWindow；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；string/callable/node path coupling 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 440. `scripts/ui/components/SelectableListWindow.cs`

- 状态：**关注**
- 规模：272 行；扩展名 `.cs`
- 文件职责线索：类型：SelectableListWindow；public 成员/声明约 7 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；string/callable/node path coupling 7处；runtime mutation/state touchpoint 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 441. `scripts/ui/components/SelectionCardBuilder.cs`

- 状态：**关注**
- 规模：123 行；扩展名 `.cs`
- 文件职责线索：类型：SelectionCardBuilder；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 442. `scripts/utils/BattleBoardPropCatalog.cs`

- 状态：**通过**
- 规模：66 行；扩展名 `.cs`
- 文件职责线索：类型：BattleBoardPropKind, BattleBoardPropCatalog；public 成员/声明约 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 443. `scripts/utils/DisplaySettingsService.cs`

- 状态：**关注**
- 规模：149 行；扩展名 `.cs`
- 文件职责线索：类型：DisplaySettingsService, struct, struct；public 成员/声明约 14 处。
- 静态关注点：serialization/save/schema touchpoint 2处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 444. `scripts/utils/FacilityConfig.cs`

- 状态：**关注**
- 规模：44 行；扩展名 `.cs`
- 文件职责线索：类型：FacilityConfig；public 成员/声明约 10 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 445. `scripts/utils/FacilityNpcConfig.cs`

- 状态：**通过**
- 规模：25 行；扩展名 `.cs`
- 文件职责线索：类型：FacilityNpcConfig；public 成员/声明约 7 处；导出字段 5 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 446. `scripts/utils/FacilitySlotConfig.cs`

- 状态：**通过**
- 规模：17 行；扩展名 `.cs`
- 文件职责线索：类型：FacilitySlotConfig；public 成员/声明约 5 处；导出字段 4 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 447. `scripts/utils/GameLog.cs`

- 状态：**关注**
- 规模：143 行；扩展名 `.cs`
- 文件职责线索：类型：GameLogLevel, GameLog；public 成员/声明约 12 处。
- 静态关注点：Godot Dictionary/Array boundary 1处；runtime mutation/state touchpoint 4处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 448. `scripts/utils/GameLogSinks.cs`

- 状态：**关注**
- 规模：102 行；扩展名 `.cs`
- 文件职责线索：类型：ConsoleLogSink, GodotEditorLogSink, GameSessionLogSink；public 成员/声明约 8 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 449. `scripts/utils/GameTextSnapshotRenderer.cs`

- 状态：**需跟进**
- 规模：1076 行；扩展名 `.cs`
- 文件职责线索：类型：GameTextSnapshotRenderer；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 2处；serialization/save/schema touchpoint 13处；runtime mutation/state touchpoint 132处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 450. `scripts/utils/GodotVariantReadExtensions.cs`

- 状态：**关注**
- 规模：126 行；扩展名 `.cs`
- 文件职责线索：类型：GodotVariantReadExtensions。
- 静态关注点：Godot Dictionary/Array boundary 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 451. `scripts/utils/MountedSubmapConfig.cs`

- 状态：**通过**
- 规模：17 行；扩展名 `.cs`
- 文件职责线索：类型：MountedSubmapConfig；public 成员/声明约 5 处；导出字段 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 452. `scripts/utils/SettlementConfig.cs`

- 状态：**关注**
- 规模：70 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementConfig, SettlementTier；public 成员/声明约 12 处；导出字段 7 处。
- 静态关注点：Godot Dictionary/Array boundary 3处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 453. `scripts/utils/SettlementDistributionRule.cs`

- 状态：**通过**
- 规模：19 行；扩展名 `.cs`
- 文件职责线索：类型：SettlementDistributionRule；public 成员/声明约 5 处；导出字段 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 454. `scripts/utils/TrueRandomSeedService.cs`

- 状态：**通过**
- 规模：70 行；扩展名 `.cs`
- 文件职责线索：类型：TrueRandomSeedService；public 成员/声明约 3 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 455. `scripts/utils/VisionSourceData.cs`

- 状态：**通过**
- 规模：24 行；扩展名 `.cs`
- 文件职责线索：类型：VisionSourceData；public 成员/声明约 7 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 456. `scripts/utils/WeightedFacilityEntry.cs`

- 状态：**通过**
- 规模：16 行；扩展名 `.cs`
- 文件职责线索：类型：WeightedFacilityEntry；public 成员/声明约 4 处；导出字段 2 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 457. `scripts/utils/WildSpawnRule.cs`

- 状态：**通过**
- 规模：30 行；扩展名 `.cs`
- 文件职责线索：类型：WildSpawnRule；public 成员/声明约 9 处；导出字段 8 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 458. `scripts/utils/WorldEventConfig.cs`

- 状态：**通过**
- 规模：43 行；扩展名 `.cs`
- 文件职责线索：类型：WorldEventTypeKind, WorldEventConfig；public 成员/声明约 9 处；导出字段 7 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 459. `scripts/utils/WorldMapCellData.cs`

- 状态：**通过**
- 规模：17 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapCellData；public 成员/声明约 7 处。
- 静态关注点：未命中本次高风险静态模式。
- 审查结论：未发现需要立即修复的静态问题；后续仅在相关功能改动时按上下文单元复核。

### 460. `scripts/utils/WorldMapContentValidator.cs`

- 状态：**需跟进**
- 规模：993 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapContentValidator；public 成员/声明约 3 处。
- 静态关注点：Godot Dictionary/Array boundary 9处；resource/path load coupling 8处；runtime mutation/state touchpoint 132处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。

### 461. `scripts/utils/WorldMapGenerationConfig.cs`

- 状态：**关注**
- 规模：168 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapGenerationConfig；public 成员/声明约 33 处；导出字段 29 处。
- 静态关注点：Godot Dictionary/Array boundary 6处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 462. `scripts/utils/WorldMapSettlementBundle.cs`

- 状态：**关注**
- 规模：11 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapSettlementBundle；public 成员/声明约 3 处；导出字段 2 处。
- 静态关注点：Godot Dictionary/Array boundary 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 463. `scripts/utils/WorldMapSettlementNamePool.cs`

- 状态：**关注**
- 规模：25 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapSettlementNamePool；public 成员/声明约 3 处；导出字段 1 处。
- 静态关注点：runtime mutation/state touchpoint 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 464. `scripts/utils/WorldMapWildSpawnBundle.cs`

- 状态：**关注**
- 规模：8 行；扩展名 `.cs`
- 文件职责线索：类型：WorldMapWildSpawnBundle；public 成员/声明约 2 处；导出字段 1 处。
- 静态关注点：Godot Dictionary/Array boundary 1处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 465. `scripts/utils/WorldPresetRegistry.cs`

- 状态：**关注**
- 规模：167 行；扩展名 `.cs`
- 文件职责线索：类型：WorldPresetRegistry, WorldPresetInfo；public 成员/声明约 4 处。
- 静态关注点：Godot Dictionary/Array boundary 3处；resource/path load coupling 5处；serialization/save/schema touchpoint 4处；runtime mutation/state touchpoint 2处。
- 审查结论：存在运行时/资源/状态耦合点；当前仅建议在触及该功能时配套窄回归验证。

### 466. `scripts/utils/generate_canyon_tiles.py`

- 状态：**需跟进**
- 规模：484 行；扩展名 `.py`
- 文件职责线索：类型：Canvas。
- 静态关注点：serialization/save/schema touchpoint 13处。
- 审查结论：包含高风险边界或维护热点；建议后续按对应上下文单元做人工复核和 headless/场景验证。


## 建议的后续验证

- `dotnet build magic.csproj`：确认 C# 项目整体编译状态。
- `python tests/run_regression_suite.py`：运行常规回归；按仓库说明不默认加入 battle simulation/balance simulation。
- 对标记“需跟进”的 UI 文件，结合对应 `.tscn` 做节点路径和信号验证。
- 对标记“需跟进”的存档/内容注册文件，任何修复前先确认是否需要 schema 兼容，避免违反兼容性策略。
