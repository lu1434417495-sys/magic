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

[高/已修复] `scripts/ui/PromotionChoiceWindow.cs:241` - 原实现的晋升确认信号在 `HideWindow()` 之后发出，而 `HideWindow()` 会清空 `_memberId`，导致 `WorldMapSystem._on_promotion_choice_submitted()` 收到空 `member_id`。本轮已把 `_memberId` 缓存为局部 `memberId` 后再关闭窗口并发信号，同时在 `tests/world_map/ui/run_promotion_choice_window_schema_regression.cs` 增加 confirm signal payload 回归，覆盖 `hero/warrior/selection`。

[中/已修复] `scripts/ui/PromotionChoiceWindow.cs:209` - 原 `_detailsLabel.Text` 直接拼入 `display_name`、`description`、`selection_hint`，而 scene 开启了 BBCode。本轮已对职业名、描述、说明和技能 id 文本做 BBCode 转义，只保留代码自身插入的受控颜色/粗体/斜体标签，并新增回归覆盖内容字段注入 `[b]`/`[color]`/`[url]` 的情况。

[中/已修复] `scripts/systems/battle/ai/BattleAiService.cs:71` - 原实现的 AI trace `Enter("choose:impl")` / `Exit("choose:impl")` 未用 `try/finally` 包住；`ChooseCommandImpl()` 或 mutation guard capture/validate 抛错会跳过 `Exit`。本轮已逐段加上 `try/finally`，确保 no-guard、mutation capture、choose impl、mutation validate 四个 span 都能退出。

[中/已修复] `scripts/systems/battle/ai/BattleAiScoreService.cs:237` - 原实现的 `BuildSkillScoreInput()` 多段 trace span 不是 exception-safe。此轮新增 `WithTraceSpan` helper，并把 metadata、filter_effects、ground_control、resource_cost、position、post_threat_projection 以及外层 build_skill_score_input 包成 guaranteed-exit span，避免异常后污染 AI trace 栈。

[中/已修复] `scripts/dev_tools/AiTraceRecorder.cs:130` - 原 trace 栈帧把 `Time.GetTicksUsec()` 的 `ulong` 和 `(ulong)0` 存入 `Godot.Collections.Dictionary`，再通过 `long`/`ulong` 混合强转取回。此轮已把 recorder 内部 timestamp、`t_enter`、`child_usec` 统一为 `long`，写入 Variant 前完成转换，并用 `GetTicksUsec()` 对 `ulong` 边界做 clamp。

[中/已修复] `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs:123` - 原“丢弃全部”会先计算同类总数，但装备分支实际只删除 `instanceId` 指定的单个装备实例，容易误导调用方。本轮已在 command 层明确拒绝无 `instance_id` 的装备批量丢弃，并将装备实例成功/回滚文案改为“丢弃装备实例”，不再伪装成“全部同类装备”。

[中/已修复] `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs:164` - 原 `RollDamageDieVirtual()` 通过 `Call("_roll_damage_die", diceSides)` 字符串动态调用已有虚方法，绕过 C# 编译期检查。本轮已改为直接调用 `_roll_damage_die(diceSides)`，保留 C# override 测试替身能力，同时移除 GodotObject.Call 字符串风险。

[低/已修复] `scripts/ui/PartyWarehouseWindow.cs:366` - 仓库详情原先对任意非空 `icon_path` 直接 `GD.Load<Texture2D>()`。内容路径拼错或指向非 Texture2D 时，每次选择条目都会产生 Godot load error/log noise；如果仓库列表频繁刷新，还会重复触发失败加载。本轮已在 UI 层增加 `ResourceLoader.Exists(icon_path, "Texture2D")` 守卫和失败路径缓存，失败时返回空贴图并只记录一次警告；内容注册阶段仍建议继续做资源路径校验。

[中/已修复] `scripts/systems/persistence/SaveSerializer.cs:1006` - `NormalizeEncounterAnchors()` 原先在判断 `Variant.Type.Dictionary` 前先对每个数组元素调用 `AsGodotObject()`；如果 Godot C# 对非 Object Variant 的转换不是安全 null，而是抛出绑定异常，字典形态的 `encounter_anchors` 会在 normalize/load 路径崩溃。本轮已改为先分支处理 Dictionary，再仅对 `Variant.Type.Object` 调用 `AsGodotObject()`；同样收紧 `SerializeObjectOrDictionaryArray()`，避免对非 Object Variant 做对象转换。

[中/已修复] `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs:1411` - headless snapshot 的 `ReadEncounterAnchorsTyped()` 原先只尝试把 `Variant` 转成 `EncounterAnchorData` 对象，既会对非 Object Variant 调用 `AsGodotObject()`，也会漏掉正式 save/world payload 中常见的 Dictionary 形态 anchor，导致 headless 快照/断言可能看不到已存在的 encounter anchors。本轮已先解析 Dictionary，再仅对 Object Variant 做对象转换。

[低/已加固] `scripts/ui/PartyWarehouseWindow.cs:68` / `scenes/ui/party_warehouse_window.tscn:152` - 仓库详情节点是 `RichTextLabel`，当前场景配置 `bbcode_enabled = false`，所以这轮没有复现 PromotionChoiceWindow 那种 BBCode 注入；但该安全性原本依赖 scene 属性不漂移。本轮在脚本 `_Ready()` 里显式设置 `details_label.BbcodeEnabled = false`，并补回归覆盖原始 `[b]` / `[url]` 文本按纯文本保留。

[低/已修复] `tests/world_map/ui/run_promotion_choice_window_schema_regression.cs:34` - 晋升窗口回归原先只覆盖 payload schema 和卡片渲染，没有覆盖确认按钮、信号载荷、BBCode 文本边界。本轮已补 `TestPromotionChoiceWindowSubmitPreservesMemberId()` 和 `TestPromotionChoiceWindowEscapesBbcodeContent()`，分别覆盖 `_memberId` 清空回归与详情 BBCode 注入边界。

Open questions / assumptions:

- `AiTraceRecorder` 默认未启用，相关 trace 问题主要影响 benchmark、battle simulation、AI profiling 和异常后的诊断可信度；如果团队不把 trace dump 作为 CI gate，严重度可下调，但仍应修复以免性能排查时被工具自身误导。
- 仓库“丢弃全部装备”的正确产品语义需要确认：是禁止批量删除装备，还是删除所有同类实例。当前实现和命令名不一致。

Residual risks / test gaps:

- 当前容器缺少 `godot` 与 `dotnet`，无法运行 `dotnet build magic.csproj`、`python tests/run_regression_suite.py` 或针对 UI/AI 的 headless runner，因此以上发现是源码级复审结论，尚未 runtime 复现。
- 战斗 preview-vs-execution、一整套 battle AI action evaluator、save schema 细节数量很大；本轮优先抓了跨文件高风险链路，没有声称覆盖所有数值平衡或 AI 决策质量问题。
- 旧的逐文件索引仍保留在后文，用于定位文件和静态热点；真正应优先处理的是本 findings-first 节列出的具体问题。


## 本轮真实深度检视与修复记录

- 不再把生成式逐文件矩阵伪装成人工深审结论；本轮实际深挖了 `PromotionChoiceWindow` 的确认链路，从 `ShowPromotion()` 写入 `_memberId`、`HideWindow()` 清状态、`_on_confirm_button_pressed()` 发信号、到 `WorldMapSystem._on_promotion_choice_submitted()` 接收参数，确认并修复了会导致晋升提交空角色 id 的实 bug。
- 修复方式：确认按钮路径先缓存 `memberId`，再调用 `HideWindow()`，最后用缓存值发出 `choice_submitted`。
- 回归方式：扩展 `run_promotion_choice_window_schema_regression.cs`，实例化真实 `.tscn`，连接 `choice_submitted`，触发 ConfirmButton 的 `Pressed` 信号，断言收到 `hero`、`warrior` 和非空 selection，并确认窗口关闭。
- 继续深挖 `PromotionChoiceWindow` 的详情渲染路径，转义内容字段中的 BBCode，并补充详情文本不接受原始 `[b]`/`[color]`/`[url]` 注入的 UI 回归。
- 继续深挖了 `BattleAiService` 与 `BattleAiScoreService` 的 AI trace 生命周期：逐行检查 Enter/Exit 周围的业务调用，修复异常时 trace 栈不退出的问题。
- `BattleAiService` 现在对 no-guard choose、mutation guard capture、guarded choose、mutation validate 四段 trace 都使用 `try/finally` 保护。
- `BattleAiScoreService` 新增 `WithTraceSpan` helper，将 score input 构建中的子 span 和外层 `build_skill_score_input` 全部改成 guaranteed-exit。
- 继续深挖 `AiTraceRecorder` 自身，移除写入 Godot Dictionary 的 `ulong` trace 时间值，避免诊断工具在 Variant 整数边界上产生类型转换风险。
- 继续深挖 `GameRuntimeWarehouseHandler.CommandDiscardAllTyped()` 的装备分支，在不擅自改变批量删除语义的前提下，明确禁止无实例 id 的装备批量丢弃，并修正文案。
- 继续深挖 `BattleDamageResolver.Dice` 的伤害骰入口，把字符串 `Call("_roll_damage_die")` 改为直接虚方法调用，避免运行时字符串分派风险。
- 继续深挖 `PartyWarehouseWindow` 仓库详情图标加载路径，增加 `ResourceLoader.Exists` 类型检查和失败路径缓存，避免错误 icon path 在选择条目或刷新列表时反复触发 `GD.Load` 错误。
- 继续深挖 `SaveSerializer.NormalizeEncounterAnchors()` 与 `SerializeObjectOrDictionaryArray()` 的 Variant 分支顺序，先处理 Dictionary，再仅对 Object Variant 调用 `AsGodotObject()`，降低 save/load 路径被 GodotSharp Variant 转换细节击穿的风险。
- 顺着同一条 encounter anchor 链路继续检查 `HeadlessGameTestSession.ReadEncounterAnchorsTyped()`，修复 headless 快照漏读 Dictionary anchors 和对非 Object Variant 直接对象转换的问题。
- 继续复查 `PartyWarehouseWindow` 的仓库详情富文本风险：确认场景当前关闭 BBCode 后，在脚本 `_Ready()` 中显式固定 `BbcodeEnabled = false`，并补充纯文本/坏 icon 路径回归，防止后续 scene drift 或资源路径错误重新变成 UI 风险。
- 其余文件的矩阵仍是系统化审查索引，不再声称等价于人工逐行证明；后续应按 findings-first 的剩余 `[中]` 项逐个做同等深度的“读代码链路 + 修代码/补回归”。

## 逐文件深度检视矩阵

本节按用户要求对 `scripts/` 下每一个文件继续做逐文件深度检视。每个条目都包含：文件职责、关键入口/类型、状态与边界、对抗性失败模式、建议验证。这里的“风险”不是一定存在 bug，而是 review 时必须尝试破坏的路径；真正已确认或高置信的问题仍以上方 findings-first 节为优先。

### 1. `scripts/dev_tools/AiTraceRecorder.cs`

- 复审状态：**需跟进**；规模：330 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：AiTraceRecorder；主要方法：Enter, Exit, SetEventCaptureEnabled, SetSampleCaptureEnabled, HasInstance, GetInstance, SetInstance, _enter_impl, _exit_impl, GetFuncStats。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×2; runtime mutation collections×12。
- 对抗性检视：
  - 检查 trace event/sample 的内存上限、Enter/Exit 配平、Variant 整数转换和 DumpTraceJson 失败路径；诊断工具不能污染 AI 热路径。
- 建议验证：dotnet build magic.csproj；补一个 AiTraceRecorder Enter/Exit/AssertBalanced 单元或 headless benchmark smoke。

### 2. `scripts/enemies/AiActionTrace.cs`

- 复审状态：**需跟进**；规模：193 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：AiActionTrace；主要方法：IsEmpty, Increment, AddBlockReason, OfferCandidate, ApplyBestDecision, MarkChosen, ToDictionary, CopyObjectDictionary, CopyScoreInput。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×2; runtime mutation collections×21。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 3. `scripts/enemies/AiCandidateSummary.cs`

- 复审状态：**需跟进**；规模：122 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：AiCandidateSummary；主要方法：Clone, Create, ToDictionary, CopyDictionary, ReadInt。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×2; runtime mutation collections×15。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 4. `scripts/enemies/AiCommandSummary.cs`

- 复审状态：**需跟进**；规模：121 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：AiCommandSummary；主要方法：FromCommand, Clone, ToDictionary, AddStringNames, AddCoords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×2; runtime mutation collections×14。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 5. `scripts/enemies/DropEntryDef.cs`

- 复审状态：**通过**；规模：20 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：DropEntryDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 6. `scripts/enemies/EnemyAiAction.cs`

- 复审状态：**需跟进**；规模：1547 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiAction, struct；主要方法：GetDeclaredSkillIds, ValidateSkillReferences, _collect_base_validation_errors, _is_supported_target_selector, _append_enemy_focus_target_selector_errors, _append_declared_skill_id, _create_decision, _create_scored_decision, _resolve_known_skill_ids, _get_skill_def。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×29; runtime mutation collections×66。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 7. `scripts/enemies/EnemyAiActionHelper.cs`

- 复审状态：**需跟进**；规模：231 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiActionHelper；主要方法：CreateDecision, CreateScoredDecision, BuildWaitCommand, BuildMoveCommand, BuildUnitSkillCommand, BuildGroundSkillCommand, SortCoords, CoordSetKey, BeginActionTrace, TraceCountIncrement。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×7。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 8. `scripts/enemies/EnemyAiBrainDef.cs`

- 复审状态：**需跟进**；规模：133 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiBrainDef；主要方法：GetResolvedStates, GetState, HasState, ValidateSchema, _validate_transition_rules。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; runtime mutation collections×20。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 9. `scripts/enemies/EnemyAiDistanceReference.cs`

- 复审状态：**通过**；规模：46 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiDistanceReference, EnemyAiDistanceReferences；主要方法：ToKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 10. `scripts/enemies/EnemyAiGenerationSlotDef.cs`

- 复审状态：**需跟进**；规模：369 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiGenerationSlotRole, EnemyAiSkillAffordance, EnemyAiActionFamily, EnemyAiGenerationSuppressionPolicy, EnemyAiGenerationSlotDef；主要方法：ToSlotRole, ToStringName, ToAffordance, ToActionFamily, ToSuppressionPolicy, MatchesAffordance, BuildSignature, ValidateSchema, _find_action_by_id, StringifyArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×16。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 11. `scripts/enemies/EnemyAiStateDef.cs`

- 复审状态：**需跟进**；规模：179 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiStateDef；主要方法：GetActions, GetTypedActions, ValidateSchema, GetTypedGenerationSlots, _validate_generation_slots。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; runtime mutation collections×29。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 12. `scripts/enemies/EnemyAiTargetSelectorRules.cs`

- 复审状态：**通过**；规模：73 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiTargetSelector, EnemyAiTargetSelectorRules；主要方法：ToKind, ToStringName, IsSupportedSelector, IsEnemyFocusSelector。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 13. `scripts/enemies/EnemyAiTransitionConditionDef.cs`

- 复审状态：**需跟进**；规模：176 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiTransitionPredicate, EnemyAiTransitionConditionDef；主要方法：ToPredicate, ToStringName, ValidateSchema, ToSignature, StringNameArrayToStrings。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×14。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 14. `scripts/enemies/EnemyAiTransitionRuleDef.cs`

- 复审状态：**需跟进**；规模：103 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyAiTransitionRuleDef；主要方法：GetTypedConditions, AppliesToState, ValidateSchema, ToSignature, StringNameArrayToStrings。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×17。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 15. `scripts/enemies/EnemyContentRegistry.cs`

- 复审状态：**需跟进**；规模：496 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyContentRegistry；主要方法：ConfigureSeedResource, ConfigureDirectories, Rebuild, Validate, ValidateTyped, _register_seed_resource, _remember_seed_resource_path, _collect_seed_directory_completeness_errors, _append_seed_dir_errors, _collect_resource_paths_in_directory。
- Godot/公开边界：Export 2 处；Signal 2 处；风险触点：Godot Dictionary/Array boundary×12; resource/path loading×8; runtime mutation collections×56。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 16. `scripts/enemies/EnemyContentSeed.cs`

- 复审状态：**需跟进**；规模：14 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyContentSeed；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 17. `scripts/enemies/EnemyTemplateDef.cs`

- 复审状态：**需跟进**；规模：762 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：EnemyTemplateDef；主要方法：GetInitialStateId, HasTag, GetAttackEquipmentItemIdResolved, GetWeaponProjectionTyped, GetAttackEquipmentProjectionTyped, GetNaturalWeaponProjectionTyped, GetUnarmedWeaponProjectionTyped, GetNaturalWeaponDamageTagResolved, GetSkillLevelTyped, ValidateSchemaTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×27; runtime mutation collections×84。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 18. `scripts/enemies/TraceDictionaryProjection.cs`

- 复审状态：**需跟进**；规模：305 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：TraceDictionaryProjection；主要方法：ToDictionary, AddValue, FromVariant, FromArray, ToArray, AddArrayValue, ReadKey, ToStringNameArray, ToVector2IArray, ToStringArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; save/schema/projection×28; runtime mutation collections×37。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 19. `scripts/enemies/WildEncounterRosterDef.cs`

- 复审状态：**需跟进**；规模：164 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：WildEncounterRosterDef；主要方法：GetMaxStage, GetStageUnitEntriesTyped, ValidateSchemaTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×20。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 20. `scripts/enemies/WildEncounterRosterStageDef.cs`

- 复审状态：**需跟进**；规模：11 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：WildEncounterRosterStageDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 21. `scripts/enemies/WildEncounterRosterUnitEntryDef.cs`

- 复审状态：**需跟进**；规模：15 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：WildEncounterRosterUnitEntryDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 22. `scripts/enemies/actions/MoveToAdvantagePositionAction.cs`

- 复审状态：**需跟进**；规模：583 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：MoveToAdvantagePositionAction, MoveToAdvantagePositioningMode, MoveCandidate；主要方法：_decide_impl, _try_collect_fast_move_candidates, _sort_full_scan_candidates, _sort_fast_candidates, _build_fast_move_preview, ToPositioningMode, ReadMetadataInt。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×22; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 23. `scripts/enemies/actions/MoveToMultiUnitSkillPositionAction.cs`

- 复审状态：**需跟进**；规模：365 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：MoveToMultiUnitSkillPositionAction；主要方法：_decide_impl, _build_anchor_target_group, _can_anchor_target_unit, _collect_reachable_move_candidates, _distance_from_anchor_to_nearest_target, _apply_target_group_score, _is_better_reposition_score_input, _is_multi_unit_skill, _has_explicit_distance_contract。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×25; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 24. `scripts/enemies/actions/MoveToRangeAction.cs`

- 复审状态：**需跟进**；规模：2482 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：MoveToRangeAction, MoveToRangeAiEvaluationMode, MoveToRangeScreeningMode, ScreeningContext, struct, ScreeningThreatEntry, MoveDistanceContract, MovePathTreeCosts；主要方法：Disabled, FromMetadata, ReadMetadataInt, TryResolvePath, FromPathTreeResult, ToCandidateBudget, FromSnapshot, FromSkillRecord, Clone, CanUseGeneratedCandidateRequestMode。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×21; runtime mutation collections×95; hot path / lifecycle×9。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 25. `scripts/enemies/actions/RetreatAction.cs`

- 复审状态：**需跟进**；规模：206 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：RetreatAction；主要方法：_decide_impl, _resolve_retreat_safe_distance, _resolve_retreat_focus_target。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×9; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 26. `scripts/enemies/actions/UseChargeAction.cs`

- 复审状态：**需跟进**；规模：535 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseChargeAction, ChargeTargetInfo, ChargeDistanceBreakpoint；主要方法：_decide_impl, _enumerate_charge_target_coords, _resolve_charge_max_distance, ReadDistanceBreakpoints, TryReadLevelBreakpoint, _resolve_charge_target_info, BuildFastChargePreview, BuildChargeScoreInput, _resolve_short_charge_block_reason, ReadDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; runtime mutation collections×13; hot path / lifecycle×6。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 27. `scripts/enemies/actions/UseChargePathAoeAction.cs`

- 复审状态：**需跟进**；规模：495 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseChargePathAoeAction, ChargeTargetInfo, PathStepHitMetrics；主要方法：AddHit, ApplyToMetadata, _decide_impl, _get_path_step_aoe_effect, _resolve_charge_target_info, BuildFastChargePathPreview, _build_path_step_hit_metrics, _build_resolved_anchor_path, _build_path_step_effect_coords, _unit_intersects_coords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×24; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 28. `scripts/enemies/actions/UseGroundRepositionSkillAction.cs`

- 复审状态：**需跟进**；规模：256 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseGroundRepositionSkillAction；主要方法：_decide_impl, _has_reposition_effect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×10; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 29. `scripts/enemies/actions/UseGroundSkillAction.cs`

- 复审状态：**需跟进**；规模：1122 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseGroundSkillAction, GroundCandidatePrefilter, GroundTargetCoordSet, GroundSkillEffectSet；主要方法：FirstOrDefault, ToSortedList, Add, _decide_impl, _is_ground_coord_set_within_cast_range, _build_ground_candidate_prefilter, _build_prefilter_effect_coords, _resolve_prefilter_direction, _unit_intersects_coords, _collect_living_units。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; runtime mutation collections×61; hot path / lifecycle×5。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 30. `scripts/enemies/actions/UseMultiUnitSkillAction.cs`

- 复审状态：**需跟进**；规模：419 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseMultiUnitSkillAction；主要方法：_decide_impl, _is_multi_unit_skill, _get_multi_unit_cast_variants, _build_target_groups, _build_candidate_pool, _append_target_group, _target_group_key, _build_multi_unit_skill_command, _collect_multi_unit_effect_defs, _resolve_enemy_frontline_unit。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; runtime mutation collections×44; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 31. `scripts/enemies/actions/UseRandomChainSkillAction.cs`

- 复审状态：**需跟进**；规模：479 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseRandomChainSkillAction, RandomChainDistanceContract, RandomChainScoreMetadata；主要方法：_is_random_chain_skill, _get_random_chain_cast_variants, _build_random_chain_skill_command, _resolve_candidate_units, _candidate_unit_ids, _collect_random_chain_effect_defs, BuildRandomChainScoreMetadata, ResolveRandomChainDistanceContract, _resolve_enemy_frontline_unit, _has_explicit_distance_contract。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×36。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 32. `scripts/enemies/actions/UseUnitSkillAction.cs`

- 复审状态：**需跟进**；规模：80 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：UseUnitSkillAction；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×7; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 33. `scripts/enemies/actions/WaitAction.cs`

- 复审状态：**需跟进**；规模：286 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：WaitAction, ActiveRestProfile；主要方法：_decide_impl, _build_active_rest_profile, _will_wait_trigger_rest, _has_affordable_legal_hostile_skill, _has_legal_unit_skill_target, _can_pay_skill_cost, _resolve_desired_rest_stamina, _get_skill_stamina_cost, _estimate_resting_recovery, _resolve_action_threshold_tu。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×4; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 34. `scripts/player/equipment/EquipmentDurabilityRules.cs`

- 复审状态：**需跟进**；规模：33 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentDurabilityRules；主要方法：GetMaxDurabilityForRarity, GetDefaultCurrentDurability, GetDisjunctionSaveBonusForRarity, IsValidCurrentDurability。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 35. `scripts/player/equipment/EquipmentEntryState.cs`

- 复审状态：**需跟进**；规模：95 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentEntryState；主要方法：IsEmpty, GetEquipmentInstance, SetEquipmentInstance, DuplicateState, ToDictionary, FromDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×4; runtime mutation collections×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 36. `scripts/player/equipment/EquipmentRequirement.cs`

- 复审状态：**需跟进**；规模：64 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentRequirementCheckResult, EquipmentRequirement；主要方法：ToDictionary, CheckResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1; runtime mutation collections×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 37. `scripts/player/equipment/EquipmentRules.cs`

- 复审状态：**需跟进**；规模：167 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentSlotKind, EquipmentRules；主要方法：GetAllSlotIdsTyped, IsValidSlot, NormalizeSlotIdsTyped, AddNormalizedSlotId, GetSlotLabel, ToSlotKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×5; runtime mutation collections×13。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 38. `scripts/player/equipment/EquipmentState.cs`

- 复审状态：**需跟进**；规模：302 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentState；主要方法：GetEquippedItemId, GetEquippedInstanceId, GetEquippedInstance, GetEntry, GetEntryForSlot, GetOccupiedSlotIdsForEntryTyped, SetEquippedEntry, ClearSlot, ClearEntrySlot, PopEquippedInstance。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×4; runtime mutation collections×23。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 39. `scripts/player/progression/AchievementDef.cs`

- 复审状态：**需跟进**；规模：130 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AchievementDef；主要方法：MatchesEvent, IsEmpty, ToDictionary, FromDictionary, _has_exact_serialized_fields, _hfs, _parse_string_name_field。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×4; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 40. `scripts/player/progression/AchievementProgressState.cs`

- 复审状态：**需跟进**；规模：82 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AchievementProgressState；主要方法：DuplicateState, ToDictionary, FromDictionary, _has_exact_fields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 41. `scripts/player/progression/AchievementRewardDef.cs`

- 复审状态：**需跟进**；规模：113 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AchievementRewardDef；主要方法：IsEmpty, ToDictionary, FromDictionary, HasExactFields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 42. `scripts/player/progression/AgeContentRegistry.cs`

- 复审状态：**需跟进**；规模：240 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AgeContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _collect_validation_errors, _append_age_profile_validation_errors, _append_age_stage_rule_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; resource/path loading×2; runtime mutation collections×19。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 43. `scripts/player/progression/AgeProfileDef.cs`

- 复审状态：**需跟进**；规模：44 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AgeProfileDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 44. `scripts/player/progression/AgeStageRule.cs`

- 复审状态：**需跟进**；规模：29 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AgeStageRule；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 45. `scripts/player/progression/AscensionContentRegistry.cs`

- 复审状态：**需跟进**；规模：246 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AscensionContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _register_ascension, _register_ascension_stage, _collect_validation_errors, _append_ascension_validation_errors, _append_ascension_stage_validation_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; resource/path loading×2; runtime mutation collections×19。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 46. `scripts/player/progression/AscensionDef.cs`

- 复审状态：**需跟进**；规模：41 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AscensionDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 47. `scripts/player/progression/AscensionStageDef.cs`

- 复审状态：**需跟进**；规模：32 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AscensionStageDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 48. `scripts/player/progression/AttributeGrowthContentRules.cs`

- 复审状态：**通过**；规模：47 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AttributeGrowthTierKind, AttributeGrowthContentRules；主要方法：GetTierBudget, IsValidGrowthTier, IsValidAttributeId, ToGrowthTierKind。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 49. `scripts/player/progression/AttributeModifier.cs`

- 复审状态：**通过**；规模：75 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AttributeModifierMode, AttributeModifier；主要方法：IsValidMode, ToMode, ToStringName, GetValueForRank, IsPercent, IsFlat。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 50. `scripts/player/progression/AttributeRequirement.cs`

- 复审状态：**通过**；规模：17 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AttributeRequirement；主要方法：MatchesValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 51. `scripts/player/progression/AttributeSnapshot.cs`

- 复审状态：**需跟进**；规模：129 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AttributeSnapshotIdKind, AttributeSnapshot, struct；主要方法：ToStringName, SetValue, GetValue, HasValue, ToDictionary, GetBaseAttributeModifierId, CalculateScoreModifier, CalculateBaseAttackBonus, CalculateSpellProficiencyBonus, GetBabRateForProgression。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×1; runtime mutation collections×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 52. `scripts/player/progression/BarrierContentRegistry.cs`

- 复审状态：**需跟进**；规模：210 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BarrierContentRegistry；主要方法：Rebuild, GetProfileDef, Validate, _scan_directory, _register_profile_resource, _append_profile_validation_errors, IsSupportedBarrierAreaPattern。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; resource/path loading×2; runtime mutation collections×22。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 53. `scripts/player/progression/BarrierLayerDef.cs`

- 复审状态：**需跟进**；规模：57 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BarrierLayerDef；主要方法：ToRuntimeDict, _ToStringArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×2; runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 54. `scripts/player/progression/BarrierOutcomeDef.cs`

- 复审状态：**需跟进**；规模：107 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BarrierOutcomeKind, BarrierOutcomeDef；主要方法：ToRuntimeDict, ToOutcomeKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 55. `scripts/player/progression/BarrierProfileDef.cs`

- 复审状态：**需跟进**；规模：76 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BarrierAnchorMode, BarrierProfileDef；主要方法：GetOrderedLayers, ToAnchorMode, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 56. `scripts/player/progression/BattleSaveContentRules.cs`

- 复审状态：**需跟进**；规模：237 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BattleSaveDcMode, BattleSaveAdvantageStateKind, BattleSaveTagKind, BattleSaveAbilityKind, BattleSaveContentRules；主要方法：IsValidSaveTag, IsValidSaveAbility, IsControlSaveTag, IsValidSaveDcMode, ToSaveDcMode, ToSaveTagKind, ToSaveAbilityKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×148。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 57. `scripts/player/progression/BloodlineContentRegistry.cs`

- 复审状态：**需跟进**；规模：215 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BloodlineContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _register_bloodline, _register_bloodline_stage, _collect_validation_errors, _append_bloodline_validation_errors, _append_bloodline_stage_validation_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; resource/path loading×2; runtime mutation collections×19。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 58. `scripts/player/progression/BloodlineDef.cs`

- 复审状态：**需跟进**；规模：29 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BloodlineDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 59. `scripts/player/progression/BloodlineStageDef.cs`

- 复审状态：**需跟进**；规模：29 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BloodlineStageDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 60. `scripts/player/progression/BodySizeContentRules.cs`

- 复审状态：**通过**；规模：118 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BodySizeCategoryKind, BodySizeContentRules；主要方法：IsValidBodySizeCategory, IsValidBodySize, GetBodySizeForCategory, BodySizeMatchesCategory, GetFootprintForBodySize, GetFootprintForCategory, ToCategoryKind, ToCanonicalCategoryKind, ToStringName, ToBodySize。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 61. `scripts/player/progression/CombatCastVariantDef.cs`

- 复审状态：**需跟进**；规模：84 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CombatCastFootprintPattern, CombatCastVariantDef；主要方法：ToFootprintPattern, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 62. `scripts/player/progression/CombatEffectDef.cs`

- 复审状态：**需跟进**；规模：659 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CombatEffectTriggerEvent, CombatEffectTriggerCondition, CombatEffectLifetimePolicy, CombatEffectDef；主要方法：MIN_JUMP_ARC_RATIO, ToTriggerEvent, ToTriggerCondition, ToLifetimePolicy, ToStringName, DuplicateForRuntime, GetIntParamTyped, GetStringNameParamTyped, GetFloatParamTyped, HasEffectTagTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×5; runtime mutation collections×8。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 63. `scripts/player/progression/CombatResourceIds.cs`

- 复审状态：**通过**；规模：47 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CombatResourceIdKind, CombatResourceIds；主要方法：ToResourceKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 64. `scripts/player/progression/CombatSkillDef.cs`

- 复审状态：**需跟进**；规模：628 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CombatSpellFateMode, CombatSpellCriticalMode, CombatSkillBacklashMode, CombatAreaOriginMode, CombatAreaDirectionMode, CombatSkillDef；主要方法：GetCastVariant, GetUnlockedCastVariants, GetEffectiveResourceCostValues, GetLevelOverride, GetCachedLevelOverride, BuildLevelOverride, DuplicateLevelOverride, TryReadResourceCostOverride, GetEffectiveAttackRollBonus, GetEffectiveCastingTimeTu。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×27; runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 65. `scripts/player/progression/CombatSkillResourceCosts.cs`

- 复审状态：**需跟进**；规模：23 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：struct；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 66. `scripts/player/progression/CombatSkillTargetingContentRules.cs`

- 复审状态：**通过**；规模：87 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CombatSkillTargetingContentRules；主要方法：IsValidCombatTargetMode, IsValidCastVariantTargetMode, IsValidTargetSelectionMode, IsValidSelectionOrderMode, IsValidAreaPattern, IsValidFootprintPattern, ValidCombatTargetModeLabel, ValidCastVariantTargetModeLabel, ValidTargetSelectionModeLabel, ValidSelectionOrderModeLabel。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 67. `scripts/player/progression/CombatTargetTeamContentRules.cs`

- 复审状态：**通过**；规模：79 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CombatTargetTeamFilterKind, CombatTargetTeamContentRules；主要方法：IsValidSkillTargetTeamFilter, IsValidEffectTargetTeamFilter, ToTargetTeamFilterKind, IsSkillTargetTeamFilterKind, ToStringName, ValidSkillTargetTeamFilterLabel, ValidEffectTargetTeamFilterLabel。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 68. `scripts/player/progression/DamageTagContentRules.cs`

- 复审状态：**通过**；规模：119 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：DamageTagKind, DamageMitigationTierKind, DamageCategoryKind, DamageTagContentRules；主要方法：ToDamageTagKind, ToMitigationTierKind, ToDamageCategoryKind, IsPhysicalDamageTag, ValidDamageTagLabel, ValidMitigationTierLabel, ValidDamageCategoryLabel。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 69. `scripts/player/progression/DerivedAttributeRule.cs`

- 复审状态：**需跟进**；规模：75 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：DerivedAttributeRule；主要方法：RefreshCache, _RebuildCache, evaluate。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 70. `scripts/player/progression/FaithDeityDef.cs`

- 复审状态：**需跟进**；规模：104 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：FaithDeityDef；主要方法：GetRankDef, GetMaxRank, Validate, _has_rank_progress_reward。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; runtime mutation collections×9。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 71. `scripts/player/progression/FaithRankDef.cs`

- 复审状态：**需跟进**；规模：172 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：struct, FaithRankDef；主要方法：HasCustomStatRequirement, HasAchievementRequirement, GetRewardEntrySpecs, Validate, ParseRewardEntrySpec, ReadStringName, ReadInt, ReadString, ReadValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; runtime mutation collections×13。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 72. `scripts/player/progression/IdentityContentRegistryBase.cs`

- 复审状态：**需跟进**；规模：468 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：IdentityContentRegistryBase；主要方法：_allowed_attribute_id_set, Validate, _scan_directory, _sorted_registry_keys, _append_string_name_field_error, _append_string_field_error, _append_int_field_error, _append_bool_field_error, _append_string_name_array_errors, _append_string_array_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×25; runtime mutation collections×28。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 73. `scripts/player/progression/PartyMemberState.cs`

- 复审状态：**需跟进**；规模：633 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PartyMemberState；主要方法：DuplicateState, GetHiddenLuckAtBirth, GetFaithLuckBonus, GetEffectiveLuck, GetCombatLuckScore, GetDropLuck, ToDictionary, FromDictionary, _get_unit_base_attributes, _parse_string_name_field。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×34; save/schema/projection×6; runtime mutation collections×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 74. `scripts/player/progression/PartyState.cs`

- 复审状态：**需跟进**；规模：899 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PartyState；主要方法：GetMemberState, HasMemberState, GetMemberStates, IsMemberDead, GetResolvedMainCharacterMemberId, GetFateRunFlag, HasFateRunFlag, SetFateRunFlag, ClearFateRunFlag, CaptureFateRunFlags。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×53; save/schema/projection×11; runtime mutation collections×49。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 75. `scripts/player/progression/PendingCharacterRewardContentRules.cs`

- 复审状态：**通过**；规模：87 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PendingCharacterRewardEntryKind, PendingCharacterRewardContentRules；主要方法：IsSupportedEntryType, RequiresSkillTarget, IsAttributeProgressEntry, IsAttributeDeltaEntry, IsValidAttributeProgressTarget, ValidEntryTypeLabel, ToEntryKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 76. `scripts/player/progression/PendingProfessionChoice.cs`

- 复审状态：**需跟进**；规模：302 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PendingProfessionChoice；主要方法：SetTriggerSkillIds, AddTriggerSkillId, SetCandidateProfessionIds, AddCandidateProfessionId, SetQualifierSkillPoolIds, AddQualifierSkillPoolId, SetAssignableSkillCandidateIds, AddAssignableSkillCandidateId, SetTargetRank, SetTargetRankMap。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×2; runtime mutation collections×26。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 77. `scripts/player/progression/ProfessionActiveCondition.cs`

- 复审状态：**通过**；规模：57 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionActiveConditionKind, ProfessionActiveCondition；主要方法：MatchesValue, ToConditionKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 78. `scripts/player/progression/ProfessionContentRegistry.cs`

- 复审状态：**需跟进**；规模：709 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionContentRegistry, struct, struct；主要方法：Setup, Rebuild, LoadFromDirectory, Validate, ScanDirectory, RegisterProfessionResource, CollectValidationErrors, AppendProfessionValidationErrors, AppendUnlockRequirementErrors, AppendRankRequirementErrors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：resource/path loading×2; runtime mutation collections×60。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 79. `scripts/player/progression/ProfessionDef.cs`

- 复审状态：**需跟进**；规模：159 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionReactivationMode, ProfessionDependencyVisibilityMode, ProfessionBaseAttackProgression, ProfessionDef；主要方法：RequiresKnowledgeUnlock, ToReactivationMode, ToDependencyVisibilityMode, ToBabProgression, ToStringName, GetRankRequirement, GetGrantedSkillsForRank。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 80. `scripts/player/progression/ProfessionGrantedSkill.cs`

- 复审状态：**通过**；规模：14 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionGrantedSkill；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 81. `scripts/player/progression/ProfessionPromotionRecord.cs`

- 复审状态：**需跟进**；规模：168 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionPromotionRecord；主要方法：DuplicateState, ToDictionary, FromDictionary, _has_exact_fields, _parse_string_name_field, _parse_unique_string_name_array。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×15; save/schema/projection×2; runtime mutation collections×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 82. `scripts/player/progression/ProfessionPromotionRequirement.cs`

- 复审状态：**通过**；规模：33 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionPromotionRequirement；主要方法：IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 83. `scripts/player/progression/ProfessionRankGate.cs`

- 复审状态：**通过**；规模：45 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionGateCheckMode, ProfessionRankGate；主要方法：ToCheckMode, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 84. `scripts/player/progression/ProfessionRankRequirement.cs`

- 复审状态：**需跟进**；规模：26 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionRankRequirement；主要方法：IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 85. `scripts/player/progression/ProgressionContentRegistry.cs`

- 复审状态：**需跟进**；规模：2421 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProgressionContentRegistry；主要方法：Rebuild, GetQuestRegistrationErrorsTyped, GetIdentityCatalogTyped, Validate, ValidateTyped, ReplaceValidationSources, CollectValidationErrors, AppendIdentityPhase2ValidationErrors, CollectValidationErrorsTyped, CollectIdentityPhase2ValidationErrorsTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×100; runtime mutation collections×204。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 86. `scripts/player/progression/ProgressionDataUtils.cs`

- 复审状态：**需跟进**；规模：216 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProgressionDataUtils；主要方法：to_string_name, string_name_to_string, to_string_name_array, string_name_array_to_string_array, to_string_name_int_map, string_name_int_map_to_string_dict, string_name_array_map_to_string_dict, sorted_string_keys, StringNameArrayToStringArray, MatchesValueRange。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×26; runtime mutation collections×13。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 87. `scripts/player/progression/QuestContentValidator.cs`

- 复审状态：**通过**；规模：249 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestContentValidator；主要方法：ValidateTyped, AppendProviderReferenceErrors, AppendObjectiveReferenceErrors, AppendRewardReferenceErrors, AppendPendingCharacterRewardReferenceErrors, ResolveProviderIdsTyped, AppendErrors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×37。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 88. `scripts/player/progression/QuestDef.cs`

- 复审状态：**需跟进**；规模：806 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestObjectiveKind, QuestRewardKind, QuestDef, ObjectiveEntryData, RewardEntryData, PendingRewardEntryData；主要方法：ToStringName, ToObjectiveKind, ToRewardKind, FromDictionary, InvalidNonDictionary, IsEmpty, ValidateSchema, GetObjectiveEntriesTyped, GetRewardEntriesTyped, HasExactSerializedFields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×48; save/schema/projection×12; runtime mutation collections×42。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 89. `scripts/player/progression/QuestProviderContentRules.cs`

- 复审状态：**通过**；规模：45 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestProviderKind, QuestProviderContentRules；主要方法：IsSupportedProviderId, SupportedProviderIds, SupportedProviderLabel, ToProviderKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 90. `scripts/player/progression/QuestState.cs`

- 复审状态：**需跟进**；规模：344 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestStatusKind, QuestState；主要方法：IsActive, IsCompleted, IsTerminal, ToStringName, ToStatusKind, GetObjectiveProgress, RecordObjectiveProgress, IsObjectiveComplete, HasCompletedAllObjectives, MarkAccepted。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×2; runtime mutation collections×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 91. `scripts/player/progression/RaceContentRegistry.cs`

- 复审状态：**需跟进**；规模：181 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：RaceContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _collect_validation_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; resource/path loading×2; runtime mutation collections×12。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 92. `scripts/player/progression/RaceDef.cs`

- 复审状态：**需跟进**；规模：56 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：RaceDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 93. `scripts/player/progression/RaceTraitContentRegistry.cs`

- 复审状态：**需跟进**；规模：112 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：RaceTraitContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _collect_validation_errors, _append_trait_validation_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; resource/path loading×2; runtime mutation collections×14。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 94. `scripts/player/progression/RaceTraitDef.cs`

- 复审状态：**需跟进**；规模：307 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：RaceTraitEffectKind, RaceTraitDef；主要方法：ToEffectKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 95. `scripts/player/progression/RacialGrantedSkill.cs`

- 复审状态：**通过**；规模：57 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：RacialSkillChargeKind, RacialGrantedSkill；主要方法：ToChargeKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 96. `scripts/player/progression/ReputationRequirement.cs`

- 复审状态：**通过**；规模：17 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ReputationRequirement；主要方法：MatchesValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 97. `scripts/player/progression/SkillContentRegistry.cs`

- 复审状态：**需跟进**；规模：2218 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillContentRegistry, struct；主要方法：FromEffect, ReadTargetSlotsMissingOrEmpty, Rebuild, LoadFromDirectory, Validate, ScanDirectory, RegisterSkillResource, NormalizeSkillDef, CollectValidationErrors, AppendSkillValidationErrors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×31; resource/path loading×2; runtime mutation collections×234。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 98. `scripts/player/progression/SkillDef.cs`

- 复审状态：**需跟进**；规模：921 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillTypeKind, SkillLearnSourceKind, SkillPracticeTierKind, SkillUnlockMode, CoreSkillTransitionMode, SkillDef, AttributeGrowthProgressEntryData, IntRequirementEntryData；主要方法：SetAttributeGrowthProgress, SetTags, HasTag, SetSkillLevelRequirements, SetAttributeRequirements, SetAttributeModifiers, SetLevelDescriptionConfigs, GetMasteryRequiredForLevel, IsProfessionSkill, CanUseInCombat。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×41; runtime mutation collections×93。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 99. `scripts/player/progression/SkillLevelDescriptionContentRules.cs`

- 复审状态：**通过**；规模：84 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillLevelDescriptionContentRules；主要方法：CollectValidationErrors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×13。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 100. `scripts/player/progression/StageAdvancementContentRegistry.cs`

- 复审状态：**需跟进**；规模：172 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：StageAdvancementContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _collect_validation_errors, _append_stage_advancement_validation_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; resource/path loading×2; runtime mutation collections×14。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 101. `scripts/player/progression/StageAdvancementModifier.cs`

- 复审状态：**需跟进**；规模：98 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：StageAdvancementTargetAxis, StageAdvancementModifier；主要方法：ToTargetAxis, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 102. `scripts/player/progression/SubraceContentRegistry.cs`

- 复审状态：**需跟进**；规模：175 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SubraceContentRegistry；主要方法：Rebuild, LoadFromDirectory, LoadFromDirectories, _collect_validation_errors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; resource/path loading×2; runtime mutation collections×11。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 103. `scripts/player/progression/SubraceDef.cs`

- 复审状态：**通过**；规模：51 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SubraceDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 104. `scripts/player/progression/TagRequirement.cs`

- 复审状态：**通过**；规模：130 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：TagRequirementSkillState, TagRequirementOriginFilter, TagRequirementSelectionRole, TagRequirement；主要方法：ToSkillState, ToOriginFilter, ToSelectionRole, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 105. `scripts/player/progression/TraitTriggerContentRules.cs`

- 复审状态：**通过**；规模：160 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：TraitTriggerKind, TraitTriggerContentRules；主要方法：HasDispatchForTraitTrigger, GetDispatchKey, GetDispatchTraitIds, ToTriggerKind, ToStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×12。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 106. `scripts/player/progression/UnitBaseAttributes.cs`

- 复审状态：**需跟进**；规模：304 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：UnitBaseAttributeKind, UnitBaseAttributes；主要方法：GetBaseAttributeIdsTyped, IsBaseAttributeId, ToStringName, ToAttributeKind, GetAttributeValue, SetAttributeValue, GetHiddenLuckAtBirth, GetFaithLuckBonus, GetEffectiveLuck, GetCombatLuckScore。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×15; save/schema/projection×2; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 107. `scripts/player/progression/UnitProfessionProgress.cs`

- 复审状态：**需跟进**；规模：233 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：UnitProfessionProgress, in；主要方法：AddCoreSkill, RemoveCoreSkill, AddGrantedSkill, AddPromotionRecord, DuplicateState, ToDictionary, FromDictionary, _has_exact_fields, _parse_string_name_field, _parse_unique_string_name_array。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×18; save/schema/projection×4; runtime mutation collections×8。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 108. `scripts/player/progression/UnitProgress.cs`

- 复审状态：**需跟进**；规模：1412 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：UnitProgress；主要方法：SetSkillProgress, GetSkillProgress, RemoveSkillProgress, SetProfessionProgress, GetProfessionProgress, RemoveProfessionProgress, SetAchievementProgressState, GetAchievementProgressState, HasKnowledge, LearnKnowledge。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×92; save/schema/projection×14; runtime mutation collections×88。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 109. `scripts/player/progression/UnitReputationState.cs`

- 复审状态：**需跟进**；规模：105 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：UnitReputationKind, UnitReputationState；主要方法：GetReputationValue, SetReputationValue, DuplicateState, ToDictionary, FromDictionary, _hfs, _parse_int_map, ToStringName, ToReputationKind。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×2; runtime mutation collections×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 110. `scripts/player/progression/UnitSkillProgress.cs`

- 复审状态：**需跟进**；规模：409 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：UnitSkillGrantSourceType, UnitSkillProgress；主要方法：IsMaxLevel, ClearProfessionAssignment, DuplicateState, ToDictionary, FromDictionary, ToGrantSourceType, ToStringName, _parse_string_name_field, _parse_unique_string_name_array, _has_exact_fields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; save/schema/projection×2; runtime mutation collections×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 111. `scripts/player/warehouse/EquipmentInstanceState.cs`

- 复审状态：**需跟进**；规模：187 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentInstanceState, RarityTier；主要方法：CreateInstance, CreateTransientInstance, FormatInstanceId, FormatPreviewInstanceId, ToDictionary, DuplicateState, FromDictionary, FromTransientLootDictionary, GetPayloadValidationError, _from_dict。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×2; runtime mutation collections×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 112. `scripts/player/warehouse/ItemContentRegistry.cs`

- 复审状态：**需跟进**；规模：845 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：ItemContentRegistry；主要方法：Rebuild, RebuildFromDirectories, Validate, ValidateTyped, EnsureBuilt, EnsureDefaultSnapshotBuilt, ScanTemplateDirectory, RegisterTemplateResource, ResolveAllTemplates, ScanDirectory。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×30; resource/path loading×4; runtime mutation collections×96。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 113. `scripts/player/warehouse/ItemDef.cs`

- 复审状态：**需跟进**；规模：354 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：ItemCategoryKind, ItemEquipmentTypeKind, WeaponPhysicalDamageTagKind, ItemDef；主要方法：GetEffectiveMaxStack, GetBasePrice, GetBuyPrice, GetSellPrice, GetTagsTyped, GetCraftingGroupsTyped, GetQuestGroupsTyped, GetItemCategoryNormalized, HasEquipmentCategory, GetEquipmentSlotIdsTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×9; runtime mutation collections×15。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 114. `scripts/player/warehouse/RecipeContentRegistry.cs`

- 复审状态：**需跟进**；规模：246 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：RecipeContentRegistry；主要方法：Setup, Rebuild, LoadFromDirectory, Validate, ValidateTyped, _scan_directory, _register_recipe_resource。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; resource/path loading×2; runtime mutation collections×38。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 115. `scripts/player/warehouse/RecipeDef.cs`

- 复审状态：**通过**；规模：33 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：RecipeDef；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 116. `scripts/player/warehouse/SkillBookItemContentValidator.cs`

- 复审状态：**通过**；规模：108 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：SkillBookItemContentValidator；主要方法：Validate, _append_skill_book_reference_errors, _append_canonical_id_collision_errors, SortedKeys。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×19。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 117. `scripts/player/warehouse/SkillBookItemFactory.cs`

- 复审状态：**通过**；规模：64 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：SkillBookItemFactory；主要方法：BuildItemIdForSkill, _build_display_name, _build_description。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：resource/path loading×1; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 118. `scripts/player/warehouse/WarehouseStackState.cs`

- 复审状态：**需跟进**；规模：43 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：WarehouseStackState；主要方法：IsEmpty, DuplicateState, ToDictionary, FromDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 119. `scripts/player/warehouse/WarehouseState.cs`

- 复审状态：**需跟进**；规模：182 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：WarehouseState；主要方法：GetStacksTyped, GetNonEmptyStacksTyped, GetStackAt, AddStack, RemoveStackAt, ReplaceStacks, GetEquipmentInstancesTyped, GetNonEmptyEquipmentInstancesTyped, GetEquipmentInstanceAt, AddEquipmentInstance。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; save/schema/projection×6; runtime mutation collections×20。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 120. `scripts/player/warehouse/WarehouseStateItemValidator.cs`

- 复审状态：**通过**；规模：137 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：WarehouseStateItemValidator；主要方法：Validate, _validate_stacks, _validate_equipment_instances, _get_item_def。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×20。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 121. `scripts/player/warehouse/WeaponDamageDiceDef.cs`

- 复审状态：**需跟进**；规模：85 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：WeaponDamageDiceDef；主要方法：ValidateDice, DuplicateDice, GetDiceCount, GetDiceSides, ToRollLabel。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 122. `scripts/player/warehouse/WeaponProfileDef.cs`

- 复审状态：**需跟进**；规模：281 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：WeaponProfileDef, PropertyMergeMode；主要方法：MergeWithTemplate, DuplicateProfile, HasAttackRangeOverride, GetPropertiesTyped, Merge, MergeProfiles, NormalizePropertiesMode, _copy_profile_fields, _inherit_string_name, _inherit_dice。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×3; runtime mutation collections×8。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 123. `scripts/systems/attributes/AttributeService.cs`

- 复审状态：**需跟进**；规模：1126 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：AttributeIdKind, AttributeSourceKind, AttributeServiceKeyKind, AttributeService, struct, struct；主要方法：ToStringName, FromDictionary, ReadProtectedWriteFlag, Setup, SetupContext, InvalidateSnapshot, GetBaseValue, GetTotalValue, GetModifier, GetActionPoints。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; save/schema/projection×4; runtime mutation collections×57。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 124. `scripts/systems/attributes/AttributeSourceContext.cs`

- 复审状态：**通过**；规模：41 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：AttributeSourceContext；主要方法：SetEffectiveAgeStage。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 125. `scripts/systems/battle/_interop/BattleTypedEnums.cs`

- 复审状态：**需跟进**；规模：937 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleAiActionKind, BattleCommandKind, PendingCastBindingModeKind, PendingCastRefundPolicy, BattleEquipmentOperationKind, BattlePhaseKind, BattleModalStateKind, BattleUnitControlMode；主要方法：ToAiActionKind, ToStringName, ToCommandKind, ToPendingCastBindingMode, ToEquipmentOperationKind, ToPhaseKind, ToModalStateKind, ToControlMode, ToAreaPattern, GetAreaPatternDistanceContractBonus。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 126. `scripts/systems/battle/ai/BattleAiActionAssembler.cs`

- 复审状态：**需跟进**；规模：1281 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiActionAssembler, in, in；主要方法：BuildUnitActionPlan, GetBrainStates, ClassifyKnownActiveSkills, GetActions, GetGenerationSlots, SortGenerationSlots, CloneRuntimeActions, CloneAction, EnableRuntimeActionDefaults, SlotMatchesAffordance。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×45; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 127. `scripts/systems/battle/ai/BattleAiActionIntent.cs`

- 复审状态：**通过**；规模：69 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiIntent, BattleAiActionIntent；主要方法：ToKind, ToStringName, IsValid, DefaultFromSlotRole。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 128. `scripts/systems/battle/ai/BattleAiCandidateEvaluationService.cs`

- 复审状态：**通过**；规模：160 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiCandidateEvaluationService；主要方法：Setup, RegisterEvaluator, Evaluate, EvaluateMoveToRangeRequest, BuildMoveToRangeCommand, BuildMoveToRangeDecision, Fail, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 129. `scripts/systems/battle/ai/BattleAiCandidateRequest.cs`

- 复审状态：**通过**；规模：303 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiCandidateRequest, MoveToRangePathSearchBudget, MoveToRangeTacticalParams, MoveToRangeRuntimeMetadata；主要方法：SetMoveToRangeSections, TryValidateCommon, TryValidateMoveToRange, TryGetMoveToRangeSections, ValidatePathBudget, ValidateTacticalParams, IsEmpty, Clone。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 130. `scripts/systems/battle/ai/BattleAiContext.cs`

- 复审状态：**需跟进**；规模：1197 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiContext, struct, RuntimeActionMetadata；主要方法：Clone, ContainsKey, MergeFrom, ExportMetadata, IsMetadataEmpty, ToDictionary, FromTraceDictionary, FromPlanMetadata, ShouldMerge, MergeStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×2; runtime mutation collections×83; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 131. `scripts/systems/battle/ai/BattleAiDecision.cs`

- 复审状态：**通过**；规模：17 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiDecision；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 132. `scripts/systems/battle/ai/BattleAiDecisionCommitter.cs`

- 复审状态：**通过**；规模：186 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiDecisionCommitter, DecisionStatePatch；主要方法：BuildTypedStatePatch, AttachStatePatch, Commit, FromDecision, ApplyTo, SetBlackboardText。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 133. `scripts/systems/battle/ai/BattleAiDecisionEngine.cs`

- 复审状态：**需跟进**；规模：823 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiDecisionEngine, ScoreInputFacts, RuntimeActionResolution；主要方法：ChooseCommandImpl, IsBetterScoreInput, EvaluateAction, EvaluateCandidateAction, FailCandidateAction, DecideWithActionFallback, ResolveRuntimeActions, ForActions, ForWait, BuildWaitDecision。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; runtime mutation collections×7。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 134. `scripts/systems/battle/ai/BattleAiFailurePolicy.cs`

- 复审状态：**通过**；规模：178 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiFailureEvent, BattleAiFailurePolicy；主要方法：Reset, SetMode, ReportActionError, ReportContractError, ReportMutationViolation, Report, ShouldAbortProcess, AbortProcessNow, ConfiguredMode。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×13。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 135. `scripts/systems/battle/ai/BattleAiMoveToRangeCandidateEvaluator.cs`

- 复审状态：**需跟进**；规模：1131 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiMoveToRangeCandidateEvaluator, GroundAoeSetupMetrics, struct, UnitAreaIndex；主要方法：IsUseful, From, CountUnitsInArea, PopCount, EvaluateMoveToRangeRequest, ApplyGroundAoeSetupScore, GetBestGroundAoeSetupMetrics, BuildBestGroundAoeSetupMetrics, BuildSetupScore, BuildFastMovePreview。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; save/schema/projection×8; runtime mutation collections×16。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 136. `scripts/systems/battle/ai/BattleAiMutationGuard.cs`

- 复审状态：**需跟进**；规模：3584 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiMutationGuard, BattleAiMutationSnapshot, BattleUnitSnapshot, BattleStateFieldsSnapshot, BattleUnitFieldsSnapshot, KnownFieldSnapshot, BattleAiBlackboardSnapshot, LayeredBarrierFieldsSnapshot；主要方法：Capture, ValidateAndRestoreTyped, ValidateAndRestoreReportTyped, Empty, CaptureStable, Restore, BuildCellDictionary, RestoreUnits, StableCells, StableUnits。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×82; save/schema/projection×13; runtime mutation collections×206; hot path / lifecycle×8。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 137. `scripts/systems/battle/ai/BattleAiMutationViolation.cs`

- 复审状态：**通过**；规模：113 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiMutationViolationException, BattleAiMutationViolationReport；主要方法：BuildActionCallSite, BuildMessage。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 138. `scripts/systems/battle/ai/BattleAiPayloadGuard.cs`

- 复审状态：**需跟进**；规模：520 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiPayloadGuard；主要方法：ValidateNoForbiddenObject, AbortFailLoudProcessIfRequested, FailLoud, ActionError, MutationViolation, CommandIsValueObject, PreviewHasNoLiveState, ScoreInputHasNoLiveState, FindForbiddenInTypedMap, FindForbiddenInTypedObject。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; scene/node/signal contract×2; runtime mutation collections×12。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 139. `scripts/systems/battle/ai/BattleAiQueryService.cs`

- 复审状态：**需跟进**；规模：595 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiQueryService, struct, struct, SkillRecord；主要方法：Setup, SetupReadOnly, GetActorId, GetActorSnapshot, GetUnitSnapshot, GetLivingUnitSnapshotsTyped, TryGetSkillRecordTyped, IsUnitMovementBlocked, GetMapSize, DistanceFromAnchorToTarget。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×2; runtime mutation collections×48; hot path / lifecycle×3。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 140. `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs`

- 复审状态：**通过**；规模：632 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiRuntimeActionPlan, RuntimeActionMetadata, RuntimeActionExportMetadata；主要方法：SetSource, AddStateActions, AddAction, AddGeneratedActionTyped, GetActions, HasActionIdentityKey, TryGetSkillAffordances, HasState, IsEmptyState, SetActionMetadata。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×54。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 141. `scripts/systems/battle/ai/BattleAiSafetyGate.cs`

- 复审状态：**通过**；规模：75 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiSafetyGate；主要方法：IsEligible, GetRejectionReason。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 142. `scripts/systems/battle/ai/BattleAiScoreContextAdapter.cs`

- 复审状态：**通过**；规模：278 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreContextAdapter；主要方法：Setup, BuildActionScoreInput, BuildSkillScoreInput, ValidateCommandActorMatch, ValidateCommandSkillMatch, ValidateCommandPreviewMetadata, ValidateScoreInput, StripRuntimeSkillResource, ResolveSkillDef, Fail。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 143. `scripts/systems/battle/ai/BattleAiScoreInput.cs`

- 复审状态：**需跟进**；规模：1455 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreInput；主要方法：Seal, IsSealed, MatchesSealedFingerprint, IsEmergencySurvivalScore, ToDictionary, FingerprintTypedState, AppendNamedCommandFingerprint, CopyCommandSlotIds, AppendNamedValueFingerprint, AppendValueFingerprint。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×29; save/schema/projection×22; runtime mutation collections×82。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 144. `scripts/systems/battle/ai/BattleAiScoreOrdering.cs`

- 复审状态：**通过**；规模：60 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreOrdering；主要方法：IsBetter。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 145. `scripts/systems/battle/ai/BattleAiScoreProfile.cs`

- 复审状态：**需跟进**；规模：321 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiMeteorFriendlyFireProfile, BattleAiScoreProfile；主要方法：SetActionBaseScores, SetBucketPriorities, GetActionBaseScore, GetBucketPriority, ToMeteorFriendlyFireProfile, ToStringName, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; save/schema/projection×1; runtime mutation collections×12。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 146. `scripts/systems/battle/ai/BattleAiScoreRuntimeMetadata.cs`

- 复审状态：**需跟进**；规模：486 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreRuntimeMetadata；主要方法：Clone, IsEmpty, ToDictionary, FromMetadata, FromRuntimeActionExportMetadata, MergeMetadata, MergeStringName, MergeNullableInt, MergeNullableVector, HasKey。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×1; runtime mutation collections×19。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 147. `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`

- 复审状态：**需跟进**；规模：1378 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, TargetEffectMetrics, TargetRoleSummary, struct；主要方法：Clone, FromEffect, ResolveMeteorUseCase, RecordMeteorHighPriorityTarget, ResolveMeteorHighPriorityReasons, ResolveComponentExpectedDamage, ResolveTargetRoleSummary, IsMeteorEliteOrBossTarget, ResolveMeteorThreatRank, ResolveMeteorThreatRankImpl。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; save/schema/projection×18; runtime mutation collections×47; hot path / lifecycle×3。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 148. `scripts/systems/battle/ai/BattleAiScoreService.Helpers.cs`

- 复审状态：**通过**；规模：409 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService；主要方法：AppendUniqueStringName, CopyStringNameArray, DistanceFromAnchorToUnit, DistanceFromAnchorToUnitCached, BuildPositionObjectiveScore, BuildDistanceBandProgressScore, BuildDistanceGap, BuildDistanceBandAbsoluteScore, ResolveActionBaseScore, ResolveActionTargetCount。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×21。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 149. `scripts/systems/battle/ai/BattleAiScoreService.Position.cs`

- 复审状态：**需跟进**；规模：675 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, HitRatePreviewEstimate；主要方法：ResolveEstimatedPercent, FromPreviewData, ResolveTargetRoleThreatMultiplierBasisPoints, ResolveTargetRoleThreatMultiplierBasisPointsImpl, CollectRoleThreatEffectDefs, IsHealOrSupportSkill, IsControlSkill, IsDamageSkill, GetUnitSkillLevel, GetPreResistanceDamageMultiplier。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; runtime mutation collections×16; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 150. `scripts/systems/battle/ai/BattleAiScoreService.Projection.cs`

- 复审状态：**需跟进**；规模：486 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, ThreatProjection, ThreatSkillEntry, ThreatProfile；主要方法：ShouldPopulateSurvivalProjection, GetCurrentActorThreatProjection, GetProjectedActorThreatProjection, SubtractSuppressedThreatsFromProjection, BuildProjectionSuppressionSignature, EmptyThreatProjection, ResolveProjectedActorCoord, ResolveActorSurvivalBudget, BuildSuppressedThreatUnitIds, CollectActorThreatProjection。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×24。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 151. `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`

- 复审状态：**需跟进**；规模：1384 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, DamageEstimateResult, DamageSaveEstimate, DamageEstimateBreakdown, DamagePreviewSnapshot, PathStepHitCountEntry；主要方法：Clone, Scaled, ToDictionary, FromPreviewSaveEstimate, FromPreviewResult, CloneSaveEstimates, CloneDamageEstimates, CloneTraceObjectList, CloneTraceObject, CloneTraceEnumerable。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×254; runtime mutation collections×79。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 152. `scripts/systems/battle/ai/BattleAiScoreService.cs`

- 复审状态：**需跟进**；规模：859 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, struct, struct, struct, ScoreBuildMetadata, ScoreRandomChainMetadata, ScorePositionMetadata, ScorePathStepAoeMetadata；主要方法：FromMetadata, Setup, SetProfile, BeginDecisionScope, EndDecisionScope, GetProfile, GetBucketPriority, BuildSkillScoreInput, BuildActionScoreInput, ResolvePrimaryCoord。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×46; hot path / lifecycle×13。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 153. `scripts/systems/battle/ai/BattleAiService.cs`

- 复审状态：**需跟进**；规模：231 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiService；主要方法：Setup, SetScoreProfile, GetScoreProfile, GetScoreService, ChooseCommand, ChooseCommandImpl, BuildActionMutationCheckpoint, AbortMutationViolation, BuildWaitDecision, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3; hot path / lifecycle×4。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 154. `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`

- 复审状态：**需跟进**；规模：395 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiSkillAffordanceClassifier；主要方法：ClassifySkill, ClassifyOptions, ClassifySelectionMode, ClassifyEffectsAndTargetMode, ResolveTeamIntent, CollectEffectDefs, IsEffectUnlockedForLevel, IsDamageEffect, IsHealEffect, IsControlEffect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×4; runtime mutation collections×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 155. `scripts/systems/battle/ai/BattleAiSkillAffordanceRecord.cs`

- 复审状态：**通过**；规模：87 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiSkillAffordanceRecord；主要方法：Empty, Clone, AddEffectRole, AddAffordance, AddActionFamily, AddVariantId, HasActionFamily, HasAnyActionFamily, AddUnique。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 156. `scripts/systems/battle/ai/BattleAiStateResolver.cs`

- 复审状态：**需跟进**；规模：444 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiStateResolver, TransitionResult, TransitionConditionTrace；主要方法：ResolveTyped, GetPreviousStateId, ResolveCurrentStateId, GetSortedRules, RuleAppliesToState, RuleMatches, ConditionMatches, GetUnitState, GetBattleState, HasAllyAtOrBelowHpBasisPoints。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×30。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 157. `scripts/systems/battle/ai/BattleAiTurnTraceProjection.cs`

- 复审状态：**通过**；规模：340 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiTurnTraceProjection, BattleAiTraceTransitionProjection, BattleAiTraceTransitionConditionProjection, BattleAiTraceUnitSnapshotProjection, BattleAiTraceUnitResultProjection, BattleAiTraceExecutionResultProjection；主要方法：Clone, CloneActionTraces, CloneSnapshots。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×48。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 158. `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`

- 复审状态：**需跟进**；规模：608 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiTypedActionHelper；主要方法：ResolveKnownSkillIds, GetSkillDef, GetSkillCastBlockReason, SortTargetUnits, GetUnitCastVariants, CollectUnitSkillEffectDefs, GetCastVariantTargetModeKind, BuildUnitSkillCommand, CreateDecision, CreateScoredDecision。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×26。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 159. `scripts/systems/battle/ai/BattleAiUnitSkillCandidateEvaluator.cs`

- 复审状态：**需跟进**；规模：532 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiUnitSkillCandidateEvaluator；主要方法：Evaluate, HasExplicitDistanceContract, FormatSkillVariantLabel, BuildSkillScoreInput, PassesFriendlyFireLimits, BuildFastUnitSkillPreview, BeginActionTrace, TraceCountIncrement, TraceAddBlockReason, OfferCandidate。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×20。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 160. `scripts/systems/battle/ai/BattleAiUnitSnapshot.cs`

- 复审状态：**需跟进**；规模：274 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiUnitBlackboardSnapshot, BattleAiUnitSnapshot；主要方法：FromBlackboard, ToPayload, AddBool, FromUnit, CopyVector2IArray, CopyStringNameArray, CopyStringNameList, ToVector2IArray, ToStringNameArray, ToIntDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; runtime mutation collections×18。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 161. `scripts/systems/battle/ai/IBattleAiScoreContext.cs`

- 复审状态：**通过**；规模：11 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：IBattleAiScoreContext；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 162. `scripts/systems/battle/ai/MoveToRangeScoreOrdering.cs`

- 复审状态：**通过**；规模：347 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：MoveToRangeScoreOrdering, FactIndex；主要方法：IsBetterCandidate, IsBetterScore, ComparePostActionSurvivalRisk, CompareNonfatalPostActionSurvivalRisk, GetDistanceGap, Get。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 163. `scripts/systems/battle/core/AttackCheckInput.cs`

- 复审状态：**通过**；规模：100 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AttackCheckInput；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 164. `scripts/systems/battle/core/AttackContext.cs`

- 复审状态：**通过**；规模：51 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AttackContext；主要方法：AddAttackRollOverride, TryConsumeAttackRollOverride。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 165. `scripts/systems/battle/core/AttackEffectResolutionResult.cs`

- 复审状态：**需跟进**；规模：1175 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AttackResolutionKind, CriticalSourceKind, ExecuteOutcomeKind, MitigationTierKind, DamageDiceMaxReasonKind, ReportEntryKind, AttackEffectResolutionResult, DamageEventResult；主要方法：ReadArray, ReadDictionary, ReadInt, ReadString, ReadStringName, ReadResolverResult, BuildGodotPayload, ParseAttackResolution, ParseCriticalSource, ParseExecuteOutcome。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×68; save/schema/projection×36; runtime mutation collections×37。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 166. `scripts/systems/battle/core/AttackPreviewData.cs`

- 复审状态：**需跟进**；规模：199 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, AttackPreviewData；主要方法：SetAttackRollModifierBreakdown, SetAttackRollModifierBreakdownPayload, BuildAttackRollModifierBreakdownPayload, AddAttackRollModifierBreakdownPayloadDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; save/schema/projection×1; runtime mutation collections×13。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 167. `scripts/systems/battle/core/AttackResolutionMetadata.cs`

- 复审状态：**通过**；规模：29 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AttackResolutionMetadata；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 168. `scripts/systems/battle/core/AttackRollResult.cs`

- 复审状态：**通过**；规模：35 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AttackRollResult；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 169. `scripts/systems/battle/core/AttackTraitTriggerResult.cs`

- 复审状态：**通过**；规模：55 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AttackTraitTriggerResult；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 170. `scripts/systems/battle/core/BattleAiBlackboard.cs`

- 复审状态：**需跟进**；规模：291 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleAiBlackboard；主要方法：ContainsKey, has, Remove, get, GetStringName, GetInt, GetBool, SetStringName, SetText, SetInt。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×14。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 171. `scripts/systems/battle/core/BattleAttackCheckPolicyContext.cs`

- 复审状态：**通过**；规模：19 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleAttackCheckPolicyContext；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 172. `scripts/systems/battle/core/BattleAttackRollModifierBundle.cs`

- 复审状态：**需跟进**；规模：54 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleAttackRollModifierBundle；主要方法：IsEmpty, AddSpec, GetEffectiveModifierDelta, BuildBreakdownPayload, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1; runtime mutation collections×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 173. `scripts/systems/battle/core/BattleAttackRollModifierSpec.cs`

- 复审状态：**需跟进**；规模：501 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleAttackRollModifierStackMode, BattleAttackRollModifierEndpointMode, BattleAttackRollModifierFootprintMode, BattleAttackRollModifierApplyTarget, BattleAttackRollModifierSpec；主要方法：ToDictionary, ToPartialDictionary, ToDictionaryWithEffectiveModifierDelta, Clone, BuildDictionary, FromDictionary, FromPartialDictionary, ToStackMode, ToStringName, ToEndpointMode。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×15; save/schema/projection×3; runtime mutation collections×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 174. `scripts/systems/battle/core/BattleBarrierInstanceState.cs`

- 复审状态：**需跟进**；规模：231 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleBarrierInstanceState；主要方法：FromRuntimeDict, GetLayersTyped, SetLayers, ToRuntimeDict, LayerArray, HasKey, ReadString, ReadStringName, ReadInt, ReadBool。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×3; runtime mutation collections×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 175. `scripts/systems/battle/core/BattleBarrierLayerState.cs`

- 复审状态：**需跟进**；规模：250 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleBarrierLayerState；主要方法：FromRuntimeDict, SetBlockedCategories, SetBreakerSkillIds, GetPassageOutcomesTyped, SetPassageOutcomes, ToRuntimeDict, ReplaceStringNameList, StringArray, OutcomeArray, HasKey。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×22; save/schema/projection×6; runtime mutation collections×21。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 176. `scripts/systems/battle/core/BattleBarrierOutcomeState.cs`

- 复审状态：**需跟进**；规模：93 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleBarrierOutcomeState；主要方法：FromRuntimeDict, ToRuntimeDict, ReadStringName, ReadInt, ReadBool。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×9。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 177. `scripts/systems/battle/core/BattleCellState.cs`

- 复审状态：**需跟进**；规模：709 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleCellState；主要方法：ClearOccupant, RecalculateRuntimeValues, SetBaseTerrain, SetHeightOffset, GetEdgeFeature, SetEdgeFeature, ClearEdgeFeature, DuplicateCell, ToDictionary, FromDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×60; save/schema/projection×16; runtime mutation collections×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 178. `scripts/systems/battle/core/BattleCommand.cs`

- 复审状态：**需跟进**；规模：121 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleCommand；主要方法：IsMove, IsSkill, IsWait, IsChangeEquipment, IsCancelCast, SetEquipmentOccupiedSlotIds, SetTargetUnitIds, ClearTargetUnitIds, AddTargetUnitId, SetTargetCoords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×16。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 179. `scripts/systems/battle/core/BattleCommonSkillOutcome.cs`

- 复审状态：**需跟进**；规模：90 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleCommonSkillOutcome；主要方法：AddChangedUnitId, AddChangedCoord, AddDefeatedUnitId, AddTargetResult, AddStatusEffectIds, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 180. `scripts/systems/battle/core/BattleEdgeFaceState.cs`

- 复审状态：**需跟进**；规模：59 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleEdgeFaceState；主要方法：HasDropFace, HasFeatureFace, HasAnyFace, BlocksMove, BlocksOccupancy。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 181. `scripts/systems/battle/core/BattleEdgeFeatureState.cs`

- 复审状态：**需跟进**；规模：372 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleEdgeFeatureKind, BattleEdgeRenderKind, BattleEdgeInteractionKind, BattleEdgeFeatureState；主要方法：IsEmpty, DuplicatesRenderOf, ToStringName, ToFeatureKind, ToRenderKind, ToInteractionKind, DuplicateFeature, ToDictionary, FromDictionary, MakeNone。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 182. `scripts/systems/battle/core/BattleEventBatch.cs`

- 复审状态：**需跟进**；规模：263 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleEventBatch；主要方法：SetChangedUnitIds, ClearChangedUnitIds, AddChangedUnitId, ContainsChangedUnitId, SetChangedCoords, ClearChangedCoords, AddChangedCoord, ContainsChangedCoord, SetLogLines, ClearLogLines。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×26; runtime mutation collections×35。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 183. `scripts/systems/battle/core/BattleLootConstants.cs`

- 复审状态：**通过**；规模：135 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleLootDropKind, BattleLootSourceKind, BattleLootSourceIdKind, BattleLootSpecialItemKind, BattleLootIds；主要方法：ToStringName, ToDropKind, ToSourceKind, ToSourceIdKind, ToSpecialItemKind。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 184. `scripts/systems/battle/core/BattlePendingCastState.cs`

- 复审状态：**通过**；规模：78 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattlePendingCastState；主要方法：SetTargetUnitIds, SetTargetCoords, RemoveTargetUnitId, Clone。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×9。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 185. `scripts/systems/battle/core/BattlePreview.cs`

- 复审状态：**需跟进**；规模：212 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattlePreview；主要方法：SetTargetUnitIds, ClearTargetUnitIds, AddTargetUnitId, ContainsTargetUnitId, SetTargetCoords, ClearTargetCoords, AddTargetCoord, ContainsTargetCoord, SetRandomChainCandidateUnitIds, ClearRandomChainCandidateUnitIds。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×18; save/schema/projection×1; runtime mutation collections×26。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 186. `scripts/systems/battle/core/BattleRepeatAttackStageSpec.cs`

- 复审状态：**需跟进**；规模：277 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRepeatAttackStageSpec；主要方法：FromRepeatAttackEffect, ResolveStageAttackPenalty, WithBaseResourceCost, WithFateAware, ResolveResourceCostForStage, ResolvePenaltyFreeStages, ReadDictionary, ReadInt, ReadFloat, ReadBool。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×15。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 187. `scripts/systems/battle/core/BattleResolutionResult.cs`

- 复审状态：**需跟进**；规模：564 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleResolutionResult；主要方法：IsEmpty, GetConvertedCalamityShards, SetLootEntries, SetOverflowEntries, SetPendingCharacterRewards, ToDictionary, HasExactFields, NormalizeFormalDropEntryPayload, CreateBaseFormalDropEntry, NormalizeDropEntryOptions。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×54; save/schema/projection×21; runtime mutation collections×10。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 188. `scripts/systems/battle/core/BattleSpecialProfileGateResult.cs`

- 复审状态：**需跟进**；规模：50 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpecialProfileGateResult；主要方法：ToDictionary, ToVariantBoundaryValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 189. `scripts/systems/battle/core/BattleSpecialProfilePreviewFacts.cs`

- 复审状态：**需跟进**；规模：174 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpecialProfilePreviewFacts；主要方法：GetFriendlyFireNumericSummary, GetAttackRollModifierBreakdown, ToStringNameArray, ToVector2IArray, ToTraceStringNameList, ToTraceVector2IList, ToTraceNumericSummaryList, ToTraceAttackRollModifierBreakdownList。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×2; runtime mutation collections×23。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 190. `scripts/systems/battle/core/BattleState.cs`

- 复审状态：**需跟进**；规模：489 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleState, BattleCellEntry, BattleUnitEntry；主要方法：IsStrongAttackDisadvantageStatusId, StrongAttackDisadvantageStatusIdsTyped, MarkMovementGeometryChanged, ResetLogEntries, ClearLogEntries, AppendLogEntry, GetLogTextByteSize, NextAttackRollNonce, AllocateCastSequence, GetLogBudgetSummaryText。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×18; save/schema/projection×1; runtime mutation collections×22。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 191. `scripts/systems/battle/core/BattleStatusEffectState.cs`

- 复审状态：**需跟进**；规模：1032 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleStatusEffectState；主要方法：IsEmpty, HasDuration, TryGetHealMultiplierPercentTyped, TryGetShieldGainMultiplierPercentTyped, TryGetAttackRollPenaltyTyped, CreateOrDuplicate, DuplicateState, ToDictionary, FromDictionary, BuildParamsProjection。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×42; save/schema/projection×14; runtime mutation collections×29。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 192. `scripts/systems/battle/core/BattleTimelineState.cs`

- 复审状态：**需跟进**；规模：187 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleTimelineState；主要方法：clear, DuplicateState, ToDictionary, FromDictionary, StringNameArrayToStrings, HasExactSchemaFields, StringsToStringNameArray, TryGetStrictInt, TryReadBoolField, TryGetRawArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×25; save/schema/projection×2; runtime mutation collections×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 193. `scripts/systems/battle/core/BattleUnitState.cs`

- 复审状态：**需跟进**；规模：2314 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleWeaponProfileKind, BattleWeaponGripKind, BattleUnitState；主要方法：CreateDefaultUnlockedCombatResourceProjection, IsValidCombatResourceId, ToStringName, ToWeaponProfileKind, ToWeaponGripKind, HasPendingCast, IsCasting, SetPendingCast, ClearPendingCast, ClearCastingTurnFlags。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×99; save/schema/projection×42; runtime mutation collections×41。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 194. `scripts/systems/battle/core/CombatResourceKind.cs`

- 复审状态：**通过**；规模：89 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：CombatResourceKind, CombatResourceKindUtils；主要方法：FromStringName, ToStringName, ToLabel, ToAbbr, ToEffectiveCostField, ToCurrentUnitField。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 195. `scripts/systems/battle/core/SkillCostTransaction.cs`

- 复审状态：**通过**；规模：44 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：SkillCostTransaction；主要方法：Clone, CooldownOnly。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 196. `scripts/systems/battle/core/WeaponDice.cs`

- 复审状态：**需跟进**；规模：74 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：WeaponDice；主要方法：DuplicateState, IsEmpty, ToDictionary, FromDictionary, FromResource, GetInt, FromValues。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 197. `scripts/systems/battle/core/WeaponProjection.cs`

- 复审状态：**需跟进**；规模：59 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：WeaponProjection；主要方法：DuplicateState, IsEmpty, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; save/schema/projection×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 198. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmCastContext.cs`

- 复审状态：**通过**；规模：21 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmCastContext；主要方法：HasDrift。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 199. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmCommitResult.cs`

- 复审状态：**需跟进**；规模：39 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmCommitResult；主要方法：AddChangedUnitId, AddChangedCoord, AddDefeatedUnitId。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 200. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmComponentFact.cs`

- 复审状态：**需跟进**；规模：66 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmComponentFact；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 201. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmImpactComponent.cs`

- 复审状态：**需跟进**；规模：84 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmImpactComponent；主要方法：AppliesToDistance, GetDamageScale, GetAverageBaseDamage, GetWorstCaseBaseDamage。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 202. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmNumericSummary.cs`

- 复审状态：**需跟进**；规模：726 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmHostileTerrainConsequence, MeteorSwarmComponentBreakdownEntry, MeteorSwarmNumericSummary；主要方法：ToDictionary, FromDictionary, ReadInt, ReadString, ReadBool, TryRead, ToDictionaryArray, ReadComponents, BuildComponentBreakdownPayload, BuildTraceComponentBreakdownList。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×51; save/schema/projection×83; runtime mutation collections×65。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 203. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmPreviewFacts.cs`

- 复审状态：**需跟进**；规模：87 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmPreviewFacts；主要方法：GetTargetNumericSummariesTyped, GetFriendlyFireNumericSummariesTyped, ToDictionaryArray, ToTraceComponentFactList。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×4; runtime mutation collections×11。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 204. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmProfile.cs`

- 复审状态：**需跟进**；规模：72 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmProfile；主要方法：GetTerrainProfilesForRing, _get_int。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 205. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTargetOutcome.cs`

- 复审状态：**需跟进**；规模：61 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmTargetOutcome；主要方法：AddComponent, AddStatusEffectId, ToSummaryDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×1; runtime mutation collections×10。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 206. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTargetPlan.cs`

- 复审状态：**需跟进**；规模：78 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmTargetPlan；主要方法：GetDistanceForUnit, GetPrimaryCoordForUnit, GetRingForCoord, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×1; runtime mutation collections×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 207. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTerrainEffectFact.cs`

- 复审状态：**通过**；规模：12 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmTerrainEffectFact；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 208. `scripts/systems/battle/core/meteor_swarm/MeteorSwarmTerrainSummaryFact.cs`

- 复审状态：**需跟进**；规模：42 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：MeteorSwarmTerrainSummaryFact；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 209. `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifest.cs`

- 复审状态：**通过**；规模：38 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpecialProfileManifest；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 210. `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifestValidator.cs`

- 复审状态：**需跟进**；规模：576 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpecialProfileManifestValidator；主要方法：ValidateManifest, ValidateMeteorSwarmProfile, _append_impact_component_errors, _append_special_skill_effect_surface_errors, _append_forbidden_fallback_errors, _append_terrain_profile_errors, _append_accuracy_modifier_spec_errors, TryGetValue, ReadStringName, TryReadInt。
- Godot/公开边界：Export 1 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×26; resource/path loading×2; runtime mutation collections×61。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 211. `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs`

- 复审状态：**需跟进**；规模：305 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpecialProfileRegistry；主要方法：SetManifestDirectory, Rebuild, Validate, GetManifest, GetManifestForSkill, HasProfile, GetSnapshot, ValidateTyped, RegisterManifestResource, AppendProfileResourcePathErrors。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×14; resource/path loading×2; runtime mutation collections×34。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 212. `scripts/systems/battle/fate/BattleFateAttackRules.cs`

- 复审状态：**通过**；规模：64 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleFateAttackRules；主要方法：DoesAttackRollHit, DoesGateDieCrit, IsHighThreatCritRoll, IsAttackCritLocked, UnitHasCritLockStatus。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 213. `scripts/systems/battle/fate/BattleFateEventBus.cs`

- 复审状态：**需跟进**；规模：98 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleFateEventBus；主要方法：dispatch, MakeReadOnlyValue, ToVariant。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; scene/node/signal contract×3; runtime mutation collections×2; string dynamic dispatch×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 214. `scripts/systems/battle/fate/FateAttackFormula.cs`

- 复审状态：**通过**；规模：96 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：FateAttackFormula, IRollSource, GodotRandomRollSource；主要方法：CalcCritGateDieSize, CalcFumbleLowEnd, CalcCombatLuckScore, CalcCritThreshold, RollDieWithDisadvantageRule, RandiRange, CreateRandomizedRng。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：randomness/determinism×11。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 215. `scripts/systems/battle/fate/FateRuntimeModule.cs`

- 复审状态：**需跟进**；规模：717 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：FateRuntimeModule；主要方法：Setup, DisposeRuntime, BeginBattle, GetMemberCalamity, GetMemberCalamityCap, GetBlackStarBrandCastCost, HasMisfortuneReason, GetMisfortuneSkillCastBlockReason, ConsumeMisfortuneSkillCastResult, HandleMisfortuneTrigger。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×79; save/schema/projection×7; runtime mutation collections×22。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 216. `scripts/systems/battle/fate/FortunaGuidanceService.cs`

- 复审状态：**通过**；规模：265 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：FortunaGuidanceEventInput, FortunaChapterCompletionInput, FortunaGuidanceService；主要方法：Setup, Dispose, HandleFateEvent, HandleBattleResolution, HandleChapterCompleted, HandleCriticalSuccessUnderDisadvantage, HandleHighThreatCriticalHit, HandleHardshipSurvival, MarkChapterEventSeen, UnlockAchievement。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×14。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 217. `scripts/systems/battle/fate/FortuneService.cs`

- 复审状态：**通过**；规模：149 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：FortuneMarkEventInput, FortuneService, SeededGodotRollSource；主要方法：Setup, Dispose, HasAttemptedFortuneMark, TryGrantFortuneMark, BuildFortuneMarkAttemptFlagId, ResolveRollSource, BuildConfirmationSeedSource, GetPartyState, GetMemberState, GetCustomStatValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：randomness/determinism×4。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 218. `scripts/systems/battle/fate/LowLuckEventService.cs`

- 复审状态：**需跟进**；规模：823 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：LowLuckFateEventPayload, LowLuckSettlementActionInput, struct, LowLuckBattleResolutionInput, struct, LowLuckEventResult, LowLuckEventKind, LowLuckEventService；主要方法：AddTriggeredEventId, AddLootEntry, AddPendingCharacterReward, ToStringName, Setup, HandleFateEvent, HandleBattleResolution, HandleSettlementAction, Dispose, TrackHardshipSurvival。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×48。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 219. `scripts/systems/battle/fate/MisfortuneGuidanceService.cs`

- 复审状态：**需跟进**；规模：467 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：MisfortuneForgeGuidanceItemEntry, MisfortuneForgeGuidanceInput, MisfortuneGuidanceService；主要方法：NormalizeEntries, Setup, BindBattleRuntimeGateway, Dispose, HandleBattleResolution, HandleForgeResult, HandleForgeResultCore, ClearExaltedReadyFlags, MarkExaltedReadyFlags, MemberHadDevoutAdversity。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×5; runtime mutation collections×25。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 220. `scripts/systems/battle/fate/MisfortuneService.cs`

- 复审状态：**需跟进**；规模：862 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：MisfortuneSkillKind, MisfortuneService, struct；主要方法：ToStringName, IsMisfortuneGatedSkill, GetSkillSidecarMissingMessage, GetSkillDefaultBlockMessage, TryGetSkillGateRule, Setup, BeginBattle, BindFateEventBus, GetMemberCalamity, GetMemberCalamityCap。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×65; runtime mutation collections×43。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 221. `scripts/systems/battle/fate/MisfortuneSkillCastResult.cs`

- 复审状态：**通过**；规模：28 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：struct；主要方法：Success, Failure。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 222. `scripts/systems/battle/presentation/BattleHudAdapter.cs`

- 复审状态：**需跟进**；规模：2425 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleHudAdapter, EquipmentPreviewRule；主要方法：EQUIPMENT_PREVIEW_DEFAULT_FAILURE_MESSAGE, SetupRuntimeContext, BuildSnapshot, BuildHoverPreview, FormatSelectedSkillHitBadgeText, BuildHoverTargetUnitSnapshot, BuildHeaderSubtitle, BuildRoundBadge, BuildQueueEntries, BuildFocusUnitSnapshot。
- Godot/公开边界：Export 0 处；Signal 6 处；风险触点：Godot Dictionary/Array boundary×121; save/schema/projection×8; runtime mutation collections×81。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 223. `scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs`

- 复审状态：**需跟进**；规模：814 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleAttackCheckPolicyService；主要方法：Setup, Dispose, BuildModifierBundle, BuildAttackContext, BuildAttackCheck, BuildAttackPreview, BuildRepeatAttackPreview, BuildRepeatAttackStageContext, BuildRepeatAttackStageHitCheck, BuildFateAwareRepeatAttackStageHitCheck。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×36。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 224. `scripts/systems/battle/rules/BattleDamagePreviewRangeService.cs`

- 复审状态：**需跟进**；规模：257 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamagePreviewRangeService, struct, struct, struct, struct, struct；主要方法：FromEffect, ToDictionary, DamageRangesToArray, BuildSkillDamagePreview, FormatDamageRangeText, BuildDamageEffectRange, BuildSkillDiceRange, BuildWeaponDiceRange, BuildDiceRange, ShouldAddWeaponDice。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×3; runtime mutation collections×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 225. `scripts/systems/battle/rules/BattleDamagePreviewResult.cs`

- 复审状态：**需跟进**；规模：469 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamagePreviewSaveEstimate, BattleDamagePreviewResult；主要方法：Create, None, ToDictionary, BuildSaveSourceArray, BuildTraceSaveSourceList, Empty, BuildSaveEstimateArray, CloneTraceObjectList, CloneTraceObject, CloneTraceEnumerable。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×18; save/schema/projection×130; runtime mutation collections×34。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 226. `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs`

- 复审状态：**需跟进**；规模：358 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamageResolver；主要方法：RollDamageDice, RollBonusDamageDice, RollWeaponDice, RollDicePool, RollDicePoolValues, RollDamageDieVirtual, BuildDicePoolTotal, BuildPreviewDiceRolls, BuildDamageDiceEventFlags, ApplyDamageDiceEventFlags。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×1; runtime mutation collections×2; string dynamic dispatch×1。
- 对抗性检视：
  - 检查 GodotObject.Call 字符串动态调用；能用 typed/virtual API 时避免运行时才发现重命名错误。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 227. `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`

- 复审状态：**需跟进**；规模：1363 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamageResolver, struct；主要方法：ApplyDispelMagicEffect, ApplyEquipmentDurabilityDamageEffect, ResolveEquipmentDurabilitySave, SelectEquipmentForDurabilityDamage, BuildEquipmentDurabilitySelection, GetEquipmentDurabilityTargetSlots, IsEquipmentDurabilityEntryAllowed, GetEquipmentDurabilitySlotWeight, GetEquipmentDurabilityWeightForSlot, ResolveExecuteEffect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×51; save/schema/projection×44; runtime mutation collections×31; randomness/determinism×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 228. `scripts/systems/battle/rules/BattleDamageResolver.Events.cs`

- 复审状态：**需跟进**；规模：397 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamageResolver；主要方法：ResolveAttackMetadata, ResolveSpellControlMetadata, BuildAttackMetadataResult, BuildAttackEffectContext, BuildTraitTriggerResultsArray, AttachAttackReportEntry, DispatchAttackResolutionEvents, DispatchSpellControlResolutionEvents, BuildAttackEventPayload, BuildSpellControlEventPayload。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×25; runtime mutation collections×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 229. `scripts/systems/battle/rules/BattleDamageResolver.Helpers.cs`

- 复审状态：**需跟进**；规模：368 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamageResolver；主要方法：CoerceEffectDefs, ToValueArray, DuplicateDictionary, GetSkillDefTyped, TryGet, GetDictionary, GetArray, GetInt, GetFloat, GetString。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×44; save/schema/projection×1; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 230. `scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs`

- 复审状态：**需跟进**；规模：657 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamageResolver；主要方法：ResolveMitigationTierResult, AppendDamageResistanceSources, StatusAppliesToDamageTag, IsPhysicalDamageTag, BuildFixedMitigation, ResolveBuffReductionResult, ResolveStanceReductionResult, ResolvePassiveReductionResult, ResolveContentDrResult, ResolveGuardBlockResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×62; runtime mutation collections×16。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 231. `scripts/systems/battle/rules/BattleDamageResolver.cs`

- 复审状态：**需跟进**；规模：2709 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamagePreviewRollMode, BattleDamagePreviewSaveMode, BattleDamagePreviewOptions, BattleDamageResolver, struct, struct, struct, struct；主要方法：FromEffect, ToDictionary, None, ToPreviewSaveEstimate, WithHpDamage, WithResolvedDamage, ToDamageApplicationInput, Create, FromDictionary, ReadBool。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×134; save/schema/projection×286; runtime mutation collections×48; randomness/determinism×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 232. `scripts/systems/battle/rules/BattleDeathResolutionRules.cs`

- 复审状态：**通过**；规模：32 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleDeathResolutionRules；主要方法：NormalFatalContext, PowerWordKillExecuteContext, IsPowerWordKillExecute。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 233. `scripts/systems/battle/rules/BattleEffectCategoryResolver.cs`

- 复审状态：**通过**；规模：64 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleEffectCategoryResolver；主要方法：ResolveCategories, AppendCategories, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×7。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 234. `scripts/systems/battle/rules/BattleEquipmentRequirementRules.cs`

- 复审状态：**通过**；规模：70 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleEquipmentRequirementRules；主要方法：UnitHasEquippedShield, UnitHasEquippedItemTag, ItemHasTag, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 235. `scripts/systems/battle/rules/BattleExecutionRules.cs`

- 复审状态：**需跟进**；规模：256 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, struct, BattleExecutionRules；主要方法：Defaults, FromEffect, Normalize, IsEmpty, ResolveThreshold, BuildExecutePlan, IsBossTarget, IsEliteOrBossTarget, ResolveNonLethalDamage, BuildSoulFractureParams。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; save/schema/projection×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 236. `scripts/systems/battle/rules/BattleHitResolver.cs`

- 复审状态：**需跟进**；规模：1717 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleHitResolver；主要方法：ResolveRepeatAttackStageHit, BuildRepeatAttackStageHitCheck, BuildFateAwareRepeatAttackStageHitCheck, BuildRepeatAttackPreview, BuildSkillAttackPreview, BuildForceHitNoCritAttackPreview, BuildSkillAttackCheck, _get_unit_attribute_value, _unit_has_attribute_value, _get_target_armor_class。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×24; runtime mutation collections×14; randomness/determinism×4。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 237. `scripts/systems/battle/rules/BattleRangeService.cs`

- 复审状态：**通过**；规模：505 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRangeService, UnitRangeInfo, struct；主要方法：GetWeaponAttackRange, UnitHasMeleeWeapon, UnitMatchesRequiredWeaponFamilies, GetEffectiveSkillRange, GetEffectiveSkillThreatRange, GetEffectiveSkillDistanceContractRange, RequiresCurrentMeleeWeapon, IsWeaponRangeSkill, ResolveBaseSkillRange, IsGroundJumpSkill。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 238. `scripts/systems/battle/rules/BattleReportFormatter.cs`

- 复审状态：**需跟进**；规模：899 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleReportFormatter, DamageResultSummary；主要方法：ToDictionary, FromDictionary, ReadInt, ReadString, ReadBool, ReadStringArray, ToStringArray, BuildAttackReportEntry, BuildSkillEventEntry, FormatMeteorSwarmSummary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×19; save/schema/projection×11; runtime mutation collections×25。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 239. `scripts/systems/battle/rules/BattleSaveResolver.cs`

- 复审状态：**需跟进**；规模：905 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSaveDegreeKind, struct, BattleSaveContext, struct, struct, BattleSaveResolver, BattleSaveTagState；主要方法：ToDictionary, ForSkill, WithSaveRollOverride, WithSaveRollOverrides, Empty, SourceArray, ResolveSaveResult, ResolveSaveDegree, EstimateSaveSuccessProbabilityResult, ResolveSaveDc。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×136; runtime mutation collections×17; randomness/determinism×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 240. `scripts/systems/battle/rules/BattleSkillResolutionRules.cs`

- 复审状态：**需跟进**；规模：738 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSkillResolutionPolicy, BattleSkillResolutionRules；主要方法：ToDictionary, ToStringNameArray, ToEffectArray, BuildSkillResolutionPolicy, NormalizeTargetUnitIds, ShouldRouteSkillCommandToUnitTargeting, GetSkillVariantCommandErrorMessage, ShouldResolveUnitSkillAsFateAttack, IsForceHitNoCritSkill, ResolveGroundCastVariant。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×7; runtime mutation collections×38。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 241. `scripts/systems/battle/rules/BattleStatusModifierRules.cs`

- 复审状态：**通过**；规模：168 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleStatusModifierRules, struct；主要方法：ApplyHealMultiplier, ApplyShieldGainMultiplier, ResolveHealMultiplierPercent, ResolveShieldGainMultiplierPercent, ResolveMinHealMultiplierPercent, ResolveMinShieldGainMultiplierPercent, BuildStatusModifierEntries, GetOptionalHealMultiplier, GetOptionalShieldGainMultiplier, ClampMultiplierPercent。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 242. `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`

- 复审状态：**需跟进**；规模：612 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, BattleStatusSemanticTable；主要方法：HasSemantic, IsHarmfulStatus, IsCleansableHarmfulStatus, BlocksPendingCast, IsDispellableHarmfulStatus, IsDispellableBeneficialStatus, IsDispellableHarmfulStatusEntry, IsDispellableBeneficialStatusEntry, GetDispelPriority, GetSemantic。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×3; runtime mutation collections×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 243. `scripts/systems/battle/rules/BattleTargetTeamRules.cs`

- 复审状态：**通过**；规模：83 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleTargetTeamRules, struct；主要方法：ResolveEffectTargetFilter, IsUnitValidForFilter, IsBeneficialFilter, IsEnemyFilter, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 244. `scripts/systems/battle/rules/BattleTemporalStatusService.cs`

- 复审状态：**需跟进**；规模：260 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：TemporalStatusReleaseKind, BattleTemporalStatusService；主要方法：HasTimeStasis, HasTimeSlow, HasTemporalCastBlock, GetActionProgressRatePercent, GetCastProgressRatePercent, ConsumeActionProgressGain, ConsumeCastProgressGain, IsTemporalStatusId, IsTemporalReleaseTargetStatusId, IsTemporalReleaseEffect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×4; runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 245. `scripts/systems/battle/runtime/AscensionTraitResolver.cs`

- 复审状态：**需跟进**；规模：151 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：AscensionTraitResolver；主要方法：ApplyToUnit, _apply_identity_def_projection, _initialize_racial_skill_charges, _append_unique_string_names。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 246. `scripts/systems/battle/runtime/BattleBarrierGeometryService.cs`

- 复审状态：**通过**；规模：125 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleBarrierGeometryService；主要方法：ClassifyFootprintTransition, LineCrossesBarrierArea, CoordInsideBarrier, FootprintOverlapsLookup, CoordLookup, LineCoords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 247. `scripts/systems/battle/runtime/BattleBarrierOutcomeResolver.cs`

- 复审状态：**需跟进**；规模：581 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleBarrierOutcomeResolver, struct, struct, struct；主要方法：FromOutcome, Setup, Dispose, ApplyPassageOutcomesResult, _ApplyOutcome, _ApplyDamageOutcome, _ApplyPoisonDeathOutcome, _ApplyStatusOutcome, _ApplyBanishOutcome, _ResolveOutcomeSave。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×61; runtime mutation collections×6; randomness/determinism×2。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 248. `scripts/systems/battle/runtime/BattleBarrierService.cs`

- 复审状态：**需跟进**；规模：684 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, struct, BattleBarrierService, struct；主要方法：Empty, FromEffect, Setup, Dispose, ApplyLayeredBarrierEffectResult, AdvanceBarrierDurations, ResolveUnitBoundaryCrossingResult, ResolveSkillBarrierInteractionResult, ResolveGroundBarrierInteractionResult, _ResolveProjectedEffectBarrierInteractionResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; save/schema/projection×14; runtime mutation collections×19。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 249. `scripts/systems/battle/runtime/BattleCastingTimeService.cs`

- 复审状态：**需跟进**；规模：720 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleCastingTimeService, struct；主要方法：Setup, Dispose, TryHandleCastingSkillStart, PreviewCancelCast, HandleCancelCast, ReconcilePendingCasts, AdvancePendingCasts, CompleteReadyPendingCasts, BuildStartPayload, ResolveCastingSpellControl。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; runtime mutation collections×9; randomness/determinism×2。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 250. `scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs`

- 复审状态：**需跟进**；规模：1271 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleChangeEquipmentResult, ChangeEquipmentRuleResult, BattleChangeEquipmentResolver；主要方法：Clone, ToDictionary, CloneStringNameList, StringifyStringNames, Setup, Dispose, PreviewCommand, HandleCommand, GetUnitHpMax, GetUnitStaminaMax。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×3; runtime mutation collections×32。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 251. `scripts/systems/battle/runtime/BattleChargeResolver.cs`

- 复审状态：**需跟进**；规模：1971 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleChargeResolver, struct, PathStepResult, ChargeBlockerResult, SidePushResult, TrapResult, ChargeTargetInfo；主要方法：FromEffect, Setup, DisposeRuntime, handle_charge_skill_command, handle_charge_skill_command_result, ValidateChargeCommandResult, BuildChargeStepAoePreviewCoords, GetChargePathStepAoeEffectDef, IsChargeOption, GetChargeEffectDef。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×36; save/schema/projection×1; runtime mutation collections×57。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 252. `scripts/systems/battle/runtime/BattleContributionEvent.cs`

- 复审状态：**需跟进**；规模：82 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleContributionRelation, BattleContributionOriginKind, BattleContributionEvent；主要方法：ToDictionary, RelationToString, OriginKindToString。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 253. `scripts/systems/battle/runtime/BattleContributionEventBuilder.cs`

- 复审状态：**需跟进**；规模：161 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleContributionEventBuilder；主要方法：FromUnits, FromDictionary, ResolveRelation, ParseRelation, ParseOriginKind, IsEmpty, ReadStringName, ReadInt, ReadBool, HasKey。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 254. `scripts/systems/battle/runtime/BattleContributionLedger.cs`

- 复审状态：**通过**；规模：24 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleContributionLedger；主要方法：Clear, Add。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 255. `scripts/systems/battle/runtime/BattleForcedMoveContext.cs`

- 复审状态：**需跟进**；规模：32 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct；主要方法：FromDirection, NormalizeAxisDirection。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 256. `scripts/systems/battle/runtime/BattleGroundEffectApplicationResult.cs`

- 复审状态：**需跟进**；规模：42 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, struct；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×3; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 257. `scripts/systems/battle/runtime/BattleGroundEffectService.cs`

- 复审状态：**需跟进**；规模：2664 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleGroundEffectService, struct, GroundUnitEffectResolution, EdgeAuthoringReference；主要方法：FromEffect, Setup, Dispose, append_result_report_entry, MarkAppliedStatusesForTurnTiming, append_result_source_status_effects, _record_effect_metrics, _record_unit_defeated, append_damage_result_log_lines, _build_skill_log_subject_label。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×60; save/schema/projection×4; runtime mutation collections×151。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 258. `scripts/systems/battle/runtime/BattleGroundSkillValidationResult.cs`

- 复审状态：**需跟进**；规模：176 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct；主要方法：FromDictionary, Denied, AllowedResult, ToTargetCoordsArray, ToPreviewCoordsArray, ToDictionary, ToVector2IArray, ReadAllowedFlag, ReadString, ReadInt。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; save/schema/projection×2; runtime mutation collections×10。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 259. `scripts/systems/battle/runtime/BattleLayeredBarrierService.cs`

- 复审状态：**通过**；规模：1 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleLayeredBarrierService；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 260. `scripts/systems/battle/runtime/BattleMagicBacklashResolver.cs`

- 复审状态：**需跟进**；规模：339 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMagicBacklashResolver；主要方法：ShouldResolveSpellControl, ApplySpellControlAfterCostResult, BuildGroundBacklashTargetCoordsResult, AppendGroundBacklashLog, ApplySpellCriticalBonus, ApplyFumbleProtectionMpDrain, CollectGroundAnchorDriftCandidates, GetFumbleProtectionUsed, SetFumbleProtectionUsed, GetMpMax。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×10; randomness/determinism×2。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 261. `scripts/systems/battle/runtime/BattleMagicBacklashResult.cs`

- 复审状态：**需跟进**；规模：282 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：class, struct, struct；主要方法：Empty, FromDictionary, ToDictionary, BoolField, IntField, StringNameField, IsEmpty, None, DictionaryField, TargetCoordsArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×23; save/schema/projection×8; runtime mutation collections×6。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 262. `scripts/systems/battle/runtime/BattleMeteorSwarmResolver.cs`

- 复审状态：**需跟进**；规模：1715 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMeteorSwarmResolver；主要方法：Setup, Dispose, PopulatePreview, BuildCastContextTyped, BuildPreviewFacts, BuildTargetPlanTyped, ResolveTyped, ResolveTarget, ApplyTerrainEffects, _build_damage_effect_def。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×51; save/schema/projection×46; runtime mutation collections×89。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 263. `scripts/systems/battle/runtime/BattleMetricsCollector.cs`

- 复审状态：**需跟进**；规模：383 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMetricEntry, BattleMetricsState, BattleMetricsCollector；主要方法：ToDictionary, IntMapToDictionary, Clear, Setup, Dispose, InitializeBattleMetrics, BuildUnitMetricEntry, EnsureUnitMetricEntry, EnsureFactionMetricEntry, RecordTurnStarted。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×8; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 264. `scripts/systems/battle/runtime/BattleMovePathResult.cs`

- 复审状态：**需跟进**；规模：111 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMovePathResult, BattleMovePathTreeResult, BattleValidatedMoveExecutionResult；主要方法：ToDictionary, ToPathArray, ToVector2IArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×15; save/schema/projection×8; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 265. `scripts/systems/battle/runtime/BattleMovementQueryService.cs`

- 复审状态：**需跟进**；规模：2370 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMovementQueryService, struct, struct, CellInfo, UnitInfo, EdgeInfo, struct, struct；主要方法：ToSnapshot, ForPathSearchBudget, Failure, Success, Setup, CollectReachableAnchors, CollectDistanceBandDestinations, CollectDistanceBandPathTargets, CollectDistanceBandPathTargetsTyped, CollectDistanceBandPathTargetsTypedImpl。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×7; runtime mutation collections×82; hot path / lifecycle×3。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 266. `scripts/systems/battle/runtime/BattleMovementService.cs`

- 复审状态：**通过**；规模：541 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMovementService, struct；主要方法：Setup, Dispose, RecordActionIssued, AppendChangedCoords, AppendChangedUnitCoords, SortCoords, IsMovementBlocked, HasStatus, GetUnitReachableMoveCoords, GetMoveCostForUnitTarget。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×26; hot path / lifecycle×1。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 267. `scripts/systems/battle/runtime/BattleRatingMemberStats.cs`

- 复审状态：**需跟进**；规模：100 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRatingMemberStats；主要方法：FromUnit, ToDictionary, IsEmpty, ReadInt, ReadString, ReadStringName, ReadDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×1; runtime mutation collections×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 268. `scripts/systems/battle/runtime/BattleRatingSystem.cs`

- 复审状态：**需跟进**；规模：512 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRatingSystem；主要方法：Setup, DisposeRuntime, InitializeBattleRatingStats, RecordSkillSuccess, RecordSkillEffectResult, RecordContributionFromUnits, RecordContributionEvent, RecordEnemyDefeatedAchievement, RecordBattleWonAchievements, FinalizeBattleRatingRewards。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×14; save/schema/projection×1; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 269. `scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs`

- 复审状态：**需跟进**；规模：928 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRepeatAttackResolver, struct；主要方法：FromEffect, GetStageDamageMultiplier, Setup, DisposeRuntime, ApplyRepeatAttackSkillResult, get_repeat_attack_effect_def, CollectRepeatAttackBaseEffects, BuildRuntimeStageSpec, BuildStageSpecFromRepeatAttackEffect, BuildStageSpecsFromRepeatAttackEffect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; runtime mutation collections×5。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 270. `scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs`

- 复审状态：**需跟进**；规模：596 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRuntimeLootResolver, ParsedDropDefinition；主要方法：ResolveWeakRef, Setup, Dispose, CollectDefeatedUnitLoot, BuildBattleResolutionResult, _IsEliteOrBossTarget, _CollectDefeatedUnitLoot, _ResolveEnemyTemplateForUnit, _BuildDefeatedUnitLootEntries, _ParseDropDefinition。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; save/schema/projection×5; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 271. `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

- 复审状态：**需跟进**；规模：5121 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRuntimeDictionaryOptions, BattleDefeatHandlingOptions, BattleStartOptions, BattleEndOptions, BattleStartFailureSnapshot, BattleRuntimeModule, in；主要方法：ReadBool, FromContext, FromDictionary, ToDictionary, ReadOptionalInt, ReadOptionalLong, ReadReachabilityPayload, ReadString, setup, FinishSetup。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×135; save/schema/projection×16; runtime mutation collections×190; hot path / lifecycle×19。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 272. `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`

- 复审状态：**需跟进**；规模：1802 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, BattleRuntimeSkillTurnResolver；主要方法：Empty, Setup, DisposeRuntime, ResolveTurnControlStatusResult, IsTurnAiOverrideActive, ClearTurnAiOverride, BuildMadnessFallbackCommand, GetSkillCastBlockReason, UnitHasMeleeWeapon, UnitMatchesRequiredWeaponFamilies。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×18; runtime mutation collections×17。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 273. `scripts/systems/battle/runtime/BattleShieldService.cs`

- 复审状态：**需跟进**；规模：536 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleShieldService；主要方法：ToDictionary, Setup, DisposeRuntime, ApplyUnitShieldEffectsResult, ApplyShieldEffectToTargetResult, _write_unit_shield, BuildUnitShieldResult, _resolve_shield_hp, ResolveShieldHp, _roll_shield_hp。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×4; runtime mutation collections×21; randomness/determinism×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 274. `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`

- 复审状态：**需跟进**；规模：3986 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSkillExecutionOrchestrator, struct, UnitSkillEffectResolution；主要方法：FromEffect, Setup, DisposeRuntime, append_result_report_entry, MarkAppliedStatusesForTurnTiming, append_result_source_status_effects, _record_action_issued, _record_skill_attempt, _record_effect_metrics, _record_unit_defeated。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×50; save/schema/projection×5; runtime mutation collections×115; hot path / lifecycle×17; randomness/determinism×4。
- 对抗性检视：
  - 检查 trace span 是否 exception-safe；Enter 后业务调用必须用 finally/guard 保证 Exit。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 275. `scripts/systems/battle/runtime/BattleSkillMasteryGrant.cs`

- 复审状态：**需跟进**；规模：75 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSkillMasteryGrant；主要方法：FromDictionary, ReadStringName, ReadString, ReadInt, ReadBool。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 276. `scripts/systems/battle/runtime/BattleSkillMasteryService.cs`

- 复审状态：**需跟进**；规模：951 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSkillMasteryService, SkillMasteryResultSnapshot, SkillMasteryResolutionEvent, SkillMasteryDamageEventSnapshot；主要方法：Clear, RecordTargetResult, RecordBonus, RecordMasteryAmount, ResolveActiveSkillMasteryAmount, ResolveMasteryRewardSkillId, BuildVajraBodyMasteryGrantTyped, BuildGuardMasteryGrantFromIncomingHitTyped, BuildBattleRatingMasteryRewardEntries, ResolveBattleRatingMasteryAmount。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×24; save/schema/projection×2; runtime mutation collections×19。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 277. `scripts/systems/battle/runtime/BattleSkillOutcomeCommitter.cs`

- 复审状态：**需跟进**；规模：245 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSkillOutcomeCommitter；主要方法：Setup, Dispose, CommitCommonOutcome, CommitMeteorSwarmResult, CommitTargetContributions, CommitStatusTurnTiming, CommitDefeatedUnits, GetUnit, IsEmpty, BuildCommonOutcomeFromMeteorResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 278. `scripts/systems/battle/runtime/BattleSpawnReachabilityService.cs`

- 复审状态：**需跟进**；规模：988 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpawnReachabilityOptions, BattleSpawnReachabilityResult, BattleSpawnReachabilityUnitResult, BattleSpawnReachabilityService, BattleSpawnReachabilityAttackSkill, BattleSpawnReachabilityAttackTarget, BattleSpawnReachabilitySearchResult, BattleSpawnReachabilityAttackMatch；主要方法：Invalid, AddInvalidEnemy, AddInvalidPlayer, ToDictionary, ToStringNameArray, IsEmpty, ValidateStateTyped, _ValidateAttackerUnit, _CollectLivingUnits, _CollectAttackSkills。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; save/schema/projection×3; runtime mutation collections×66。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 279. `scripts/systems/battle/runtime/BattleSpecialProfileGate.cs`

- 复审状态：**需跟进**；规模：289 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSpecialProfileGate, SpecialProfileGateSnapshot, struct；主要方法：Setup, PreflightSkill, PreviewSkill, CanExecuteSkill, EvaluateSkill, Block, Empty, FromDictionary, ReadDictionary, ReadStringList。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×4; runtime mutation collections×11。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 280. `scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs`

- 复审状态：**需跟进**；规模：1468 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleSpecialSkillResolver, struct, struct；主要方法：Empty, ToDictionary, FromStatus, Setup, Dispose, IsUnitValidForEffect, ApplySkillMasteryGrant, ApplySkillMasteryGrantTyped, AppendChangedCoords, AppendChangedUnitId。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×26; save/schema/projection×4; runtime mutation collections×48。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 281. `scripts/systems/battle/runtime/BattleTargetCollectionResult.cs`

- 复审状态：**需跟进**；规模：53 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleTargetCollectionResult；主要方法：HandledResult, UnhandledResult, ToDictionary, ToTargetCoordsArray, SortCoords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×1; runtime mutation collections×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 282. `scripts/systems/battle/runtime/BattleTargetCollectionService.cs`

- 复审状态：**通过**；规模：257 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleTargetCollectionService；主要方法：CollectCombatProfileTargetCoords, CollectSkillTargetCoords, CollectTargetCoords, IsSelfTargetCollection, CollectSelfTargetCoords, CollectTargetUnitCoords, GetEffectiveAreaPattern, GetEffectiveAreaValue, GridIsInside, GridGetAreaCoords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×8。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 283. `scripts/systems/battle/runtime/BattleTimelineDriver.cs`

- 复审状态：**需跟进**；规模：823 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleTimelineDriver；主要方法：Setup, Dispose, AdvanceTimeline, _RecordTurnStarted, _GetUnitStaminaMax, _AppendChangedUnitId, _CollectDefeatedUnitLoot, _ClearDefeatedUnit, _AdvanceUnitTurnTimers, _ApplyTurnStartStatuses。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×3; runtime mutation collections×19。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 284. `scripts/systems/battle/runtime/BattleUnitFactory.cs`

- 复审状态：**需跟进**；规模：1103 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleUnitFactory, AllyUnitDefaults, EnemyUnitDefaults, EnemyWeaponDefaults；主要方法：_snap, _csd, _gv, _sv, Setup, DisposeRuntime, GetCharacterGateway, GetTerrainGenerator, GetMemberState, GetMemberAttributeSnapshot。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×53; save/schema/projection×1; runtime mutation collections×37。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 285. `scripts/systems/battle/runtime/BattleUnitSkillValidationResult.cs`

- 复审状态：**需跟进**；规模：109 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct；主要方法：Denied, AllowedResult, ToTargetUnitIdsArray, ToTargetUnitsArray, ToRandomChainCandidateUnitIdsArray, ToPreviewCoordsArray, ToDictionary, ToStringNameArray, ToUnitArray, ToVector2IArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×1; runtime mutation collections×14。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 286. `scripts/systems/battle/runtime/IBattleRatingCharacterGateway.cs`

- 复审状态：**需跟进**；规模：91 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：IBattleRatingCharacterGateway, IBattleRuntimeCharacterGateway；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 287. `scripts/systems/battle/runtime/PassiveStatusOrchestrator.cs`

- 复审状态：**需跟进**；规模：43 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：PassiveStatusOrchestrator；主要方法：ApplyToUnit, _clear_identity_projection, _suppresses_original_race_traits。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 288. `scripts/systems/battle/runtime/RaceTraitResolver.cs`

- 复审状态：**需跟进**；规模：156 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：RaceTraitResolver；主要方法：ApplyToUnit, _apply_identity_def_projection, _initialize_racial_skill_charges, _append_unique_string_names, _merge_damage_resistances。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 289. `scripts/systems/battle/runtime/SkillPassiveResolver.cs`

- 复审状态：**需跟进**；规模：350 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：SkillPassiveResolver；主要方法：ApplyToUnit, GetSkillProgress, SyncVajraBodyStatus, ResolveVajraBodyEffectiveLevel, SyncShootingSpecializationStatus, IsSkillPassiveActive, SyncLastStandStatus, BuildPassiveSkillStatus, GetSkillDef。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×4; runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 290. `scripts/systems/battle/runtime/TestCsBase.cs`

- 复审状态：**通过**；规模：6 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：TestCsBase；主要方法：FetchName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 291. `scripts/systems/battle/runtime/TraitTriggerHooks.cs`

- 复审状态：**需跟进**；规模：560 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：TraitDispatchResult, TraitTriggerHooks, struct；主要方法：ToDictionary, BuildPayload, IsEmpty, FromDictionary, HasDispatchForTraitTrigger, get_dispatch_trait_ids, OnNaturalOne, OnCrit, OnFatalDamage, OnBattleStartResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×24; save/schema/projection×2; runtime mutation collections×8。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 292. `scripts/systems/battle/sim/BattleSimContentProvider.cs`

- 复审状态：**通过**；规模：32 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimContentProvider；主要方法：Dispose。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 293. `scripts/systems/battle/sim/BattleSimExecutionLoop.cs`

- 复审状态：**需跟进**；规模：271 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimExecutionLoop, BattleSimExecutionLoopResult；主要方法：Run, AdvanceStep, HasReadyUnits, HasProgressed, IssueManualPolicy, GetUnit, PrintTraceStats, ReadInt64。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×2。
- 对抗性检视：
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 294. `scripts/systems/battle/sim/BattleSimFactionMetricSummary.cs`

- 复审状态：**需跟进**；规模：58 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimFactionMetricSummary；主要方法：AccumulateFrom, ToDictionary, ReadInt。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 295. `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`

- 复审状态：**需跟进**；规模：1403 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimFormalRosterOptionsData, BattleSimFormalCombatFixture, in；主要方法：SetupContent, BuildRoster, _apply_content_catalogs, BuildRuntimeContext, ApplyStartedBattleMetadata, GetPartyState, GetMemberState, HasItemDefCatalog, GetItemDef, GetMemberAttributeSnapshotForEquipmentView。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×85; runtime mutation collections×56; randomness/determinism×6。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 296. `scripts/systems/battle/sim/BattleSimOutputFiles.cs`

- 复审状态：**需跟进**；规模：22 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimOutputFiles；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; save/schema/projection×8。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 297. `scripts/systems/battle/sim/BattleSimOverrideApplier.cs`

- 复审状态：**需跟进**；规模：495 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimOverrideApplier；主要方法：ApplyProfileTyped, ApplyPatchEntryTyped, _resolve_action_resource, GetBrainStates, _set_value_by_path, _set_value_recursive, _resolve_dictionary_key, _object_has_property, _coerce_value, ToVariant。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×19; runtime mutation collections×22。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 298. `scripts/systems/battle/sim/BattleSimOverrideApplyResult.cs`

- 复审状态：**通过**；规模：26 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimOverrideApplyResult；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×8。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 299. `scripts/systems/battle/sim/BattleSimProfileComparison.cs`

- 复审状态：**需跟进**；规模：59 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimProfileComparison；主要方法：ToDictionary, ToIntDictionary, ToFloatDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×1; runtime mutation collections×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 300. `scripts/systems/battle/sim/BattleSimProfileDef.cs`

- 复审状态：**需跟进**；规模：37 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimProfileDef；主要方法：ToDictionary, ToDict。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 301. `scripts/systems/battle/sim/BattleSimProfileReportEntry.cs`

- 复审状态：**需跟进**；规模：24 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimProfileReportEntry；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; save/schema/projection×3; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 302. `scripts/systems/battle/sim/BattleSimProfileSummary.cs`

- 复审状态：**需跟进**；规模：86 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimProfileSummary；主要方法：ToDictionary, ToIntDictionary, ToFloatDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×2; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 303. `scripts/systems/battle/sim/BattleSimReportBuilder.cs`

- 复审状态：**需跟进**；规模：351 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimReportBuilder；主要方法：BuildProfileSummary, BuildProfileComparisons, MergeSkillCounter, MergeActionChoices, MergeFactionMetricTotals, IncrementCounter, CollectStringKeys, GetDictionary, TryGetVariantKey。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; runtime mutation collections×29。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 304. `scripts/systems/battle/sim/BattleSimRunReport.cs`

- 复审状态：**需跟进**；规模：66 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimRunReport；主要方法：ToDictionary, ToGodotTraceArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×2; runtime mutation collections×6。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 305. `scripts/systems/battle/sim/BattleSimRunner.cs`

- 复审状态：**需跟进**；规模：467 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimRunner；主要方法：Setup, SetProgressLoggingEnabled, SetProgressLogPath, RunScenario, _ResolveProfiles, _RunSingleSimulation, _BuildEncounterAnchor, _CountLivingUnits, _BuildFinalUnitSnapshots, CloneAiTurnTraces。
- Godot/公开边界：Export 2 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×17; save/schema/projection×26; runtime mutation collections×20。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 306. `scripts/systems/battle/sim/BattleSimScenarioDef.cs`

- 复审状态：**需跟进**；规模：231 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimScenarioDef；主要方法：ResolveSeeds, BuildStartContext, ToDictionary, _build_unit_payloads, _build_spawn_coords, _build_cells, _resolve_override_coord, _apply_cell_override。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×3; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 307. `scripts/systems/battle/sim/BattleSimScenarioReport.cs`

- 复审状态：**需跟进**；规模：34 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimScenarioReport；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×5; runtime mutation collections×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 308. `scripts/systems/battle/sim/BattleSimTerrainGenerator.cs`

- 复审状态：**需跟进**；规模：125 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimTerrainGenerator；主要方法：GenerateTyped, _resolve_map_size, _duplicate_vector2i_array, ReadContextValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×15; runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 309. `scripts/systems/battle/sim/BattleSimTraceSummaryBuilder.cs`

- 复审状态：**需跟进**；规模：1535 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimTraceSummaryBuilder, IGodotDictionaryConvertible, TraceSummaryOptionsData, CompactRunTraceData, CompactTurnTraceData, CompactActionTraceData, CompactTopCandidateData, CompactCommandSummaryData；主要方法：HasTraces, Build, BuildCompactRunTraceData, SummarizeActionTracesData, SummarizeTopCandidatesData, SummarizeTraceCommandData, SummarizeExecutionResultData, SummarizeUnitResultsData, SummarizeUnitSnapshotsData, SummarizeUnitSnapshotData。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×27; save/schema/projection×94; runtime mutation collections×117。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 310. `scripts/systems/battle/sim/BattleSimUnitSpec.cs`

- 复审状态：**需跟进**；规模：480 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimUnitSpec；主要方法：ToBattleUnitState, ApplyAttributeDefaults, BuildFormalAttributeSnapshot, CalculateInitialHpMax, ApplyAcComponentOverridesToProgress, ApplyAttributeOverrides, IsFormalAttributeOverride, GetBaseAttributeValue, HasAttributeOverride, GetAttributeOverride。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 311. `scripts/systems/battle/terrain/BattleEdgeService.cs`

- 复审状态：**需跟进**；规模：416 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleEdgeService, EdgeLookup；主要方法：EnsureRuntimeEdgeFaces, MarkRuntimeEdgeFacesDirty, BuildEdgeFacesForCells, GetAllEdgeFaces, GetEdgeFace, GetEdgeFaceFromCache, IsTraversableBetween, IsTraversableInCache, IsEdgeFaceTraversable, BlocksOccupancyBetween。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×25; save/schema/projection×2; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 312. `scripts/systems/battle/terrain/BattleGridDistanceService.cs`

- 复审状态：**需跟进**；规模：52 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleGridDistanceService；主要方法：GetDistance, GetDistanceFromUnitToCoord, GetDistanceBetweenUnits。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 313. `scripts/systems/battle/terrain/BattleGridService.cs`

- 复审状态：**需跟进**；规模：2209 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：struct, BattleGridService, MovePathNode, struct；主要方法：GetCellState, HasCell, GetCellBaseTerrainId, GetUnitAtCoord, IsInside, GetNeighbors4, GetFootprintCoords, GetUnitTargetCoords, GetDistance, GetAreaCoords。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×40; save/schema/projection×5; runtime mutation collections×58。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 314. `scripts/systems/battle/terrain/BattleTerrainEffectState.cs`

- 复审状态：**需跟进**；规模：631 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleTerrainEffectState；主要方法：CopyResidualParams, ToDictionary, FromDictionary, ToDictionaryArray, FromDictionaryArray, DuplicateArray, HasExactSerializedFields, BuildParamsProjection, GetString, GetStringName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×67; save/schema/projection×29; runtime mutation collections×19。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 315. `scripts/systems/battle/terrain/BattleTerrainEffectSystem.cs`

- 复审状态：**需跟进**；规模：615 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleTerrainEffectSystem；主要方法：_ResolveRuntime, Setup, GetMoveCostDeltaForUnitTarget, UpsertTimedTerrainEffect, ProcessTimedTerrainEffects, ApplyTimedTerrainEffectTick, _GetTimedTerrainMoveCostDelta, _IsBlockedByNonstackingStatus, _UnitHasAnyStatus, _BuildTimedTerrainEffect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; save/schema/projection×6; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 316. `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`

- 复审状态：**需跟进**；规模：1776 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleTerrainProfileKind, BattleTerrainGenerator, TerrainQualityResult；主要方法：ToStringName, ToProfileKind, Generate, ResolveTerrainProfileId, NormalizeWaterHeights, GenerateDefault, GenerateCanyon, GenerateNarrowAssault, GenerateHoldoutPush, BuildLayout。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×103; save/schema/projection×3; runtime mutation collections×61; randomness/determinism×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 317. `scripts/systems/battle/terrain/BattleTerrainRules.cs`

- 复审状态：**需跟进**；规模：282 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleTerrainKind, BattleTerrainRules；主要方法：ToStringName, ToTerrainKind, NormalizeTerrainId, IsWaterTerrain, GetGlobalPassable, GetBaseMoveCost, CanUnitEnterTerrain, GetUnitMoveCost, GetDisplayName, CanHostTent。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 318. `scripts/systems/battle/terrain/BattleTerrainTopologyService.cs`

- 复审状态：**通过**；规模：367 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleTerrainTopologyChange, BattleTerrainTopologyService；主要方法：ReclassifyAllWaterTerrain, ReclassifyWaterTerrainNearCoords, ReclassifyComponents, CollectAllWaterCoords, CollectSeedWaterCoords, CollectComponent, ComponentHasOutlet, ResolveFlowDirection, IsShallowCell, IsWaterLike。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×31。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 319. `scripts/systems/battle/terrain/BattleVirtualBoardOverlay.cs`

- 复审状态：**需跟进**；规模：157 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleVirtualBoardOverlay；主要方法：ReleaseUnit, PlaceUnit, GetOccupant, HasOverride, Describe, CopyDictionary, NormalizeStringName, IsEmpty, Fail。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×18; save/schema/projection×6; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 320. `scripts/systems/content/GameContentCatalog.cs`

- 复审状态：**需跟进**；规模：194 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：GameContentCatalog；主要方法：BindSession, ClearSessionBinding, Rebuild, ResetSnapshot, GetRevision, HasSessionTyped, GetSessionTyped, IsBoundToSession, GetProgressionContentRegistryTyped, GetProgressionIdentityCatalogTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×26。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 321. `scripts/systems/content/GameRoot.cs`

- 复审状态：**通过**；规模：47 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：GameRoot；主要方法：BindSession, DisposeOwnedRuntimeResources, HasSessionTyped, GetSessionTyped, GetContentCatalogTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 322. `scripts/systems/content/IValidatableRegistry.cs`

- 复审状态：**通过**；规模：6 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：IValidatableRegistry；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 323. `scripts/systems/content/skills/ISkillCatalog.cs`

- 复审状态：**通过**；规模：46 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：ISkillCatalog；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 324. `scripts/systems/content/skills/SkillCatalog.cs`

- 复审状态：**通过**；规模：151 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SkillCatalog, EffectiveCombatProfileCacheKey；主要方法：Equals, GetRevision, HasSkill, TryGetSkillDef, GetCombatProfileTyped, GetEffectiveCombatProfile, GetEffectiveResourceCostValues, GetEffectiveAttackRollBonus, GetEffectiveAreaPattern, GetEffectiveAreaValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×7。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 325. `scripts/systems/content/skills/SkillEffectiveCombatProfile.cs`

- 复审状态：**通过**；规模：57 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SkillEffectiveCombatProfile；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 326. `scripts/systems/content/skills/SkillEffectiveCombatProfileResolver.cs`

- 复审状态：**需跟进**；规模：189 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SkillEffectiveCombatProfileResolver；主要方法：Resolve, BuildUncached, BuildMissing, ToGodotArray, ResolveResourceCosts, TryReadResourceCostOverride, ReadIntOverride, ReadAreaPatternOverride, BuildUnlockedCastVariantsSnapshot。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 327. `scripts/systems/fate/LowLuckRelicRules.cs`

- 复审状态：**需跟进**；规模：217 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：LowLuckRelicItemKind, LowLuckRelicAttributeKind, LowLuckRelicStatusKind, LowLuckPathTagKind, LowLuckRelicRules；主要方法：ToStringName, ToItemKind, ToAttributeKind, ToStatusKind, SnapshotHasFlag, UnitHasFlag, NormalizePathTags, ShouldRevealHiddenPath, ToPathTagKind, MemberHasItem。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×2; runtime mutation collections×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 328. `scripts/systems/game_runtime/BattleRefreshMode.cs`

- 复审状态：**通过**；规模：19 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：BattleRefreshMode, BattleRefreshModes；主要方法：ToPayloadValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 329. `scripts/systems/game_runtime/BattleSessionFacade.cs`

- 复审状态：**需跟进**；规模：1138 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：BattleSessionFacade；主要方法：Setup, GetSelectedBattleSkillName, GetSelectedBattleSkillVariantName, GetSelectedBattleSkillTargetCoords, GetSelectedBattleSkillTargetUnitIds, GetSelectedBattleSkillValidTargetCoords, GetSelectedBattleSkillRequiredCoordCount, GetBattleMovementReachableCoords, GetBattleOverlayTargetCoords, GetBattleActiveUnitName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×10; randomness/determinism×1。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 330. `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`

- 复审状态：**需跟进**；规模：899 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeBattleLootCommitService, ItemCommitResult, BattleLootCommitResult；主要方法：Create, Success, Setup, CommitBattleLootToSharedWarehouseTyped, ClearRegularBattleCalamityShardFlags, BuildBattleResolutionStatusMessageTyped, BuildLastBattleLootSnapshotTyped, FormatBattleDropEntries, CommitBattleLootToSharedWarehouseInternal, CommitFixedItemLootEntry。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×44; save/schema/projection×7; runtime mutation collections×15。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 331. `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`

- 复审状态：**需跟进**；规模：2094 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeBattleSelection；主要方法：Setup, GetSelectedBattleSkillName, GetSelectedBattleSkillVariantName, GetSelectedBattleSkillTargetCoords, GetSelectedBattleSkillTargetUnitIds, GetSelectedBattleSkillValidTargetCoords, GetSelectedBattleSkillRequiredCoordCount, SelectBattleSkillSlotTyped, CycleSelectedBattleSkillOption, ClearBattleSkillSelection。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×4; runtime mutation collections×62。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 332. `scripts/systems/game_runtime/GameRuntimeBattleSelectionState.cs`

- 复审状态：**通过**；规模：70 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeBattleSelectionState；主要方法：SetTargetCoords, SetTargetUnitIds, ClearTargets, ClearSkillSelection, ResetForBattleEnd。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×8。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 333. `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`

- 复审状态：**需跟进**；规模：548 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeBattleWritebackService, BattleLocalWritebackResult, BattleLocalCandidateValidationResult；主要方法：Success, Failed, FromFailureDictionary, ToDictionary, Setup, CommitBattleLocalViewsToPartyStateTyped, ReportConsistencyFailure, ReportInoptionFailure, CommitBattleLocalViewsToPartyStateInternal, ClonePartyStateForBattleWriteback。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×3; runtime mutation collections×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 334. `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`

- 复审状态：**需跟进**；规模：487 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeCharacterInfoBuilder；主要方法：Setup, Dispose, BuildCharacterInfoMetaLabel, BuildWorldCharacterInfoSections, BuildBattleCharacterInfoSections, BuildBattleCharacterIdentityEntries, BuildBattleCharacterInfoFatePayload, BuildBattleCharacterInfoBaseEntries, BuildBattleCharacterStatusEntries, BuildBattleCharacterSkillEntries。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×22; runtime mutation collections×17。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 335. `scripts/systems/game_runtime/GameRuntimeCommandLogger.cs`

- 复审状态：**需跟进**；规模：599 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeCommandLogger, CommandLogScope；主要方法：Empty, Create, Clone, BuildContext, MarkLogged, Setup, Dispose, BeginLoggedCommand, FinishLoggedCommand, LogActiveCommandScopeResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×19; save/schema/projection×16; runtime mutation collections×21。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 336. `scripts/systems/game_runtime/GameRuntimeFacade.cs`

- 复审状态：**需跟进**；规模：3882 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeFacade, RuntimeCommandCode, RuntimeCommandResult；主要方法：Success, Failure, ToDictionary, Setup, RebuildWildEncounterRosterDefIndex, BindRuntimeSidecarOwners, GetStatusText, GetLogSnapshot, GetRecentLogs, GetActiveLogFilePath。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×270; save/schema/projection×28; runtime mutation collections×114。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 337. `scripts/systems/game_runtime/GameRuntimePartyCommandHandler.cs`

- 复审状态：**需跟进**；规模：742 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimePartyCommandHandler；主要方法：Setup, Dispose, CommandOpenPartyTyped, CommandSelectPartyMemberTyped, CommandSetPartyLeaderTyped, CommandMoveMemberToActiveTyped, CommandMoveMemberToReserveTyped, CommandApplyPartyRosterTyped, CommandPartyEquipItemTyped, CommandPartyUnequipItemTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×7; runtime mutation collections×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 338. `scripts/systems/game_runtime/GameRuntimePendingBattleGenerationRequest.cs`

- 复审状态：**需跟进**；规模：32 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimePendingBattleGenerationRequest；主要方法：Set, CloneContext, Clear。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; runtime mutation collections×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 339. `scripts/systems/game_runtime/GameRuntimePendingSubmapPrompt.cs`

- 复审状态：**需跟进**；规模：63 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimePendingSubmapPrompt；主要方法：Set, Clear, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 340. `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`

- 复审状态：**需跟进**；规模：834 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeQuestCommandHandler, QuestCommandDefData, QuestProgressCommandPayloadData, QuestCommandDataReader；主要方法：Setup, Dispose, CommandAcceptQuestTyped, CommandProgressQuestTyped, CommandCompleteQuestTyped, CommandSubmitQuestItemTyped, CommandClaimQuestTyped, HasRuntime, CommandOkTyped, CommandErrorTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×2; runtime mutation collections×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 341. `scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs`

- 复审状态：**需跟进**；规模：779 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeRewardFlowHandler；主要方法：Setup, Dispose, GetCurrentPromotionPrompt, CommandConfirmPendingRewardTyped, CommandChoosePromotionTyped, CommandSubmitPromotionChoiceTyped, CommandCancelPromotionChoiceTyped, CommandConfirmActiveRewardTyped, CommandCloseActiveModalTyped, OnCharacterInfoWindowClosed。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×1。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 342. `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`

- 复审状态：**需跟进**；规模：3740 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeSettlementCommandHandler, SettlementActionValidationResult, ContractBoardQuestData, SettlementServiceEntryResolution, StagecoachDestinationData, SettlementPersistResult；主要方法：Success, Failure, ToDictionary, Missing, FromServiceData, SetupRuntime, DisposeRuntime, GetSettlementWindowData, GetShopWindowData, GetContractBoardWindowData。
- Godot/公开边界：Export 0 处；Signal 2 处；风险触点：Godot Dictionary/Array boundary×270; save/schema/projection×9; runtime mutation collections×86; randomness/determinism×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 343. `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`

- 复审状态：**需跟进**；规模：977 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeSnapshotBuilder；主要方法：Setup, Dispose, BuildHeadlessSnapshot, BuildTextSnapshot, BuildWorldSnapshot, BuildSubmapSnapshot, BuildGameOverSnapshot, BuildPartySnapshot, BuildQuestSnapshot, BuildQuestEntries。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×46; save/schema/projection×7; runtime mutation collections×43。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 344. `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`

- 复审状态：**需跟进**；规模：806 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeWarehouseHandler, WarehouseTransactionSnapshot；主要方法：Setup, Dispose, GetWarehouseWindowData, CommandOpenPartyWarehouseTyped, CommandDiscardOneTyped, CommandDiscardAllTyped, CommandUseItemTyped, CommandAddItemTyped, OpenPartyWarehouseWindow, OnPartyWarehouseWindowClosed。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×2; runtime mutation collections×12。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 345. `scripts/systems/game_runtime/IGameRuntimeSnapshotSource.cs`

- 复审状态：**需跟进**；规模：68 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：IGameRuntimeSnapshotSource；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×29。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 346. `scripts/systems/game_runtime/RuntimeModalKind.cs`

- 复审状态：**通过**；规模：48 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：RuntimeModalKind, RuntimeModalKinds；主要方法：ToPayloadValue, IsSettlementServiceModal。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 347. `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`

- 复审状态：**需跟进**；规模：605 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：WorldMapRuntimeProxy；主要方法：Setup, Dispose, GetStatusText, GetActiveModalId, GetGameOverContext, GetActiveSettlementId, GetActiveMapId, GetActiveMapDisplayName, GetSubmapReturnHintText, GetPendingSubmapPrompt。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 348. `scripts/systems/game_runtime/WorldMapSystem.cs`

- 复审状态：**需跟进**；规模：1075 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：WorldMapSystem；主要方法：_update_responsive_log_layout, RenderFromRuntime, _handle_world_input, _process_world_held_movement, _get_world_move_direction_for_key, _press_world_move_key, _release_world_move_key, _get_active_world_move_direction, _get_active_world_move_keycode, _clear_world_move_hold。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×21; scene/node/signal contract×26; runtime mutation collections×4; hot path / lifecycle×2; string dynamic dispatch×1。
- 对抗性检视：
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 349. `scripts/systems/game_runtime/headless/GameTextCommandResult.cs`

- 复审状态：**需跟进**；规模：189 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameTextCommandResult, AssertionEntry；主要方法：ToDictionary, AddAssertion, SetSnapshot, Render, CloneTypedArray, CloneTypedValue, ProjectDictionary, ProjectArray, ProjectValue。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×13; save/schema/projection×2; runtime mutation collections×28。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 350. `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`

- 复审状态：**需跟进**；规模：1389 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameTextCommandRunner, IntParseResult, CoordParseResult, ExpectationResult, CommandOutcome；主要方法：initialize, GetSession, ExecuteLine, FinalizeExpectResult, ExecuteCommand, ExecutePresetCommand, ExecuteSaveCommand, ExecuteGameCommand, ExecuteWorldCommand, ExecuteSubmapCommand。
- Godot/公开边界：Export 2 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×5; runtime mutation collections×61。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 351. `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`

- 复审状态：**需跟进**；规模：1449 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：HeadlessGameTestSession, SessionCommandOutcome, BattleEquipmentInstanceSelection, ChangeEquipmentReportSummary；主要方法：initialize, GetGameSession, GetGameSessionTyped, GetRuntimeFacade, GetRuntimeFacadeTyped, HasWorldLoaded, ListPresets, ListSaveSlots, CreateNewGameTyped, LoadGameTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×33; scene/node/signal contract×1; save/schema/projection×34; runtime mutation collections×88; randomness/determinism×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 352. `scripts/systems/inventory/EquipmentDropService.cs`

- 复审状态：**通过**；规模：122 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：EquipmentDropService；主要方法：ConfigureRng, SetRngForTesting, SetRollRangeForTesting, RollDrops, RollDropRarity, _roll_3d6, _resolve_rarity_from_score, _assert_drop_luck_in_range。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×7; randomness/determinism×8。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 353. `scripts/systems/inventory/PartyEquipmentService.cs`

- 复审状态：**需跟进**；规模：630 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：PartyEquipmentService, EquipmentDisplacedEntry, EquipmentEquipPreviewResult, EquipmentActionResult, EquipmentViewEntry；主要方法：SuccessResult, Failed, Setup, Dispose, GetItemDef, GetEquipmentState, GetEquippedEntriesTyped, BuildAttributeModifiersTyped, PreviewEquipTyped, EquipItemTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; save/schema/projection×1; runtime mutation collections×47。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 354. `scripts/systems/inventory/PartyItemUseService.cs`

- 复审状态：**需跟进**；规模：201 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：PartyItemUseService, PartyItemUseOptions, PartyItemUseResult；主要方法：ToLearnSkillOptions, Create, WithReason, WithSkill, WithConfirmationRequired, WithSuccess, ToDictionary, Setup, Dispose, UseItemTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×1; runtime mutation collections×13。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 355. `scripts/systems/inventory/PartyWarehouseService.cs`

- 复审状态：**需跟进**；规模：1203 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：PartyWarehouseService, WarehouseBatchItemEntry, WarehouseBatchSwapResult, WarehouseAddItemResult, WarehouseRemoveItemResult；主要方法：ToDictionary, Success, Blocked, WithError, Setup, SetupPartyBackpackView, GetTotalCapacity, GetUsedSlots, GetFreeSlots, IsOverCapacity。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×23; save/schema/projection×6; runtime mutation collections×54。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 356. `scripts/systems/inventory/WarehouseInventoryEntry.cs`

- 复审状态：**需跟进**；规模：88 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：WarehouseInventoryEntry；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/warehouse/ 或 godot --headless --script tests/equipment/run_party_equipment_regression.cs。

### 357. `scripts/systems/persistence/FileIOCoordinator.cs`

- 复审状态：**需跟进**；规模：336 行；上下文：Save/session/persistence：重点检查 schema、Normalize/Serialize、dirty scope、兼容策略。
- 关键类型/入口：FileIOCoordinator；主要方法：WriteCompressedVariantAtomically, ReplaceFileAtomically, RecoverReplaceTarget, IsCompressedVariantFileReadable, RenameFile, RemoveFileIfExists, RemoveDirectoryRecursive, PushError。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×8; runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：python tests/run_regression_suite.py + save/session focused runner。

### 358. `scripts/systems/persistence/GameLogService.cs`

- 复审状态：**需跟进**；规模：299 行；上下文：Save/session/persistence：重点检查 schema、Normalize/Serialize、dirty scope、兼容策略。
- 关键类型/入口：GameLogService, GameLogEntry；主要方法：Setup, AppendEntry, GetRecentEntries, BuildSnapshot, StartNewSession, ClearEntries, GetLogPath, GetVirtualLogPath, SetFileOutputEnabled, IsFileOutputEnabled。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; scene/node/signal contract×3; save/schema/projection×6; runtime mutation collections×7; string dynamic dispatch×1; randomness/determinism×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：python tests/run_regression_suite.py + save/session focused runner。

### 359. `scripts/systems/persistence/GameSession.cs`

- 复审状态：**需跟进**；规模：3323 行；上下文：Save/session/persistence：重点检查 schema、Normalize/Serialize、dirty scope、兼容策略。
- 关键类型/入口：GameSession, ContentValidationDomainSnapshotData, ContentValidationSnapshotData；主要方法：ToDictionary, EnumerateDomainErrors, DisposeOwnedRuntimeResources, EnsureWorldReady, EnsureGameRoot, StartNewGame, CreateNewSave, ListSaveSlots, PeekSaveSlots, LoadSave。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×199; save/schema/projection×253; resource/path loading×1; runtime mutation collections×90; randomness/determinism×6。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：python tests/run_regression_suite.py + save/session focused runner。

### 360. `scripts/systems/persistence/SaveSerializer.cs`

- 复审状态：**需跟进**；规模：1508 行；上下文：Save/session/persistence：重点检查 schema、Normalize/Serialize、dirty scope、兼容策略。
- 关键类型/入口：SaveSerializer；主要方法：Setup, BuildSavePayload, BuildWorldStatePayload, BuildMetaPayload, DecodePayload, BuildSaveMeta, ExtractSaveMetaFromPayload, NormalizeSaveMeta, NormalizeWorldData, SerializeWorldData。
- Godot/公开边界：Export 1 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×168; save/schema/projection×112; runtime mutation collections×43。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：python tests/run_regression_suite.py + save/session focused runner。

### 361. `scripts/systems/progression/AgeStageResolution.cs`

- 复审状态：**通过**；规模：19 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AgeStageResolution；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 362. `scripts/systems/progression/AgeStageResolver.cs`

- 复审状态：**通过**；规模：188 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AgeStageResolver, StageCandidate；主要方法：ResolveEffectiveStage, _resolve_base_stage_id, _collect_age_stage_order, _resolve_modifier_stage_result, _uses_identity_stage_axis, _modifier_applies_to_member, _build_result。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 363. `scripts/systems/progression/AscensionApplyService.cs`

- 复审状态：**通过**；规模：110 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AscensionApplyService；主要方法：Setup, ApplyAscension, RevokeAscension, IsValidAscensionStagePair, MemberMatchesAllowedIdentity。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 364. `scripts/systems/progression/AttributeGrowthResult.cs`

- 复审状态：**需跟进**；规模：89 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AttributeGrowthResult；主要方法：NotApplied, AppliedResult, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 365. `scripts/systems/progression/AttributeGrowthService.cs`

- 复审状态：**通过**；规模：79 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：AttributeGrowthService；主要方法：Setup, GetTierBudget, IsValidGrowthTier, IsValidAttributeId, ApplyAttributeProgressTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 366. `scripts/systems/progression/BloodlineApplyService.cs`

- 复审状态：**通过**；规模：63 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：BloodlineApplyService；主要方法：Setup, ApplyBloodline, RevokeBloodline, IsValidBloodlineStagePair。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 367. `scripts/systems/progression/CharacterAttributeChangeFact.cs`

- 复审状态：**需跟进**；规模：146 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterAttributeChangeFact；主要方法：PermanentDelta, GrowthResult, ToDictionary, FromDictionary, ReadStringName, ReadString, ReadInt, ReadOptionalInt, TryRead。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 368. `scripts/systems/progression/CharacterCreationIdentityOptionService.cs`

- 复审状态：**通过**；规模：269 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterCreationIdentityOptionService；主要方法：CollectCreationRaceIds, CollectSubraceIdsForRace, ChooseRaceId, ChooseSubraceId, IsValidCreationRaceSubracePair, SortStringNames, ContainsId。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×50。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 369. `scripts/systems/progression/CharacterCreationService.cs`

- 复审状态：**需跟进**；规模：446 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterCreationOptions, CharacterCreationService, ProgressionContentSourceRef；主要方法：CalculateInitialHpMax, CreateMemberFromCharacterCreationPayloadWithoutContentSource, CreateMemberFromCharacterCreationPayloadForContentSource, CreateMemberFromCharacterCreationPayloadForIdentityCatalog, CreateMemberFromCharacterCreationPayload, ApplyCharacterCreationPayloadToMemberWithoutContentSource, ApplyCharacterCreationPayloadToMemberForContentSource, ApplyCharacterCreationPayloadToMemberForIdentityCatalog, ApplyCharacterCreationPayloadToMember, MapRerollCountToHiddenLuckAtBirth。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×17; runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 370. `scripts/systems/progression/CharacterKnowledgeChangeFact.cs`

- 复审状态：**需跟进**；规模：67 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterKnowledgeChangeFact；主要方法：ToDictionary, FromDictionary, ReadStringName, ReadString, TryRead。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 371. `scripts/systems/progression/CharacterManagementModule.cs`

- 复审状态：**需跟进**；规模：4298 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterManagementModule, LearnSkillOptionsData, AttributeGrowthEntryData, AchievementProgressSummaryEntry, DailyPracticeGrowthResult, PendingCharacterRewardEntryData, QuestSubmitItemPreviewData, QuestObjectiveDefData；主要方法：ToDictionary, setup, GetPartyState, HasItemDefCatalog, SetPartyState, GetRaceDefForMember, GetSubraceDefForMember, GetBloodlineDefForMember, GetBloodlineStageDefForMember, GetAscensionDefForMember。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×178; save/schema/projection×11; runtime mutation collections×164。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 372. `scripts/systems/progression/CharacterMasteryChangeFact.cs`

- 复审状态：**需跟进**；规模：89 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterMasteryChangeFact；主要方法：ToDictionary, FromDictionary, ReadStringName, ReadString, ReadInt, TryRead。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 373. `scripts/systems/progression/CharacterProgressionDelta.cs`

- 复审状态：**需跟进**；规模：523 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：CharacterProgressionDelta；主要方法：SetLeveledSkillIds, AddLeveledSkillId, SetGrantedSkillIds, AddGrantedSkillId, SetChangedProfessionIds, AddChangedProfessionId, HasChangedProfessionId, SetPendingProfessionChoices, AddPendingProfessionChoice, SetMasteryChanges。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×48; save/schema/projection×6; runtime mutation collections×51。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 374. `scripts/systems/progression/FaithService.cs`

- 复审状态：**通过**；规模：485 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：FaithDevotionResult, FaithService；主要方法：Setup, Rebuild, GetFaithDeityDef, Validate, ExecuteDevotion, GetCurrentRank, Dispose, ScanDirectory, RegisterDeityResource, CollectValidationErrorsInto。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：resource/path loading×2; runtime mutation collections×21。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 375. `scripts/systems/progression/IdentityPayloadValidator.cs`

- 复审状态：**需跟进**；规模：426 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：IdentityPayloadValidator；主要方法：ValidatePartyIdentityForContentSource, ValidatePartyIdentity, ValidateMemberIdentityForContentSource, ValidateMemberIdentityTyped, ValidateMemberIdentityCore, ResolveBodySizeCategoryForContentSource, ResolveBodySizeCategoryForMemberTyped, ResolveBodySizeCategoryForMemberCore, RefreshMemberBodySizeFromContentSource, RefreshMemberBodySizeFromIdentityTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×38。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 376. `scripts/systems/progression/LevelGrowthEvaluationService.cs`

- 复审状态：**通过**；规模：134 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：LevelGrowthEvaluationService；主要方法：Setup, SetActiveTriggerCoreSkillTyped, ClearActiveTriggerCoreSkillTyped, HasActiveTriggerCoreSkill, IsActiveTriggerReadyForLevelUp, ApplyLevelUpTyped, GetSkillDef。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 377. `scripts/systems/progression/LevelGrowthTriggerResult.cs`

- 复审状态：**需跟进**；规模：64 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：LevelGrowthTriggerResult；主要方法：Fail, SetSuccess, ClearSuccess, LevelUpSuccess, ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×4; save/schema/projection×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 378. `scripts/systems/progression/MisfortuneBlackOmenService.cs`

- 复审状态：**通过**；规模：338 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：MisfortuneBlackOmenHookPayload, MisfortuneBlackOmenResult, MisfortuneBlackOmenHookKind, MisfortuneBlackOmenService；主要方法：ToStringName, Setup, Dispose, TryRunHook, GrantDoomMark, TryGrantCursedRelicEliteOrBossVictory, TryGrantBossCurseSurvivalVictory, TryGrantDeadRoadLanternBlackOmenPath, HasCursedRelic, HasBossCurse。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×8。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 379. `scripts/systems/progression/PassiveSourceContext.cs`

- 复审状态：**通过**；规模：14 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PassiveSourceContext；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 380. `scripts/systems/progression/PendingCharacterReward.cs`

- 复审状态：**需跟进**；规模：192 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PendingCharacterReward；主要方法：IsEmpty, DuplicateState, ToDictionary, FromDictionary, _parse_string_name_field, _has_exact_fields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; save/schema/projection×4; runtime mutation collections×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 381. `scripts/systems/progression/PendingCharacterRewardEntry.cs`

- 复审状态：**需跟进**；规模：139 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PendingCharacterRewardEntry；主要方法：IsEmpty, DuplicateState, ToDictionary, FromDictionary, _parse_string_name_field, _has_exact_fields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 382. `scripts/systems/progression/PracticeGrowthService.cs`

- 复审状态：**通过**；规模：491 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PracticeTrackKind, PracticeGrowthService；主要方法：ToStringName, ToTrackKind, Setup, GetTrackTypeForSkill, GetPracticeTier, ResolveTierValue, ResolveTierName, GetActivePracticeSkill, CanLearnPracticeSkillTyped, CalculateReplacementLevel。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×13。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 383. `scripts/systems/progression/PracticeSkillLearnStatus.cs`

- 复审状态：**需跟进**；规模：91 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：PracticeSkillLearnStatus；主要方法：NonPractice, Practice, WithPredictedLevel, ToCanLearnDictionary, ToLearnedStatusDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 384. `scripts/systems/progression/ProfessionAssignmentService.cs`

- 复审状态：**通过**；规模：293 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionAssignmentService；主要方法：Setup, CanAssignCoreSkillToProfession, AssignCoreSkillToProfession, RemoveCoreSkillFromProfession, CanPromoteNonCoreToCore, PromoteNonCoreToCore, GetProfessionCoreSkillIds, GetSkillAssignedProfession, GetSkillProgress, GetProfessionProgress。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×20。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 385. `scripts/systems/progression/ProfessionRuleService.cs`

- 复审状态：**需跟进**；规模：709 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProfessionRuleService；主要方法：Setup, IsProfessionKnowledgeUnlocked, CanUnlockProfession, CanRankUpProfession, CanSatisfyTagRules, CanSatisfyProfessionGates, CanSatisfyAttributeRules, CanSatisfyReputationRules, GetEligibleSkillIds, SkillMatchesTagRequirement。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×6; runtime mutation collections×32。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 386. `scripts/systems/progression/ProgressionIdentityCatalogData.cs`

- 复审状态：**通过**；规模：58 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProgressionIdentityCatalogData；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×30。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 387. `scripts/systems/progression/ProgressionService.cs`

- 复审状态：**需跟进**；规模：1604 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：ProgressionService；主要方法：Setup, SetupInternal, RefreshRuntimeState, LearnKnowledge, LearnSkill, CanLearnSkill, GrantRacialSkill, GrantSkillMastery, SetSkillCore, RecalculateCharacterLevel。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×72; save/schema/projection×9; runtime mutation collections×43; randomness/determinism×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 388. `scripts/systems/progression/QuestCommandResultData.cs`

- 复审状态：**需跟进**；规模：290 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestSubmitItemResultData, QuestClaimResultData；主要方法：ContainsClaimableQuest, ToDictionary, Success, Failed, ContainsStringName, CloneStringNameList, ToStringNameArray, CloneItemRewards, ClonePendingCharacterRewards, CloneUnsupportedRewardTypes。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×25; save/schema/projection×2; runtime mutation collections×22。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 389. `scripts/systems/progression/QuestProgressService.cs`

- 复审状态：**需跟进**；规模：1004 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestProgressEventKind, QuestProgressService, QuestActiveObjectiveMatch, QuestProgressEventData, QuestObjectiveDefData, QuestProgressDataReader, QuestProgressApplyResultData, QuestProgressEventContextData；主要方法：ToStringName, ToEventKind, Setup, SetPartyState, Dispose, GetPartyState, GetActiveQuestsTyped, GetClaimableQuestsTyped, GetClaimableQuestIdsTyped, GetCompletedQuestIdsTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×39; save/schema/projection×9; runtime mutation collections×46。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 390. `scripts/systems/progression/RacialSkillGrantService.cs`

- 复审状态：**通过**；规模：280 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：RacialSkillGrantService, RacialGrantEntry；主要方法：BackfillParty, RevokeOrphanParty, BackfillMember, RevokeOrphanMember, CollectMemberRacialGrantEntries, _append, CollectActiveIdentityGrantLookup, IdentityGrantKey, IsRacialGrantedSourceType, _build_progression_service。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×23。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 391. `scripts/systems/progression/SkillEffectiveMaxLevelRules.cs`

- 复审状态：**通过**；规模：127 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillEffectiveMaxLevelRules；主要方法：GetEffectiveMaxLevel, GetEffectiveAbsoluteMaxLevel, IsAtEffectiveMaxLevel, _uses_dynamic_max_level, _get_dynamic_max_level_stat_value。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 392. `scripts/systems/progression/SkillLevelDescriptionFormatter.cs`

- 复审状态：**需跟进**；规模：495 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillLevelDescriptionFormatter；主要方法：BuildLevelDescription, RenderTemplate, MergeVariantMap, _is_optional_value_visible, _merge_matching_effect_params, _merge_matching_effect_typed_fields, _collect_level_effect_defs, _append_level_effect_defs, _effect_unlocked_at_level, _merge_damage_effect_typed_fields。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×31。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 393. `scripts/systems/progression/SkillMergeService.cs`

- 复审状态：**需跟进**；规模：463 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillMergeService；主要方法：Setup, MergeSkills, ApplyCompositeUpgradeResult, DetachMergedSourceSkills, AttachMergedResultSkill, NormalizeSourceSkillIds, AllSourceSkillsExist, InferTargetProfessionIdFromSources, GetOrCreateResultSkillProgress, ClearCompositeTriggerReferences。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; save/schema/projection×4; runtime mutation collections×18。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 394. `scripts/systems/progression/StageAdvancementApplyService.cs`

- 复审状态：**通过**；规模：72 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：StageAdvancementApplyService；主要方法：Setup, AddStageAdvancementModifier, RemoveStageAdvancementModifier, ModifierAppliesToMember。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 395. `scripts/systems/settlement/SettlementForgeService.cs`

- 复审状态：**需跟进**；规模：879 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementForgeService, RecipeItemValidationResult；主要方法：Success, Failed, IsSupportedInteraction, HasAvailableRecipeTyped, ExecuteRecipeResultTyped, BuildWindowDataTyped, _resolve_recipe, _list_matching_recipes, _build_recipe_window_entries, _build_recipe_window_entry。
- Godot/公开边界：Export 0 处；Signal 3 处；风险触点：Godot Dictionary/Array boundary×91; runtime mutation collections×48。
- 对抗性检视：
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 396. `scripts/systems/settlement/SettlementPanelKind.cs`

- 复审状态：**通过**；规模：47 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementPanelKind, SettlementPanelKinds；主要方法：TryParse, ToPayloadValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 397. `scripts/systems/settlement/SettlementResearchService.cs`

- 复审状态：**需跟进**；规模：641 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementResearchService, ResearchMemberAvailability；主要方法：ToDictionary, IsSupportedInteraction, BuildServiceMetadataTyped, ExecuteTyped, _build_result, _validate_execution_schema, _validate_research_catalog_schema, _validate_research_candidate_schema, _validate_required_string_fields, _resolve_tarGetMemberState。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×54; save/schema/projection×2; runtime mutation collections×14。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 398. `scripts/systems/settlement/SettlementServiceMetadata.cs`

- 复审状态：**需跟进**；规模：63 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementServiceMetadata；主要方法：ToDictionary, IsReservedField。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×1; runtime mutation collections×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 399. `scripts/systems/settlement/SettlementServiceResult.cs`

- 复审状态：**需跟进**；规模：171 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementServiceResult；主要方法：SetInventoryDelta, SetPendingCharacterRewardsTyped, SetQuestProgressEventsTyped, SetServiceSideEffects, ToDictionary, DuplicateDictionary, DuplicatePendingRewardList, PendingRewardDictionaryArray, QuestProgressEventDictionaryArray, ReplacePendingRewardList。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×19; save/schema/projection×3; runtime mutation collections×17。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查事务快照是否覆盖 world_data、party_state、selected member 和 modal 状态；失败后 UI 必须重绘。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 400. `scripts/systems/settlement/SettlementShopService.cs`

- 复审状态：**需跟进**；规模：1115 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementShopService, ShopItemId, struct, ShopDefinition, ShopStockEntry；主要方法：BuildWindowDataTyped, BuyTyped, SellTyped, GetOrRefreshShopState, GenerateShopState, BuildShopEntry, MergeShopEntry, PickWeightedRandomEntry, ResolveBuyPrice, ResolveSellPrice。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×69; save/schema/projection×7; runtime mutation collections×28; randomness/determinism×5。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 401. `scripts/systems/settlement/SettlementShopTradeResult.cs`

- 复审状态：**通过**；规模：27 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementShopTradeResult；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 402. `scripts/systems/settlement/SettlementSubmissionSource.cs`

- 复审状态：**通过**；规模：62 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementSubmissionSource, SettlementSubmissionSources；主要方法：TryParse, FromPanelKind, ToPayloadValue。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 403. `scripts/systems/world/EncounterAnchorData.cs`

- 复审状态：**需跟进**；规模：245 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：EncounterAnchorKind, EncounterAnchorData；主要方法：ToStringName, ToEncounterKind, ToDictionary, FromDictionary, HasExactSerializedFields, TryParseStringNameField, IsValidEncounterKind。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×7。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 404. `scripts/systems/world/EncounterRosterBuilder.cs`

- 复审状态：**需跟进**；规模：1441 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：EncounterRosterBuilder, ParsedDropDefinition, PreviewLootEntryData, EncounterBuildContextData；主要方法：WithQuantity, WithDropEntryId, ToDictionary, Setup, BuildEnemyUnitsTyped, BuildLootEntriesTyped, LooksLikeSkillDefDict, ResolveEnemyTemplate, ResolveWildEncounterRoster, BuildPreviewLootEntriesFromRoster。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×61; save/schema/projection×4; runtime mutation collections×84。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 405. `scripts/systems/world/WildEncounterGrowthSystem.cs`

- 复审状态：**通过**；规模：107 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WildEncounterGrowthSystem；主要方法：ApplyStepAdvance, ApplyBattleVictory, GetRoster。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 406. `scripts/systems/world/WorldMapDataContext.cs`

- 复审状态：**需跟进**；规模：1263 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapDataContext, WorldMapContextSyncResult, WorldMapSubmapEnterResult, WorldMapSubmapReturnResult, WorldMapSubmapReturnStackEntry, WorldMapMountedSubmapData, WorldMapSettlementRecordData, WorldMapSettlementData；主要方法：BindRootWorldData, Reset, Dispose, IsSubmapActive, GetWorldStep, GetPlayerStartSettlementName, GetActiveWorldData, GetActiveGenerationConfig, GetActiveWorldFogState, SaveActiveWorldFogState。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×102; save/schema/projection×32; resource/path loading×1; runtime mutation collections×40。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 407. `scripts/systems/world/WorldMapFogFactionState.cs`

- 复审状态：**通过**；规模：36 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapFogFactionState；主要方法：ClearVisible, MarkVisible, MarkExplored, IsVisible, IsExplored。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 408. `scripts/systems/world/WorldMapFogSystem.cs`

- 复审状态：**需跟进**；规模：409 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapFogStateKind, WorldMapFogSystem, CoordParseResult；主要方法：Setup, GetWorldSizeCells, RebuildVisibilityForFaction, MarkExplored, RevealDiamond, IsVisible, IsExplored, GetFogState, ToFogStateValue, ToFogStateKind。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×17; save/schema/projection×6; runtime mutation collections×39。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 409. `scripts/systems/world/WorldMapFootprintState.cs`

- 复审状态：**通过**；规模：13 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapFootprintState；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 410. `scripts/systems/world/WorldMapGridSystem.cs`

- 复审状态：**通过**；规模：217 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapGridSystem；主要方法：Setup, GetWorldSizeCells, GetChunkSize, GetCell, IsCellInsideWorld, IsCellWalkable, GetOccupantRoot, CanPlaceFootprint, RegisterFootprint, ClearFootprint。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×9。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 411. `scripts/systems/world/WorldMapOccupantState.cs`

- 复审状态：**通过**；规模：13 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapOccupantState；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 412. `scripts/systems/world/WorldMapSpawnSystem.cs`

- 复审状态：**需跟进**；规模：2042 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldMapSpawnSystem, WorldBuildData, SettlementInstanceData, FacilityInstanceData, ServiceNpcInstanceData, ServiceEntryData, SettlementStateData, WorldNpcInstanceData；主要方法：ToDictionary, ProjectSettlements, ProjectWorldNpcs, ProjectEncounterAnchors, ProjectWorldEvents, ProjectMountedSubmaps, ProjectFacilities, ProjectServices, ProjectServiceNpcs, Equals。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×44; save/schema/projection×17; resource/path loading×10; runtime mutation collections×160; randomness/determinism×12。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 413. `scripts/systems/world/WorldTimeSystem.cs`

- 复审状态：**通过**；规模：82 行；上下文：World runtime/data：重点检查 world_data 字典、Vector2I key、fog/encounter lifecycle。
- 关键类型/入口：WorldTimeSystem, WorldTimeAdvanceResult；主要方法：StepToDay, AdvanceWorldStep, Invalid。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 414. `scripts/tools/tile_baker.gd`

- 复审状态：**通过**；规模：275 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：无显式类型；主要方法：_ready, _build, _mode, _material_for_mode, _real_pbr_material, _load_image_texture, _mud_material, _mud_ramp, _water_material, _water_ramp。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：resource/path loading×5。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 415. `scripts/tools/tree_baker.gd`

- 复审状态：**通过**；规模：157 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：无显式类型；主要方法：_ready, _bake, _model_path, _apply_foliage_alpha, _override_leaf_surfaces, _load_tex, _node_aabb。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：resource/path loading×5。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 416. `scripts/ui/BattleBoard2D.cs`

- 复审状态：**需跟进**；规模：681 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleBoard2D；主要方法：Configure, UpdateSelection, SetViewportSize, BeginViewportPan, EndViewportPan, IsViewportPanning, HandleViewportMouseMotion, ZoomViewport, PanViewportDirection, HandleViewportMouseButton。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×11; scene/node/signal contract×14; save/schema/projection×3; runtime mutation collections×7; string dynamic dispatch×3。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 417. `scripts/ui/BattleBoardController.cs`

- 复审状态：**需跟进**；规模：1898 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleBoardController；主要方法：BindLayers, Configure, UpdateMarkers, Clear, _refresh_tileset_profile, HasLayersBound, IsRenderContentReady, _redraw, _draw_terrain_layers, _draw_edge_faces。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×42; save/schema/projection×1; resource/path loading×4; runtime mutation collections×33; hot path / lifecycle×1。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 418. `scripts/ui/BattleBoardProp.cs`

- 复审状态：**关注**；规模：174 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleBoardProp；主要方法：Configure, ApplyInteractionState, DrawSpikeBarricade, DrawObjectiveMarker, DrawTent, DrawTorch, SignedOffset, Ratio, StableHash。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：scene/node/signal contract×2; hot path / lifecycle×3。
- 对抗性检视：
  - 检查绘制路径是否每帧分配数组/字典/纹理；大地图或战斗面板需要避免热路径 GC。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 419. `scripts/ui/BattleBoardRenderProfile.cs`

- 复审状态：**需跟进**；规模：434 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleBoardRenderProfile；主要方法：TERRAIN_PROFILE_DEFAULT, TERRAIN_PROFILE_CANYON, TERRAIN_PROFILE_NARROW_ASSAULT, TERRAIN_PROFILE_HOLDOUT_PUSH, RENDER_PROFILE_CANYON_ISO64, SOURCE_LAND, SOURCE_WATER, SOURCE_MUD, SOURCE_EDGE_DROP_EAST, SOURCE_EDGE_DROP_SOUTH。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; save/schema/projection×4; resource/path loading×1; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 420. `scripts/ui/BattleHoverPreviewOverlay.cs`

- 复审状态：**需跟进**；规模：425 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleHoverPreviewOverlay；主要方法：Clear, ApplyPreview, _build_layout, _refresh_target_unit, _refresh_hit_stages, _refresh_hit_summary, _refresh_fate_badges, _refresh_damage_label, _refresh_invalid_label, _build_hit_stage_segment。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×24; runtime mutation collections×1; hot path / lifecycle×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 421. `scripts/ui/BattleMapPanel.cs`

- 复审状态：**需跟进**；规模：2610 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleMapPanel；主要方法：INVALID_HOVER_COORD, IsLoadingBattle, GetLoadingProgress, IsBattleRenderContentReady, PanBattleCamera, SetupRuntimeContext, ShowBattle, _store_pending_show_battle_payload, RefreshOverlay, Refresh。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×77; scene/node/signal contract×62; save/schema/projection×9; resource/path loading×5; runtime mutation collections×40; hot path / lifecycle×2; string dynamic dispatch×12。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 422. `scripts/ui/BattleSkillSlotButton.cs`

- 复审状态：**通过**；规模：147 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleSkillSlotButton；主要方法：MakeLabel, _build_tooltip_panel_style。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 423. `scripts/ui/BattleUiTheme.cs`

- 复审状态：**通过**；规模：155 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：BattleUiTheme；主要方法：PANEL_BG, PANEL_BG_ALT, PANEL_BG_DEEP, PANEL_EDGE, PANEL_EDGE_SOFT, PANEL_EDGE_GLOW, PANEL_SHADOW, CHIP_BG, CHIP_EDGE, TEXT_PRIMARY。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 424. `scripts/ui/CharacterCreationWindow.cs`

- 复审状态：**需跟进**；规模：1699 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：CharacterCreationWindow, AttributeModifierTotals, CharacterCreationWindowNodeExtensions；主要方法：SetProgressionContentRegistry, ShowWindow, HideWindow, _cache_attribute_rows, _build_row_styles, _apply_button_palettes, _apply_button_palette, _make_button_stylebox, _on_name_text_submitted, _on_name_confirmed。
- Godot/公开边界：Export 0 处；Signal 4 处；风险触点：Godot Dictionary/Array boundary×23; scene/node/signal contract×44; runtime mutation collections×70; string dynamic dispatch×3; randomness/determinism×4。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 425. `scripts/ui/CharacterInfoWindow.cs`

- 复审状态：**需跟进**；规模：613 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：CharacterInfoWindow, EntryKind, CharacterInfoPayload, CharacterInfoSection, CharacterInfoEntry；主要方法：ShowCharacter, HideWindow, _close_window, _on_shade_gui_input, _rebuild_sections, _clear_sections, _build_section_panel, _build_pair_entry, _build_text_entry, _create_section_panel_stylebox。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×29; scene/node/signal contract×11; save/schema/projection×10; runtime mutation collections×37; string dynamic dispatch×1。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 426. `scripts/ui/DisplaySettingsWindow.cs`

- 复审状态：**关注**；规模：194 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：DisplaySettingsWindow；主要方法：ConfigureOptions, ShowWindow, HideWindow, GetSelectedSettings, _rebuild_resolution_options, _find_resolution_index, _get_selected_resolution, _on_fullscreen_toggled, _update_hint, _apply。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：scene/node/signal contract×13; runtime mutation collections×5; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 427. `scripts/ui/LoginScreen.cs`

- 复审状态：**需跟进**；规模：440 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：LoginScreen；主要方法：_on_start_button_pressed, _on_test_button_pressed, _on_load_button_pressed, _on_settings_button_pressed, _open_start_game_picker, _on_world_preset_confirmed, _on_world_preset_picker_cancelled, _open_character_creation_for, _can_open_character_creation, _on_character_creation_confirmed。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; scene/node/signal contract×10; save/schema/projection×11; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 428. `scripts/ui/MasteryRewardWindow.cs`

- 复审状态：**需跟进**；规模：168 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：MasteryRewardWindow；主要方法：ShowReward, HideWindow, _build_details_text, _build_entry_line, _read_entry_target_label, _get_reward_entries, _read_reward_text, _on_confirm_button_pressed, _on_shade_gui_input。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×2; scene/node/signal contract×8; runtime mutation collections×10; string dynamic dispatch×1。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 429. `scripts/ui/PartyManagementWindow.cs`

- 复审状态：**需跟进**；规模：1375 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PartyManagementWindow, struct；主要方法：ShowParty, SetAchievementDefs, SetItemDefs, SetSkillDefs, SetProfessionDefs, SetCharacterManagement, SetPartyState, RefreshView, GetPartyState, GetSelectedMemberId。
- Godot/公开边界：Export 0 处；Signal 5 处；风险触点：Godot Dictionary/Array boundary×15; scene/node/signal contract×36; save/schema/projection×1; runtime mutation collections×149; string dynamic dispatch×6。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 430. `scripts/ui/PartyMemberOptionUtils.cs`

- 复审状态：**需跟进**；规模：259 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PartyMemberOptionUtils；主要方法：GetPartyState, BuildMemberOptions, BuildMemberVariantMap, BuildMemberVariantLabel, ResolveDefaultMemberId, GetMemberVariantDisplayName, _append_member_option, _build_explicit_member_options, DictHas, DictArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×32; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 431. `scripts/ui/PartyWarehouseWindow.cs`

- 复审状态：**需跟进**；规模：719 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PartyWarehouseWindow, WarehouseWindowData, WarehouseEntry, struct；主要方法：ShowWarehouse, SetWindowData, RefreshView, HideWindow, _rebuild_stack_list, _restore_selection, _rebuild_target_member_selector, _refresh_details, _refresh_controls, _get_selected_entry_data。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×26; scene/node/signal contract×26; resource/path loading×1; runtime mutation collections×21; string dynamic dispatch×4。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 432. `scripts/ui/PromotionChoiceWindow.cs`

- 复审状态：**需跟进**；规模：432 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PromotionChoiceWindow；主要方法：ShowPromotion, HideWindow, _clear_cards, _rebuild_choice_cards, _create_card, _select_choice, _refresh_details, _on_card_gui_input, _on_confirm_button_pressed, _on_cancel_button_pressed。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×44; scene/node/signal contract×13; runtime mutation collections×16; hot path / lifecycle×1; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：godot --headless -s res://tests/world_map/ui/run_promotion_choice_window_schema_regression.cs，并新增 confirm signal payload 用例。

### 433. `scripts/ui/RuntimeLogDock.cs`

- 复审状态：**需跟进**；规模：502 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：RuntimeLogDock, struct；主要方法：IsCollapsed, GetCollapsedHeight, GetPreferredHeight, _toggle_collapsed, _cycle_opacity, ShowWorldLogs, ShowBattleLogs, ClearLogs, GetDesignPanelSize, ApplyLayoutScale。
- Godot/公开边界：Export 0 处；Signal 4 处；风险触点：Godot Dictionary/Array boundary×18; scene/node/signal contract×11; runtime mutation collections×18; hot path / lifecycle×1; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 434. `scripts/ui/SaveListWindow.cs`

- 复审状态：**需跟进**；规模：102 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：SaveListWindow；主要方法：FormatUnixTime, FormatWorldSize, DictString, DictInt, TryRead。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×10; scene/node/signal contract×4; save/schema/projection×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 435. `scripts/ui/SettlementWindow.cs`

- 复审状态：**需跟进**；规模：1101 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：SettlementWindow, SettlementWindowData, struct, struct, struct, ServiceEntry, ResolvedService, MemberOption；主要方法：ShowSettlement, HideWindow, SetFeedback, _refresh_view, _build_meta_text, _build_facility_text, _build_resident_text, _rebuild_member_selector, _select_member, _refresh_member_state。
- Godot/公开边界：Export 0 处；Signal 7 处；风险触点：Godot Dictionary/Array boundary×54; scene/node/signal contract×19; runtime mutation collections×57; hot path / lifecycle×1; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 436. `scripts/ui/ShopWindow.cs`

- 复审状态：**需跟进**；规模：992 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：ShopWindow, ShopWindowData, ShopEntry, MemberOption；主要方法：ShowShop, ShowStagecoach, HideWindow, RefreshView, _build_meta_text, _build_member_selector, _resolve_default_member_id, _select_member, _refresh_member_state, _rebuild_entry_list。
- Godot/公开边界：Export 0 处；Signal 5 处；风险触点：Godot Dictionary/Array boundary×38; scene/node/signal contract×25; runtime mutation collections×38; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 RichTextLabel 内容字段是否转义；配置文本不能获得未授权 BBCode 控制权。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 437. `scripts/ui/SubmapEntryWindow.cs`

- 复审状态：**需跟进**；规模：318 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：SubmapEntryWindow；主要方法：ShowPrompt, HideWindow, _on_confirm_button_pressed, _on_cancel_button_pressed, _on_shade_gui_input, _cache_default_metrics, _apply_prompt_metrics, _restore_default_metrics, _set_font_override, _read_int_property。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; scene/node/signal contract×17; runtime mutation collections×1; string dynamic dispatch×3。
- 对抗性检视：
  - 检查 HideWindow 与 EmitSignal 顺序；若信号使用实例字段，必须在清状态前缓存。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 438. `scripts/ui/WorldMapView.cs`

- 复审状态：**需跟进**；规模：577 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：WorldMapView；主要方法：Configure, SetRuntimeState, RefreshWorld, _draw_cells, _draw_cell_background, _draw_settlements, _draw_settlement_footprint_cells, _draw_settlement_body, _draw_mobile_entities, _draw_world_event_marker。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×23; scene/node/signal contract×6; hot path / lifecycle×5; string dynamic dispatch×2。
- 对抗性检视：
  - 检查绘制路径是否每帧分配数组/字典/纹理；大地图或战斗面板需要避免热路径 GC。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 439. `scripts/ui/WorldPresetPickerWindow.cs`

- 复审状态：**需跟进**；规模：56 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：WorldPresetPickerWindow；主要方法：DictString。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×6; scene/node/signal contract×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 440. `scripts/ui/components/SelectableListWindow.cs`

- 复审状态：**需跟进**；规模：272 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：SelectableListWindow；主要方法：ShowWindow, HideWindow, GetSelectedItemId, OnItemSelected, OnItemActivated, RefreshDetail, EmitSelected, EmitCancel, OnShadeGuiInput, ApplyItemListTheme。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; scene/node/signal contract×7; runtime mutation collections×6。
- 对抗性检视：
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 441. `scripts/ui/components/SelectionCardBuilder.cs`

- 复审状态：**需跟进**；规模：123 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：SelectionCardBuilder；主要方法：MakeStyle, BuildCard, MakeLabel, ExtractChipStrings, DictString。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 442. `scripts/utils/BattleBoardPropCatalog.cs`

- 复审状态：**通过**；规模：66 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：BattleBoardPropKind, BattleBoardPropCatalog；主要方法：ToStringName, ToPropKind, IsSupported, RequiresInteractionShape, GetSortPriority。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 443. `scripts/utils/DisplaySettingsService.cs`

- 复审状态：**需跟进**；规模：149 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：DisplaySettingsService, struct, struct；主要方法：Setup, ListResolutionOptions, GetDefaultSettings, LoadSettings, LoadAndApply, SaveSettings, ApplySettings, NormalizeSettings, NormalizeResolution, DescribeSettings。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×9; runtime mutation collections×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 444. `scripts/utils/FacilityConfig.cs`

- 复审状态：**需跟进**；规模：44 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：FacilityConfig；主要方法：GetTemplateId, GetPrimaryServiceName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 445. `scripts/utils/FacilityNpcConfig.cs`

- 复审状态：**通过**；规模：25 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：FacilityNpcConfig；主要方法：GetTemplateId。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 446. `scripts/utils/FacilitySlotConfig.cs`

- 复审状态：**通过**；规模：17 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：FacilitySlotConfig；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 447. `scripts/utils/GameLog.cs`

- 复审状态：**需跟进**；规模：143 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：GameLogLevel, GameLog；主要方法：AddSink, RemoveSink, ClearSinks, Fatal, Error, Warning, Info, Debug, Write。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×4。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 448. `scripts/utils/GameLogSinks.cs`

- 复审状态：**需跟进**；规模：102 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：IGameLogSink, ConsoleLogSink, GodotEditorLogSink, GameSessionLogSink；主要方法：Write。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2。
- 对抗性检视：
  - 检查弱引用 owner 生命周期；handler 不应在 runtime dispose 后继续持有 stale state。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 449. `scripts/utils/GameTextSnapshotRenderer.cs`

- 复审状态：**需跟进**；规模：1076 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：GameTextSnapshotRenderer；主要方法：RenderFullSnapshot, RenderWorldSnapshot, FormatCoord, AppendSection, BuildSessionLines, BuildPresetLabels, BuildStatusLines, BuildValidationLines, BuildLogLines, ExtractLogFileName。
- Godot/公开边界：Export 1 处；Signal 5 处；风险触点：Godot Dictionary/Array boundary×104; runtime mutation collections×132。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 450. `scripts/utils/GodotVariantReadExtensions.cs`

- 复审状态：**需跟进**；规模：126 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：GodotVariantReadExtensions；主要方法：GetValueOrDefault, TryAsDictionary, TryAsGodotArray, TryAsInt, TryAsVector2I, TryAsBool, TryRead, ToVariant。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 451. `scripts/utils/MountedSubmapConfig.cs`

- 复审状态：**通过**；规模：17 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：MountedSubmapConfig；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 452. `scripts/utils/SettlementConfig.cs`

- 复审状态：**需跟进**；规模：70 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：SettlementConfig, SettlementTier；主要方法：GetTemplateId, GetFootprintSize, GetTierName。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 453. `scripts/utils/SettlementDistributionRule.cs`

- 复审状态：**通过**；规模：19 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：SettlementDistributionRule；主要方法：GetSettlementTemplateId。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 454. `scripts/utils/TrueRandomSeedService.cs`

- 复审状态：**通过**；规模：70 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：TrueRandomSeedService；主要方法：GenerateSeed, RandiRange, SeedFromCryptoBytes, SeedFromFallbackRng, FallbackRngRange。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：randomness/determinism×7。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 455. `scripts/utils/VisionSourceData.cs`

- 复审状态：**通过**；规模：24 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：VisionSourceData；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 456. `scripts/utils/WeightedFacilityEntry.cs`

- 复审状态：**通过**；规模：16 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WeightedFacilityEntry；主要方法：GetFacilityTemplateId。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 457. `scripts/utils/WildSpawnRule.cs`

- 复审状态：**通过**；规模：30 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WildSpawnRule；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 458. `scripts/utils/WorldEventConfig.cs`

- 复审状态：**通过**；规模：43 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldEventTypeKind, WorldEventConfig；主要方法：ToStringName, ToEventTypeKind。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 459. `scripts/utils/WorldMapCellData.cs`

- 复审状态：**通过**；规模：17 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldMapCellData；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 460. `scripts/utils/WorldMapContentValidator.cs`

- 复审状态：**需跟进**；规模：993 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldMapContentValidator；主要方法：ValidateWorldPresetsTyped, ValidateGenerationConfigTyped, ValidateGenerationConfigInternal, BuildEffectiveSettlementResources, BuildEffectiveFacilityResources, BuildEffectiveWildSpawnRules, ValidateFacilityLibrary, ValidateFacilityNpcs, ValidateSettlementLibrary, ValidateFacilitySlots。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×16; resource/path loading×9; runtime mutation collections×131。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 461. `scripts/utils/WorldMapGenerationConfig.cs`

- 复审状态：**需跟进**；规模：168 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldMapGenerationConfig；主要方法：GetWorldSizeCells, GetTargetSettlementCount, GetSettlementSpacingCells。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 462. `scripts/utils/WorldMapSettlementBundle.cs`

- 复审状态：**需跟进**；规模：11 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldMapSettlementBundle；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 463. `scripts/utils/WorldMapSettlementNamePool.cs`

- 复审状态：**通过**；规模：25 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldMapSettlementNamePool；主要方法：BuildUniqueDisplayNames。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 464. `scripts/utils/WorldMapWildSpawnBundle.cs`

- 复审状态：**需跟进**；规模：8 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldMapWildSpawnBundle；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 465. `scripts/utils/WorldPresetRegistry.cs`

- 复审状态：**需跟进**；规模：167 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：WorldPresetRegistry, WorldPresetInfo；主要方法：ToDictionary, GetDefaultPresetId, ListPresetsTyped, ListPresets, TryGetPresetTyped, GetPreset, TryGetPresetForGenerationConfigTyped, GetPresetForGenerationConfig, GetFallbackPresetName, GetFileName。
- Godot/公开边界：Export 1 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×4; resource/path loading×5; runtime mutation collections×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 466. `scripts/utils/generate_canyon_tiles.py`

- 复审状态：**关注**；规模：484 行；上下文：Utility/config：重点检查 resource path、randomness、content validator、shared helper contract。
- 关键类型/入口：Canvas；主要方法：无可提取方法。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：scene/node/signal contract×3。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

## Suggested follow-up / 完成口径

- 优先修复 findings-first 中的 `[高]` 与 `[中]` 项，再为每个修复跑对应 narrow headless runner。
- 有 Godot/.NET 工具链后运行：`dotnet build magic.csproj`、`python tests/run_regression_suite.py`。
- 本报告已经覆盖 `scripts/` 下每一个文件；后续新增文件应按同样字段补入矩阵。
