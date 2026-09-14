# scripts 检视保留决策与修复台账

本文件是**活台账**，不是某一时间点的审查报告。它保留两类内容：

1. **保留决策** —— 已经过复核、明确"不修"或"暂不修"的项，附判定理由。再次遇到时不要重新当作 finding 上报。
2. **修复台账** —— 曾被报告为问题、现已修复或已撤销的结论。新一轮检视命中同一失败模式时，先读这里确认是否已关闭。

来源：`scripts_directory_file_by_file_review_2026-06-17.md`（2026-06-17 逐文件模板扫描，2026-07-25 / 2026-08-05 两轮复核完成后于 2026-08-06 删除）。原文件的 447 条历史矩阵条目由风险模板批量生成，"需跟进"只表示当时的检查方向而非 finding；复核后全部关闭且未产生新的 correctness finding，故矩阵不再保留。已提交的历史版本可用 `git log -- docs/reviews/scripts_directory_file_by_file_review_2026-06-17.md` 找回。

**新一轮检视请另建文档，不要向本文件机械补行。**本文件只接受"保留决策"和"结论关闭"两类写入。

---

## 一、当前保留决策

### 死代码清理（2026-08-05 记录，非 correctness）

`GameRuntimeFacade.Commands.cs` 的 `_execute_logged_command` 已无任何调用方，而它是 `GameRuntimeCommandLogger.FinishLoggedCommand` 的唯一调用者，因此 `_previousCommandLogScope` 的 scope 恢复路径整体不可达。

当前 55 条命令都走 `ExecuteLoggedCommandTyped`（该方法不调用 Finish，scope 由下一次 `BeginLoggedCommand` 覆盖），所以**今天不存在误归属**。风险在于将来：若把 Finish 路径重新接回而不补 `try/finally`，命令体抛异常后 `_activeCommandLogScope` 会被恢复成上一条命令的 scope，后续结果将记到错误的 event_id 名下。

处理建议：要么删除这条死路径，要么接回时同时补异常安全包装。

### 接受风险，暂不修复

`UnitProgress.cs:225-251` 在递归访问子节点后才写 `visited`，循环 merge source 会无限递归。

已确认当前递归 getter 没有生产调用，并在 API 旁注明无环前提和未来接线要求。2026-08-05 补充证据：该"无环前提"实际**由实现强制而非仅靠约定**——`SkillMergeService.DetachMergedSourceSkills` 在合并时会 `RemoveSkillProgress` 删除源技能进度并 `BlockSkillRelearn` 阻止重学，源技能无法再作为后续合并结果重新出现，因此 A→B→A 形状不可构造。

### 架构债，非 correctness bug

settlement 通用 action/service-entry 分发与 forge 的 facility/recipe/window build 仍大量解析 `GDictionary`，属于 typed-boundary 架构债。

forge 确认提交已于 2026-07-25 改为 `ForgeActionRequest` 强类型 C# 事件并沿 proxy/facade/handler typed 入口传递，不再发出 Dictionary signal。商店库存、刷新和买卖事务已使用 typed owner/result，剩余 Dictionary 主要位于同步窗口投影，不再列入核心状态债。当前没有证据把保留项升级成 correctness bug。

### 范围排除（非生产工具）

`scripts/tools/tree_baker.gd` 与 `tile_baker.gd` 于 2026-07-22 按范围决策移出修复清单：它们是人工使用的非生产工具，不属于正式运行链或资产构建验收。**本次排除依据是 owner/交付范围，不是 GDScript 语言**；生产链中的同类失败模式仍需正常处理。

### 未复核残留

原矩阵中两条从未被重写成当前 owner 结论的模板条目，既未确认有问题也未确认无问题：

- `scripts/ui/DisplaySettingsWindow.cs`（194 行）——建议验证：实例化 paired `.tscn` 并验证 GetNode 路径、unique name、类型与信号连接同步。
- `scripts/utils/generate_canyon_tiles.py`（484 行）——建议验证：`dotnet build magic.csproj`；必要时补最近 domain 的 headless runner。

---

## 二、已关闭结论台账

以下问题曾被报告，现已修复或已撤销。**命中同一失败模式时先核对本节，不要重报。**

### 战斗 AI

- **AI 普通伤害估算的正式减伤语义缺口**（2026-07-24 修复）：production AI 使用已注入的 `BattleDamageResolver.PreviewDamageEffectTyped(...)`，以 Average/Expected 模式复用正式抗性、固定减伤、护盾吸收与生命伤害语义；多段伤害会串联 source/target preview-after 克隆状态，护盾和消耗型攻击状态不会被每段重新使用。击杀分支以护盾后的生命伤害判断，真实战斗单位不被预览修改。回归：`run_battle_ai_score_input_metrics_regression.cs`。
- **AI 同分目标顺序**（2026-07-24 修复）：`BattleAiTypedActionHelper.CompareTargets(...)` 保留威胁、HP 比例与距离的原有战术优先级，并在这些指标完全相同时按 `unit_id` ordinal 比较，使 target list 成为与 `BattleState` 单位插入/恢复顺序无关的全序；Boss objective 的显式目标置顶仍在排序后生效。回归：`run_battle_ai_unit_skill_candidate_evaluator_regression.cs`（正反两种插入顺序）。
- **AI action/skill 兼容性缺口**（2026-07-24 修复）：敌方内容校验会按 action kind 验证主动战斗技能、unit/ground target mode、random-chain/multi-unit selection mode、`meteor_swarm` 专用地格路由、变体 command route、multi action 候选容量，以及 charge、path-step AOE、blink/jump reposition 所需的同一单格 cast option；brain 层验证等级无关契约，模板层再按 `skill_level_map` 验证实际等级已解锁的变体与 unit pipeline 可执行的 base + variant 效果，`range_skill_ids` 仍只作混合技能站位参考。`frontline_bulwark` 的两个嘲讽 action 已改用 Ground action 和 `frontline_guard` 评分桶，Ground 评分也会把实际状态/控制命中视为有效收益。
- **AI evaluator trace span 的异常安全缺口**（2026-07-23 修复）：`BattleAiChargeActionEvaluator`、`BattleAiChargePathAoeActionEvaluator`、`BattleAiMoveToRangeActionEvaluator` 的 15 个手写 `Enter/Exit` 区间已改用 `BattleAiTraceSpan`。三个 focused runner 分别从正式 preview 或移动成本查询注入异常，验证原异常不被替换、recorder 栈恢复平衡且目标 span 确实完成。
- **AI trace 关闭路径的无效构造**（2026-07-22 修复）：公共 trace 入口会在 `trace_enabled=false` 时直接返回，12 个 evaluator 也会在构造 action metadata、候选摘要及其 extra 字典前门控；关闭时不再消耗 trace nonce、复制候选或维护 Top 5，开启时保留原 trace 结构与决策结果。
- 旧 #22–#33 action 条目的运行时方法、规模和 owner 已整体迁入 `scripts/systems/battle/ai/*Evaluator.cs`；其中 trace 异常安全、随机链 seed、retreat/multi-target 限制等失败模式已在当前 C# owner 上重新核验，分别提升为 finding 或降为 AI quality limitation。

### 战斗运行时

- **`BattleTerrainGenerator` 的 typed cells → Godot Dictionary → typed cells 往返**（2026-07-24 移除）：生成器现在直接返回一次性 managed `BattleTerrainLayout`，`BattleUnitFactory` 以 typed `List<Vector2I>` 应用出生点覆盖，`BattleRuntimeModule` 通过 `TakeCells()` 把唯一 cell graph 移交 `BattleState` 并在 owner 内重建 columns；未移交的 layout 会释放 cells。固定种子地形确定性、typed columns、出生点避水、启动重试、异常清理和 pending 语义均由 focused regression 覆盖。该项原本只是架构债，直接消除，不追溯改判为 correctness bug。

### 存档与状态

- **仓库普通运行态立即写入完整存档**（2026-07-24 修复）：`GameRuntimeWarehouseHandler` 的直接加入、丢弃一件、丢弃全部和技能书使用成功后统一调用 `StagePartyState()`；facade 只通过 `GameSession.SetPartyState(...)` 同步 canonical party 并标记 `party_state` pending dirty，不进入 `RuntimeTransaction.Commit(...)` 或磁盘写入。仓库 mutation snapshot 仅在 session staging 失败时回滚，payload 写入失败不再影响库存命令。
- **存档文件缺失的异常契约**（2026-07-24 修复）：`SaveRepository.ReadSavePayload(...)` 对检查前缺失和检查后打开失败都会返回 typed I/O 错误，缺失统一归一化为 `Error.DoesNotExist`，`emitErrors` 只控制诊断记录、不再控制是否抛异常；`GameSession.LoadSave(...)` 对不存在的槽位同样返回 `DoesNotExist`，并在缓存索引仍引用已消失 payload 时移除失效索引项。
- **据点顶层商店 seed/刷新步数契约漂移**（2026-07-23 解决，原 F-14）：删除 `shop_inventory_seed` / `shop_last_refresh_step` 镜像，只由每个商店子状态持有实际 seed、刷新步数和库存；刷新仍为彼此独立的真随机，且只更新目标商店。破坏性 schema 变更归入 v15，v14 直接拒绝且不提供迁移。
- **任务存档的 `last_progress_context` 安全拒绝**（2026-07-21 修复）：底层 context parser 继续严格抛出 `ArgumentException`，`QuestState.FromDictionary` 在存档 DTO 边界将其转换为 `null`，使 `SaveSerializer` 对损坏输入返回 `Error.InvalidData`，不会中断存档读取链。
- **Quest public API 的非法 active 状态缺口**（2026-07-24 临时封口）：`QuestProgressService.RecordProgress(...)` 达成全部目标后复用 `CompleteQuest → MarkQuestClaimable`，不会把 completed 对象留在 `active_quests`；`PartyState.SetQuestState(...)` 只路由 Active、Completed、Rewarded，尚无正式 owner/save contract 的 Failed、Inactive、Unknown 暂时拒绝，不新增 `failed_quests` 或存档兼容逻辑。
- **结论已撤销：技能书使用不是原子事务**（2026-07-24 撤销）：`UseItemTyped(...)` 先通过同一个 `PartyWarehouseService` 确认库存大于零，随后同步学习技能；学习路径只修改角色成长/成就状态，不触碰仓库，也没有异步、回调或其他可重入点。因而紧接着扣减 1 本技能书必然成功，`consume_failed` 只是不可达的防御分支，不能据此认定存在"失败但技能已学习"的 correctness bug。
- **结论已撤销：`PartyState` 空 roster 会写出空 leader 坏档**：玩法硬约束要求主角始终上阵，正式编成入口禁止将主角移入替补并保持 active 非空；主角死亡产生的空 roster 只属于 Game Over 临时态，该结算分支跳过写盘并丢弃 pending save，现有回归验证重新载入仍回到战前存档。

### 仓库与装备

- **仓库无实例 payload 装备的 allocator 失败原子性缺口**（2026-07-22 修复）：`PartyWarehouseService` 会检查正式添加结果，实例 id 分配失败时返回 `warehouse_blocked_swap`，现有 batch transaction 随即恢复此前的取出操作；回归覆盖空 allocator 下材料保留、装备不写入。
- **结论已移除：仓库批量交换忽略普通物品存入失败**：普通单件 deposit 的 preview 与实际添加在同一事务态、相同容量/堆叠算法下连续执行，preview 失败会阻断并回滚，不存在原结论描述的普通物品漂移。
- **装备能力 registry 的校验/投影顺序**（2026-07-21 修复）：validation 已产生错误时不再进入 Resource→Definition 投影；错误类型的嵌套条件组会由 `Rebuild` 返回 `EQA_CONDITION_GROUP_TYPE_INVALID`，而不会被投影层 `InvalidOperationException` 替代。失败构建仍不发布候选快照。

### 内容与文本

- **技能等级描述表达式死循环**（2026-07-20 修复）：表达式和变量 token 只处理一轮，失败字段原位显示 `[描述配置错误]` 且继续渲染后续字段；内容校验会提前拒绝空、未闭合及语法错误表达式。

### BattleSim 与报告

- **BattleSim 异常路径清理**（2026-07-21 修复）：`AiTraceRecorder` 的实例作用域在成功和异常时都会恢复此前 recorder，保留外层 profiler；`BattleSimRunner` 在 setup、开战、执行循环或结果采集失败时都会释放本轮 runtime，主体与 teardown 同时失败时保留两条异常。共享的 caller-owned terrain generator 不由单轮 runtime 误释放。
- **BattleSim 报告写盘契约**（2026-07-21 修复）：`BattleSimReportFileWriter` 使用秒级时间加 GUID 生成批次名；report、trace 与存在 trace 时的 summary 全部确认写入后才返回路径并打印成功。打开、逐次写入或 flush 失败会恢复此前 `OutputFiles`、清理本批次残缺产物并向上传播，不再出现假成功或同秒覆盖。
- **simulation 终止状态区分**：现已区分 battle ended、idle stall、iteration budget exhausted 与 invalid runtime；未完成 runs 保留诊断但不再进入胜率、均值、技能/action/faction 汇总，CLI 对不完整实验返回非零。两个手写 benchmark 汇总器与冻结 6v12 runner 的分析包消费者也按相同完成态规则过滤。
- **Faction action/skill 报告缺口**（2026-07-22 修复）：正式单局 JSON、Godot report projection、trace summary、profile summary、冻结 6v12 runner 的 run detail 与 analysis packet 现在都会保留每个 faction 的 `action_counts`、`skill_attempt_counts` 和 `skill_success_counts`；汇总仍只纳入 battle-ended runs，残缺输入缺少任一计数表时标为 unavailable 而不伪装成零。
- **冻结 6v12 runner 的 raw aggregate 混合口径**（2026-07-22 修复）：执行循环的 `termination_kind` 现在是逐 run 完成态的唯一真相源，raw `runs[]` 显式输出 `battle_ended / termination_kind`；`run_count` 表示尝试数，`completed_run_count` 表示 battle-ended 数，未完成局只保留诊断并按 idle/budget/invalid 分类。请求轮数未全部正常结束时 raw 与 trace summary 都保持 `is_complete=false`，runner 返回 `2`；旧报告的保守 packet fallback 继续保留。

### 已整体失效的旧结论

- 原 findings-first 中的晋升信号/BBCode、旧 AI trace scope 与 Variant 计时、装备 discard-all、骰子字符串虚调用、图标路径、encounter anchor 恢复等具体问题均已由当前实现覆盖。
- 旧 weak-owner、settlement transaction、UI Hide/Signal、RichText、DTO `ToDictionary` 等泛化模板已有 typed owner、集中 teardown 或回归覆盖，不再作为待修问题。
- 以下文件已不存在，其仍有意义的失败模式已映射到当前 owner 后重新判断：`RaceTraitContentRegistry.cs`、`RaceTraitDef.cs`、`TestCsBase.cs`、`BattleSimTerrainGenerator.cs`、`SkillEffectiveCombatProfile.cs`、`SkillEffectiveCombatProfileResolver.cs`、`PartyMemberOptionUtils.cs`。
