# scripts 目录逐文件代码审查报告

> **当前修复说明（2026-07-23）**：`shop_inventory_seed` / `shop_last_refresh_step` 已从据点顶层 schema 删除，F-14 不再是待修 finding；各商店继续独立使用真随机并只持有自己的 seed、刷新步数和库存，save v15 严格拒绝 v14。

- 原始审查日期：`2026-06-17`
- 当前代码复核：`2026-07-23`
- 原始索引：466 个脚本文件；清除 19 个已失效条目后保留 447 个历史条目。当前 `scripts/` 已有 862 个脚本（859 个 `.cs`、2 个 `.gd`、1 个 `.py`）。
- 当前定位：本文件是 `scripts/` 检视意见的主索引。下方 2026-06-17 矩阵只保留为历史路由，不再声称覆盖当前全部文件；是否需要修复，以上方 findings-first 为准。
- 复核方法：按职责把旧 GDScript、旧路径和旧类型映射到当前 C#、GDScript 或 Python owner，再检查同一失败模式是否仍存在。不能以语言迁移、文件改名或模板命中本身作为“已修复”或“仍有问题”的证据。

## 当前确认需要修复（findings-first）

### 中优先级

1. **部分 AI evaluator 的 trace span 仍不具备异常安全。** `BattleAiChargeActionEvaluator`、`BattleAiChargePathAoeActionEvaluator`、`BattleAiMoveToRangeActionEvaluator` 仍有裸 `AiTraceRecorder.Enter/Exit`；中间异常会留下栈帧，后续 trace mismatch。项目已有 exception-safe `BattleAiTraceSpan`，不能因为旧 GDScript owner 已迁移就删除这类 finding。
2. **AI action 与 skill 的 target mode 不匹配时内容校验仍会放行。** `EnemyAiAction.cs:42-54` 只检查 skill id；unit/ground evaluator 到运行时才跳过不匹配技能，形成“内容校验通过但 action 永远无候选”。
3. **AI 同分目标缺少稳定 id 兜底。** `BattleAiTypedActionHelper.cs:401-416,454-483` 只比较距离和 HP，输入来自 `BattleState` 的 Dictionary values；完全同分时结果依赖枚举顺序。
4. **AI 普通伤害估算仍绕开部分正式减伤语义。** `BattleAiScoreService.Scoring.cs:685-753` 的估算路径把 `ShieldAbsorbed` 固定为 0，可能高估打不穿目标的伤害与击杀线。

### 低优先级但逻辑确实不闭合

- **接受风险，暂不修复**：`UnitProgress.cs:225-251` 在递归访问子节点后才写 `visited`，循环 merge source 会无限递归；已确认当前递归 getter 没有生产调用，并在 API 旁注明无环前提和未来接线要求。
- `QuestProgressService.RecordProgress(...)` 直接把 active quest 标成 completed，却不迁移到 claimable；`PartyState.SetQuestState(...)` 还会把 failed 状态放进 active。两者当前生产链没有调用，但一旦使用会生成反序列化拒绝的状态。
- `BattleTerrainGenerator` 的 typed cells → Godot Dictionary → typed cells 往返，以及 settlement handler/forge/shop 核心的 `GDictionary` 状态处理仍是架构债；当前没有证据把它们升级成 correctness bug。

## 已移除的过时结论

- 据点顶层商店 seed/刷新步数契约漂移已于 2026-07-23 解决：删除 `shop_inventory_seed` / `shop_last_refresh_step` 镜像，只由每个商店子状态持有实际 seed、刷新步数和库存；刷新仍为彼此独立的真随机，且只更新目标商店。破坏性 schema 变更归入 v15，v14 直接拒绝且不提供迁移。
- 原 findings-first 中的晋升信号/BBCode、旧 AI trace scope 与 Variant 计时、装备 discard-all、骰子字符串虚调用、图标路径、encounter anchor 恢复等具体问题均已由当前实现覆盖。
- simulation 终止状态现已区分 battle ended、idle stall、iteration budget exhausted 与 invalid runtime；未完成 runs 保留诊断但不再进入胜率、均值、技能/action/faction 汇总，CLI 对不完整实验返回非零。两个手写 benchmark 汇总器与冻结 6v12 runner 的分析包消费者也按相同完成态规则过滤。
- Faction action/skill 报告缺口已于 2026-07-22 修复：正式单局 JSON、Godot report projection、trace summary、profile summary、冻结 6v12 runner 的 run detail 与 analysis packet 现在都会保留每个 faction 的 `action_counts`、`skill_attempt_counts` 和 `skill_success_counts`；汇总仍只纳入 battle-ended runs，残缺输入缺少任一计数表时标为 unavailable 而不伪装成零。
- 冻结 6v12 runner 的 raw aggregate 混合口径已于 2026-07-22 修复：执行循环的 `termination_kind` 现在是逐 run 完成态的唯一真相源，raw `runs[]` 显式输出 `battle_ended / termination_kind`；`run_count` 表示尝试数，`completed_run_count` 表示 battle-ended 数，未完成局只保留诊断并按 idle/budget/invalid 分类，不再污染胜率、回合、技能、伤害、mastery 或 per-unit 汇总。请求轮数未全部正常结束时 raw 与 trace summary 都保持 `is_complete=false`，trace summary 同步保留请求数与超时事实，runner 返回 `2`；旧报告的保守 packet fallback 继续保留。
- AI trace 关闭路径的无效构造已于 2026-07-22 修复：公共 trace 入口会在 `trace_enabled=false` 时直接返回，12 个 evaluator 也会在构造 action metadata、候选摘要及其 extra 字典前门控；关闭时不再消耗 trace nonce、复制候选或维护 Top 5，开启时保留原 trace 结构与决策结果。
- 已不存在的 `RaceTraitContentRegistry.cs`、`RaceTraitDef.cs`、`TestCsBase.cs`、`BattleSimTerrainGenerator.cs`、`SkillEffectiveCombatProfile.cs`、`SkillEffectiveCombatProfileResolver.cs`、`PartyMemberOptionUtils.cs` 条目已从矩阵删除；其仍有意义的失败模式已映射到当前 owner 后重新判断。
- 旧 #22–#33 action 条目的运行时方法、规模和 owner 已整体迁入 `scripts/systems/battle/ai/*Evaluator.cs`，旧条目已删除；其中 trace 异常安全、随机链 seed、retreat/multi-target 限制等失败模式先在当前 C# owner 上重新核验，再分别提升为 finding 或降为 AI quality limitation。
- 旧 weak-owner、settlement transaction、UI Hide/Signal、RichText、DTO `ToDictionary` 等泛化模板已有 typed owner、集中 teardown 或回归覆盖，不再作为待修问题。
- 旧“仓库批量交换忽略普通物品存入失败”结论已移除：普通单件 deposit 的 preview 与实际添加在同一事务态、相同容量/堆叠算法下连续执行，preview 失败会阻断并回滚，不存在原结论描述的普通物品漂移。
- 技能等级描述表达式死循环已于 2026-07-20 修复：表达式和变量 token 只处理一轮，失败字段原位显示 `[描述配置错误]` 且继续渲染后续字段；内容校验会提前拒绝空、未闭合及语法错误表达式。
- 装备能力 registry 的校验/投影顺序已于 2026-07-21 修复：validation 已产生错误时不再进入 Resource→Definition 投影；错误类型的嵌套条件组会由 `Rebuild` 返回 `EQA_CONDITION_GROUP_TYPE_INVALID`，而不会被投影层 `InvalidOperationException` 替代。失败构建仍不发布候选快照。
- 任务存档的 `last_progress_context` 安全拒绝已于 2026-07-21 修复：底层 context parser 继续严格抛出 `ArgumentException`，`QuestState.FromDictionary` 在存档 DTO 边界将其转换为 `null`，使 `SaveSerializer` 对损坏输入返回 `Error.InvalidData`，不会中断存档读取链。
- BattleSim 异常路径清理已于 2026-07-21 修复：`AiTraceRecorder` 的实例作用域在成功和异常时都会恢复此前 recorder，保留外层 profiler；`BattleSimRunner` 在 setup、开战、执行循环或结果采集失败时都会释放本轮 runtime，主体与 teardown 同时失败时保留两条异常。共享的 caller-owned terrain generator 不由单轮 runtime 误释放。
- BattleSim 报告写盘契约已于 2026-07-21 修复：`BattleSimReportFileWriter` 使用秒级时间加 GUID 生成批次名；report、trace 与存在 trace 时的 summary 全部确认写入后才返回路径并打印成功。打开、逐次写入或 flush 失败会恢复此前 `OutputFiles`、清理本批次残缺产物并向上传播，不再出现假成功或同秒覆盖。
- `scripts/tools/tree_baker.gd` 与 `tile_baker.gd` 于 2026-07-22 按范围决策移出修复清单：它们是人工使用的非生产工具，不属于正式运行链或资产构建验收。本次排除依据是 owner/交付范围，不是 GDScript 语言；生产链中的同类失败模式仍需正常处理。
- `PartyState` 的“空 roster 会写出空 leader 坏档”结论已移除：玩法硬约束要求主角始终上阵，正式编成入口禁止将主角移入替补并保持 active 非空；主角死亡产生的空 roster 只属于 Game Over 临时态，该结算分支跳过写盘并丢弃 pending save，现有回归验证重新载入仍回到战前存档。当前没有允许全灭状态继续保存的需求。
- 仓库无实例 payload 装备的 allocator 失败原子性缺口已于 2026-07-22 修复：`PartyWarehouseService` 会检查正式添加结果，实例 id 分配失败时返回 `warehouse_blocked_swap`，现有 batch transaction 随即恢复此前的取出操作；回归覆盖空 allocator 下材料保留、装备不写入。
- 原 466 项矩阵主要由少量风险模板批量生成；“需跟进”只表示当时的检查方向，不能等同于 finding。没有当前调用链、失败模式和代码证据的模板项不进入修复清单。

## 逐文件深度检视矩阵

本节保留 2026-06-17 对当时 466 个脚本生成的逐文件索引。每个条目包含当时的文件职责、关键入口/类型、状态与边界、对抗性失败模式和建议验证；路径、规模、owner 与方法列表可能已经迁移。这里的“风险”不是 bug 结论，当前真正确认的问题只以上方 findings-first 为准。

### 1. `scripts/dev_tools/AiTraceRecorder.cs`

- 复审状态：**通过**；规模：330 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：AiTraceRecorder；主要方法：Enter, Exit, SetEventCaptureEnabled, SetSampleCaptureEnabled, HasInstance, GetInstance, SetInstance, _enter_impl, _exit_impl, GetFuncStats。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×2; runtime mutation collections×12。
- 当前结论：process-global recorder 可通过嵌套 instance scope 临时替换；scope 在成功或异常退出时恢复此前实例，后续静态 Enter/Exit 继续写入外层 recorder。
- 建议验证：`run_ai_trace_recorder_regression.cs`。

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

- 复审状态：**确认问题**；原规模：1547 行；当前文件已缩为 authoring/validation schema，执行 owner 已迁至 evaluator。
- 关键类型/入口：EnemyAiAction, struct；主要方法：GetDeclaredSkillIds, ValidateSkillReferences, _collect_base_validation_errors, _is_supported_target_selector, _append_enemy_focus_target_selector_errors, _append_declared_skill_id, _create_decision, _create_scored_decision, _resolve_known_skill_ids, _get_skill_def。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×29; runtime mutation collections×66。
- 当前 finding：skill id 存在即可通过引用校验，没有验证 action 需要的 unit/ground target mode；错误配置只会在运行时被 evaluator 静默跳过。
- 建议验证：unit action 引用 ground skill、ground action 引用 unit skill 时，内容 registry 应直接报错。

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

- 复审状态：**通过**；规模：392 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestStatusKind, QuestState；主要方法：IsActive, IsCompleted, IsTerminal, ToStringName, ToStatusKind, GetObjectiveProgress, RecordObjectiveProgress, IsObjectiveComplete, HasCompletedAllObjectives, MarkAccepted。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×2; runtime mutation collections×5。
- 对抗性检视：`last_progress_context` 的严格解析错误会在 `QuestState` 边界安全拒绝；底层 parser 的类型约束未被放宽。
- 已验证：typed party/quest state 回归与 save serializer quest round-trip 回归均覆盖损坏 context 的安全拒绝。

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

- 复审状态：**接受风险，暂不修复**；规模：1411 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：UnitProgress；主要方法：SetSkillProgress, GetSkillProgress, RemoveSkillProgress, SetProfessionProgress, GetProfessionProgress, RemoveProfessionProgress, SetAchievementProgressState, GetAchievementProgressState, HasKnowledge, LearnKnowledge。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×92; save/schema/projection×14; runtime mutation collections×88。
- 当前 finding：递归展开 merge source 时在访问子节点之后才把当前 id 放入 `visited`，A → B → A 会栈溢出；已确认当前递归 getter 无生产调用，按范围决策接受风险，不修改算法或读档契约。
- 处理决策：代码旁已注明无环前提；只有未来准备接入生产调用时，才必须先补循环来源回归、在进入递归节点前标记 visiting/visited，并在写入/读档边界拒绝环。

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

- 复审状态：**通过（2026-07-20 已修复）**；规模：186 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiDecisionCommitter, DecisionStatePatch；主要方法：BuildTypedStatePatch, AttachStatePatch, Commit, FromDecision, ApplyTo, SetBlackboardText。
- Godot/公开边界：Export 0 处；Signal 0 处；关键触点：decision patch 的生产提交链。
- 当前验证：`BattleRuntimeModule.advance(...)` 在 `advance:ai_decision_commit` 阶段、执行 command 前提交 patch；`run_battle_ai_melee_charge_behavior_regression.cs` 通过真实 AI turn 覆盖 state transition、blackboard 与 turn decision count 恰好提交一次。

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
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
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
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 150. `scripts/systems/battle/ai/BattleAiScoreService.Projection.cs`

- 复审状态：**需跟进**；规模：486 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, ThreatProjection, ThreatSkillEntry, ThreatProfile；主要方法：ShouldPopulateSurvivalProjection, GetCurrentActorThreatProjection, GetProjectedActorThreatProjection, SubtractSuppressedThreatsFromProjection, BuildProjectionSuppressionSignature, EmptyThreatProjection, ResolveProjectedActorCoord, ResolveActorSurvivalBudget, BuildSuppressedThreatUnitIds, CollectActorThreatProjection。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×24。
- 对抗性检视：
  - 未命中特定高危模式；仍需按职责检查 null 输入、非法 id、重复 id、空集合和资源缺失。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 151. `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`

- 复审状态：**确认问题**；规模：1384 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, DamageEstimateResult, DamageSaveEstimate, DamageEstimateBreakdown, DamagePreviewSnapshot, PathStepHitCountEntry；主要方法：Clone, Scaled, ToDictionary, FromPreviewSaveEstimate, FromPreviewResult, CloneSaveEstimates, CloneDamageEstimates, CloneTraceObjectList, CloneTraceObject, CloneTraceEnumerable。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×254; runtime mutation collections×79。
- 当前 finding：普通伤害估算路径没有完整复用正式 preview/mitigation 语义，并把 `ShieldAbsorbed` 固定为 0，会高估部分目标的实际承伤与击杀线。
- 建议验证：用相同 source/target 对比 AI estimate 与正式 preview，覆盖 shield、guard、fixed mitigation。

### 152. `scripts/systems/battle/ai/BattleAiScoreService.cs`

- 复审状态：**需跟进**；规模：859 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiScoreService, struct, struct, struct, ScoreBuildMetadata, ScoreRandomChainMetadata, ScorePositionMetadata, ScorePathStepAoeMetadata；主要方法：FromMetadata, Setup, SetProfile, BeginDecisionScope, EndDecisionScope, GetProfile, GetBucketPriority, BuildSkillScoreInput, BuildActionScoreInput, ResolvePrimaryCoord。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×2; runtime mutation collections×46; hot path / lifecycle×13。
- 建议验证：tests/battle_runtime/ai/ 下最近的 focused runner；避免默认跑 battle simulation/balance。

### 153. `scripts/systems/battle/ai/BattleAiService.cs`

- 复审状态：**需跟进**；规模：231 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiService；主要方法：Setup, SetScoreProfile, GetScoreProfile, GetScoreService, ChooseCommand, ChooseCommandImpl, BuildActionMutationCheckpoint, AbortMutationViolation, BuildWaitDecision, IsEmpty。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×3; hot path / lifecycle×4。
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

- 复审状态：**确认问题**；规模：608 行；上下文：Battle AI/enemy content：重点检查 preview/commit 分离、mutation guard、score trace、content id。
- 关键类型/入口：BattleAiTypedActionHelper；主要方法：ResolveKnownSkillIds, GetSkillDef, GetSkillCastBlockReason, SortTargetUnits, GetUnitCastVariants, CollectUnitSkillEffectDefs, GetCastVariantTargetModeKind, BuildUnitSkillCommand, CreateDecision, CreateScoredDecision。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×26。
- 当前 finding：目标比较只覆盖距离与 HP；完全同分时没有稳定 `unit_id` 兜底，选择结果依赖 Dictionary 枚举顺序。
- 建议验证：反转单位插入顺序后，相同战局应仍选择同一稳定 id。

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

- 复审状态：**已排除旧 finding**；规模：358 行；上下文：Battle runtime/rules。
- 关键类型/入口：BattleDamageResolver；主要方法：RollDamageDice, RollBonusDamageDice, RollWeaponDice, RollDicePool, RollDicePoolValues, RollDamageDieVirtual, BuildDicePoolTotal, BuildPreviewDiceRolls, BuildDamageDiceEventFlags, ApplyDamageDiceEventFlags。
- 当前结论：旧 `GodotObject.Call` 字符串虚调用已经不存在，骰子 override 走 typed/virtual API；Dictionary 触点本身没有当前失败证据。
- 建议验证：维持 damage dice override 与 preview/execution 回归，不列为待修问题。

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

- 复审状态：**通过（2026-07-20 随机契约确认）**；规模：2709 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleDamagePreviewRollMode, BattleDamagePreviewSaveMode, BattleDamagePreviewOptions, BattleDamageResolver, struct, struct, struct, struct；主要方法：FromEffect, ToDictionary, None, ToPreviewSaveEstimate, WithHpDamage, WithResolvedDamage, ToDamageApplicationInput, Create, FromDictionary, ReadBool。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×134; save/schema/projection×286; runtime mutation collections×48; randomness/determinism×2。
- 当前验证：正式伤害骰直接使用 `TrueRandomSeedService` 符合设计；battle seed 只控制地形，不能也不应决定伤害序列。需要稳定边界测试时应注入显式 roll override，不得把 production combat RNG 改成 seeded replay。

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

- 复审状态：**通过（2026-07-20 随机契约确认）**；规模：1717 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleHitResolver；主要方法：ResolveRepeatAttackStageHit, BuildRepeatAttackStageHitCheck, BuildFateAwareRepeatAttackStageHitCheck, BuildRepeatAttackPreview, BuildSkillAttackPreview, BuildForceHitNoCritAttackPreview, BuildSkillAttackCheck, _get_unit_attribute_value, _unit_has_attribute_value, _get_target_armor_class。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×24; runtime mutation collections×14; randomness/determinism×4。
- 当前验证：命中、暴击和 reroll 使用独立 `TrueRandomSeedService` 是正式契约；`attack_roll_nonce` 是消费计数，不是由 battle seed 派生随机序列的输入。

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

- 复审状态：**通过（2026-07-20 随机契约确认）**；规模：905 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSaveDegreeKind, struct, BattleSaveContext, struct, struct, BattleSaveResolver, BattleSaveTagState；主要方法：ToDictionary, ForSkill, WithSaveRollOverride, WithSaveRollOverrides, Empty, SourceArray, ResolveSaveResult, ResolveSaveDegree, EstimateSaveSuccessProbabilityResult, ResolveSaveDc。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×136; runtime mutation collections×17; randomness/determinism×4。
- 当前验证：正常、优势和劣势豁免骰使用独立 `TrueRandomSeedService` 符合正式契约；固定 battle seed 只应复现地形，不应复现豁免结果。

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
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 248. `scripts/systems/battle/runtime/BattleBarrierService.cs`

- 复审状态：**需跟进**；规模：684 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, struct, BattleBarrierService, struct；主要方法：Empty, FromEffect, Setup, Dispose, ApplyLayeredBarrierEffectResult, AdvanceBarrierDurations, ResolveUnitBoundaryCrossingResult, ResolveSkillBarrierInteractionResult, ResolveGroundBarrierInteractionResult, _ResolveProjectedEffectBarrierInteractionResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×9; save/schema/projection×14; runtime mutation collections×19。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 249. `scripts/systems/battle/runtime/BattleCastingTimeService.cs`

- 复审状态：**需跟进**；规模：720 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleCastingTimeService, struct；主要方法：Setup, Dispose, TryHandleCastingSkillStart, PreviewCancelCast, HandleCancelCast, ReconcilePendingCasts, AdvancePendingCasts, CompleteReadyPendingCasts, BuildStartPayload, ResolveCastingSpellControl。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×5; runtime mutation collections×9; randomness/determinism×2。
- 对抗性检视：
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 250. `scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs`

- 复审状态：**需跟进**；规模：1271 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleChangeEquipmentResult, ChangeEquipmentRuleResult, BattleChangeEquipmentResolver；主要方法：Clone, ToDictionary, CloneStringNameList, StringifyStringNames, Setup, Dispose, PreviewCommand, HandleCommand, GetUnitHpMax, GetUnitStaminaMax。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×12; save/schema/projection×3; runtime mutation collections×32。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 251. `scripts/systems/battle/runtime/BattleChargeResolver.cs`

- 复审状态：**需跟进**；规模：1971 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleChargeResolver, struct, PathStepResult, ChargeBlockerResult, SidePushResult, TrapResult, ChargeTargetInfo；主要方法：FromEffect, Setup, DisposeRuntime, handle_charge_skill_command, handle_charge_skill_command_result, ValidateChargeCommandResult, BuildChargeStepAoePreviewCoords, GetChargePathStepAoeEffectDef, IsChargeOption, GetChargeEffectDef。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×36; save/schema/projection×1; runtime mutation collections×57。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
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
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 266. `scripts/systems/battle/runtime/BattleMovementService.cs`

- 复审状态：**通过**；规模：541 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleMovementService, struct；主要方法：Setup, Dispose, RecordActionIssued, AppendChangedCoords, AppendChangedUnitCoords, SortCoords, IsMovementBlocked, HasStatus, GetUnitReachableMoveCoords, GetMoveCostForUnitTarget。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：runtime mutation collections×26; hot path / lifecycle×1。
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
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 269. `scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs`

- 复审状态：**需跟进**；规模：928 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRepeatAttackResolver, struct；主要方法：FromEffect, GetStageDamageMultiplier, Setup, DisposeRuntime, ApplyRepeatAttackSkillResult, get_repeat_attack_effect_def, CollectRepeatAttackBaseEffects, BuildRuntimeStageSpec, BuildStageSpecFromRepeatAttackEffect, BuildStageSpecsFromRepeatAttackEffect。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; runtime mutation collections×5。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 270. `scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs`

- 复审状态：**需跟进**；规模：596 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRuntimeLootResolver, ParsedDropDefinition；主要方法：ResolveWeakRef, Setup, Dispose, CollectDefeatedUnitLoot, BuildBattleResolutionResult, _IsEliteOrBossTarget, _CollectDefeatedUnitLoot, _ResolveEnemyTemplateForUnit, _BuildDefeatedUnitLootEntries, _ParseDropDefinition。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; save/schema/projection×5; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 271. `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

- 复审状态：**需跟进**；规模：5121 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleRuntimeDictionaryOptions, BattleDefeatHandlingOptions, BattleStartOptions, BattleEndOptions, BattleStartFailureSnapshot, BattleRuntimeModule, in；主要方法：ReadBool, FromContext, FromDictionary, ToDictionary, ReadOptionalInt, ReadOptionalLong, ReadReachabilityPayload, ReadString, setup, FinishSetup。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×135; save/schema/projection×16; runtime mutation collections×190; hot path / lifecycle×19。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 272. `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`

- 复审状态：**需跟进**；规模：1802 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, struct, BattleRuntimeSkillTurnResolver；主要方法：Empty, Setup, DisposeRuntime, ResolveTurnControlStatusResult, IsTurnAiOverrideActive, ClearTurnAiOverride, BuildMadnessFallbackCommand, GetSkillCastBlockReason, UnitHasMeleeWeapon, UnitMatchesRequiredWeaponFamilies。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×18; runtime mutation collections×17。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 273. `scripts/systems/battle/runtime/BattleShieldService.cs`

- 复审状态：**需跟进**；规模：536 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：struct, BattleShieldService；主要方法：ToDictionary, Setup, DisposeRuntime, ApplyUnitShieldEffectsResult, ApplyShieldEffectToTargetResult, _write_unit_shield, BuildUnitShieldResult, _resolve_shield_hp, ResolveShieldHp, _roll_shield_hp。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×4; runtime mutation collections×21; randomness/determinism×2。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 274. `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`

- 复审状态：**需跟进**；规模：3986 行；上下文：Battle runtime/rules：重点检查 AP/TU/cost/cooldown、preview-vs-execution、state mutation。
- 关键类型/入口：BattleSkillExecutionOrchestrator, struct, UnitSkillEffectResolution；主要方法：FromEffect, Setup, DisposeRuntime, append_result_report_entry, MarkAppliedStatusesForTurnTiming, append_result_source_status_effects, _record_action_issued, _record_skill_attempt, _record_effect_metrics, _record_unit_defeated。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×50; save/schema/projection×5; runtime mutation collections×115; hot path / lifecycle×17; randomness/determinism×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
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

- 复审状态：**通过**；规模：271 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimExecutionLoop, BattleSimExecutionLoopResult；主要方法：Run, AdvanceStep, HasReadyUnits, HasProgressed, IssueManualPolicy, GetUnit, PrintTraceStats, ReadInt64。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×1; runtime mutation collections×2。
- 当前结论：终止状态传播、completed-only 汇总与 incomplete CLI 退出码已有 synthetic 回归；loop trace 仅在显式开启时安装局部 recorder，并通过 instance scope 在成功或异常退出时恢复此前 recorder。
- 建议验证：`run_ai_trace_recorder_regression.cs` 与 `run_battle_sim_exception_cleanup_regression.cs`。

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

- 复审状态：**通过**；规模：86 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimProfileSummary；主要方法：ToDictionary, ToIntDictionary, ToFloatDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×2; runtime mutation collections×9。
- 当前结论：保留全部 attempt 数并独立暴露 completed/unfinished/stalled/budget/invalid 计数；正常统计分母只使用 completed runs。
- 建议验证：`run_battle_sim_report_builder_regression.cs`。

### 303. `scripts/systems/battle/sim/BattleSimReportBuilder.cs`

- 复审状态：**通过**；规模：351 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimReportBuilder；主要方法：BuildProfileSummary, BuildProfileComparisons, MergeSkillCounter, MergeActionChoices, MergeFactionMetricTotals, IncrementCounter, CollectStringKeys, GetDictionary, TryGetVariantKey。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×13; runtime mutation collections×29。
- 当前结论：winner、平均值、skill/action/faction totals 与 profile comparison 均只消费 battle-ended runs；无 completed sample 时不生成 comparison。
- 建议验证：`run_battle_sim_report_builder_regression.cs`。

### 304. `scripts/systems/battle/sim/BattleSimRunReport.cs`

- 复审状态：**通过**；规模：66 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimRunReport；主要方法：ToDictionary, ToGodotTraceArray。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×7; save/schema/projection×2; runtime mutation collections×6。
- 当前结论：typed `TerminationKind` 是完成态真相，`battle_ended`/`stalled` 为派生事实；file 与 Godot projection 均保留明确终止原因。
- 建议验证：`run_battle_sim_report_builder_regression.cs` 与 trace projection lease regression。

### 305. `scripts/systems/battle/sim/BattleSimRunner.cs`

- 复审状态：**通过**；规模：467 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimRunner；主要方法：Setup, SetProgressLoggingEnabled, SetProgressLogPath, RunScenario, _ResolveProfiles, _RunSingleSimulation, _BuildEncounterAnchor, _CountLivingUnits, _BuildFinalUnitSnapshots, CloneAiTurnTraces。
- Godot/公开边界：Export 2 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×17; save/schema/projection×26; runtime mutation collections×20。
- 当前结论：runner 将文件 IO 委托给 `BattleSimReportFileWriter`；只有完整产物集写入成功才发布 output paths 与 `report-written`。runtime 创建后的 setup/start/loop/result-capture 异常均会触发完整 teardown，且不会误释放 caller-owned 的共享 terrain generator；stalled/budget/invalid 终止状态已完整传入报告并从正常汇总排除。
- 建议验证：`run_battle_sim_exception_cleanup_regression.cs` 与 `run_battle_sim_report_output_regression.cs`。

### 306. `scripts/systems/battle/sim/BattleSimScenarioDef.cs`

- 复审状态：**需跟进**；规模：231 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimScenarioDef；主要方法：ResolveSeeds, BuildStartContext, ToDictionary, _build_unit_payloads, _build_spawn_coords, _build_cells, _resolve_override_coord, _apply_cell_override。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×20; save/schema/projection×3; runtime mutation collections×9。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 307. `scripts/systems/battle/sim/BattleSimScenarioReport.cs`

- 复审状态：**通过**；规模：34 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：BattleSimScenarioReport；主要方法：ToDictionary。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×8; save/schema/projection×5; runtime mutation collections×4。
- 当前结论：scenario 从原始 runs 派生完整性与各终止类别计数，避免只靠路径存在或 runner 返回误判实验成功。
- 建议验证：`run_battle_sim_report_builder_regression.cs`。

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
- 建议验证：tests/battle_runtime/runtime|rules|skills 下最近的 focused runner。

### 316. `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`

- 复审状态：**架构债，非 correctness finding**；规模：1776 行；上下文：battle terrain generation。
- 关键类型/入口：BattleTerrainProfileKind, BattleTerrainGenerator, TerrainQualityResult；主要方法：ToStringName, ToProfileKind, Generate, ResolveTerrainProfileId, NormalizeWaterHeights, GenerateDefault, GenerateCanyon, GenerateNarrowAssault, GenerateHoldoutPush, BuildLayout。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×103; save/schema/projection×3; runtime mutation collections×61; randomness/determinism×2。
- 当前结论：terrain RNG 已受稳定 seed 驱动；剩余问题是正式算法仍做 typed cells → `GDictionary` → typed cells 往返，增加分配与双重校验漂移风险。
- 建议验证：保持固定 seed 地形回归；只有重构 typed boundary 时才补 allocation/投影等价性测试。

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
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 321. `scripts/systems/content/GameRoot.cs`

- 复审状态：**通过**；规模：47 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：GameRoot；主要方法：BindSession, DisposeOwnedRuntimeResources, HasSessionTyped, GetSessionTyped, GetContentCatalogTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：未命中高危触点。
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
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 330. `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`

- 复审状态：**需跟进**；规模：899 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeBattleLootCommitService, ItemCommitResult, BattleLootCommitResult；主要方法：Create, Success, Setup, CommitBattleLootToSharedWarehouseTyped, ClearRegularBattleCalamityShardFlags, BuildBattleResolutionStatusMessageTyped, BuildLastBattleLootSnapshotTyped, FormatBattleDropEntries, CommitBattleLootToSharedWarehouseInternal, CommitFixedItemLootEntry。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×44; save/schema/projection×7; runtime mutation collections×15。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 331. `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`

- 复审状态：**需跟进**；规模：2094 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeBattleSelection；主要方法：Setup, GetSelectedBattleSkillName, GetSelectedBattleSkillVariantName, GetSelectedBattleSkillTargetCoords, GetSelectedBattleSkillTargetUnitIds, GetSelectedBattleSkillValidTargetCoords, GetSelectedBattleSkillRequiredCoordCount, SelectBattleSkillSlotTyped, CycleSelectedBattleSkillOption, ClearBattleSkillSelection。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; save/schema/projection×4; runtime mutation collections×62。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
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
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 334. `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`

- 复审状态：**需跟进**；规模：487 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeCharacterInfoBuilder；主要方法：Setup, Dispose, BuildCharacterInfoMetaLabel, BuildWorldCharacterInfoSections, BuildBattleCharacterInfoSections, BuildBattleCharacterIdentityEntries, BuildBattleCharacterInfoFatePayload, BuildBattleCharacterInfoBaseEntries, BuildBattleCharacterStatusEntries, BuildBattleCharacterSkillEntries。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×22; runtime mutation collections×17。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 335. `scripts/systems/game_runtime/GameRuntimeCommandLogger.cs`

- 复审状态：**需跟进**；规模：599 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeCommandLogger, CommandLogScope；主要方法：Empty, Create, Clone, BuildContext, MarkLogged, Setup, Dispose, BeginLoggedCommand, FinishLoggedCommand, LogActiveCommandScopeResult。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×19; save/schema/projection×16; runtime mutation collections×21。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 336. `scripts/systems/game_runtime/GameRuntimeFacade.cs`

- 复审状态：**需跟进**；规模：3882 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeFacade, RuntimeCommandCode, RuntimeCommandResult；主要方法：Success, Failure, ToDictionary, Setup, RebuildWildEncounterRosterDefIndex, BindRuntimeSidecarOwners, GetStatusText, GetLogSnapshot, GetRecentLogs, GetActiveLogFilePath。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×270; save/schema/projection×28; runtime mutation collections×114。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 337. `scripts/systems/game_runtime/GameRuntimePartyCommandHandler.cs`

- 复审状态：**需跟进**；规模：742 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimePartyCommandHandler；主要方法：Setup, Dispose, CommandOpenPartyTyped, CommandSelectPartyMemberTyped, CommandSetPartyLeaderTyped, CommandMoveMemberToActiveTyped, CommandMoveMemberToReserveTyped, CommandApplyPartyRosterTyped, CommandPartyEquipItemTyped, CommandPartyUnequipItemTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：save/schema/projection×7; runtime mutation collections×4。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
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
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 341. `scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs`

- 复审状态：**需跟进**；规模：779 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeRewardFlowHandler；主要方法：Setup, Dispose, GetCurrentPromotionPrompt, CommandConfirmPendingRewardTyped, CommandChoosePromotionTyped, CommandSubmitPromotionChoiceTyped, CommandCancelPromotionChoiceTyped, CommandConfirmActiveRewardTyped, CommandCloseActiveModalTyped, OnCharacterInfoWindowClosed。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; runtime mutation collections×1。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 342. `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`

- 复审状态：**架构债，非 correctness finding**；当前主文件约 3037 行；上下文：Game runtime settlement orchestration。
- 关键类型/入口：GameRuntimeSettlementCommandHandler, SettlementActionValidationResult, ContractBoardQuestData, SettlementServiceEntryResolution, StagecoachDestinationData, SettlementPersistResult；主要方法：Success, Failure, ToDictionary, Missing, FromServiceData, SetupRuntime, DisposeRuntime, GetSettlementWindowData, GetShopWindowData, GetContractBoardWindowData。
- Godot/公开边界：Export 0 处；Signal 2 处；风险触点：Godot Dictionary/Array boundary×270; save/schema/projection×9; runtime mutation collections×86; randomness/determinism×1。
- 当前结论：weak owner/teardown 与 settlement persist rollback 已有实现和回归；剩余是 action dispatch/build/persist 的核心状态仍大量使用 `GDictionary`，属于 typed-boundary 债。
- 建议验证：保持 world-map proxy 与 persist-failure rollback 回归；不要把 Dictionary 命中本身登记为 bug。

### 343. `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`

- 复审状态：**需跟进**；规模：977 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeSnapshotBuilder；主要方法：Setup, Dispose, BuildHeadlessSnapshot, BuildTextSnapshot, BuildWorldSnapshot, BuildSubmapSnapshot, BuildGameOverSnapshot, BuildPartySnapshot, BuildQuestSnapshot, BuildQuestEntries。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×46; save/schema/projection×7; runtime mutation collections×43。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs。

### 344. `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`

- 复审状态：**需跟进**；规模：806 行；上下文：Game runtime facade/handler：重点检查 modal state、world/battle切换、handler ownership、typed boundary。
- 关键类型/入口：GameRuntimeWarehouseHandler, WarehouseTransactionSnapshot；主要方法：Setup, Dispose, GetWarehouseWindowData, CommandOpenPartyWarehouseTyped, CommandDiscardOneTyped, CommandDiscardAllTyped, CommandUseItemTyped, CommandAddItemTyped, OpenPartyWarehouseWindow, OnPartyWarehouseWindowClosed。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×3; save/schema/projection×2; runtime mutation collections×12。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
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

- 复审状态：**通过**；规模：1203 行；上下文：Inventory/warehouse/equipment：重点检查容量、实例 id、stack/equipment 分支、persist rollback。
- 关键类型/入口：PartyWarehouseService, WarehouseBatchItemEntry, WarehouseBatchSwapResult, WarehouseAddItemResult, WarehouseRemoveItemResult；主要方法：ToDictionary, Success, Blocked, WithError, Setup, SetupPartyBackpackView, GetTotalCapacity, GetUsedSlots, GetFreeSlots, IsOverCapacity。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×23; save/schema/projection×6; runtime mutation collections×54。
- 当前结论：普通物品 preview/commit 使用相同容量与堆叠规则；无实例 payload 装备的正式添加结果也已检查，allocator 返回空值时 batch 会失败并回滚此前取出。
- 建议验证：容量失败和装备实例 id 分配失败时 batch 均原子回滚并保持原状态。

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

- 复审状态：**已排除旧风险**；规模：523 行；上下文：Progression/content。
- 关键类型/入口：CharacterProgressionDelta；主要方法：SetLeveledSkillIds, AddLeveledSkillId, SetGrantedSkillIds, AddGrantedSkillId, SetChangedProfessionIds, AddChangedProfessionId, HasChangedProfessionId, SetPendingProfessionChoices, AddPendingProfessionChoice, SetMasteryChanges。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×48; save/schema/projection×6; runtime mutation collections×51。
- 当前结论：正式 owner 使用 CLR typed lists 和 typed views；Godot Dictionary 只用于兼容投影/解析边界，没有证据支持旧“Dictionary 回流业务态”意见。
- 建议验证：维持现有 projection round-trip 回归即可，不列为待修问题。

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
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 388. `scripts/systems/progression/QuestCommandResultData.cs`

- 复审状态：**需跟进**；规模：290 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestSubmitItemResultData, QuestClaimResultData；主要方法：ContainsClaimableQuest, ToDictionary, Success, Failed, ContainsStringName, CloneStringNameList, ToStringNameArray, CloneItemRewards, ClonePendingCharacterRewards, CloneUnsupportedRewardTypes。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×25; save/schema/projection×2; runtime mutation collections×22。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 389. `scripts/systems/progression/QuestProgressService.cs`

- 复审状态：**确认低优先级 API 问题**；规模：1004 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：QuestProgressEventKind, QuestProgressService, QuestActiveObjectiveMatch, QuestProgressEventData, QuestObjectiveDefData, QuestProgressDataReader, QuestProgressApplyResultData, QuestProgressEventContextData；主要方法：ToStringName, ToEventKind, Setup, SetPartyState, Dispose, GetPartyState, GetActiveQuestsTyped, GetClaimableQuestsTyped, GetClaimableQuestIdsTyped, GetCompletedQuestIdsTyped。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×39; save/schema/projection×9; runtime mutation collections×46。
- 当前 finding：公开 `RecordProgress` 完成全部目标时只改 quest status，不把对象从 active 迁到 claimable；当前生产链使用另一套 event API。
- 建议验证：直接 API 与 event API 必须得到相同 active/claimable 状态和可 round-trip 存档。

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

- 复审状态：**通过（2026-07-20 已修复）**；规模：591 行；上下文：Progression/content：重点检查 typed resource、rank/skill/profession ids、schema validation。
- 关键类型/入口：SkillLevelDescriptionFormatter；主要方法：BuildLevelDescription, RenderTemplate, MergePlainMap, _is_optional_value_visible, _merge_matching_effect_params, _merge_matching_effect_typed_fields, _collect_level_effect_defs, _append_level_effect_defs, _effect_unlocked_at_level, _try_evaluate_expression。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×6; runtime mutation collections×31。
- 当前验证：expression 与普通变量均使用单次替换，不重扫替换产物；parse/execute 失败、自引用或间接循环字段显示 `[描述配置错误]`，后续字段继续渲染。内容规则同步拒绝空、未闭合与语法错误表达式。
- 回归验证：`run_level_description_template_regression.cs` 覆盖 parse/execute 失败、自引用和间接循环；`run_skill_level_description_typed_regression.cs` 覆盖加载期合法/非法表达式。

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

- 复审状态：**架构债，非 correctness finding**；规模：879 行；上下文：settlement forge service。
- 关键类型/入口：SettlementForgeService, RecipeItemValidationResult；主要方法：Success, Failed, IsSupportedInteraction, HasAvailableRecipeTyped, ExecuteRecipeResultTyped, BuildWindowDataTyped, _resolve_recipe, _list_matching_recipes, _build_recipe_window_entries, _build_recipe_window_entry。
- Godot/公开边界：Export 0 处；Signal 3 处；风险触点：Godot Dictionary/Array boundary×91; runtime mutation collections×48。
- 当前结论：settlement transaction rollback 已有 focused regression；剩余是 recipe/facility/execute 核心仍以 `GDictionary` 处理，属于 typed-boundary 债。
- 建议验证：维持 persist-failure rollback 回归；typed 化时再补 projection equivalence。

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
- 建议验证：dotnet build magic.csproj；必要时补最近 domain 的 headless runner。

### 400. `scripts/systems/settlement/SettlementShopService.cs`

- 复审状态：**确认问题**；规模：1115 行；上下文：General script：按 GodotSharp/resource 边界、typed API、测试入口检查。
- 关键类型/入口：SettlementShopService, ShopItemId, struct, ShopDefinition, ShopStockEntry；主要方法：BuildWindowDataTyped, BuyTyped, SellTyped, GetOrRefreshShopState, GenerateShopState, BuildShopEntry, MergeShopEntry, PickWeightedRandomEntry, ResolveBuyPrice, ResolveSellPrice。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×69; save/schema/projection×7; runtime mutation collections×28; randomness/determinism×5。
- 当前 finding：设计、生成和存档都写 `shop_inventory_seed`，刷新却不消费该字段而重新生成真随机 seed，schema 不能控制或复现刷新结果。
- 架构债：核心 shop state/交易仍使用 `GDictionary`；这不是上述 correctness finding 的替代证据。
- 建议验证：固定 settlement state/seed 的刷新结果可重放，且 save/load 后一致。

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

- 复审状态：**范围排除（非生产工具）**；规模：275 行；上下文：GDScript 离线烘焙工具。
- 关键类型/入口：无显式类型；主要方法：_ready, _build, _mode, _material_for_mode, _real_pbr_material, _load_image_texture, _mud_material, _mud_ramp, _water_material, _water_ramp。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：resource/path loading×5。
- 当前结论：实现与注释不一致的事实保留为历史工具说明，但该脚本不属于生产运行链或正式资产构建验收，不进入修复队列。
- 建议验证：无；仅在未来把该工具纳入正式资产流水线时重新审核输出契约。

### 415. `scripts/tools/tree_baker.gd`

- 复审状态：**范围排除（非生产工具）**；规模：157 行；上下文：GDScript 离线烘焙工具。
- 关键类型/入口：无显式类型；主要方法：_ready, _bake, _model_path, _apply_foliage_alpha, _override_leaf_surfaces, _load_tex, _node_aabb。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：resource/path loading×5。
- 当前结论：输入失败边界缺失的事实保留为历史工具说明，但该脚本不属于生产运行链或正式资产构建验收，不进入修复队列。
- 建议验证：无；仅在未来把该工具纳入正式资产流水线时重新审核输入和失败退出契约。

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

- 复审状态：**性能建议，非 finding**；规模：174 行；上下文：UI/scene adapter。
- 关键类型/入口：BattleBoardProp；主要方法：Configure, ApplyInteractionState, DrawSpikeBarricade, DrawObjectiveMarker, DrawTent, DrawTorch, SignedOffset, Ratio, StableHash。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：scene/node/signal contract×2; hot path / lifecycle×3。
- 当前结论：存在短数组分配，但节点没有 `_Process`，只在 configure/state/resized 等事件后 `QueueRedraw`；没有 profiling 证据支持把它列为每帧 GC 问题。
- 建议验证：只在实际大场景 profiling 显示 draw 分配成为热点时再优化。

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
  - 检查随机源是否应可重放；生成内容/战斗结果要区分真随机、seeded random 和测试注入。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 425. `scripts/ui/CharacterInfoWindow.cs`

- 复审状态：**需跟进**；规模：613 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：CharacterInfoWindow, EntryKind, CharacterInfoPayload, CharacterInfoSection, CharacterInfoEntry；主要方法：ShowCharacter, HideWindow, _close_window, _on_shade_gui_input, _rebuild_sections, _clear_sections, _build_section_panel, _build_pair_entry, _build_text_entry, _create_section_panel_stylebox。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×29; scene/node/signal contract×11; save/schema/projection×10; runtime mutation collections×37; string dynamic dispatch×1。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 426. `scripts/ui/DisplaySettingsWindow.cs`

- 复审状态：**关注**；规模：194 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：DisplaySettingsWindow；主要方法：ConfigureOptions, ShowWindow, HideWindow, GetSelectedSettings, _rebuild_resolution_options, _find_resolution_index, _get_selected_resolution, _on_fullscreen_toggled, _update_hint, _apply。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：scene/node/signal contract×13; runtime mutation collections×5; string dynamic dispatch×2。
- 对抗性检视：
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
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 429. `scripts/ui/PartyManagementWindow.cs`

- 复审状态：**需跟进**；规模：1375 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PartyManagementWindow, struct；主要方法：ShowParty, SetAchievementDefs, SetItemDefs, SetSkillDefs, SetProfessionDefs, SetCharacterManagement, SetPartyState, RefreshView, GetPartyState, GetSelectedMemberId。
- Godot/公开边界：Export 0 处；Signal 5 处；风险触点：Godot Dictionary/Array boundary×15; scene/node/signal contract×36; save/schema/projection×1; runtime mutation collections×149; string dynamic dispatch×6。
- 对抗性检视：
  - 检查 Dictionary 只在资源/边界投影；正式业务态必须回到 typed owner，schema 变更不能私加兼容。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 431. `scripts/ui/PartyWarehouseWindow.cs`

- 复审状态：**需跟进**；规模：719 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PartyWarehouseWindow, WarehouseWindowData, WarehouseEntry, struct；主要方法：ShowWarehouse, SetWindowData, RefreshView, HideWindow, _rebuild_stack_list, _restore_selection, _rebuild_target_member_selector, _refresh_details, _refresh_controls, _get_selected_entry_data。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×26; scene/node/signal contract×26; resource/path loading×1; runtime mutation collections×21; string dynamic dispatch×4。
- 对抗性检视：
  - 检查资源路径存在性、类型和失败缓存；错误路径不能在高频 UI/循环中反复刷日志。
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 432. `scripts/ui/PromotionChoiceWindow.cs`

- 复审状态：**需跟进**；规模：432 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：PromotionChoiceWindow；主要方法：ShowPromotion, HideWindow, _clear_cards, _rebuild_choice_cards, _create_card, _select_choice, _refresh_details, _on_card_gui_input, _on_confirm_button_pressed, _on_cancel_button_pressed。
- Godot/公开边界：Export 0 处；Signal 1 处；风险触点：Godot Dictionary/Array boundary×44; scene/node/signal contract×13; runtime mutation collections×16; hot path / lifecycle×1; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：godot --headless -s res://tests/world_map/ui/run_promotion_choice_window_schema_regression.cs，并新增 confirm signal payload 用例。

### 433. `scripts/ui/RuntimeLogDock.cs`

- 复审状态：**需跟进**；规模：502 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：RuntimeLogDock, struct；主要方法：IsCollapsed, GetCollapsedHeight, GetPreferredHeight, _toggle_collapsed, _cycle_opacity, ShowWorldLogs, ShowBattleLogs, ClearLogs, GetDesignPanelSize, ApplyLayoutScale。
- Godot/公开边界：Export 0 处；Signal 4 处；风险触点：Godot Dictionary/Array boundary×18; scene/node/signal contract×11; runtime mutation collections×18; hot path / lifecycle×1; string dynamic dispatch×2。
- 对抗性检视：
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
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 436. `scripts/ui/ShopWindow.cs`

- 复审状态：**需跟进**；规模：992 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：ShopWindow, ShopWindowData, ShopEntry, MemberOption；主要方法：ShowShop, ShowStagecoach, HideWindow, RefreshView, _build_meta_text, _build_member_selector, _resolve_default_member_id, _select_member, _refresh_member_state, _rebuild_entry_list。
- Godot/公开边界：Export 0 处；Signal 5 处；风险触点：Godot Dictionary/Array boundary×38; scene/node/signal contract×25; runtime mutation collections×38; string dynamic dispatch×2。
- 对抗性检视：
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 437. `scripts/ui/SubmapEntryWindow.cs`

- 复审状态：**需跟进**；规模：318 行；上下文：UI/scene adapter：重点检查 paired `.tscn`、GetNode 路径、信号载荷、RichText/输入事件。
- 关键类型/入口：SubmapEntryWindow；主要方法：ShowPrompt, HideWindow, _on_confirm_button_pressed, _on_cancel_button_pressed, _on_shade_gui_input, _cache_default_metrics, _apply_prompt_metrics, _restore_default_metrics, _set_font_override, _read_int_property。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×10; scene/node/signal contract×17; runtime mutation collections×1; string dynamic dispatch×3。
- 对抗性检视：
  - 检查 paired scene 的节点路径、unique name、类型和信号连接是否同步。
- 建议验证：对应 UI scene headless runner；至少实例化 paired `.tscn` 并验证 GetNode/信号。

### 438. `scripts/ui/WorldMapView.cs`

- 复审状态：**性能建议，非 finding**；规模：577 行；上下文：UI/scene adapter。
- 关键类型/入口：WorldMapView；主要方法：Configure, SetRuntimeState, RefreshWorld, _draw_cells, _draw_cell_background, _draw_settlements, _draw_settlement_footprint_cells, _draw_settlement_body, _draw_mobile_entities, _draw_world_event_marker。
- Godot/公开边界：Export 0 处；Signal 0 处；风险触点：Godot Dictionary/Array boundary×23; scene/node/signal contract×6; hot path / lifecycle×5; string dynamic dispatch×2。
- 当前结论：绘制中存在短集合分配，但没有 `_Process` 每帧重绘；只在 runtime state、world refresh 或尺寸变化后 redraw，现阶段没有 correctness 或已证实性能 bug。
- 建议验证：在大地图 profiling 证明分配/GC 热点后再决定是否缓存。

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

- 修复顺序以上方高、中优先级 findings 为准；矩阵里的“需跟进”不自动进入待办。
- 行为修复应补对应 narrow regression；模拟汇总修复应使用 ended/stalled synthetic runs 验证分母。地形可用固定 battle seed 验证，战斗掷骰必须保持真实独立随机，边界测试使用显式 roll override。例行验证至少运行 `dotnet build magic.csproj`，不要把 balance simulation 混进普通全量回归。
- 本矩阵源自 2026-06-17 当时的 466 个文件，清理后只保留 447 个仍存在路径的历史条目。新增或拆分后的 owner 应按 `docs/design/project_context_units.md` 和当前系统文档加载，不能继续向旧矩阵机械补行并声称当前全覆盖。
