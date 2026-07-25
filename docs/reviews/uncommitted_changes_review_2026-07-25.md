# 未提交变更详细检视（2026-07-25）

> 时间点审查记录。结论可能已被后续提交修复或失效，使用前必须对照当前代码重新验证。

## 范围与方法

- 对象：`git diff HEAD` 全部未提交变更，154 个文件（+8256/−5390）。
- 六大主题：多目标战斗模式（Boss/Rescue/Escape/Escort/Intercept/Defense）、战斗 AI 评估器重写与 mutation guard 重构、装备能力接口解耦（rules 不再引用 concrete runtime）、代码结构迁移（PendingCharacterReward / WorldMapContentValidator / WorldPresetRegistry / BattleAttackRollModifierSpec 搬迁、BattleTimelineStatusBridgeService 删除）、属性/进度规则收敛（AttributeContentRules、随机起始技能法力支持）、新遭遇与技能数据（mist_hollow 系列、red_dragon_lair、basic_meditation）。
- 方法：按主题分簇审查（目标系统 / 战斗 AI / 伤害规则与装备接口 / 结构迁移与 runtime 生命周期 / 属性进度与数据配置），`tools/architecture/` 生成物按审查规范排除。
- 已验证：`dotnet build magic.csproj` 通过（0 警告 0 错误）；四个结构迁移文件经逐行对照确为纯移动，全仓（.cs/.tscn/.tres/.gd/project.godot/.json）无旧路径残留；critical 级发现（AI 评分克隆副作用）已经根代理抽查源码核实。
- 未验证：本环境 headless 回归无法运行（未改动的对照测试也在进程启动/关闭阶段失败，属环境问题），所有行为结论基于静态阅读。

## 发现（按严重度排序）

### Critical

- **[critical] `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs:784-785` — AI 评分热路径对活体单位调用 `clone()`，而 `clone()` 会回写源实例**（已抽查核实）。`EstimateDamageForTargetWithPreviewEffect` 中 `workingSource = sourceUnit?.clone()` / `workingTarget = targetUnit?.clone()` 直接作用于战斗中的活体单位；`BattleUnitState.clone()`（`BattleUnitState.cs:1361-1366`）克隆前先对 `this` 执行 `NormalizeShieldState()`（异常时 `ClearShield()`）、`NormalizeWeaponProjection()`（回写 weapon_current_grip / weapon_uses_two_hands）、`SyncDefaultCombatResourceUnlocks()`（向 unlocked_combat_resource_ids 追加），`GetEquipmentView()` 还会惰性赋值 `equipment_view`。失败模式：①生产默认 guard=Disabled（`BattleAiService.cs:13-14`），未预先归一化的单位（护盾刚过期、武器投影中间态、缺默认资源解锁）在 AI 评分时被**静默改写战斗状态**；②guard 开启时（测试/模拟/tuning）同一副作用被稳定快照捕获为 mutation → 抛 `BattleAiMutationViolationException`，评分变成战斗崩溃源；③`EnsureBodySizeProjectionInvariant()` 会把数据问题升级为评分期异常。新增测试只断言 hp/shield 不变，未覆盖 unlocked ids、武器投影、equipment_view。

### High

- **[high] `scripts/enemies/EnemyTemplateDef.cs:936-948` + `scripts/systems/battle/rules/BattleRangeService.cs:74-78` — 敌人武器投影缺 `weapon_range_type`，与新增 melee 判定冲突**。敌人侧 `_build_weapon_projection_from_item_definition` 不设置 `weapon_range_type`（玩家侧 `CharacterManagementModule.cs:1127` 有），40 个敌人模板运行时该字段全为 `""`。本次 `UnitHasMeleeWeapon` 新增 `WeaponRangeType == "melee"` 要求后，持械敌人全部判为"无近战武器"。失败模式：带 RequiresWeapon / RequiredWeaponFamilies 且有 melee 标签的敌人技能被施放门（`BattleRuntimeSkillTurnResolver.cs:369`）、已知技能过滤（`BattleUnitFactory.cs:839-843`）、可达性判断（`BattleSpawnReachabilityService.cs:800-804`）三处静默剔除。`basic_attack` 只有 `add_weapon_dice` 而无 `requires_weapon`，常规普攻不暴露——任何近战武器技能敌人静默失效。修复方向：敌人投影补 `weapon_range_type = itemDefinition.GetWeaponRangeType()`。

- **[high] `scripts/systems/battle/runtime/BattleRuntimeModule.cs:816-821` — Intercept 绑定失败不参与 terrain/placement 重试**。`InitializeBattleObjective` 失败时仅 Escape/Escort 走 `continue` 重试，Intercept（及 Boss/Rescue/Defense）直接终端失败。但 `TryCreateIntercept` 的 `TryResolveEdgeZoneCoords`（出口 strip 无可通行格）与 `ResolveExitPlacements`（目标 footprint 无合法出口落点）都是地形相关失败，换 seed 可能成功。`docs/design/battle/objective_runtime.md` 明确写 Intercept 出口绑定参加重试。失败模式：玩家触发 mist_hollow_intercept，首个 seed 出口侧地形不通 → 直接"遭遇战生成失败"，而重试本可成功。Boss/Rescue/Defense 绑定失败与地形无关，终端处理合理，问题仅在 Intercept。

- **[high] `docs/design/battle/objective_runtime.md` vs 代码/seed — Defense 上岸状态自相矛盾**。文档声明 P2 范围不含 defense、"仍不能在正式内容中使用"、registry 遇其他 objective Resource "必须校验失败"；但代码完整实现了 Defense（`BattleDefenseObjectiveDef`、registry `case BattleDefenseObjectiveDef` 含 duration 校验、`BattleDefenseObjectiveRuntimeState`、`EvaluateDefense`、HUD 标题"坚守防线"、AI stable projection），seed 包含 `mist_hollow_defense.tres`（duration_tu=200），且存在 `run_battle_defense_objective_regression.cs`（512 行，文档回归清单未列出）。两者必有一错：同步文档（Defense 已上岸），或将 defense 移出 seed 并让 registry 拒绝之。

- **[high] `scripts/systems/battle/rules/BattleDamageResolver.cs:692/745/761/2205`、`Dice.cs:108`、`DamageOutcome.cs:137/407`、`DtoHelpers.cs:159` — 装备能力上下文的 `BattleState` 从保证非空退化为可空**。旧代码所有 query/sink 调用点用 `?? equipmentAbilityService.GetBattleState()` 兜底；新代码直接透传 `damageContext?.BattleState` / `attackContext?.BattleState`。主要伤害入口（orchestrator、chain、charge、ground effect、meteor、barrier、repeat attack、装备 direct-effect/skill-trigger）已补 `WithBattleState(...)`，但 `ApplyDirectDamageToTargetTyped` / `ApplyTaggedDirectDamageToTargetTyped` / `ResolveFallDamageResult` 的 `battleState` 参数默认 null，provider 侧 `ResolveConsumeStatusStacksAction`（`BattleEquipmentAbilityRuntimeService` 约 :649）与 `ResolveApplyStatusTargets`（约 :906）无兜底。失败模式：遗漏/未来调用方传 null 时，范围选择/状态消耗类装备反应静默失效，fact context 的 `current_tu` 退化为 −1，时间线条件求值结果改变。编译期无法发现的隐式契约变化。

- **[high] `scripts/systems/battle/ai/BattleAiService.cs:139-153` + `BattleAiMutationSnapshots.cs:94-119` — mutation guard 删除全部回滚实现**。`ValidateAndRestore` 改名 `Validate` 并删除约 600 行 Restore 实现（SnapshotState.Restore、BattleStateFieldsSnapshot.Restore、BattleUnitSnapshot.Restore、barrier 相关）。违规即抛异常，被污染的 BattleState 原样留在 runtime。默认路径（`BattleRuntimeModule.cs:1015` 无 catch）战斗中止尚可接受；但任何 guard-enabled 且 catch 后继续的 harness（战斗模拟、GPU tuning 自进化循环、headless 会话）将在污染状态上继续推进且无任何恢复兜底。新测试已把"不回滚、fixture 直接废弃"固化为预期，需确认所有 guard-enabled 消费方满足"异常即废弃整场战斗"前提。

- **[high] 五个 AI evaluator 统一改为"按目标循环、首个有决策目标即返回"——选择语义与候选预算语义同时改变**（`BattleAiMoveToAdvantageActionEvaluator.cs:52,134`、`BattleAiMoveToRangeActionEvaluator.cs:119,251`、`BattleAiGroundRepositionActionEvaluator.cs:42`、`BattleAiRetreatActionEvaluator.cs:80`、`BattleAiChargeActionEvaluator.cs:114`）。旧逻辑只评估 `targets[0]`（selector 最高优先目标），不可达则整个 action 放弃；新逻辑取第一个能产生任意合法决策的目标，跨目标不再比较评分。叠加 objective 目标强插队首（`BattleAiTypedActionHelper.cs:200-204`），"objective 目标的任意可行动作"永远赢过"非 objective 目标的最优动作"。另外 `MoveToAdvantage` 的 `action.CandidateLimit` 按目标重置（evaluationCount 循环内归零），N 个目标最多评估 N×CandidateLimit 个候选，上限语义被悄悄放大；survival 模式 `already_safe` 从整动作短路改为按目标跳过，改变了旧回归的守护边界。

- **[high] 性能：目标循环 × 每目标全量重算，AI 决策成本随敌人数线性（部分平方）放大**。①`MoveToAdvantage`：fast candidates 不可用时每个目标做一次 map_size 全图扫描+排序（旧代码一次）；②`MoveToRange`：`CollectReachableMoveCandidates`（BFS）与 `BuildPathProgressDecision` 按目标重复；③`GroundReposition`：技能条目解析与地面坐标集枚举（重型）按目标重算无缓存；④`BattleAiObjectiveActionEvaluator.BuildExitMoveDecision` → `BattleAiQueryService.GetCurrentTurnPathToBestAnchor`（:286-356）：撤离区每个锚点跑一次完整 A*，16 格宽×深 2 的撤离区 = 32 次寻路/单位/回合；⑤评分侧每 (技能, 目标) 深克隆两个完整单位 + 每 damage effect 跑正式 `PreviewDamageEffectTyped`，取代旧纯算术估算。`run_battle_ai_performance_baseline` 只改了一行方法名，未见基线随新成本模型重校准。

### Medium

- **[medium] `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs:362-369、491-501、534/586/619`；`scripts/systems/battle/rules/BattleRangeService.cs:210-239、97` — "解耦"变更中混入实质玩法规则改动**。(a) 新增 `RequiresCurrentWeapon → UnitHasEquippedWeapon` 门，非 melee 标签的 weapon-required 技能从"必须近战武器"放宽为"任意已装备武器"（弓可满足）；(b) 三处武器 family 检查从 `UnitHasMeleeWeapon` 放宽为 `UnitHasEquippedWeapon`；(c) `RequiresCurrentMeleeWeapon` 改名为 `RequiresCurrentWeapon`，新义为旧义 + melee 标签。方向合理（修正"弓被当近战武器"旧缺陷），但属行为变更，应单独验证并在提交说明中显式列出；与上条 high 叠加后，敌人侧实际是"收紧 + 数据缺失"而非放宽。

- **[medium] `scripts/systems/battle/ai/BattleAiDecisionEngine.cs:73-78` — objective 决策绕过 brain 全链路**。`_objective.Evaluate` 在 brain 解析之前短路返回：`decision.brain_id/state_id` 为空，无 PrepareDecision、无状态转移、不经 `BattleAiSafetyGate.IsEligible`、不与任何战斗动作评分竞争。后果：①escort/intercept 目标"路线受阻即 wait"（fallbackToCombat:false），被贴身卡住时**永远不反击**，intercept 玩法可能因此无威胁；②escape 目标进撤离区后 hold，即使身边有可击杀敌人；③下游 `AI[{brain_id}/{state_id}/{action_id}]` 日志对空 id 的处理依赖隐式约定。若为设计意图（VIP 只跑不打），需文档显式声明。

- **[medium] `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs:951-958` — 护盾扣减口径未验证，存在双重扣减风险**。`ApplyBranchLethalEstimate` 用 `DamageOnSaveFailure − targetShieldBefore` 判致死；若 `PreviewDamageEffectTyped` 产出的 `DamageOnSaveFailure` 已是护盾后 HP 伤害（preview 自身有 ShieldAbsorbed 记账），则护盾被减两次 → 带盾目标致死率被系统性低估，AI 放弃实际可行的击杀。审查中断前未能确认口径，需核实。

- **[medium] `scripts/systems/battle/runtime/BattleRuntimeModule.cs:1645-1658、1661-1664、2823-2826` — 服务 getter 从懒装配改为未装配即抛**。`GetAttackCheckPolicyService()` / `GetEquipmentAbilityRuntimeService()` 未装配抛 `InvalidOperationException`；`ConfigureHitResolverForTests` 从 null 安全改为无条件 `BindEquipmentRulePorts()`（`_damage_resolver == null` 时抛）。多处调用点写 `runtime?.GetAttackCheckPolicyService()`（`BattleGroundEffectService.cs:903`、`BattleChargeResolver.cs:966`、`BattleSkillExecutionOrchestrator.cs:1792`、`DefinitionGates.cs:103`、`BattleRepeatAttackResolver.cs:748`），只防 runtime 为 null，防不住抛异常。失败模式：未经完整装配的 partial-setup 测试夹具/工具代码从"正常工作"变崩溃。

- **[medium] `scripts/systems/battle/runtime/BattleSkillMasteryService.cs:316` — 删除 `if (result.Damage <= 0) return null;` 守卫**。0 伤害命中（如全减免）现在会消耗/触发 guard reaction，改变战斗日志与 reaction 次数。出现在声明"零行为变化"的文件中，大概率属于 warrior_guard 数值簇（`data/configs/skills/warrior_guard.tres` 同期改动），需确认归属并在提交说明中声明。

- **[medium] `scripts/systems/battle/content/BattleEncounterContentRegistry.cs` — 出口边与出生边无冲突校验，战斗可在首次 flush 被即时判决**。内容校验只对 escort 验证了 actor 入口/出口 zone（`ValidateEscortRoute`）；没有任何校验防止：(a) escape 的 `exit_edge` 与己方出生侧相同 → 全体必需队员出生即落在出口区 → 第一次 `FlushBattleOutcomeEvaluation` 直接判胜；(b) intercept 的 `exit_edge` 与敌方出生侧相同 → 信使出生即在出口 → 首次 flush 直接判负。该即时终局路径已被测试（`TestEscortArrivalSucceedsWhileEnemiesSurvive`）证实是活行为；`mist_hollow_escape`（exit_edge=Right）与 `mist_hollow_intercept`（exit_edge=Left）的安全性完全取决于地形生成器逐 seed 的出生侧分配。

- **[medium] `scripts/systems/battle/core/BattleUnitState.cs:65、1664、1937` — `encounter_actor_id` 加入严格精确字段集，旧战斗内存档将被静默拒绝**。`FromDictionary` 先过 `HasExactFields(payload, ToDictFields)`，任何变更前保存的战斗内 unit payload 因字段集不匹配直接返回 null，失败形态是"字段不匹配"而非"版本不兼容"，排查困难。本次 diff 未见 save/index 版本号提升。save-format 破坏性变更，提交说明需显式声明版本策略。

- **[medium] `scripts/player/progression/PartyState.cs:282-297` — 任务状态路由收紧，Failed/未知状态静默丢弃**。旧逻辑把非 Completed/Rewarded 的全部放入 active_quests；新逻辑只接受 Active/Completed/Rewarded（有注释说明理由：避免 strict save loading 拒绝）。含 failed 任务的旧存档加载后该任务无声消失，属有意设计但需在提交说明中声明。

- **[medium] `scripts/systems/battle/ai/BattleAiService.cs:118-137` — 评估异常 + 同时检测到 mutation 时原始异常被包装**。旧代码评估抛异常原样上抛；新代码若 guard 同时发现状态 diff，原始异常降级为 MutationViolationException 的 inner，改变上游按异常类型分类处理的可见性。且该路径在状态可能半毁时再跑一次完整 `CaptureStable`（`ValidateReportTyped` 无 try 保护），捕获自身再抛会掩盖原始异常。

### Low

- **[low] `scripts/systems/battle/runtime/BattleRuntimeModule.Objectives.cs:88-92`** — 失败的 mutation scope 在 depth 0 无条件 `_objectiveEvaluationDirty = false`，可能吞掉 scope 外（`HandleObjectiveInteraction`、`_record_unit_defeated`）设置的待评估标记 → 异常路径下终局不评估，战斗卡在不结束状态。建议 failed 收尾时保留进入 scope 前的 dirty 值。
- **[low] `scripts/systems/battle/runtime/BattleRuntimeModule.cs:1461`** — `_ensure_sidecars_ready()` 新增 `ObjectDisposedException.ThrowIf(_disposed, this)`：Dispose 后一帧内经 bridge/metrics/movement borrower 的迟滞 UI 调用从无害 no-op 变异常。
- **[low] `scripts/systems/game_runtime/BattleSessionFacade.cs:504` vs `GameRuntimeFacade.BattleResolution.cs:77,111`** — 同步失败路径（`HandleBattleStartFailure`）不做 world-sync flush，延迟失败路径（`HandleDeferredBattleStartFailure`）做；同一终端失败的持久化语义不一致，若刻意设计建议加注释。
- **[low] `scripts/ui/BattleBoardController.cs:282,573,664,920,935,1999`** — 删除全部 UI 侧 `RefreshFootprint()` 兜底，信任 runtime setter 路径新鲜；若存在直接写字段绕过 setter 的路径，token 位置/深度排序/选中判定将用过期 footprint 渲染。
- **[low] `scripts/enemies/EnemyTemplateDef.cs:638-650`** — `skill_level_map` 的 String 畸形键检测分支是死代码（Godot 4 中 String/StringName 键被视为相等，`ContainsKey(skillId)` 永远先命中）。
- **[low] `scripts/enemies/EnemyTemplateDef.cs:590-599`** — brain 动作引用模板未声明技能时兼容性校验整体跳过（`!declaredSkillIds.Contains(skillId)` 直接 continue），悬空引用运行时静默无候选，无 schema 报错。
- **[low] `scripts/enemies/definitions/EnemyAiActionSkillCompatibilityRules.cs:40、64-74`** — `meteor_swarm` 特判是硬编码字符串 profile id；建议把"需要 ground 坐标"提升为 profile 定义上的 typed 标记。
- **[low] `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs:570-586`** — `ResolveSkillCommandEntryLevel` 每次 `new BattleSkillAvailabilityService(...)`，热路径逐次分配（功能正确，preview/执行一致性反而改善）。
- **[low] `scripts/systems/battle/core/BattleObjectiveProgressSnapshot.cs:91-99`** — `ExitEdgeWireValue` 与 `BattleObjectiveRuntimeCodec.ToWireValue(BattleMapEdge)` 逐字重复，两处映射将来可能漂移。
- **[low] `scripts/systems/battle/runtime/BattleRuntimeModule.cs:809-814`** — escape/escort 绑定重试分支的 failure snapshot 漏填 `PlacementAttempts`，终端失败诊断信息不一致。
- **[low] `scripts/systems/progression/QuestProgressService.cs:167`** — `RecordProgress` 现在返回 `CompleteQuest(...)` 的结果：进度已记录但任务已 claimable/completed 时返回 false，调用方可能误读为"进度未记录"。
- **[low] 仓库卫生** — `.tmp_battle_sim/` 未被 .gitignore 覆盖且未跟踪，属瞬态工作区状态，提交前注意排除。
- **[low] `scripts/systems/content/world/WorldPresetRegistry.cs:5`** — "纯移动"基本属实但非严格逐行：新增一行 owner 注释、尾部多空行、编码 UTF-16→UTF-8（后者是改进）。无行为影响，仅记账。
- **[low] `BattleRuntimeModule.cs` 中 `InitializeBattleObjective` 失败原因从 `unsupported_objective_definition` 改为 `invalid_objective_binding`** — 仓库内消费者已同步，若有外部日志分析消费旧字符串会断。

## 开放问题 / 假设

1. **Defense 是否本切片意图上岸？**（最重要待澄清项）若是，`objective_runtime.md` 边界声明、registry 白名单描述与回归清单必须同步；若非，`mist_hollow_defense.tres` 应移出 seed 且 registry 拒绝 `BattleDefenseObjectiveDef`。
2. 敌人 `weapon_range_type` 缺口是遗漏还是有意——当前正确性取决于"敌人技能恰好不含 RequiresWeapon"这一巧合；若有意需注释与测试固化。
3. guard 不回滚的"异常即废弃"语义是否被所有 sim/tuning/headless harness 满足（已确认 `BattleRuntimeModule.cs:1015` 无 catch，未向上追完整个调用栈）。
4. evaluator "首个可行目标胜出" + objective 强插队首是否为设计意图；若意图是"objective 仅同等条件优先"，当前实现是过度优先。
5. 战斗中是否完全不存在存档入口（决定 objective runtime state 无 save 序列化路径是否安全）。
6. `DamageOnSaveFailure/Success` 的护盾口径（见 medium）；`PreviewDamageEffectTyped` 返回的 `SourcePreviewAfter/TargetPreviewAfter` 是否为 preview 内部新克隆；`clone()` 深拷贝边界（status effects/attribute_snapshot）未逐字段核实。
7. escort/intercept "受阻即 wait 不反击"是否为玩法设计意图。
8. `basic_meditation` 的 `learn_source = &"player"` 意味着玩家可手动学习——自动授予之外的暴露是否有意（tres 文件已验证为正常 UTF-8，无编码问题）。
9. Defense 终局顺序（同 TU 时 deadline 优先于队伍覆灭判胜、目标死亡优先于 deadline 判负）已被测试固化，但 `objective_runtime.md` 未记录 defense 语义矩阵，无法对照确认。
10. `_sourceEventOrdinal` 从 module 字段迁入 bridge 实例字段：bridge 生命周期与 module 一致，假定 event id 不要求跨 module 实例全局唯一（旧实现也不保证）。

## 残余风险 / 测试缺口

- **本环境 headless 回归无法执行**（未改动对照测试亦在进程启动/关闭阶段失败，属环境问题）。建议在有环境的机器上至少补跑：`run_battle_runtime_borrower_teardown`、`run_battle_projection_lease`、`run_battle_map_panel_schema`、`run_battle_unit_state_schema_contract`、5 个新 objective runner（含 defense）、`run_battle_ai_objective_behavior`、`run_enemy_ai_action_skill_compatibility`、`run_game_session_random_start_skill`。
- 缺"敌人持近战武器 + melee 标签武器技能"回归用例（现有武器能力回归全是玩家侧）——敌人 `weapon_range_type` 缺口无任何测试会暴露。
- 缺"AI 评分不得触碰活体单位任何字段"的全字段不变量测试（现只断言 hp/shield）；缺 guard-enabled 下完整 `ChooseCommand` + 新 preview 评分路径的零违规端到端用例。
- 缺：Rescue 同原子批 secured + 队伍覆灭判胜（文档承诺的优先级）、Intercept 绑定失败跨 terrain seed 重试、registry 拒绝 elimination/boss/escape/intercept + scenario_actors 组合、scenario actor id 与 roster actor id 跨阵营同名的运行时解析唯一性、escape 出生侧==出口边的即时判胜防护。
- 缺 AI 性能基线随"目标数 × 每目标重算"和 preview 克隆成本的重校准；多敌人（6v12）+ 多撤离区锚点场景的决策耗时无针对性断言。
- `run_battle_ai_objective_behavior_regression.cs`（801 行）与 `run_enemy_ai_action_skill_compatibility_regression.cs` 只核对了测试名清单，未逐条核对断言强度；旧 mutation guard 测试（4366 行）与新（3930 行）按测试名 1:1 比对过，但未逐个 diff 断言体——旧断言中"恢复后状态逐字段相等"类守护已随回滚删除而消失，等价弱化断言未逐一核实。
- `run_battle_weapon_dice`(+114)、`run_warrior_repeat_attack_mastery_bonus`(+156)、各武器能力回归的断言变化未逐条核对是否掩盖回退（parity 测试已核，改动为签名适配 + 新增探针注入断言，原断言保留，未见掩盖）。
- 未深审：`BattleCommandPreviewService` 的 Interact/objective 预览分支、`BattleRuntimeSkillTurnResolver` 的 `InitializeAppliedStatusTimelineTicks`/`NormalizeAppliedStatusIds`、`BattleUnitState` 的 footprint 不变量收紧（`EnsureBodySizeProjectionInvariant` 抛异常会把既往宽松路径的数据问题升级为运行时异常）、`BattleHudAdapter`/`BattleHudSnapshot` 进度投影与出口 marker 渲染、`HeadlessGameTestSession.cs:493-522` 的 mutation scope 使用、`GameRuntimeBattleLootCommitService` 对新 outcome 的消费路径。
- 多 scenario actor 共用同一入口 zone 无内容级容量校验（配置错误晚发现，运行时靠放置失败 → 重试 → 终端兜底）。
- `WeakReference<IBattleContingencyRuntimePort>` 静默为 null 时 auto-cast 静默丢失，无告警路径；建议补运行期断言或日志。
- `tools/architecture/` 的 layer_rules/baseline 变更已确认与新目录结构同步，无残余风险。
- 文本快照新增 `objective_progress` 行：仓库内基线（golden hash、snapshot builder 回归）已同步更新，假定不存在仓库外消费者。

## 总结

编译通过、结构迁移质量高（四处搬迁均验证为纯移动且引用收敛完整）、新 objective 体系设计与测试配套较完整。但存在 1 个 critical（AI 评分 `clone()` 副作用，已核实）与 7 个 high（敌人 melee 数据缺口、Intercept 重试缺失、Defense 文档矛盾、BattleState 可空契约、guard 无回滚、AI 目标选择语义变化、AI 性能放大）。建议提交前至少处理 critical 与敌人 `weapon_range_type` 缺口，澄清 Defense 上岸状态，并在提交说明中显式列出：`RequiresCurrentWeapon` 语义变化、`BattleSkillMasteryService` 0 伤害守卫删除、`BattleUnitState` save schema 破坏、PartyState failed 任务丢弃策略。
