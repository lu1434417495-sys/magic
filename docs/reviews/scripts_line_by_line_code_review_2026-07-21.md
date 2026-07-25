# `scripts/` 逐行代码检视（2026-07-21）

## 结论

本轮在当前工作树中确认 **30 项问题**：P1 级 5 项、P2 级 17 项、P3 级 8 项；未发现 P0 级问题。最优先处理的不是代码风格，而是五个会破坏架构真相或交付完整性的边界：

1. 据点状态经不完整 typed DTO 写回时会删除商店库存、刷新种子、冷却和服务扩展字段。
2. `scripts/` 下有 48 个被 tracked 代码直接引用的源码文件尚未纳入 Git，干净检出无法复现当前本地构建。
3. `PartyMemberState.current_hp` 与 `is_dead` 可持久化为矛盾值，队伍层和战斗层会得出相反结论。
4. 装备门槛绕过正式有效属性快照，只读取 base attributes；身份、升华、职业等永久修正无法满足门槛。
5. BattleSim 把战斗启动失败写成 `IdleStall`，会污染模拟报告和后续调参依据。

`dotnet build magic.csproj --no-restore` 通过，常规回归套件 394/394 通过。通过并不等于以下问题不存在：多个现有测试只验证局部 service mutation 或路径非空，没有穿过实际 owner 写回、异常回滚和干净检出边界。

> **统一收尾状态（2026-07-25）**：上述数量、优先级和验证结果保留为原始审计快照。当前 30 项正式 finding 中，28 项已完整解决；F-02 因并发迁移又出现 2 个未跟踪源码而保持处理中；F-28 经确认接受风险、暂不修改。F-26 已按方案 3 建立正式 quest journal owner、失败态与失败重启策略。原 F-27 已复核撤销，不计入当前 30 项；原 F-23/F-24 已按非生产工具范围排除。

## 审计快照与范围

- 工作树基线：`43c5cc32`，分支 `agent/structured-logging-test-performance`。
- 检视对象：`scripts/` 下全部当前代码文件；`.uid` 等非代码 sidecar 不计入。
- `scripts/tools/tree_baker.gd` 与 `tile_baker.gd` 虽被枚举，但按 2026-07-22 范围决策属于人工使用的非生产工具，不计入 actionable finding。
- 总量：883 个代码文件、304,795 个物理行；其中 880 个 `.cs`、2 个 `.gd`、1 个 `.py`。
- 工作树在审计开始前已有大量未提交和未跟踪修改；本报告描述的是该脏工作树快照，不等同于 `HEAD`。
- 本轮只新增本审计文档，不修改被检视代码，也不改写已有 review 文档。

| 分区 | 文件数 | 物理行 | 检视重点 |
|---|---:|---:|---|
| progression / equipment / warehouse / enemies / attributes / inventory | 341 | 71,830 | 持久状态、内容校验、装备与成长真相 |
| battle `_interop` / ai / core / fate / rules | 202 | 79,901 | 规则纯度、AI 决策、快照/回滚、依赖方向 |
| battle runtime / sim / terrain / presentation | 140 | 75,387 | runtime owner、生命周期、模拟与报告 |
| content / game_runtime / lifecycle / persistence / settlement / world / ui / utils / tools | 199 | 77,460 | session/world transaction、UI borrower、工具链 |
| shared fate rule | 1 | 217 | 跨世界/战斗低运规则 |
| **合计** | **883** | **304,795** | |

检视方式是：先按文件和物理行全量枚举，再以当前 `docs/design/project_context_units.md` 指定的 owner/read set 为入口，双向追踪写入口、消费者、持久化边界、异常路径、Godot native owner/borrower 和测试；对候选问题继续检查当前调用方和现有回归。单纯“大文件”“无 namespace”或理论上不可达的模式没有计为 finding。

## 严重度定义

- **P0**：当前可直接造成广泛数据毁损、不可恢复崩溃或安全事故；本轮无。
- **P1**：可破坏持久数据/权威真相、使交付不可复现，或系统性污染正式分析结果，应在继续扩展相关模块前处理。
- **P2**：有可达行为错误、事务/生命周期缺口或明确的架构反向依赖，应进入近期修复队列。
- **P3**：当前调用受限、条件较窄或主要影响工具/资源债务，但接线或规模扩大后会转为行为问题。

## P1：高优先级问题

### F-01 据点 typed 写回是破坏性投影，会删除完整服务状态

> **修复状态（2026-07-23）：已解决。** 当前由完整不可变 `WorldMapSettlementStateData` 持有精确 5 字段 schema，商店子树也已 typed；单字段修改通过 `With*` 保留整个聚合，买入、重开、持久化失败回滚与磁盘 `LoadSave` 均有回归。存档版本已升至 v15，并按决策直接拒绝 v14，不提供迁移。

**位置**

- `docs/design/world/settlement_module.md:52`
- `scripts/systems/world/WorldMapDataContext.cs:1394-1495`
- `scripts/systems/world/WorldRuntimeData.cs:366-405`
- `scripts/systems/world/WorldMapDataContext.cs:433-449`
- `scripts/systems/game_runtime/GameRuntimeServiceWindowCommandHandler.cs:134-185,330-347,411-438`
- `scripts/systems/settlement/SettlementShopService.cs:478-545`

**问题与调用链**

当前设计要求 `settlement_state` 至少保存 `visited`、`reputation`、`shop_inventory_seed`、`shop_last_refresh_step`，并允许研究、驿站、商店等服务扩展字段。`WorldMapSettlementStateData` 却只读取和输出 `Visited`、`Reputation`、`ActiveConditions`。`TrySetSettlementState` 随后用 `state.BuildSnapshotPlain()` 替换完整的 canonical `settlement_state`。

生产路径会直接经过该投影：打开商店、购买、刷新会先在 `GDictionary` 中写 `shop_states`、`world_step`、反馈和库存，再调用 `SetActiveSettlementState`；第一次访问据点也会由 `MarkSettlementVisited` 重建部分 DTO。因此以下字段会被删除：`shop_states`、`shop_inventory_seed`、`shop_last_refresh_step`、`cooldowns`、`world_step`、`shop_feedback_text` 及所有服务扩展字段。下次打开商店时 `GetOrRefreshShopState` 看不到旧库存，会重新生成商店；购买造成的库存扣减也无法穿过 world owner 写回。

现有 `run_settlement_shop_stock_persistence_regression.cs` 只断言 `SettlementShopService.BuyTyped` 对传入的局部 dictionary 做了扣减，没有调用 `WorldRuntimeData.TrySetSettlementState`，所以测试通过并不能覆盖本问题。

**建议**

- 让 settlement aggregate 的 typed owner覆盖当前完整 schema；`WithVisited` 等操作必须基于完整不可变快照更新单字段，而不是用部分 DTO 替换 owner。
- 对当前 schema 做严格校验；不要通过“保留未知旧字段”的兼容 fallback 掩盖 owner 缺失。如需迁移旧存档，按仓库兼容策略另行确认。
- 增加 sentinel 扩展键、嵌套库存和冷却字段，覆盖 `mark visited → open → buy → refresh → save/load` 全链路。

### F-02 48 个新增源码未纳入 Git，当前构建不可交付

> **修复状态（2026-07-25）：处理中。** 原审计快照中的 48 个文件已陆续纳入 Git；本次首次重新核对时剩余的 14 个源码也已逐个确认归属并加入当前 index。统一收尾期间，并发工作树又新增 `BattleExitObjectiveRules.cs` 与 `BattleUnitWeaponProjectionState.cs` 两个未跟踪源码，且对应迁移尚未完成，因此不能继续标记为已解决。附录 A 保留原始 48 文件清单作为审计证据；最终提交必须重新枚举全部未跟踪源码，让 typed owner/rule/DTO 与各自调用方、回归按功能主题共同落地，并以干净 worktree 构建作为最终门禁。

**位置**

- `scripts/systems/persistence/GameSession.cs:14-17`
- `scripts/systems/persistence/SaveSerializer.cs:57-59`
- `scripts/player/progression/SkillContentRegistry.cs:31-41`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs:28-33`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs:350-357`
- `scripts/systems/battle/sim/BattleSimRunReport.cs:10`

`git ls-files --others --exclude-standard -- scripts` 返回 48 个 `.cs`。tracked 代码已经直接引用其中的 `SaveSchemaVersions`、技能/装备能力 validator、AI snapshot、damage resolver partial、runtime 拆分 service、BattleSim termination enum 和 BattleMapPanel partial。当前本地编译成功只是因为这些文件存在于工作树；若只提交 tracked diff，干净检出会缺类型并编译失败。

这不是普通提交卫生问题：源码归属已经成为模块拆分的必要组成部分，Git membership 是构建图的一部分。

**建议**

- 在提交前逐个确认 48 个文件是否属于本次拆分；应纳入的文件必须与引用它们的修改一起提交。
- 在干净 worktree/clone 中执行 `git ls-files --error-unmatch <file>`、`dotnet build magic.csproj` 和对应 headless 回归。
- 完整清单见附录 A。

### F-03 `current_hp` 与 `is_dead` 是可矛盾的两套持久化真相

> **修复状态（2026-07-22）：已解决。** `current_hp` 现为唯一权威，`is_dead` 仅由 `current_hp <= 0` 派生；已删除允许绕过同步的生命写入口。当前 schema 保留 `is_dead` 作为冗余一致性校验，读入时严格拒绝两个方向的矛盾 payload，不做自动归一化或历史存档迁移。据点普通恢复会跳过死亡成员，战斗资源写回也统一走同一不变量。

**位置**

- `scripts/player/progression/PartyMemberState.cs:223-250,496-502,617-660`
- `scripts/player/progression/PartyState.cs:56-60`
- `scripts/systems/battle/runtime/BattleUnitFactory.cs:664-680`

反序列化分别检查 HP 和死亡标记的类型，却不检查 `is_dead == (current_hp <= 0)`；`SetVitals(hp, mp, aura, dead)` 也允许任意组合。`PartyState` 直接消费 `is_dead`，而战斗工厂按 `current_hp` 构造资源状态。于是 `is_dead=true/current_hp>0` 会在队伍逻辑中死亡、在战斗中存活，反向组合也会得出相反结果。

**建议**

- 只保留一个权威 owner；通常由 HP 派生死亡状态，所有写入口走同一不变量。
- 当前 schema 读入时严格拒绝矛盾 payload。
- 若希望自动修复历史存档，需要先确认兼容/迁移策略，不能在本修复中偷偷增加 fallback。

### F-04 装备需求绕过正式有效属性快照

> **修复状态（2026-07-22）：已解决。** `EquipmentRequirementDefinition` 不再读取基础属性，属性门槛必须显式消费角色管理层生成的稳定有效属性快照。世界整备与战斗换装都会先复制当前装备视图、移除候选最终占槽会替换的装备，并在不加入候选自身的情况下重算快照；战斗路径使用 battle-local 装备 view，不读取可能含临时状态的 `BattleUnitState.attribute_snapshot`。缺少快照 provider 时属性门槛失败关闭，HUD 换装预览缓存也加入稳定属性 fingerprint，避免身份、职业、永久奖励或 trait 改变后复用旧结论。

**位置**

- `docs/design/progression/character_module.md:84-86`
- `scripts/player/equipment/EquipmentRequirementDefinition.cs:50-61`
- `scripts/systems/inventory/PartyEquipmentService.cs:347-360`
- `scripts/systems/attributes/AttributeService.cs:447-460,511-541`
- `data/configs/ascensions/titan_avatar.tres:6-14`
- `data/configs/items/weapon_unique_greataxe_mountainbreaker.tres:25-34`

当前架构把 base attributes、identity、profession、equipment、status/reward modifiers 的合并结果定义为属性快照真相；装备预览/提交却直接读取 `memberState.progression.unit_base_attributes`。因此基础 Strength 17、`titan_avatar` 永久修正 +3 的角色有效 Strength 已达 20，仍无法装备要求 Strength 20 的裂山者。

**建议**

- 装备预览构建 detached 的正式有效属性上下文，并明确哪些来源可以满足需求。
- 计算时必须先移除被替换装备的修正，候选装备本身也不能反向满足自己的门槛。
- 补充 base 17 + ascension 3、只靠被替换装备达标、preview/commit 和战斗内换装一致性测试。

### F-05 BattleSim 把启动失败归类为 `IdleStall`

> **修复状态（2026-07-22）：已解决。** `BattleSimExecutionLoop` 现在只推进 runtime 当前正式持有的 state；启动失败形成的非 owner state 会在 0 次迭代时归类为 `InvalidRuntime`。`BattleSimRunReport`、正式文件投影和 trace summary 均保留 typed `start_failure`，空阵容与布阵耗尽回归确认不会增加 stalled 统计；若出生可达性首轮失败、后续重试成功，runtime 会在成功返回前清空瞬态失败快照。

**位置**

- `scripts/systems/battle/runtime/BattleRuntimeModule.cs:643-656,795-805`
- `scripts/systems/battle/sim/BattleSimRunner.cs:249-262`
- `scripts/systems/battle/sim/BattleSimExecutionLoop.cs:54-59,112-153`

单位非法或布阵耗尽时，runtime 清空内部 `_state`，但向调用方返回一个非空的空 `BattleState`。Runner 无条件把它交给执行循环；循环只检查参数是否为 null，随后在无进展阈值后记录 `IdleStall`。`invalid_start_units`、`spawn_reachability`、`placement_exhausted` 因而丢失原始原因，并污染 stalled 统计和后续 AI/数值调参依据。

**建议**

- `StartBattle` 应返回显式 typed result，成功 state 与 failure reason 互斥。
- Runner 至少核对 `ReferenceEquals(runtime.GetState(), state)` 并读取 `GetLastStartFailureSnapshot()`。
- 分别强制空阵容与布阵失败，断言 `InvalidRuntimeRunCount == 1`、`StalledRunCount == 0` 且报告保留 reason。

## P2：中优先级问题

### F-06 AI mutation guard 的 stable fingerprint 漏掉权威字段

> **修复状态（2026-07-24）：已解决。** mutation guard 现在以 mutation 专用 typed stable snapshot 覆盖 unit/status/state、cell/column、terrain、layered barrier、blackboard、raw equipment、attribute、objective/final decision、target mark、temporary edge、allocator 与 definition index。canonical key、顺序、重复项、非法哨兵、`null`、类型身份和浮点位模式都进入比较，结构门禁锁定新增权威字段的覆盖。最终设计取消回滚：snapshot 层只保存检测基线，整图恢复副本、`Restore` API 和恢复型回归均已删除；owner-local exact capture seam 仅用于维持检测精度。

**位置**：`scripts/systems/battle/ai/BattleAiMutationSnapshots.cs`、`BattleAiMutationStableProjection.cs`、`BattleAiMutationSnapshotModel.cs`、`BattleAiMutationBarrierSnapshots.cs`，以及 `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`；对照 `BattleState`、`BattleUnitState`、`BattleCellState`、terrain/barrier owner 与 `SkillDefinition` 的 exact/freeze seam。

unit snapshot 漏掉 `equipment_view_initialized`、已消费 contingency setup、装备能力来源、时间进度修正、creature tags、`weapon_range_type`；status projection 漏掉 `forced_move_immune`、`counts_as_debuff_*`、各 `lock_*`；state snapshot 也未覆盖 equipment target marks 和 cast sequence。stable diff 为零时 guard 直接返回，不执行恢复；这些字段被 evaluator 修改后会假绿并留在正式 state。浮点值先降为 `float` 再近似比较，也会漏掉小幅 double 修改。

**架构修复**：用权威 typed snapshot/schema 同时驱动 capture、stable projection 与 restore；仅对白名单派生缓存豁免，并增加“新增权威字段必须进入 snapshot”的结构测试。

### F-07 mutation guard 用有损诊断投影做事务回滚

> **修复状态（2026-07-24）：已解决。** 按最终设计，`FullSnapshotDiagnostic` 是显式测试/诊断断言，不是运行时事务：AI 决策正常返回后比较完整权威投影，发现差异立即记录并抛出 `BattleAiMutationViolationException`，不回滚已失败的 battle fixture；production 默认 `Disabled`，不捕获也不比较。report 与 promotion queue 已删除键白名单，所有键、嵌套值和类型身份都进入 stable diff。snapshot 层的整图恢复副本、`Restore` API、回建 materializer 与恢复型回归均已删除。

**位置**：`BattleAiMutationGuard.cs:27-94`，`BattleAiMutationSnapshots.cs:596-603,636-638,1045-1075`，`BattleState.cs:210-257`。

报告与 promotion 队列只捕获固定白名单键；一次无关 HP mutation 触发 Restore 后，原有 `text`、`event_tags`、`refund_policy` 等扩展键会被删除。Restore 还直接替换 `log_entries`，没有恢复 `_log_text_byte_size`，后续日志会提前裁剪。诊断摘要不是无损事务快照，二者不应复用。

### F-08 evaluator 异常路径跳过 mutation guard 校验

> **修复状态（2026-07-24）：已解决。** `FullSnapshotDiagnostic` 现在在 evaluator 异常退出时、context 清理前执行同一份 stable 校验。没有 mutation 时用原始 `throw;` 保留 evaluator 异常；存在 mutation 时记录 violation 并抛出 `BattleAiMutationViolationException`，原 evaluator 异常作为 `InnerException` 保留。失败 fixture 不回滚；production `Disabled` 路径仍不捕获也不比较。

**位置**：`scripts/systems/battle/ai/BattleAiService.cs:108-141`。

`ChooseCommandImpl` 若先修改状态再抛异常，旧控制流会直接进入外层 finally；finally 只清 runtime binding 和 score scope，正常返回路径上的校验不会执行。于是测试虽然会因 evaluator 异常失败，却没有 mutation diff，甚至可能在“预期抛异常”的用例中漏掉非法写入。修复后的异常作用域只负责 validate/report：无差异时原样重抛，有差异时以 guard exception 明确暴露双重故障，并始终保留已失败状态供断言后废弃。

### F-09 rules 层仍把具体 runtime 当 service locator

> **修复状态（2026-07-24）：已解决。** 原 12-member 聚合端口已按读写职责拆为 `IBattleEquipmentAttackCheckQuery`、`IBattleEquipmentDamageQuery` 与 `IBattleEquipmentCombatReactionSink`。`BattleAttackCheckPolicyService` 删除 `WeakReference<BattleRuntimeModule>` 和隐藏 state fallback；`BattleDamageResolver` 的装备 query/reaction 状态显式来自 typed context。三个端口只由 `BattleRuntimeModule.BindEquipmentRulePorts()` 集中装配，service getter 不再执行 `Setup`，对应架构 baseline 旧债已删除。

**位置**：`scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs:15-28,623-642,930-940,1152-1164`。

规则服务虽然局部使用 `IBattleEquipmentAbilityReactionService`，却必须先解析 `WeakReference<BattleRuntimeModule>` 才能取得接口和 state。脱离完整 runtime 的 preview/test 无法注入实现；runtime 引用失效时装备攻击修正和强制暴击会静默缺席。这是“类型抽象了、依赖获取方式没抽象”。`Setup` 应直接注入 state accessor、hit resolver 和 equipment reaction interface。

### F-10 AI 另建简化伤害公式，忽略护盾和固定减伤

> **修复状态（2026-07-24）：已解决。** production `BattleAiScoreService` 现在使用已注入的 `BattleDamageResolver.PreviewDamageEffectTyped(...)`，以 Average/Expected 模式复用正式抗性、固定减伤、护盾吸收与生命伤害语义；多段伤害串联 source/target preview-after 克隆状态，击杀分支按护盾后的生命伤害计算，预览不修改真实单位。focused regression 覆盖 half resistance + shield、不误判击杀和两段连续消耗护盾。

**位置**：`scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs:685-753`。

估算固定写 `ShieldAbsorbed = 0`，并以调整后伤害直接比较 `current_hp`。5 HP、10 shield、预计伤害 6 的目标会被判断为稳定击杀，实际 HP 伤害为 0；guard、stance/content/equipment fixed mitigation 也未计入。该估算被 path-step、effect metrics、threat projection 多处复用。AI 应消费 canonical read-only damage preview，而不是维护第二套 resolver。

### F-11 AI 完整平局没有稳定键

> **修复状态（2026-07-24）：已解决。** 正式目标 comparator 在威胁、HP、距离等业务指标完全相同时，统一以 `unit_id` 的 ordinal 顺序作为最终稳定键。回归用相反的 `BattleState` 单位插入顺序构造同一战术状态，两次都选择 `target_a`，确认目标不再继承 dictionary 插入顺序。

**位置**：`scripts/systems/battle/ai/BattleAiTypedActionHelper.cs:537-552`；回归见 `tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs:388-407`。

威胁、HP、距离全部相同时 comparator 返回 0；输入来自 state dictionary 枚举，首目标取决于单位插入/恢复顺序。相同战局可产生不同命令和模拟结果。所有 selector 应以 `unit_id` ordinal 作为最终稳定键。

### F-12 标称只读的规则查询会修改 live footprint

> **修复状态（2026-07-24）：已解决。** `coord` 与 `body_size/body_size_category` 作为 authoritative geometry；`footprint_size/occupied_coords` 只在 `BattleUnitState` setter 和 `BattleState.SetUnit/SetUnits/SetUnitsFromDictionary` admission 写入口重建。攻击、邻接、AI、grid、runtime 与 UI 读路径已删除外部 `RefreshFootprint()`，clone/snapshot/load 对不一致投影 fail-fast；源码门禁同时禁止 owner 外重新调用 refresh 或直接写 `coord/body_size/body_size_category/footprint_size/occupied_coords`。查询回归验证 stale fixture 不被修复、引用与 movement revision 均保持不变，写入口回归验证同一 stale fixture 会被重建。存档字段与格式未改变。

**位置**：`BattleAttackCheckPolicyService.cs:450-496`，`BattleState.cs:605-769`，`BattleUnitState.cs:321-332,611-631`。

preview/context 和攻击劣势查询通过 `UnsafeUnitForReadOnlyRules` 调 `RefreshFootprint()`。如果投影不一致，纯查询会写 `footprint_size/occupied_coords`，却没有调用 `MarkMovementGeometryChanged()`；preview 不再幂等，路径/视线缓存仍持有旧 revision。footprint 不变量应由 state owner 在写入口维护，read view 不应提供可写逃生口。

### F-13 save payload 与 index 是两个独立提交点

> **修复状态（2026-07-24）：已解决。** 当前正式定义 `<save_id>.dat` 为唯一权威提交点，`index.dat` 为可重建派生缓存。payload 原子写成功后，即使 index 写入失败，`PersistGameState` 也保持成功语义、清除 runtime dirty，并记录 `session.save.index.degraded_after_payload_commit` warning；index session cache 同时失效，后续列表读取从合法 payload 重建且不会重复发布同一 save id。新增独立 `fail_index_write` seam，回归覆盖新建存档和已有存档更新两条链。payload/index schema 均未改变。

**位置**：`scripts/systems/persistence/GameSession.cs:1524-1567`，`GameSession.SaveIndexAndFileIO.cs:54-133,175-244,480-584`。

payload 已原子写成功后才写 index；index 写失败时 API 返回失败，但 payload 已永久存在。稍后 index rebuild 会扫描该 orphan payload 并重新发布它，使此前被报告为“保存失败”的新存档重新出现。需要 commit marker/journal 或显式 `partial/recovered` 状态来定义事务；不能简单删除 payload，因为进程中断也可能发生在两个写入之间。

### F-14 `shop_inventory_seed` 是未消费的持久化契约

> **修复状态（2026-07-23）：已解决。** 已按决策从据点顶层 schema、生成、投影和存档中删除 `shop_inventory_seed` / `shop_last_refresh_step`。每个 `shop_states[shop_id]` 独立持有实际 seed 与刷新步数；到期刷新仍独立获取真随机 seed，只更新目标商店。该破坏性 schema 变更纳入 v15，v14 直接拒绝且不迁移。

**位置**：`WorldMapSpawnProjection.cs:260-269`，`GameRuntimeSettlementCommandHandler.cs:1906-1921`，`SettlementShopService.cs:478-545`。

生成器和设计都保存 `shop_inventory_seed`，但 `GenerateShopState` 每次调用 `TrueRandomSeedService.GenerateSeed()`，从不读取它。即使修复 F-01，持久 seed 仍不能控制或复现刷新。应选择“用持久 seed 派生每次刷新”或“删除该 schema 字段”；删除/改变含义涉及保存契约，需要明确决策。

### F-15 save tag 校验在内容 owner 之间不一致

> **修复状态（2026-07-24）：已解决。** 新增共享 `SaveTagListContentRules`，合法值仍由 `BattleSaveContentRules` 的 typed save-tag 集合唯一拥有；列表规则统一检查 null/空元素、已移除的 `_advantage/_disadvantage/_immunity` 后缀、未知 tag 与重复值。Race、Subrace、Trait（含嵌套 passive status）、Enemy 和技能 effect 三组 save-tag 字段均调用同一规则，删除 Trait/Enemy 的重复实现。聚焦回归覆盖五类 owner，正式全内容 validation 保持零错误。资源字段、运行时匹配语义与存档格式均未改变。

**位置**：`SaveTagListContentRules.cs:1-67`，`RaceContentRegistry.cs:164-180`，`SubraceContentRegistry.cs:158-174`，`TraitContentRegistry.cs:197-211,315-319`，`SkillCombatProfileValidator.cs:672-689`，`EnemyTemplateDef.cs:488-506`。

Race/Subrace 只检查非空，技能 effect 的三组 save tag 数组没有语义校验；Trait/Enemy 则调用 `BattleSaveContentRules.IsValidSaveTag`。拼错 tag、旧后缀写法会通过部分 registry，运行时精确匹配时静默失效。应抽取共享 `SaveTagListContentRules`，统一验证合法集合、重复值与旧写法。

### F-16 生产装备能力构建跳过多数跨表校验

> **修复状态（2026-07-24）：已解决。** `EquipmentAbilityContentValidationContext` 只保留并强制要求 trait、skill、外部 status 三个 open-content catalog；`null` 目录稳定返回 `EQA_VALIDATION_CONTEXT_INCOMPLETE`，非 null 空目录仍执行 membership 校验。damage tag 与 equipment slot 改为直接消费 `DamageTagContentRules` / `EquipmentRules` 的 closed typed domain，删除可选白名单和 `HasKnownValues` 跳过路径。status 由 `StatusContentRules` 的系统声明、技能 effect、trait passive status 与全部装备 pack 的创建型 action 先组成声明目录，再统一校验 condition/fact/action 引用；`knockdown_immunity` 已有正式系统声明并由 battle 语义表消费。收紧校验同时把正式装备内容中的旧 `cold` / `necrotic` 值改为 canonical `freeze` / `negative_energy`，不增加兼容别名。聚焦 registry、正式 `ProgressionContentRegistry` 全内容入口和 status semantics 回归均通过，编译 0 warning / 0 error。

**位置**：`ProgressionContentRegistry.cs:719-725`，`EquipmentAbilityRuntimeDefinitions.cs:827-845`，`EquipmentAbilityBindingValidator.cs:954-997`，`EquipmentAbilityPayloadValidators.cs:55,152,275,1536-1578`。

生产 validation context 只填 `KnownTraitIds` 和 `KnownSkillIds`。status、damage type、equipment slot 集合为空时，validator 通过 `HasKnownValues` 跳过检查；拼错引用仍能进入 sealed process snapshot，运行时表现为能力不触发。无法提供 canonical 集合应使构建显式失败，而不是把“不可校验”当“合法”。

### F-17 远程魔法攻击类别校验条件不可达

> **修复状态（2026-07-24）：已解决。** 没有把死条件改成另一套“远程魔法攻击”推断，而是删除该推断职责。新增 `CombatSkillDef.projectile_kind = none/nonmagical/magical/current_weapon` 与 `CombatCastVariantDef.projectile_kind_override`；`BattleEffectCategoryResolver` 从 typed 投送定义派生 `projectile`、`magical_projectile`、`nonmagical_projectile`。职业、magic tag、射程、伤害、豁免与技能 id 均不再决定是否为投射物。派生类别禁止写入 `delivery_categories/effect_categories`，旧 missile 名称加载期报错且无兼容别名。80 个原有技能、基础攻击与虹光法球红/橙层已迁移；variant override 已贯通 preview/commit/ground/repeat/chain 链路。聚焦 schema、resolver、虹光法球、随机链及正式全内容 validation 均通过。

**位置**：`CombatProjectileContentRules.cs:1-55`，`CombatSkillDef.cs:247-263`，`CombatCastVariantDef.cs:43-58`，`SkillCombatProfileValidator.cs:197-234,475-540,1261-1323`，`BattleEffectCategoryResolver.cs:7-89`，`BattleEquipmentAttackModifierResolver.cs:525-605`。

原问题中的 `IsAttackDamage` 要求 `save_dc_mode == ""`，而合法默认是 `static`，空值反而会被 save mode 校验拒绝；因此原 `magical_missile` 必填检查不可达。进一步检视确认，“法师 + 魔法 + 远程 + 攻击伤害”本身也不能证明投送形式是魔法投射物，所以修复以显式 typed 投送 schema 取代条件修补。

### F-18 identity registry 的字段校验 helper 是空实现

**位置**：`IdentityContentRegistryBase.cs:203-212`；调用见 `RaceContentRegistry.cs:107-119`、`SubraceContentRegistry.cs:114-126`、`AgeContentRegistry.cs:206-218`、`BloodlineContentRegistry.cs:180-192,233-245`、`AscensionContentRegistry.cs:180-192,245-257`、`StageAdvancementContentRegistry.cs:116-122`；回归见 `tests/progression/schema/run_identity_required_text_schema_regression.cs`。

修复前，`_append_string_field_error`、`_append_int_field_error`、`_append_bool_field_error` 都没有逻辑，但多个 registry 把它们当校验入口。至少空白 `display_name`/`description` 可进入 snapshot，而且代码外观会让维护者误以为已经验证。字符串非空规则应实现；没有统一约束的 int/bool helper 应删除，由各 registry 写显式范围/组合规则。

> **修复状态（2026-07-24）：已解决。** 字符串入口已改名为 `_append_required_string_field_error`，并通过 `string.IsNullOrWhiteSpace` 拒绝 `null`、空串和纯空白；Race、Subrace、Age、Bloodline、Ascension、StageAdvancement 六个注册表的顶层及嵌套必填文本均接入该规则。无语义的 int/bool helper 与调用已删除，既有 owner-specific 数值规则（如 race 正速度、age 年龄顺序、stage advancement 轴向约束）继续负责真实约束。聚焦回归覆盖 15 个调用点；正式资源验证保持 `official_content errors=0`。本次不改变资源字段、typed definition 或存档格式。

### F-19 合法价格范围内会发生 32 位乘法溢出

**位置**：修复前为 `ItemDefinition.cs:545-550`、`ItemDef.cs:60-67,392-397`；当前规则 owner 为 `ItemPriceRules.cs`，回归见 `tests/warehouse/run_item_price_rules_regression.cs`。

修复前，`price * basisPoints` 在 `int` 中完成。默认 10000 时价格达到约 214,748 即可能溢出，商店 11000 时从 195,226 起即可触发；schema 允许 999,999。结果可能变负使商品消失，也可能回绕成错误低价。

> **修复状态（2026-07-25）：已解决。** `ItemPriceRules.ApplyBasisPoints` 现为唯一实现：输入先归一化为非负 `long`，乘法与 half-up 舍入均在 `long` 中完成，超过 `int` 返回契约时饱和到 `int.MaxValue`。`ItemDefinition` 与 authored `ItemDef` 的既有入口只做委托，不再复制公式。聚焦回归覆盖 schema 最大价格 `999999`、11000 basis points、旧溢出阈值、负输入、舍入和饱和行为；原有装备价格、据点商店与正式资源校验均通过。无资源字段或存档格式变更。

### F-20 `BattleGroundEffectService.Dispose()` 未解绑拆分子服务

> **修复状态（2026-07-24）：已解决。** owner 的幂等 teardown 现在依次关闭 validation、relocation、coord 三个子服务，再清除自身 runtime；各步骤通过统一异常聚合路径执行，单个清理失败不会跳过后续 borrower 解绑。runtime borrower teardown 回归要求 ground-effect owner 的 `ActiveDependencyCount` 在 dispose 后归零。

**位置**：`scripts/systems/battle/runtime/BattleGroundEffectService.cs:29-71`；回归见 `tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs:694-715`。

owner 创建三个子服务，Dispose 只清自己的 runtime。外部若仍持有子服务且 runtime 尚活，relocation 仍可调用 `MoveUnitForce` 修改战场。三个 child 应有幂等 teardown，清 runtime、owner 与 sibling，并纳入 borrower teardown/rebind 回归。

### F-21 单场模拟异常时 runtime 与全局 trace recorder 不保证清理

> **修复状态（2026-07-24）：已解决。** 单场模拟从 runtime 创建完成起进入异常清理边界，运行失败时仍执行 `runtime.Dispose()`；若业务异常和清理异常同时发生，则以 `AggregateException` 保留两者。loop trace 改用 `AiTraceRecorder.PushInstance(...)` 的作用域绑定，异常退出也会恢复进入前 recorder，而不是把进程级实例一律清空。聚焦回归分别覆盖 step executor 抛异常后的 recorder 恢复，以及 simulation 失败后的 runtime/state/AI borrower/sidecar 清理。

**位置**：`scripts/systems/battle/sim/BattleSimRunner.cs:241-329`，`scripts/systems/battle/sim/BattleSimExecutionLoop.cs:99-125`；回归见 `tests/battle_runtime/runtime/run_battle_sim_exception_cleanup_regression.cs:46-178`。

runtime 只在正常尾部 dispose；静态 `AiTraceRecorder` 只在正常执行后清空。`StartBattle`、`AdvanceStep`、指标抓取或报告构建抛异常时，会留下 runtime borrower/state/terrain owner，且 recorder 可污染后续模拟。两层都需要 `try/finally`，recorder 应恢复此前实例而不是一律设 null。

### F-22 报告文件未写成功也会返回路径并记录成功

> **修复状态（2026-07-24）：已解决。** `BattleSimReportFileWriter` 现在是 report、trace 与 summary 的唯一写盘 owner：目录创建、打开、写入、flush 和最终文件存在性任一失败都会抛出；失败时恢复此前 `OutputFiles` 并清理本批次残缺文件。Runner 只在 `Write(...)` 成功返回后发布路径和记录 `report-written`。聚焦回归覆盖同秒唯一文件名与 JSON 可解析、后续文件写失败时恢复旧路径并删除前序残片，以及不可打开路径的失败传播。

**位置**：`scripts/systems/battle/sim/BattleSimRunner.cs:118-137`，`scripts/systems/battle/sim/BattleSimReportFileWriter.cs:51-174`；回归见 `tests/battle_runtime/runtime/run_battle_sim_report_output_regression.cs:60-179`。

输出路径在 `FileAccess.Open` 前写入；Open 返回 null 时只跳过写入，调用方仍记录 `report-written`。现有测试只断言路径非空。路径应在写入成功后发布；不可写目录测试需断言无成功日志、无假路径，正常路径还应检查文件存在且 JSON 可解析。

### 范围排除：`scripts/tools` 人工离线工具

原 F-23 `tree_baker.gd` 与 F-24 `tile_baker.gd` 的观察事实仍成立，但二者不属于生产运行链或正式资产构建验收，因此不计入问题总数和修复队列。排除依据是 owner/交付范围，不是脚本语言；若未来接入正式资产流水线，应重新审核输入、输出和失败退出契约。

## P3：低优先级或潜伏问题

### F-25 `BattleMapPanel.HideBattle()` 保留完整上场战斗对象图

> **修复状态（2026-07-24）：已解决。** `BattleMapPanel` 现在通过统一 pending payload 清理路径释放 `BattleState`、选择参数和集合；payload 应用前先转入局部变量并从 panel 清除，`HideBattle()` 与 `_ExitTree()` 也执行同一清理。离树时额外释放 hover preview 的 `BattleState`，并通过不发 signal 的 reveal ticket 失效路径阻止异步 continuation 继续刷新已离树 UI。聚焦 panel schema/lifecycle 与 world battle loading overlay 回归均通过。

**位置**：`scripts/ui/BattleMapPanel.cs:77-87,232-269,382-418,696-706,727-739,803-835,902-909`；对照 `BattleBoard2D.cs:270-299`。

panel 保存 `_pending_battle_state` 和相关列表；Hide 只切 visible 并清 board，不清 pending state，`_ExitTree` 也未清。已结束战斗会被 UI 持有到下一场战斗或 panel 回收。建立统一 `ClearPendingPayload`，在 apply/hide/teardown 后释放，并用 weak-reference/lifecycle 回归验证。

### F-26 Quest public API 可把 Completed/Failed 留在 active 集合

> **修复状态（2026-07-25）：已完整解决。** `PartyState` 内部新增 `QuestJournalState`，作为 active、claimable、failed、rewarded 四个互斥阶段的唯一 mutation owner；accept/progress/complete/reward/fail/restart 均通过原子迁移，所有 query 返回 detached `QuestState`，外部不能再借用引用绕过集合归属。`QuestFailureRequest` 提供 typed 失败输入，失败态持久化时间、原因与上下文；authored `failure_policy = terminal/restartable` 经 `QuestDefinition.CanRestartAfterFailure` 投影，失败重启不再滥用成功后的 `is_repeatable`。PartyState schema 升至 v8、顶层 SaveVersion 升至 17，按确认不提供旧存档兼容；全部现有 quest 显式设为 terminal。聚焦回归覆盖集合互斥、detached query、terminal/restartable 分支、失败态往返、旧 Party v7 拒绝、集合/状态错配拒绝及 runtime snapshot。

**位置**：`scripts/player/progression/QuestJournalState.cs`、`PartyState.cs`、`QuestState.cs`、`QuestDef.cs`，`scripts/systems/progression/QuestProgressService.cs`、`QuestFailureRequest.cs`，`scripts/systems/persistence/SaveSchemaVersions.cs`。

修复前，`RecordProgress` 完成全部目标后只在活引用上 `MarkCompleted`，没有经 `MarkQuestClaimable` 移出 `active_quests`；`SetQuestState(Failed)` 也会路由到 active。读档要求 active 项状态必须为 Active，因此该 public API 可写出无法读回的 PartyState。当前实现已删除这种“外借 canonical 引用后原地改状态”的模型。

### F-27 技能书使用不是原子事务

> **复核状态（2026-07-24）：撤销，不再作为 finding。** `CountItem(...) > 0` 与随后 `RemoveItemTyped(..., 1)` 使用同一个同步 `PartyWarehouseService` 和同一份仓库状态；二者之间的 `LearnSkillTyped(...)` 只修改角色成长/成就状态，不接触仓库，且没有异步、回调或可重入点。因此当前正式路径中扣减 1 必然成功，`consume_failed` 只是防御分支，不能证明存在失败后留下已学习技能的可达状态。成功后的存档提交失败已有上层快照回滚，但这并不会让扣减失败分支变得可达。

**位置**：`scripts/systems/inventory/PartyItemUseService.cs:146-151`。

服务先学习技能、再扣库存。扣减失败会返回 `consume_failed`，但新技能和被替换的练功技能不回滚；上层快照只在 persist failure 时恢复。当前预检令正常单线程路径较难触发，但服务契约已经允许“失败且改状态”。

### F-27A 仓库命令把普通运行态变更立即写入完整存档

> **修复状态（2026-07-24）：已解决。** 仓库直接加入、丢弃一件、丢弃全部和技能书使用成功后改走 `StagePartyState()`；facade 只调用 `GameSession.SetPartyState(...)` 同步 canonical party 并保留 `party_state` pending dirty，不调用 `RuntimeTransaction.Commit(...)` 或磁盘写入。session staging 失败仍按原 snapshot 恢复，但 payload 写入失败不再参与仓库命令成败。`run_world_map_runtime_proxy_regression.cs` 会强制 payload 写入失败并依次覆盖四种 mutation，验证命令成功、状态保留、pending dirty 且没有 save error。

**位置**：`scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs:137,204,269,332,884-888`，`scripts/systems/game_runtime/RuntimeTransaction.cs:277-287`，`scripts/systems/persistence/GameSession.cs:1355-1374`。

修复前，技能书使用、丢弃和直接加入库存成功后都会调用 `PersistPartyState()`；该入口不只是同步 session 内存，而会继续执行 `CommitRuntimeState(...) → PersistGameState()`。按当前产品保存时机，普通仓库操作只应更新运行态、把 `party_state` stage 到 `GameSession` 并保留 pending dirty，由既定 canonical flush 统一落盘；不能让每次库存操作自行建立磁盘保存点，也不能只修改技能书路径。

### F-28 `UnitProgress` 合并来源成环会栈溢出（接受风险，暂不修复）

> **决策状态（2026-07-24）：接受风险，暂不修复。** 当前递归查询没有生产调用方，本轮不扩大为 merge graph schema、写入校验或存档契约变更；源码已在 API 旁明确记录无环前提与未来接线前必须完成的 cycle detection。该项不是“已解决”，未来出现第一个生产调用方前必须重新开启。

**位置**：`scripts/player/progression/UnitProgress.cs:225-251`。

DFS 在递归返回后才把节点加入 `visited`，A→B→A 会无限递归。已确认当前递归查询没有生产调用方，因此本轮不修改算法或读档契约，只在 API 旁记录无环前提；未来接线前必须在进入节点时标记，并在写入/读入来源图时拒绝环。

### F-29 save 文件在检查后消失会抛异常而非返回 DoesNotExist

> **修复状态（2026-07-24）：已解决。** repository 对缺失 target 和 open 期间消失都返回 typed 错误，缺失统一为 `Error.DoesNotExist`；`emitErrors` 只记录诊断，不再抛异常。`LoadSave(...)` 对未知槽位也返回 `DoesNotExist`，缓存索引仍引用已消失 payload 时会移除该失效索引项。`run_invalid_save_graceful_regression.cs` 覆盖缓存槽位后删除 `.dat` 的完整路径，确认加载不抛异常、不产生 active world，并清理索引。

**位置**：`SaveRepository.cs:31-55`，`GameSession.cs:724-738,1438-1468`。

外部同步/删除若发生在存在性检查与 open 之间，repository 在 recovery 后仍找不到文件时抛 `InvalidOperationException`。自动加载循环预期 `LoadSave` 返回错误码并跳过坏候选，因此窄窗口会打断整个恢复流程。应把可预期 I/O 消失映射为 typed read result。

### F-30 Vector2I 校验与 world runtime 读取支持集不一致

> **修复状态（2026-07-24）：已解决。** 当前 save schema 只接受 Godot 原生 `Variant.Type.Vector2I`。`SaveSerializer` 已删除 `{x,y}` dictionary 的校验和读取兼容路径，玩家起点、挂载子地图坐标、返回栈、世界事件、资源点及据点坐标统一在进入 typed world owner 前拒绝非原生表示。后续统一迷雾持久态时，`fog_states` v2 的 explored/revealed 也改为原生 `Vector2I` 数组，旧字典/字符串表示不再接受；该破坏性开发期调整把顶层 save version 提升为 16，不提供迁移。持久化回归验证原生坐标规范化后保值、dictionary 坐标被拒绝，相邻 save/submap 回归通过。

**位置**：`SaveSerializer.cs`，`tests/runtime/persistence/run_invalid_save_graceful_regression.cs`。

serializer 接受 native `Vector2I` 和 `{x,y}` dictionary，自己的 helper 也支持两者；验证后却交给 `WorldRuntimeData.FromDictionary`，其 `ReadVector2I` 只接受 native 值。合法的 dictionary `player_start_coord` 因而会静默变为 `(0,0)`。当前磁盘主路径通常写 native 值，所以列为潜伏 boundary mismatch。

### F-31 部分 AI trace span 的 Enter/Exit 不具备异常安全性

> **修复状态（2026-07-25）：已解决。** 原始 finding 列出的 charge、charge-path AOE 与 move-to-range evaluator 已统一使用 `BattleAiTraceSpan`，异常注入回归覆盖 preview、path 和 score 失败。后续复核发现 `BattleSkillPreviewService` 的 orchestrator、unit/ground preview、target validation、damage preview 与日志 span 仍存在同类裸 `Enter/Exit`；现已全部改为作用域 span，并在 `run_ai_trace_recorder_regression.cs` 增加异常关闭行为和该 owner 禁止裸 trace pair 的架构契约。`dotnet build magic.csproj --no-restore` 以 0 warning / 0 error 通过；trace recorder、melee charge、charge-path AOE、move-to-range、unit target gate、ground protocol、meteor preview 七条聚焦回归全部通过。

**位置**：`BattleAiChargeActionEvaluator.cs`，`BattleAiChargePathAoeActionEvaluator.cs`，`BattleAiMoveToRangeActionEvaluator.cs`，`BattleSkillPreviewService.cs`，`tests/battle_runtime/ai/run_ai_trace_recorder_regression.cs`。

preview/path/score 在手写 Enter 与 Exit 之间抛异常时，frame 留在全局 recorder stack，外层 span 随后出现 mismatch。统一改用 `using BattleAiTraceSpan` 或 try/finally。

### F-32 高频 barrier/simulation fixture 仍创建无 owner 的 Godot 集合

> **修复状态（2026-07-24）：已解决。** barrier 两个 owner 直接调用 `IEnumerable<Vector2I>` typed changed-coord 重载，删除临时 Godot Array/转换 helper。formal fixture 的六维属性、技能配置、核心技能和装备占位改为 CLR DTO/List；仅角色创建遗留 payload 在同步调用处建立 Request-domain projection lease。battle character gateway 的成就返回值收敛为 `IReadOnlyList<StringName>`，simulation 不再为每个事件创建空 typed Array，正式角色模块只在 Godot-facing 公共包装处投影。架构回归锁定 typed barrier 路径、plain roster contract 与每成员 lease 回到基线。

**位置**：`BattleBarrierService.cs`，`BattleBarrierOutcomeResolver.cs`，`IBattleRatingCharacterGateway.cs`，`BattleSimFormalCombatFixture.cs`，`CharacterManagementModule.cs`。

barrier 路径创建裸 `Godot.Collections.Array`，而 runtime 已有 CLR typed overload；formal fixture 也大量生成不受 lease/scope 所有的临时 Godot wrapper。它们依赖终结器回收并增加 native churn。barrier 应直接走 typed overload；fixture 应用 CLR DTO 构造 roster，仅在遗留 Godot API 的最窄边界创建 request lease。

## 架构层面的共同根因

### 1. “typed” 不等于“拥有完整状态”

F-01 的 DTO 类型是强类型的，但它只覆盖 aggregate 的三个字段，随后却拥有了完整替换权。typed owner 必须是 total projection；partial view 只能用于只读展示，不能用于 canonical 写回。

### 2. 诊断断言不是事务

F-06～F-08 原先把稳定摘要、差异展示和状态恢复混在同一份手写字段清单中。最终边界已收敛为完整 stable detection：正常与异常退出都在清理前校验，发现差异即报告并失败，不回滚已失败 fixture；production 默认完全关闭该诊断。

### 3. 接口抽象之后仍要修正依赖获取方向

F-09 已有接口却仍从具体 `BattleRuntimeModule` service locator 获取它。真正的解耦需要由 composition root 显式注入接口和 read model，rules 不能反向知道 runtime owner。

### 4. 生命周期必须覆盖异常和“外部仍持有 child”的情况

F-20、F-21、F-25、F-31、F-32 都不是简单 null-check 缺失，而是 owner/borrower 的结束边界不完整。Dispose 必须使所有拆分 child 失效；临时全局 recorder 和 native wrapper 必须由作用域保证恢复。

### 5. 持久状态只能有一个事实 owner

F-03、F-04、F-13、F-14、F-30 分别展示了死亡、属性、保存提交、商店随机性和坐标表示的双重真相。每类数据需要一个 canonical schema/owner，其他层只消费其 typed snapshot 或 typed result。

### 6. `game_runtime` / settlement / world 的 raw dictionary 仍是高风险交界面

在 `scripts/systems/game_runtime`、`settlement`、`world`、`persistence` 中，`GDictionary` 仍同时承担 request、mutable domain state、UI context 和 save payload。F-01 就发生在 dictionary domain state 与不完整 typed DTO 之间。后续迁移应优先围绕事务 aggregate 和边界 DTO，而不是机械地把每个 dictionary 包一层类型。

## 收尾状态与剩余门禁

1. **已完整解决**：F-01、F-03～F-22、F-25、F-26、F-27A、F-29～F-32；每项状态、当前 owner 与回归依据见对应 finding。
2. **处理中**：F-02 原剩余 14 个源码已进入 index，但收尾期间又出现 2 个仍在迁移中的未跟踪源码；待并发改动稳定后必须重新确认归属、按功能主题与调用方和测试共同提交，并在干净 worktree/clone 构建。
3. **接受风险**：F-28 保持未修；第一个生产调用方接入递归 merge source API 前，cycle detection 是前置门禁。
4. **撤销或排除**：原 F-27 已复核撤销；原 F-23/F-24 属人工离线工具，不进入当前生产修复队列。

## 验证记录

已执行：

```powershell
dotnet build magic.csproj --no-restore
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_runtime_data_typed_regression.cs
godot --headless -s res://tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs
python tests/run_regression_suite.py --jobs auto
git ls-files --others --exclude-standard -- scripts
```

结果：

- `dotnet build`：通过，0 warning / 0 error。
- 三个 settlement/world focused runner：通过。
- 常规回归：394 passed / 0 failed，总耗时 261.5 秒。
- 未运行数值型 battle simulation/balance 入口；它们按仓库规则不属于常规全量回归，本轮也没有用户要求做平衡分析。
- `scripts/` 未跟踪代码文件：48。

上述结果是修复前的审计基线；当时尚未覆盖完整 settlement 状态写回、clean checkout、AI evaluator 先突变后异常、simulation start failure 分类、不可写报告目录、HP/death 矛盾存档、有效属性装备门槛等。

### F-01 修复复验（2026-07-21）

- `dotnet build magic.csproj --no-restore`：通过，0 warning / 0 error。
- `python tests/run_regression_suite.py --pattern tests/world_map/runtime --jobs auto`：28 passed / 0 failed。
- `python tests/run_regression_suite.py --jobs auto`：396 passed / 0 failed；未包含按仓库规则排除的数值型 battle simulation/balance 入口。
- `run_runtime_lifecycle_boundary_regression.cs`：通过，确认商店 window result 只长期持有 plain CLR graph，Godot dictionary 仅作为 Request-domain 短投影。

### F-03 修复复验（2026-07-22）

- `dotnet build magic.csproj --no-restore`：通过，0 warning / 0 error。
- `run_party_member_state_owner_api_regression.cs`：通过，覆盖构造态、写入口、存活/死亡 round-trip 与双向矛盾 payload 拒绝。
- `run_party_state_duplicate_regression.cs`、`run_battle_permadeath_regression.cs`、`run_invalid_save_graceful_regression.cs`：通过，覆盖 clone、真实死亡磁盘重载及非法存档失败边界。
- `run_game_runtime_settlement_command_handler_regression.cs`：通过，确认普通据点恢复不会复活死亡成员。
- `python tests/run_regression_suite.py --jobs auto`：396 passed / 0 failed；未包含按仓库规则排除的数值型 battle simulation/balance 入口。

### F-04 修复复验（2026-07-22）

- `dotnet build magic.csproj --no-restore`：通过，0 warning / 0 error。
- `run_party_equipment_regression.cs`：通过，覆盖 base Strength 17 + `titan_avatar` +3、缺少快照 provider 失败关闭、被替换装备单独垫门槛失败、候选装备不能自我满足、不冲突装备仍可贡献，以及 preview/commit 一致性。
- `run_battle_change_equipment_requirement_regression.cs`：通过，覆盖稳定升华来源、battle-local 被替换装备排除、临时 battle snapshot 不参与，以及 preview/IssueCommand 失败无 AP/背包/装备副作用。
- `run_mountainbreaker_weapon_ability_regression.cs`、`run_attribute_source_context_regression.cs`、`run_battle_hud_typed_projection_regression.cs`、`run_game_runtime_party_command_handler_regression.cs`、`run_battle_equipment_text_command_regression.cs`：通过，覆盖既有裂山者门槛、正式聚合入口、HUD/文本投影和世界 runtime 接线。
- `python tests/run_regression_suite.py --jobs auto`：396 passed / 0 failed，总耗时 258.8 秒；未包含按仓库规则排除的数值型 battle simulation/balance 入口。

### F-05 修复复验（2026-07-22）

- `dotnet build magic.csproj -nologo -clp:ErrorsOnly`：通过，0 warning / 0 error。
- `run_battle_sim_start_failure_regression.cs`：通过，覆盖空阵容 `invalid_start_units`、强制地形耗尽 `placement_exhausted`，以及首轮 `spawn_reachability` 失败后重试成功并清空瞬态失败快照；失败两例均为 `InvalidRuntime`、0 iterations、0 idle loops，`InvalidRuntimeRunCount == 1`、`StalledRunCount == 0`，并在 Godot report、file report、trace summary 三条投影中保留原因。
- `run_battle_sim_report_builder_regression.cs`、`run_battle_sim_typed_report_regression.cs`、`run_battle_sim_trace_summary_builder_regression.cs`、`run_battle_simulation_regression.cs`、`run_battlesim_formal_fixture_regression.cs`、`run_battle_sim_exception_cleanup_regression.cs`、`run_battle_ai_trace_projection_lease_regression.cs`、`run_world_map_battle_loading_overlay_regression.cs`：通过，覆盖终止分类、typed 报告、投影金值、正常启动、正式 fixture、异常清理和世界侧跨帧重试。
- `python tests/run_regression_suite.py --jobs auto`：397 passed / 0 failed，总耗时 261.9 秒；未包含按仓库规则排除的数值型 battle simulation/balance 入口。
- 未运行 6v12 数值基线或其他平衡 benchmark；本次只运行行为契约回归。

### 统一收尾复核（2026-07-25）

- 源码复核确认 F-11 的 ordinal `unit_id` 最终平局键、F-20 的 ground-effect 子服务 teardown、F-21 的 runtime/trace 异常清理和 F-22 的报告原子发布边界均仍存在；对应聚焦回归也包含上述失败模式。
- 本次两次尝试重新执行 `dotnet build magic.csproj --no-restore`，但当前并发工作树中的另一组 `BattleUnitState` weapon owner 迁移尚未完成，分别产生 877 和 843 个编译错误；错误数在两次构建间变化也表明文件仍在被并发修改。该失败不是本次文档修改造成的；由于 build 未通过，本次没有运行 Godot headless 回归，不能把旧程序集结果记为当前 PASS。
- F-02 首次复核时的 14 个剩余源码均已进入 index；收尾过程中又出现 `BattleExitObjectiveRules.cs` 与 `BattleUnitWeaponProjectionState.cs` 两个未跟踪源码，因此 F-02 保持处理中，干净 worktree 构建仍是提交整理阶段的最终门禁。

### F-26 正式修复复验（2026-07-25）

- `dotnet build magic.csproj --no-restore`：通过，0 warning / 0 error；此前统一收尾复核记录的并发 weapon owner 迁移编译阻塞已经消失。
- `run_typed_party_quest_state_regression.cs`、`run_quest_progress_service_regression.cs`、`run_party_state_duplicate_regression.cs`、`run_quest_accept_requirement_evaluator_regression.cs`、`run_character_management_quest_materializer_regression.cs`：通过。
- `run_save_serializer_quest_round_trip_regression.cs`、`run_save_projection_lease_regression.cs`、`run_game_runtime_snapshot_builder_regression.cs`、`run_quest_config_validation.cs`：通过。
- `run_game_runtime_settlement_command_handler_regression.cs`、`run_npc_quest_offer_regression.cs`、`run_text_command_quest_progress_regression.cs`：通过；前两个 runner 的坏内容 fixture 会按预期输出 validation error，但测试最终 PASS。
- 本次只执行 F-26 相关聚焦回归，没有重新运行常规全量回归或数值型 battle simulation。

## 已复核并排除的旧/表面候选

- 新建存档的持久化失败回滚当前已存在于 `GameSession.cs:636-706`，不再沿用旧 review 结论。
- save schema/version 当前由 `SaveSchemaVersions.cs` 提供单一来源；旧的多处常量漂移结论不成立，但该文件尚未跟踪，转化为 F-02。
- `QuestState.last_progress_context` 当前已捕获 `ArgumentException`。
- equipment ability registry 当前先检查 errors，再做 definition projection；旧的投影顺序崩溃结论不成立。
- ground barrier 的 manual/pending/auto-cast/preview 当前共用 clip helper，未确认规则漂移。
- movement query cache 会由 `MovementGeometryRevision` 驱动重建；未确认陈旧缓存。F-12 是另一条“只读查询写 state 且不推进 revision”的确定问题。
- 单纯文件过大、partial 拆分、无 namespace、理论 barrier id 碰撞没有独立计为缺陷。
- `scripts/systems/fate/LowLuckRelicRules.cs` 已纳入逐行覆盖，未发现新的可证实问题。

## 剩余设计决策

Failed quest 的正式归属已经确定：它保留在 `QuestJournalState` 的独立持久 failed 集合中，终止态记录失败时间、原因和上下文；只有内容显式声明 restartable 时才可清空旧失败记录并重新进入 fresh active。F-26 不再有待决设计。

其余原开放项均已闭合：F-04 已统一由正式有效属性快照决定装备门槛；F-14 已删除据点级 `shop_inventory_seed`；F-13 已确定 payload 是权威提交点、index 是可重建缓存。F-28 不是待决方案，而是已接受并带有启用前置门禁的风险。

## 附录 A：48 个未跟踪源码

```text
scripts/player/progression/CombatEffectCategoryContentRules.cs
scripts/player/progression/equipment_abilities/EquipmentAbilityBindingValidator.cs
scripts/player/progression/equipment_abilities/EquipmentAbilityDefinitionProjection.cs
scripts/player/progression/equipment_abilities/EquipmentAbilityPayloadValidators.cs
scripts/player/progression/SkillCombatProfileValidator.cs
scripts/player/progression/SkillDamageEffectValidator.cs
scripts/player/progression/SkillExecuteEffectValidator.cs
scripts/systems/battle/ai/BattleAiMutationBarrierSnapshots.cs
scripts/systems/battle/ai/BattleAiMutationSnapshotModel.cs
scripts/systems/battle/ai/BattleAiMutationSnapshots.cs
scripts/systems/battle/ai/BattleAiMutationStableProjection.cs
scripts/systems/battle/core/BattleEquipmentAbilityReactionContracts.cs
scripts/systems/battle/rules/BattleDamageResolver.DamageOutcome.cs
scripts/systems/battle/rules/BattleDamageResolver.Preview.cs
scripts/systems/battle/rules/BattleDamageResolver.SaveBranch.cs
scripts/systems/battle/rules/BattleEquipmentDurabilityResolver.cs
scripts/systems/battle/rules/IBattleEquipmentAbilityReactionService.cs
scripts/systems/battle/runtime/BattleAiDecisionBindingService.cs
scripts/systems/battle/runtime/BattleChainDamageService.cs
scripts/systems/battle/runtime/BattleCommandPreviewService.cs
scripts/systems/battle/runtime/BattleContingencyBridgeService.cs
scripts/systems/battle/runtime/BattleEquipmentAbilityConditionEvaluator.cs
scripts/systems/battle/runtime/BattleEquipmentAbilityStateResolver.cs
scripts/systems/battle/runtime/BattleEquipmentAreaActionResolver.cs
scripts/systems/battle/runtime/BattleEquipmentAttackModifierResolver.cs
scripts/systems/battle/runtime/BattleEquipmentDirectEffectActionResolver.cs
scripts/systems/battle/runtime/BattleEquipmentSkillTriggerActionResolver.cs
scripts/systems/battle/runtime/BattleEquipmentStatusActionResolver.cs
scripts/systems/battle/runtime/BattleEquipmentSummonResolver.cs
scripts/systems/battle/runtime/BattleEquipmentTargetMarkResolver.cs
scripts/systems/battle/runtime/BattleGroundEffectCoordService.cs
scripts/systems/battle/runtime/BattleGroundRelocationService.cs
scripts/systems/battle/runtime/BattleGroundSkillValidationService.cs
scripts/systems/battle/runtime/BattleMetricsReportService.cs
scripts/systems/battle/runtime/BattleMovementCommandService.cs
scripts/systems/battle/runtime/BattleRandomChainSkillService.cs
scripts/systems/battle/runtime/BattleSkillPreviewService.cs
scripts/systems/battle/runtime/BattleSkillTargetValidationService.cs
scripts/systems/battle/runtime/BattleSpawnPlacementService.cs
scripts/systems/battle/runtime/BattleSpecialSkillGateService.cs
scripts/systems/battle/runtime/BattleTimelineStatusBridgeService.cs
scripts/systems/battle/sim/BattleSimTerminationKind.cs
scripts/systems/persistence/SaveSchemaVersions.cs
scripts/ui/BattleMapPanel.CommandDock.cs
scripts/ui/BattleMapPanel.Equipment.cs
scripts/ui/BattleMapPanel.SkillGrid.cs
scripts/ui/BattleMapPanel.Styles.cs
scripts/ui/BattleMapPanel.TimelineBadges.cs
```
