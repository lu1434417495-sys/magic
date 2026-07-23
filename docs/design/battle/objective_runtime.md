# 战斗目标与终局运行时

> 状态：`Current / Implemented (9/9: elimination + boss + rescue + escape + escort + defense + intercept + node_operation + control)`
> 核对日期：`2026-07-24`

## 当前实现边界

当前代码已经建立统一的战斗目标与终局管线，`elimination`（歼灭）、`boss`（击败首领）、`rescue`（拯救）、`escape`（逃离）、`escort`（护送）、`defense`（防守）、`intercept`（截击）、`node_operation`（节点作业）和 `control`（区域占领）九种模式均具备可创作内容、运行时求值、HUD/快照投影、AI affordance、正式内容和回归。

这一区分仍是硬边界：mode 出现在 enum 中不等于该模式已经可玩。`BattleEncounterContentRegistry` 只接受九种已有完整实现的 objective Resource，包括 `BattleNodeOperationObjectiveDef` 与 `BattleControlObjectiveDef`；其他 objective Resource 必须校验失败。

## 内容所有权

正式链路为：

```text
EncounterAnchorData.encounter_profile_id
  -> BattleEncounterDefinition
    -> roster_profile_id -> WildEncounterRosterDefinition
    -> objective -> BattleObjectiveDefinition
    -> scenario_actors -> BattleScenarioActorDefinition[]
    -> world_resolution -> success / failure / draw policy
```

`BattleEncounterDef` 是遭遇级 authoring owner。敌方编队、胜负目标和战后世界处理在这里汇合；世界锚点不再保存 `enemy_roster_template_id`，运行时也不得从敌人存在与否推断默认歼灭目标。缺失或不存在的 `encounter_profile_id` 必须在内容校验或开战入口失败，不能回退到旧 schema。

当前正式内容：

- `wolf_wilds`：`wolf_pack_skirmish` 编队、歼灭目标、成功后清除。
- `wolf_den`：`wolf_den` 成长编队、歼灭目标、成功后压制 3 世界步。
- `mist_hollow`：`mist_hollow` 编队、歼灭目标、成功后清除。
- `red_dragon_lair`：`red_dragon_lair` 编队、击败 `red_dragon_boss`、成功后清除。
- `mist_hollow_escape`：`mist_hollow` 编队、全体持久队员撤至右侧 `east_escape` 出口、所有结果均保留世界遭遇。
- `mist_hollow_rescue`：`mist_hollow` 编队、在右侧入口区生成 battle-only `captured_scout`，持久队员相邻交互解救、所有结果均保留世界遭遇。
- `mist_hollow_escort`：`mist_hollow` 编队、在左侧 `west_entry` 生成 battle-only `refugee_guide`，保护其自动寻路至右侧 `east_safe_exit`、所有结果均保留世界遭遇。
- `mist_hollow_intercept`：独立 `mist_hollow_intercept` 编队，在每个成长阶段唯一标记敌方 `mist_courier`，要求其抵达左侧 `west_breakthrough` 前将其击败、所有结果均保留世界遭遇。
- `mist_hollow_defense`：`mist_hollow` 编队、在左侧防线生成 battle-only `mist_warden`，保护其存活 200 TU、所有结果均保留世界遭遇。
- `mist_hollow_node_operation`：`mist_hollow` 编队、完成分置左右两侧的 `west_mist_seal` 与 `east_mist_seal` 两个净化作业、所有结果均保留世界遭遇。
- `mist_hollow_control`：`mist_hollow` 编队、争夺左右两侧两个区域并先累计到 100 分、所有结果均保留世界遭遇。

后七项是已进入正式 seed、可显式启动和校验的 canonical 内容，但未加入默认世界随机生成权重；随机投放属于世界内容与平衡决策，不由目标框架擅自改变。

### 首领内容标识

Boss 目标配置 `target_actor_id`，不配置运行时 `unit_id` 或敌人模板 id。`WildEncounterRosterUnitEntryDef.actor_id` 是 roster slot 的稳定实例标识；非空 actor 必须 `count == 1`，并在同一 stage 内唯一。Boss encounter 的目标 actor 必须在该 roster 的每个 stage 恰好出现一次。

`EncounterRosterBuilder` 保持原有 `<anchor>_<ordinal>` unit id 生成规则，同时把 actor 投影到 battle-only `BattleUnitState.encounter_actor_id`。因此 roster 重排不会改变内容引用的目标身份，重复使用同一模板的多个敌人也不会产生歧义。

### 逃离出口

Escape 目标配置稳定 `exit_zone_id`、类型化 `BattleMapEdge`（`Left / Right / Top / Bottom`）和正数 `exit_depth`，不保存依赖具体地图尺寸的绝对坐标。开战后从实际可通行地格解析并冻结排序后的 `ExitCoords`；容量验证复用正式 footprint 几何规则，包括地形可进入性和 footprint 内部的 `blocks_occupancy` 边。覆盖整张地图、空区域，或无法让全部必需单位以完整 footprint 同时、无重叠合法落位的出口会拒绝初始化。

### 截击目标与出口

Intercept 与 Boss 一样通过 roster entry 的稳定 `actor_id` 指定敌方实例；目标必须在编队的每个成长阶段恰好出现一次。它同时配置稳定 `exit_zone_id`、类型化地图边和正数纵深。开战后运行时在敌方 active index 中唯一绑定目标 unit，并从实际地图冻结出口坐标；目标的完整 footprint 必须至少存在一个合法出口落点。Intercept 不创建 scenario actor，也不以 enemy template id 猜测目标。

### 防守目标与时间

Defense 配置友方 scenario actor 的稳定 `target_actor_id` 与相对 `duration_tu`。持续时间必须为正数且是 5 TU 粒度的整数倍；开战后运行时唯一绑定目标 unit，冻结初始持久队员，并以当时 `current_tu` 生成不可变 `StartTu` 和 `DeadlineTu`。当前切片只保护 battle-only 单位，不接受静态节点、区域失守或波次结束作为替代目标。

### 节点作业

NodeOperation 的 `operation_nodes` 可声明任意正数个节点，不表示固定八个节点。每个节点配置稳定 `node_id`、显示名、zone id、类型化地图边与正数纵深；内容加载拒绝空集合、空字段和重复 node id。开战后运行时按 node id 稳定排序，从实际可通行且未被单位占据的候选格中为每个节点冻结唯一坐标。节点是 battle-only objective overlay，不进入单位索引、敌我阵营、战利品、角色写回或世界存档。

只有本场初始化时冻结的持久队员可以对同格或相邻的未完成节点执行 typed `Interact`，每次成功操作消耗 1 AP。全部节点完成即成功；完成前初始持久队员全部阵亡即失败；敌军存活或全灭不参与结算。同一原子变更中最后节点完成与队伍覆灭同时发生时，完成成功优先。当前切片不实现顺序链、搬运物、节点被摧毁、超时或多阶段作业。

### 区域占领

Control 的 `control_zones` 可声明任意正数个区域。每个区域配置稳定 `zone_id`、显示名、类型化地图边与正数纵深；`score_target` 必须为正数且按 5 TU 对齐。开战后从实际可通行格解析并冻结各区域坐标，区域之间不得重叠，并要求每个区域都至少存在一个我方单位和一个敌方单位可用完整 footprint 合法落位的位置。

每个 timeline 原子时间步在推进 `current_tu` 后、状态周期结算前读取所有存活单位的完整 footprint。一个区域只有我方单位时，我方获得 `tu_delta` 分；只有敌方单位时，敌方获得 `tu_delta` 分；双方同时存在为争夺、双方都不在为中立，这两种状态均不计分。多个同阵营独占区按区域数叠加计分。任一方先到目标即结算；同一时间步双方同时到达目标为 Draw；达标前初始持久队伍覆灭为失败。分数达标与队伍覆灭同批发生时先按分数结算，敌军全灭不能替代占领条件。当前切片不实现总时限、到时领先判定、占领衰减、锁定进度或连续控制时长。

`BattleEncounterWorldResolutionDefinition` 分别声明 `PlayerSuccess`、`PlayerFailure`、`Draw` 的 `Preserve / Clear / Suppress` 行为。只要任一分支使用 `Suppress`，`suppression_steps` 必须为正；没有分支使用时必须为 0。

### 场景参与者

`BattleEncounterDef.scenario_actors` 是救援、护送与防守 NPC 的正式 authoring owner。每个 actor 配置稳定 `actor_id`、正式 enemy template、显示名、入口 zone id、类型化边与纵深。内容校验要求 actor id 在 encounter 内唯一、template 存在，并要求 Rescue/Escort/Defense 目标恰好绑定一个 scenario actor；当前其他 objective 不允许声明 scenario actor，避免未定义的单位归属与结算语义。

`EncounterRosterBuilder` 复用 enemy template 的属性、体型、技能、装备和 AI brain 投影创建 battle-only 单位，再强制写入玩家友方阵营、AI control、稳定 `encounter_actor_id` 和空 `source_member_id`。运行时按实际地图解析入口区并放置；scenario actor 不进入玩家队伍成长、装备、资源、死亡或奖励写回，也不生成敌方战利品。

## 运行时所有权

`BattleState` 持有：

- 一个 `BattleObjectiveRuntimeState`，表示本场目标运行态；
- 最多一个不可变 `BattleFinalDecision`；
- `winner_faction_id` 只由 final decision 派生，不是可写真相。

`BattleFinalDecision` 包含 `ObjectiveMode`、`Outcome`、`EndReason`、`DecisionTu`。当前合法终局组合是：

| Mode | Outcome | EndReason | 派生 winner |
|---|---|---|---|
| `elimination` | `PlayerSuccess` | `EliminationHostilesDefeated` | `player` |
| `elimination` | `PlayerFailure` | `EliminationAlliesDefeated` | `hostile` |
| `elimination` | `Draw` | `EliminationMutualDestruction` | `draw` |
| `boss` | `PlayerSuccess` | `BossTargetDefeated` | `player` |
| `boss` | `PlayerFailure` | `BossPartyDefeated` | `hostile` |
| `boss` | `Draw` | `BossMutualDestruction` | `draw` |
| `rescue` | `PlayerSuccess` | `RescueTargetSecured` | `player` |
| `rescue` | `PlayerFailure` | `RescueTargetDefeated` | `hostile` |
| `rescue` | `PlayerFailure` | `RescuePartyDefeated` | `hostile` |
| `escape` | `PlayerSuccess` | `EscapeRequiredUnitsReachedExit` | `player` |
| `escape` | `PlayerFailure` | `EscapeRequiredUnitDefeated` | `hostile` |
| `escort` | `PlayerSuccess` | `EscortTargetReachedExit` | `player` |
| `escort` | `PlayerFailure` | `EscortTargetDefeated` | `hostile` |
| `escort` | `PlayerFailure` | `EscortPartyDefeated` | `hostile` |
| `defense` | `PlayerSuccess` | `DefenseDeadlineReached` | `player` |
| `defense` | `PlayerFailure` | `DefenseTargetDefeated` | `hostile` |
| `defense` | `PlayerFailure` | `DefensePartyDefeated` | `hostile` |
| `intercept` | `PlayerSuccess` | `InterceptTargetDefeated` | `player` |
| `intercept` | `PlayerFailure` | `InterceptTargetEscaped` | `hostile` |
| `intercept` | `PlayerFailure` | `InterceptPartyDefeated` | `hostile` |
| `intercept` | `Draw` | `InterceptMutualDestruction` | `draw` |
| `node_operation` | `PlayerSuccess` | `NodeOperationAllNodesCompleted` | `player` |
| `node_operation` | `PlayerFailure` | `NodeOperationPartyDefeated` | `hostile` |
| `control` | `PlayerSuccess` | `ControlPlayerScoreReached` | `player` |
| `control` | `PlayerFailure` | `ControlHostileScoreReached` | `hostile` |
| `control` | `Draw` | `ControlScoresTied` | `draw` |
| `control` | `PlayerFailure` | `ControlPartyDefeated` | `hostile` |

Boss 初始化在单位放置后解析并冻结目标 unit id，以及本场开始时所有 `source_member_id != ""` 的友方 unit id。杂兵是否存活不影响成功；目标死亡且仍有必需队员存活为成功，目标存活而必需队伍全灭为失败，同一原子变更中双方同时满足死亡条件为 Draw。召唤物和 scenario-only 友军不能替代持久队员维持目标。

Rescue 初始化以 scenario actor id 唯一冻结救援目标 unit 和初始持久队员。只有该集合内仍存活、仍有 AP 的当前行动队员能对相邻目标执行 typed `Interact` 命令；交互消耗 1 AP，并在最外层 mutation flush 将目标标为 secured。目标先死亡或持久队伍在解救前覆灭为失败，目标 secured 且仍存活为成功；敌军是否存活不参与结算。同一原子交互中若目标已 secured、救援队伍随后覆灭，任务成功优先；目标死亡始终优先失败。当前成功条件是“解除被困状态”，不包含救出后再移动到安全区或超时。

Escape 同样在单位放置后冻结初始持久队员集合，召唤物不加入。P1 的完成语义是“所有必需队员同时存活，且各自完整 `occupied_coords` 都位于出口区域”；任一必需队员死亡优先判失败，敌方存活或全灭都不改变逃离结果。P1 不实现逐个离场后继续战斗的 evacuated store，避免把离场单位伪装为死亡或污染 timeline、targeting 和战后写回；该扩展需作为独立后续切片实现。

Escort 初始化冻结 scenario actor unit、初始持久队员和从实际地图解析出的出口格，并验证目标完整 footprint 至少存在一个合法出口落点。内容校验要求入口/出口 zone id 和 map edge 均不同。目标存活且完整进入出口为成功；目标死亡或抵达前持久队伍覆灭为失败；敌军是否存活不参与结算。同一原子变更中若目标已存活抵达、持久队伍同时覆灭，任务成功优先；目标死亡始终优先失败。当前路线是“类型化入口区 → 类型化出口区”的直接正式寻路，阻路时等待、下一行动重新寻路；内容可配置的中途检查点链、跟随对象切换和超时仍未实现。

Defense 初始化冻结 scenario actor unit、初始持久队员、开始 TU 和绝对截止 TU。目标存活且当前 TU 到达截止值为成功；到时前目标死亡或初始持久队伍覆灭为失败；敌军存活数不参与结算，提前全灭也不会跳过剩余时间。同一原子变更中目标死亡始终优先于到时成功；目标仍存活且到时与持久队伍覆灭同时发生时成功优先。当前切片不生成波次，也不把静态 objective node 混入 scenario actor schema。

Intercept 初始化冻结 roster actor 对应的敌方目标、初始持久队员和从实际地图解析出的出口格。目标在抵达前被击败为成功；活着完整进入出口，或目标仍未被阻止时初始持久队伍覆灭为失败；其他护卫敌军是否存活不参与结算。同一原子变更中目标与必需队伍同时死亡为 Draw；目标已死亡时不会再按抵达出口判失败。当前完成语义是“击败逃跑目标”，不包含完成投送动作、超时或伤害阈值等替代截停条件。

NodeOperation 初始化冻结初始持久队员与每个节点的 authored facts/实际坐标。运行时状态拥有逐节点完成位，预览只校验当前行动者、持久队员身份、存活/AP、目标坐标、完成状态和网格距离；正式命令先推进节点状态，再扣除 1 AP，并由最外层 mutation flush 统一求值。UI、AI 和快照只能消费 detached 或 typed 只读事实，不得直接推进节点。

Control 初始化冻结初始持久队员、每个区域的 authored facts/实际坐标、目标分数和双方当前分数。timeline driver 是占领分推进 owner；区域归属由存活单位的完整 footprint 即时求值，不保存第二套可漂移的 owner 字段。UI、AI 和快照只读区域、归属与分数事实，不能直接推进计分。

`BattleResolutionResult` 复制 final decision，并把 `winner_faction_id` 与 `encounter_resolution` 作为只读输出投影。Fate、掉落、任务、世界结算和 BattleSim 的业务判断消费 typed `Outcome`，不能再比较 winner 字符串。

## 原子求值边界

所有可能改变目标事实的根 mutation 必须遵循：

```text
BeginObjectiveMutation
  -> 完整命令 / timeline step / start-confirm reaction / promotion choice
  -> 任意同步递归反应与多目标伤害
EndObjectiveMutation
  -> FlushBattleOutcomeEvaluation
```

嵌套 mutation 只标记 dirty，只有最外层成功结束时求值。因此同一技能让首领和必需队伍同时阵亡时只能得到一次 `BossMutualDestruction`，截击目标与必需队伍同时阵亡时只能得到一次 `InterceptMutualDestruction`；逃离中同一原子变更同时发生“到达出口”和“必需队员死亡”时按失败处理；防守中目标死亡优先于到时，而目标仍存活时到时优先于队伍覆灭；节点作业中最后节点完成优先于队伍同批覆灭；区域占领中双方同刻达标为 Draw，单方达标优先于同批队伍覆灭。mutation 异常时不提交中间终局。

终局决定一旦锁存不可替换，同时冻结 timeline。若 `PromotionChoice` 或 `StartConfirm` modal 仍在处理，系统只锁存决定，等待 modal 结束后由同一 flush 管线完成战斗。`CompleteBattle` 是 phase、奖励、resolution result、batch flags 和终局日志的唯一完成入口；重复 flush 必须幂等。

## 开战和失败策略

`BattleRuntimeModule.StartBattle*` 必须显式接收 `BattleObjectiveDefinition`。`GameRuntimeFacade` 只从 anchor 对应的正式 `BattleEncounterDefinition` 解析该目标。缺少目标时在创建 pending generation request 前立即失败，避免界面永久停留在 BattleLoading 或 battle save lock 无法释放。

目标运行态初始化位于地形格、双方单位和 scenario actor 完成放置之后：Elimination 只需创建空运行态；Boss 必须在敌方 active index 中唯一绑定 actor；Rescue 必须在友方索引中唯一绑定 battle-only scenario actor；Escape 必须从实际地图解析出口并冻结必需队员；Escort 同时绑定 scenario actor、冻结持久队员并验证目标 footprint 可进入出口；Defense 绑定 scenario actor、冻结持久队员和 deadline；Intercept 同时在敌方索引中唯一绑定 roster actor、冻结持久队员并验证目标 footprint 可进入出口；NodeOperation 冻结持久队员并为每个 authored node 绑定唯一、可通行、初始化时未占用的实际格；Control 冻结持久队员、互不重叠的可通行区域与分数目标，并验证双方完整 footprint 都存在合法区域落点。scenario actor 的入口区放置以及 Escape/Escort/Intercept/NodeOperation/Control 的地形绑定都会参加既有 placement/terrain 重试；最终仍无法绑定时返回终端 `invalid_objective_binding`，清除 pending loading 并释放 battle save lock，不能回退到歼灭模式。

只有显式声明 `EmptyGenerationIsPending` 的异步地形生成器，才可在整轮返回空地形后保留 pending 并于后续 frame 重试。每次 placement attempt 都会刷新结构化失败原因，较早的 Escape 绑定失败不能盖住较新的“地形尚未就绪”。普通生成器、布阵或目标绑定在同步重试次数耗尽后都是终端失败，必须清 pending/modal 并释放 battle save lock；若终端失败发生在后续 frame，解锁后还必须把玩家位置与 typed world owner 走 canonical world-sync flush。“内容缺目标”“确定生成失败”和“地形尚未就绪”是三种不同状态。

## 展示、快照与 AI

`BattleObjectiveProgressSnapshot` 是规则状态到 HUD、GameRuntime 和文本快照等通用只读消费者的统一 detached 投影。HUD 显示目标标题和进度；Rescue 提示持久队员移动到目标相邻位置并点击解救；Escape/Escort/Intercept 把冻结的出口格以绿色 marker 绘制在棋盘上；Defense 显示目标状态和剩余 TU；NodeOperation 显示完成数/总数，并只标记未完成节点；Control 显示双方分数、目标分、逐区域归属，并把全部占领格以绿色 marker 绘制。`GameRuntimeSnapshotBuilder` 的 `battle.objective` 与文本快照输出目标 actor、目标存活、secured/到达状态、必需队员、到达数量、出口 id/边/深度/坐标、当前/开始/截止/剩余 TU、逐节点进度、逐占领区事实、双方占领分与敌军存活数。棋盘 marker 与目标专用 AI 可以只读访问 typed objective runtime facts，但任何展示或 AI 消费者都不得改写目标运行态；只有正式 `Interact` 命令可以推进 Rescue 或 NodeOperation 状态，Control 计分只由 timeline driver 推进。

Boss 模式下，受强制目标/嘲讽规则约束后，必需队员的 AI target 排序把正式 Boss unit 放在首位，但保留其他合法目标供首领不可达时使用。Escape 模式下，自动控制的必需队员使用统一寻路选择通往任一合法出口落点的本回合路径，因此允许绕障所需的等距第一步；出口暂时被占据或无路可走时回退常规战斗 AI，完整进入出口后才原地等待其他队员。

Rescue 目标的 scenario actor 在获救前始终等待玩家交互，不会借用模板 brain 主动战斗。Escort 目标始终优先沿正式寻路结果向出口移动；路径暂不可执行时返回专用 wait，下一次行动重新寻路，不会转入普通攻击。Defense 目标固定等待，不借用模板 brain 主动战斗；受强制目标规则约束后，敌方 AI 把该目标排在其他合法玩家单位之前，并保留普通候选作为不可攻击时的回退。Intercept 目标采用同样的出口寻路/等待策略，不借用模板 brain 攻击；受强制目标规则约束后，初始持久队员的 AI 优先攻击该目标，并在其当前不可作为合法目标时保留普通候选。NodeOperation 的初始持久队员 AI 相邻时优先提交可预览的 Interact，否则沿正式路径移向任一未完成节点的同格或相邻 anchor；无路时回退普通战斗 AI。Control AI 优先向敌占、中立、争夺区域寻路；身处争夺区时回退常规战斗，所有区域均为己方且自己已在区域内时原地守点。AI mutation stable projection 覆盖九种 objective runtime state，包括 Rescue 的 mutable secured bit、Escort/Intercept 的冻结出口、Defense 的冻结 TU deadline、NodeOperation 的逐节点坐标/完成位，以及 Control 的冻结区域与双方分数。

## 存档边界

战斗目标运行态、scenario actor、`encounter_actor_id`、Rescue secured bit、冻结出口坐标、Defense deadline、NodeOperation 节点完成位和 Control 区域/分数仍是 battle-only，不写入世界存档。本切片不增加旧 encounter/save schema 兼容、别名、迁移或 fallback，也不为旧 payload 补默认目标；现有旧存档不在支持范围内。

## 回归入口

```bash
dotnet build magic.csproj
godot --headless -s res://tests/battle_runtime/objectives/run_battle_elimination_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_boss_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_escape_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_rescue_escort_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_intercept_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_defense_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_node_operation_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_control_objective_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_objective_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs
godot --headless -s res://tests/battle_runtime/presentation/run_battle_hud_typed_projection_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_wild_encounter_roster_typed_regression.cs
godot --headless -s res://tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs
godot --headless -s res://tests/world_map/runtime/run_game_runtime_pending_battle_request_regression.cs
godot --headless -s res://tests/world_map/runtime/run_wild_encounter_growth_system_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```
