# `scripts/` 逐行代码检视（2026-07-21）

## 结论

本轮在当前工作树中确认 **30 项问题**：P1 级 5 项、P2 级 17 项、P3 级 8 项；未发现 P0 级问题。最优先处理的不是代码风格，而是五个会破坏架构真相或交付完整性的边界：

1. 据点状态经不完整 typed DTO 写回时会删除商店库存、刷新种子、冷却和服务扩展字段。
2. `scripts/` 下有 48 个被 tracked 代码直接引用的源码文件尚未纳入 Git，干净检出无法复现当前本地构建。
3. `PartyMemberState.current_hp` 与 `is_dead` 可持久化为矛盾值，队伍层和战斗层会得出相反结论。
4. 装备门槛绕过正式有效属性快照，只读取 base attributes；身份、升华、职业等永久修正无法满足门槛。
5. BattleSim 把战斗启动失败写成 `IdleStall`，会污染模拟报告和后续调参依据。

`dotnet build magic.csproj --no-restore` 通过，常规回归套件 394/394 通过。通过并不等于以下问题不存在：多个现有测试只验证局部 service mutation 或路径非空，没有穿过实际 owner 写回、异常回滚和干净检出边界。

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

> **修复状态（2026-07-23）：已解决。** mutation guard 现在以 mutation 专用 typed snapshot 同时完成完整 capture、stable projection 与 exact restore。除原先遗漏的 unit/status/state 字段外，还覆盖 cell/column、terrain effect 及其 attack-roll modifier、layered barrier、blackboard 值与 presence flag、raw equipment entry、attribute 原始 map、objective/final decision、target marks、temporary-edge state 和两个 allocator。容器 canonical key 与对象内 `unit_id` / `status_id` / `coord` / barrier id 分开指纹和恢复；status、target mark、temporary edge 与 barrier layer/outcome 的原始集合保留顺序、重复项、非法哨兵、`null`、空集合与 `null` 元素；可空 `StringName` 不再与空值合并。回滚使用 owner 的 exact seam，不经过资源夹断、body 规范化、派生属性重算、gameplay cleanup 或 barrier payload 过滤。plain payload 同时编码 CLR/Godot 类型和完整分量，浮点按位比较。AI skill/barrier definition index 都复制调用方字典、只暴露只读视图，并由快照保存 canonical key 与 definition 引用 identity；`SkillDefinition`、combat/variant/effect 等整张技能定义图的公开构造输入均防御性冻结，mutable accuracy spec 只以 clone 暴露，因此引用指纹的不可变前提可证明。结构门禁锁定 unit、state、status、cell、terrain、barrier、blackboard、pending cast、equipment 等 owner，行为回归逐类验证检测与精确恢复。

**位置**：`scripts/systems/battle/ai/BattleAiMutationSnapshots.cs`、`BattleAiMutationStableProjection.cs`、`BattleAiMutationSnapshotModel.cs`、`BattleAiMutationBarrierSnapshots.cs`，以及 `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`；对照 `BattleState`、`BattleUnitState`、`BattleCellState`、terrain/barrier owner 与 `SkillDefinition` 的 exact/freeze seam。

unit snapshot 漏掉 `equipment_view_initialized`、已消费 contingency setup、装备能力来源、时间进度修正、creature tags、`weapon_range_type`；status projection 漏掉 `forced_move_immune`、`counts_as_debuff_*`、各 `lock_*`；state snapshot 也未覆盖 equipment target marks 和 cast sequence。stable diff 为零时 guard 直接返回，不执行恢复；这些字段被 evaluator 修改后会假绿并留在正式 state。浮点值先降为 `float` 再近似比较，也会漏掉小幅 double 修改。

**架构修复**：用权威 typed snapshot/schema 同时驱动 capture、stable projection 与 restore；仅对白名单派生缓存豁免，并增加“新增权威字段必须进入 snapshot”的结构测试。

### F-07 mutation guard 用有损诊断投影做事务回滚

**位置**：`BattleAiMutationGuard.cs:27-94`，`BattleAiMutationSnapshots.cs:596-603,636-638,1045-1075`，`BattleState.cs:210-257`。

报告与 promotion 队列只捕获固定白名单键；一次无关 HP mutation 触发 Restore 后，原有 `text`、`event_tags`、`refund_policy` 等扩展键会被删除。Restore 还直接替换 `log_entries`，没有恢复 `_log_text_byte_size`，后续日志会提前裁剪。诊断摘要不是无损事务快照，二者不应复用。

### F-08 evaluator 抛异常时 mutation guard 不校验也不恢复

**位置**：`scripts/systems/battle/ai/BattleAiService.cs:108-141`。

`ChooseCommandImpl` 若先修改状态再抛异常，控制流进入外层 finally；finally 只清 runtime binding 和 score scope，成功路径上的 `ValidateAndRestoreReportTyped` 不会执行。应把 capture/validate/restore 放进异常安全作用域：异常时先无条件恢复，再保留原始异常栈重抛。

### F-09 rules 层仍把具体 runtime 当 service locator

**位置**：`scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs:15-28,623-642,930-940,1152-1164`。

规则服务虽然局部使用 `IBattleEquipmentAbilityReactionService`，却必须先解析 `WeakReference<BattleRuntimeModule>` 才能取得接口和 state。脱离完整 runtime 的 preview/test 无法注入实现；runtime 引用失效时装备攻击修正和强制暴击会静默缺席。这是“类型抽象了、依赖获取方式没抽象”。`Setup` 应直接注入 state accessor、hit resolver 和 equipment reaction interface。

### F-10 AI 另建简化伤害公式，忽略护盾和固定减伤

**位置**：`scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs:685-753`。

估算固定写 `ShieldAbsorbed = 0`，并以调整后伤害直接比较 `current_hp`。5 HP、10 shield、预计伤害 6 的目标会被判断为稳定击杀，实际 HP 伤害为 0；guard、stance/content/equipment fixed mitigation 也未计入。该估算被 path-step、effect metrics、threat projection 多处复用。AI 应消费 canonical read-only damage preview，而不是维护第二套 resolver。

### F-11 AI 完整平局没有稳定键

**位置**：`BattleAiTypedActionHelper.cs:149-194,401-416,454-483`。

威胁、HP、距离全部相同时 comparator 返回 0；输入来自 state dictionary 枚举，首目标取决于单位插入/恢复顺序。相同战局可产生不同命令和模拟结果。所有 selector 应以 `unit_id` ordinal 作为最终稳定键。

### F-12 标称只读的规则查询会修改 live footprint

**位置**：`BattleAttackCheckPolicyService.cs:455-496`，`BattleState.cs:1321-1361`，实际写入 `BattleUnitState.cs:314-326`。

preview/context 和攻击劣势查询通过 `UnsafeUnitForReadOnlyRules` 调 `RefreshFootprint()`。如果投影不一致，纯查询会写 `footprint_size/occupied_coords`，却没有调用 `MarkMovementGeometryChanged()`；preview 不再幂等，路径/视线缓存仍持有旧 revision。footprint 不变量应由 state owner 在写入口维护，read view 不应提供可写逃生口。

### F-13 save payload 与 index 是两个独立提交点

**位置**：`scripts/systems/persistence/GameSession.cs:1518-1564`，`GameSession.SaveIndexAndFileIO.cs:433-505`。

payload 已原子写成功后才写 index；index 写失败时 API 返回失败，但 payload 已永久存在。稍后 index rebuild 会扫描该 orphan payload 并重新发布它，使此前被报告为“保存失败”的新存档重新出现。需要 commit marker/journal 或显式 `partial/recovered` 状态来定义事务；不能简单删除 payload，因为进程中断也可能发生在两个写入之间。

### F-14 `shop_inventory_seed` 是未消费的持久化契约

> **修复状态（2026-07-23）：已解决。** 已按决策从据点顶层 schema、生成、投影和存档中删除 `shop_inventory_seed` / `shop_last_refresh_step`。每个 `shop_states[shop_id]` 独立持有实际 seed 与刷新步数；到期刷新仍独立获取真随机 seed，只更新目标商店。该破坏性 schema 变更纳入 v15，v14 直接拒绝且不迁移。

**位置**：`WorldMapSpawnProjection.cs:260-269`，`GameRuntimeSettlementCommandHandler.cs:1906-1921`，`SettlementShopService.cs:478-545`。

生成器和设计都保存 `shop_inventory_seed`，但 `GenerateShopState` 每次调用 `TrueRandomSeedService.GenerateSeed()`，从不读取它。即使修复 F-01，持久 seed 仍不能控制或复现刷新。应选择“用持久 seed 派生每次刷新”或“删除该 schema 字段”；删除/改变含义涉及保存契约，需要明确决策。

### F-15 save tag 校验在内容 owner 之间不一致

**位置**：`RaceContentRegistry.cs:164-183`，`SubraceContentRegistry.cs:158-177`，`CombatEffectDef.cs:453-460`，`SkillCombatProfileValidator.cs:664-706`。

Race/Subrace 只检查非空，技能 effect 的三组 save tag 数组没有语义校验；Trait/Enemy 则调用 `BattleSaveContentRules.IsValidSaveTag`。拼错 tag、旧后缀写法会通过部分 registry，运行时精确匹配时静默失效。应抽取共享 `SaveTagListContentRules`，统一验证合法集合、重复值与旧写法。

### F-16 生产装备能力构建跳过多数跨表校验

**位置**：`ProgressionContentRegistry.cs:719-725`，`EquipmentAbilityRuntimeDefinitions.cs:827-845`，`EquipmentAbilityBindingValidator.cs:954-997`，`EquipmentAbilityPayloadValidators.cs:55,152,275,1536-1578`。

生产 validation context 只填 `KnownTraitIds` 和 `KnownSkillIds`。status、damage type、equipment slot 集合为空时，validator 通过 `HasKnownValues` 跳过检查；拼错引用仍能进入 sealed process snapshot，运行时表现为能力不触发。无法提供 canonical 集合应使构建显式失败，而不是把“不可校验”当“合法”。

### F-17 远程魔法攻击类别校验条件不可达

**位置**：`SkillCombatProfileValidator.cs:211-224,1230-1255`，`CombatEffectDef.cs:367-372`。

`IsAttackDamage` 要求 `save_dc_mode == ""`，而合法默认是 `static`，空值反而会被 save mode 校验拒绝。因此合法 direct damage 永远不会触发 `magical_missile` 必填检查。应由 typed save semantics 判断“是否具有豁免”，不要用互相冲突的字符串条件。

### F-18 identity registry 的字段校验 helper 是空实现

**位置**：`IdentityContentRegistryBase.cs:203-228`；调用见 `RaceContentRegistry.cs:107-134`、`SubraceContentRegistry.cs:114-126`、`AgeContentRegistry.cs:206-236`、`AscensionContentRegistry.cs:180-248`。

`_append_string_field_error`、`_append_int_field_error`、`_append_bool_field_error` 都没有逻辑，但多个 registry 把它们当校验入口。至少空白 `display_name`/`description` 可进入 snapshot，而且代码外观会让维护者误以为已经验证。字符串非空规则应实现；没有统一约束的 int/bool helper 应删除，由各 registry 写显式范围/组合规则。

### F-19 合法价格范围内会发生 32 位乘法溢出

**位置**：`ItemDefinition.cs:545-550`，`ItemDef.cs:60-67,392-397`。

`price * basisPoints` 在 `int` 中完成。默认 10000 时价格约超过 214,748 即溢出，商店 11000 时约 195,225 即可触发；schema 允许 999,999。结果可能变负使商品消失，也可能回绕成错误低价。应使用 `long` 乘法和舍入，再 checked/clamp 转回 int。

### F-20 `BattleGroundEffectService.Dispose()` 未解绑拆分子服务

**位置**：`BattleGroundEffectService.cs:28-50`，`BattleGroundRelocationService.cs:10-29,99-152`，`BattleGroundSkillValidationService.cs:10-32`，`BattleGroundEffectCoordService.cs:10-26`。

owner 创建三个子服务，Dispose 只清自己的 runtime。外部若仍持有子服务且 runtime 尚活，relocation 仍可调用 `MoveUnitForce` 修改战场。三个 child 应有幂等 teardown，清 runtime、owner 与 sibling，并纳入 borrower teardown/rebind 回归。

### F-21 单场模拟异常时 runtime 与全局 trace recorder 不保证清理

**位置**：`BattleSimRunner.cs:231-291`，`BattleSimExecutionLoop.cs:75-100`。

runtime 只在正常尾部 dispose；静态 `AiTraceRecorder` 只在正常执行后清空。`StartBattle`、`AdvanceStep`、指标抓取或报告构建抛异常时，会留下 runtime borrower/state/terrain owner，且 recorder 可污染后续模拟。两层都需要 `try/finally`，recorder 应恢复此前实例而不是一律设 null。

### F-22 报告文件未写成功也会返回路径并记录成功

**位置**：`BattleSimRunner.cs:118-124,373-469`，测试 `run_battle_ai_vs_ai_simulation_regression.cs:59-65`。

输出路径在 `FileAccess.Open` 前写入；Open 返回 null 时只跳过写入，调用方仍记录 `report-written`。现有测试只断言路径非空。路径应在写入成功后发布；不可写目录测试需断言无成功日志、无假路径，正常路径还应检查文件存在且 JSON 可解析。

### 范围排除：`scripts/tools` 人工离线工具

原 F-23 `tree_baker.gd` 与 F-24 `tile_baker.gd` 的观察事实仍成立，但二者不属于生产运行链或正式资产构建验收，因此不计入问题总数和修复队列。排除依据是 owner/交付范围，不是脚本语言；若未来接入正式资产流水线，应重新审核输入、输出和失败退出契约。

## P3：低优先级或潜伏问题

### F-25 `BattleMapPanel.HideBattle()` 保留完整上场战斗对象图

**位置**：`scripts/ui/BattleMapPanel.cs:77-87,230-265,393-407,687-697,787-803`；对照 `BattleBoard2D.cs:270-299`。

panel 保存 `_pending_battle_state` 和相关列表；Hide 只切 visible 并清 board，不清 pending state，`_ExitTree` 也未清。已结束战斗会被 UI 持有到下一场战斗或 panel 回收。建立统一 `ClearPendingPayload`，在 apply/hide/teardown 后释放，并用 weak-reference/lifecycle 回归验证。

### F-26 Quest public API 可把 Completed/Failed 留在 active 集合

**位置**：`QuestProgressService.cs:142-168`，`PartyState.cs:274-287,379-387,562-575`。

`RecordProgress` 完成全部目标后只在活引用上 `MarkCompleted`，没有经 `MarkQuestClaimable` 移出 `active_quests`；`SetQuestState(Failed)` 也会路由到 active。读档要求 active 项状态必须为 Active，因此该 public API 可写出无法读回的 PartyState。当前未发现生产调用方，故列 P3；应删除入口或统一委托 PartyState 状态迁移。

### F-27 技能书使用不是原子事务

**位置**：`scripts/systems/inventory/PartyItemUseService.cs:146-151`。

服务先学习技能、再扣库存。扣减失败会返回 `consume_failed`，但新技能和被替换的练功技能不回滚；上层快照只在 persist failure 时恢复。当前预检令正常单线程路径较难触发，但服务契约已经允许“失败且改状态”。

### F-28 `UnitProgress` 合并来源成环会栈溢出（接受风险，暂不修复）

**位置**：`scripts/player/progression/UnitProgress.cs:225-251`。

DFS 在递归返回后才把节点加入 `visited`，A→B→A 会无限递归。已确认当前递归查询没有生产调用方，因此本轮不修改算法或读档契约，只在 API 旁记录无环前提；未来接线前必须在进入节点时标记，并在写入/读入来源图时拒绝环。

### F-29 save 文件在检查后消失会抛异常而非返回 DoesNotExist

**位置**：`SaveRepository.cs:31-55`，`GameSession.cs:724-738,1438-1468`。

外部同步/删除若发生在存在性检查与 open 之间，repository 在 recovery 后仍找不到文件时抛 `InvalidOperationException`。自动加载循环预期 `LoadSave` 返回错误码并跳过坏候选，因此窄窗口会打断整个恢复流程。应把可预期 I/O 消失映射为 typed read result。

### F-30 Vector2I 校验与 world runtime 读取支持集不一致

**位置**：`SaveSerializer.cs:852-866,1013-1059,1423-1447`，`WorldRuntimeData.cs:87-105,620-625`。

serializer 接受 native `Vector2I` 和 `{x,y}` dictionary，自己的 helper 也支持两者；验证后却交给 `WorldRuntimeData.FromDictionary`，其 `ReadVector2I` 只接受 native 值。合法的 dictionary `player_start_coord` 因而会静默变为 `(0,0)`。当前磁盘主路径通常写 native 值，所以列为潜伏 boundary mismatch。

### F-31 部分 AI trace span 的 Enter/Exit 不具备异常安全性

**位置**：`BattleAiChargeActionEvaluator.cs:96-102,140-146,156-280`，`BattleAiChargePathAoeActionEvaluator.cs:169-171`，`BattleAiMoveToRangeActionEvaluator.cs:676-678,767-787,1591-1699,1833-1919`。

preview/path/score 在手写 Enter 与 Exit 之间抛异常时，frame 留在全局 recorder stack，外层 span 随后出现 mismatch。统一改用 `using BattleAiTraceSpan` 或 try/finally。

### F-32 高频 barrier/simulation fixture 仍创建无 owner 的 Godot 集合

**位置**：`BattleBarrierService.cs:217,243,848,1259-1269`，`BattleBarrierOutcomeResolver.cs:551-583`，`BattleSimFormalCombatFixture.cs:459-483,575-710,879-917,1281-1313,1455-1479`。

barrier 路径创建裸 `Godot.Collections.Array`，而 runtime 已有 CLR typed overload；formal fixture 也大量生成不受 lease/scope 所有的临时 Godot wrapper。它们依赖终结器回收并增加 native churn。barrier 应直接走 typed overload；fixture 应用 CLR DTO 构造 roster，仅在遗留 Godot API 的最窄边界创建 request lease。

## 架构层面的共同根因

### 1. “typed” 不等于“拥有完整状态”

F-01 的 DTO 类型是强类型的，但它只覆盖 aggregate 的三个字段，随后却拥有了完整替换权。typed owner 必须是 total projection；partial view 只能用于只读展示，不能用于 canonical 写回。

### 2. 诊断投影不能复用为事务快照

F-06～F-08 把稳定摘要、差异展示和无损回滚混在同一份手写字段清单中。摘要允许丢字段和近似值；事务恢复不允许。应分成完整 rollback snapshot 与可读 diagnostic projection。

### 3. 接口抽象之后仍要修正依赖获取方向

F-09 已有接口却仍从具体 `BattleRuntimeModule` service locator 获取它。真正的解耦需要由 composition root 显式注入接口和 read model，rules 不能反向知道 runtime owner。

### 4. 生命周期必须覆盖异常和“外部仍持有 child”的情况

F-20、F-21、F-25、F-31、F-32 都不是简单 null-check 缺失，而是 owner/borrower 的结束边界不完整。Dispose 必须使所有拆分 child 失效；临时全局 recorder 和 native wrapper 必须由作用域保证恢复。

### 5. 持久状态只能有一个事实 owner

F-03、F-04、F-13、F-14、F-30 分别展示了死亡、属性、保存提交、商店随机性和坐标表示的双重真相。每类数据需要一个 canonical schema/owner，其他层只消费其 typed snapshot 或 typed result。

### 6. `game_runtime` / settlement / world 的 raw dictionary 仍是高风险交界面

在 `scripts/systems/game_runtime`、`settlement`、`world`、`persistence` 中，`GDictionary` 仍同时承担 request、mutable domain state、UI context 和 save payload。F-01 就发生在 dictionary domain state 与不完整 typed DTO 之间。后续迁移应优先围绕事务 aggregate 和边界 DTO，而不是机械地把每个 dictionary 包一层类型。

## 建议修复顺序

1. **先止损**：F-01、F-03；加能证明完整状态和不变量的失败回归，再修 owner。
2. **恢复可交付性**：F-02；确认 48 个文件归属，用干净 worktree 构建。
3. **统一正式真相**：F-04、F-13、F-30；F-14 已于 2026-07-23 解决。
4. **重建 AI 事务边界**：F-06～F-12；完整 rollback snapshot 与 diagnostic projection 分离。
5. **修正模拟可信度和生命周期**：F-05、F-20～F-22、F-31～F-32。
6. **收紧内容入口**：F-15～F-19；所有回归应从真实 registry/process snapshot 入口进入。
7. **处理潜伏 API**：F-25～F-29；原 F-23/F-24 已按非生产工具范围排除。

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

## 已复核并排除的旧/表面候选

- 新建存档的持久化失败回滚当前已存在于 `GameSession.cs:636-706`，不再沿用旧 review 结论。
- save schema/version 当前由 `SaveSchemaVersions.cs` 提供单一来源；旧的多处常量漂移结论不成立，但该文件尚未跟踪，转化为 F-02。
- `QuestState.last_progress_context` 当前已捕获 `ArgumentException`。
- equipment ability registry 当前先检查 errors，再做 definition projection；旧的投影顺序崩溃结论不成立。
- ground barrier 的 manual/pending/auto-cast/preview 当前共用 clip helper，未确认规则漂移。
- movement query cache 会由 `MovementGeometryRevision` 驱动重建；未确认陈旧缓存。F-12 是另一条“只读查询写 state 且不推进 revision”的确定问题。
- 单纯文件过大、partial 拆分、无 namespace、理论 barrier id 碰撞没有独立计为缺陷。
- `scripts/systems/fate/LowLuckRelicRules.cs` 已纳入逐行覆盖，未发现新的可证实问题。

## 开放设计决策

以下决策会影响修复形态，不应在实现时自行增加兼容逻辑：

1. 装备门槛允许哪些有效属性来源：身份、职业、升华等永久来源应与当前属性真相一致；临时 status 和其他装备是否允许，需要明确。无论选择什么，preview/commit/battle-local 都必须共用同一规则。
2. **已决策（2026-07-23）**：`shop_inventory_seed` 不是确定性契约，已从据点顶层 schema 删除；各商店继续独立使用真随机并持有自己的实际 seed / 刷新步数。
3. Failed quest 是独立持久集合、终止后移除，还是允许继续保留；不能继续放在只接受 Active 的集合中。
4. save payload 已提交、index 失败时，用户可见语义是 partial、recovered 还是 failed-with-orphan。

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
