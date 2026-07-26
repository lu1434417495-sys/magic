# 战斗运行时模块可重建规格说明

> 状态：`Current / Implemented`
> 核对日期：`2026-07-26`

更新日期：`2026-07-26`

## 目标与边界

本文描述 `BattleRuntimeModule`、`BattleSessionFacade`、战斗 state、命令、时间线、单位工厂、移动、技能、AI、结算与世界回写的可重建规格。目标是代码丢失时能重建世界遭遇进入战斗、玩家/AI 行动、战斗结束写回的功能主线。

不覆盖：具体每个技能数值、每个 AI profile 的调参、战斗 UI 美术。

## 模块拓扑

```text
GameRuntimeFacade / BattleSessionFacade
  -> GameRuntimeBattleSelection -> IGameRuntimeBattleSelectionPort
  -> BattleRuntimeModule
    -> BattleState / BattleUnitState / BattleTimelineState
    -> BattleObjectiveEvaluationService / BattleFinalDecision
    -> BattleUnitFactory / BattleMovementService / BattleMovementQueryService
    -> BattleSkillExecutionOrchestrator / BattleDamageResolver / BattleHitResolver
    -> BattleAiService / BattleAiDecisionEngine
    -> BattleRuntimeLootResolver / BattleSkillMasteryService
  -> BattleMapPanel / BattleBoardController / BattleHudAdapter
  -> GameRuntimeBattleWritebackService
```

`BattleSessionFacade` 是世界 runtime 到 battle runtime 的窄门面；它只弱借 `IGameRuntimeBattleSessionPort`，通过该端口提交开战、推进、preview/command、presentation delta、promotion prompt 与终局结算意图，不持有 `GameRuntimeFacade`、`BattleRuntimeModule`、`BattleGridService`、`PartyState` 或 `GameContentCatalog`。selection 只通过 `IBattleSelectionSessionSurface` 暴露会话编排所需能力，profession 也只按 id 借用 definition，不暴露完整 catalog。`RuntimeCommandResult` / `RuntimeCommandCode` 是独立 runtime command contract，不再由 facade 类型嵌套拥有。dispose 只断开弱端口；battle/session/grid/selection owner 仍由 facade 递归释放，开战到结算的同步顺序不变。`BattleRuntimeModule` 拥有战斗规则与 state；UI 只展示 state 和发送 command。`GameRuntimeBattleSelection` 只弱借 `IGameRuntimeBattleSelectionPort`，通过该端口借用 typed battle/grid/catalog facts、读写 facade-owned selection state，并提交 preview/command/status 意图；它不持有 `GameRuntimeFacade` 或 `BattleRuntimeModule`。技能定义必须经 `ISkillCatalog` 查询，选择命令返回 selection 域自己的 typed 结果。hover/选择预览仍只走正式 `PreviewCommand`，真正执行仍走 `IssueBattleCommand` 并由 battle runtime 重新校验，不能把 preview 结果当作提交授权。facade dispose 时 selection sidecar 清空隐式 cast-variant cache 并断开弱端口，不拥有或释放 battle state、grid、catalog。

module 主文件第一批拆出 spawn 放置（`BattleSpawnPlacementService`）、特殊技能门禁与状态写入（`BattleSpecialSkillGateService`）、移动与强制位移命令（`BattleMovementCommandService`）、metrics/报告/effect origin（`BattleMetricsReportService`），第二批拆出 AI 决策绑定（`BattleAiDecisionBindingService`）、contingency 桥（`BattleContingencyBridgeService`）与只读命令 preview/entry 校验（`BattleCommandPreviewService`）。七个 module-owned service 都只弱借用 module，并由 owner-local `BattleRuntimeModuleBorrowerSet` 作为构造和 `FinishSetup` 的单一有序接线源；原 `BattleTimelineStatusBridgeService` 经 owner 复核确认没有独立状态或 capability，已于 `2026-07-24` 删除。装备规则端口由 module 的 `BindEquipmentRulePorts()` 集中装配：`BattleEquipmentAttackModifierResolver` 提供 attack-check/damage 两个只读 query，`BattleEquipmentAbilityRuntimeService` 提供同步 reaction sink，policy/damage resolver 不再反向定位 module；service getter 只返回已装配实例，不执行 `Setup`。timeline phase、current TU、`tu_per_tick`、ready unit、action-threshold 校验/日志、stamina 与 Control objective 分数推进归 `BattleTimelineDriver`；unit action progress、threshold 与 action-rate remainder 的唯一存储及本地累计/crossing 不变量归 `BattleUnitActionClockState`；单次 activation 的行动/移动/锁定移动点授权/施法耗尽事实归 `BattleUnitTurnState`，timeline 在 activation start 重置四项、在 activation end 只清施法耗尽；cooldown map 与 last-turn TU anchor 的唯一存储及本地推进不变量归 `BattleUnitCooldownState`，`BattleRuntimeSkillTurnResolver` 负责基于 current TU 的惰性消费、5 TU 粒度诊断、静滞 anchor 平移、turn timer、状态周期 tick/duration/turn-start 规则、新状态的 `next_tick_at_tu` 初始化与护盾 duration 调度，护盾实际递减/到期清空归 `BattleUnitShieldState`；module 只在 `MarkAppliedStatusesForTurnTiming(...)` 中保持“先初始化 tick anchor，再通知 Fate”的跨 owner 编排。`BattleAiDecisionBindingService` 私有持有 per-unit action-plan index，module 只保留单项借用查询与生命周期编排窄入口。涉及 AI plan 的 rebind/teardown 先关闭 decision context/helper consumer，再清空并释放 action plan，之后才逆序断开 borrower set 和释放底层 sidecar。装备技能 usage 与 granted-skill reaction 的真实提交仍归 `BattleSkillExecutionOrchestrator`，preview service 不执行提交副作用；兄弟服务/测试需要的 internal 入口由 module 保留窄委托。`BattleContingencySystem` 只弱借用由 bridge 实现的 `IBattleContingencyRuntimePort`，不再经 module 反向转发 auto-cast；回合开始的 contingency 与 sequential auto-cast 编排仍归 module/timeline owner，metrics service 只记录指标。`BattleGroundEffectService` 同样拆出风力推移/位移（`BattleGroundRelocationService`）、地面技能校验（`BattleGroundSkillValidationService`）、坐标构建与效果收集（`BattleGroundEffectCoordService`），主 service 在 `Setup` 正序接线，并在 `Dispose` 逆序断开三个 child 的 runtime/owner/sibling borrower。

`BattleTerrainEffectSystem` 不再弱借 concrete module，而是只借 `IBattleTerrainEffectRuntime` 所需的 state/grid/damage、增量、状态时钟、击倒和贡献能力；Fate guidance 只借 `IMisfortuneGuidanceBattleQuery` 的 calamity snapshot/reason。`BattleRuntimeModule.DomainPorts.cs` 集中实现这两个端口，domain owner 保持原同步调用栈和原 teardown 时机。

## Setup 输入

BattleRuntime setup 必须注入：CharacterManagementModule、typed skill defs、enemy templates、enemy AI brains、EncounterRosterBuilder、EquipmentDropService、typed item defs、equipment instance id allocator、battle special profile registry snapshot、skill catalog typed view。世界 runtime 还必须从 `GameContentCatalog` 建立正式 `BattleEncounterDefinition` 索引，供 roster、objective 与战后世界处理共同解析。

不要从 GameRuntimeFacade 或 UI dictionary 临时扫描技能/敌人内容；内容读取走 GameContentCatalog typed snapshot。

## BattleState 契约

BattleState 至少包含：battle id、map size、terrain/cells、units dictionary、timeline、round/tick、objective runtime、不可变 final decision、loot/contribution、modal state、overlay data。`winner_faction_id` 只由 final decision 派生，不是可写状态。BattleUnitState 包含 unit id、faction、coord、footprint、resources、attributes、known skills、equipment/weapon projection、status effects、AI brain/template id。

单位局部几何的 anchor、body size/category、footprint 与 occupied coords 由 plain C# `BattleUnitGeometryState` 作为唯一原子存储；五项不能拆成两个 owner，因为 occupied 同时依赖 anchor 与 body。`BattleUnitState` 只暴露 typed setter、不可变零复制 read view、detached copy 与 mutation-exact gateway，生产读路径不得 refresh 或修复。normal anchor/body 写保持 row-major 占用顺序并在投影已一致时不替换内部列表；admission 校验 category/size 身份后可刷新派生 footprint/occupied，clone、canonical 与 strict load 对缺失 owner 或不一致正式投影 fail-fast。mutation exact 保留 owner presence、raw null/empty、非法值、重复项及顺序；69-key canonical codec 与 AI stable projection 继续使用原五个 flat key、原顺序和原类型。cell occupant 拓扑、落点合法性、事务提交和 movement geometry revision 仍归 `BattleGridService` / `BattleState`，geometry setter 本身不推进全局 revision。

known active skill ids、skill level map 与 lock-hit bonus map 由 plain C# `BattleUnitKnownSkillState` 作为唯一存储；三组件彼此独立，不能根据 active ids 同步裁剪 map。`BattleUnitState` 只保留 typed gateway：active 热路径读取使用不可变、零副本的 struct view，主技能显式取有序列表首项，level missing 与显式 `0` 继续区分。canonical、detached 与 AI mutation snapshot 从借用只读组合视图直接填充最终容器；只有显式 mutation-exact capture/restore seam 创建拥有型 raw 副本。normal 写、strict codec 与 mutation exact 保持各自原有规则；gameplay clone、69-key canonical codec、detached trace 和 AI snapshot 继续投影原有字段与范围，不公开 owner 内部可变容器。mutation stable projection 复用旧三个 key，并以内部 diagnostic sentinel 区分 owner 缺失和 owner 存在但三个组件均为 null。

战斗内已消耗的 contingency setup id 由 `BattleConsumedContingencySetupCollection` 作为唯一有序、去重存储；`BattleUnitState` 只保留 Mark/Has/Get/Replace typed gateway。该集合随 gameplay clone 深拷贝并进入 AI mutation stable projection，但仍是 runtime-only overlay，不进入 BattleUnitState 的 canonical codec 或 detached trace snapshot。

战斗内的 per-battle charge、per-turn charge、per-turn limit 与 fumble protection counter 由 plain C# `BattleUnitChargeState` 作为唯一存储；`BattleUnitState` 保留 Get/Has/Set/Remove/Reset typed gateway，`TraitTriggerHooks` 与其他 runtime 不直接持有内部 map。per-turn reset 在 owner 内原子执行“清 current，再按正 limit 回填”；missing key 与值为 `0` 仍是不同状态。四组 map 随 gameplay clone 深拷贝并继续以原四个 stable key 进入 AI mutation exact diagnostic，但不进入 69-key canonical codec 或 detached trace snapshot。

护盾的 current/max HP、duration、family 与两个 source id 由 plain C# `BattleUnitShieldState` 作为唯一存储；`BattleUnitState` 只提供 typed snapshot、六字段原子替换、duration 推进、扣减、清理与归一化 gateway，同族刷新和异族替换策略仍归 `BattleShieldService`。普通读取返回 raw 值且不隐式归一化；canonical codec 与 detached snapshot 保留原先会先归一化 live unit 的语义，AI mutation exact diagnostic 则保留负数、over-max 以及 nullable `StringName` 的原始差异。六个 canonical flat key、字段顺序、类型及六个 mutation stable key 均保持不变，gameplay clone 仍先归一化原 unit 后深拷贝 owner。`shield_duration` 使用战场 TU：非静滞单位在 timeline status phase 由 `BattleRuntimeSkillTurnResolver` 推进，到期时 owner 原子清空六字段；step 开始时处于 `time_stasis` 的单位在整个 step 内冻结护盾 duration，即使静滞在该 step 末自然到期，也从下一 step 才恢复推进。

冷却 map 与 `last_turn_tu` anchor 由 plain C# `BattleUnitCooldownState` 作为唯一存储；`BattleUnitState` 只保留单项/批量写、detached snapshot、anchor 和推进 gateway。typed 单项写以非正值删除，批量写只保留非空 `StringName` key 的正值；canonical codec/load、detached trace、gameplay clone 与 AI exact capture 不经过该归一化入口，继续保留既有 raw 0/负值与 key 身份。gameplay clone 对非 null map 深拷贝并把非法 null map 归一为空；AI exact 仍区分 null 与 present-empty。69-key codec 的 `cooldowns`、`last_turn_tu` flat key、相对顺序和类型，以及 AI stable key 均不变。anchor 未初始化值仍为 `-1`；owner 原子执行“按 current TU 重基准 → 检查 5 TU 粒度 → 合法时推进 cooldown”，resolver 只记录非法粒度并决定 changed-unit；静滞只平移已初始化 anchor，保留静滞前 backlog，anchor-only 变化不单独提交 changed-unit。

单次 activation 的 `has_taken_action_this_turn`、`has_moved_this_turn`、`can_use_locked_move_points_this_turn` 与 casting-exhausted 状态由 plain C# `BattleUnitTurnState` 作为唯一存储；`BattleUnitState` 只暴露语义读取、标记、授权、activation reset 与 casting clear gateway。普通移动锁定仍等价于“已行动或已移动”，特殊技能授予的 locked move-point 权限不自行创建或消耗移动点。activation start 原子清除四项；activation end 只清 casting-exhausted，前三项保留到下一次 activation。gameplay clone 深拷贝四项，AI mutation exact 继续使用原四个 stable key并区分 owner 丢失；69-key canonical codec、detached trace 与 load 仍只包含原有的 `has_taken_action_this_turn`、`can_use_locked_move_points_this_turn` 两项，另两项保持 runtime-only。action progress/threshold、temporal remainder、casting remainder 与 resting 状态不属于该 owner。

`is_resting` 由 plain C# `BattleUnitRestState` 单独持有，因为它跨 activation 保留，不能随 turn flags 在 activation start 清除。`BattleTimelineDriver` 仍在活跃单位未行动的回合结束时标记 resting；实际行动通过 `BattleUnitState.CommitActionTakenThisTurnTyped()` 原子写入 turn owner 并清除 rest owner，`BattleMetricsCollector` 的非 wait、正 AP 行动与 `BattleCastingTimeService` 成功建立 pending cast 的提交点共同复用该网关。读条启动仍以真实 `apCost = 0` 记录 metrics，不以伪造成本驱动玩法状态。Timeline 读取 resting fact 计算 stamina 恢复倍率，资源 owner 只推进 stamina 与恢复余数。gameplay clone 深拷贝 rest owner；69-key canonical codec、detached snapshot 与 AI stable projection 继续使用原 `is_resting` flat key、相对顺序和 bool 类型，mutation exact 通过同一 key 保留 owner-presence 诊断。

地形移动能力的有序 `movement_tags` 由 plain C# `BattleUnitMovementTagState` 单独持有；它不拥有 move points、unit geometry、地形规则或 movement-query cache，也不与生命周期不同的 vision/proficiency/save tags 聚合。normal replace/add 过滤空值、首次去重并保序，factory/simulation caller 保留既有 Variant 转换；grid、charge 与 movement query 只借用专用 struct read view，fly/amphibious/wade 的通行与成本语义仍归 `BattleTerrainRules`。strict codec、gameplay clone、canonical/detached 与 mutation exact 分别保留既有校验、null 归一、flat array 和 raw 诊断语义，原 `movement_tags` key、位置与类型不变。当前生产写入都发生在单位加入 `BattleState` 前；未来的入场后动态标签必须经 state-level gateway 写入并推进 movement geometry revision，避免 query snapshot/cache 失效遗漏。

race/subrace 投影出的有序 `vision_tags` 与 `proficiency_tags` 由 plain C# `BattleUnitVisionProficiencyState` 共同持有。二者同源、同刷新 envelope，但在 owner 内保持两个独立组件；`RaceTraitResolver` 先按 race → subrace 顺序完成两组临时归一化，再一次替换 owner，升格抑制路径同时清空两组。normal 写过滤空值、首次去重并保序，生产代码只通过 `HasVisionTag(...)`、`HasProficiencyTag(...)`、组合只读 view 与 typed 写入口访问。save tags、damage resistance、save bonus 和 effective trait 仍由各自现有投影链负责，不属于该 owner。strict codec 继续分别校验原两个数组；gameplay clone 分别深拷贝并把 null 归一为空；mutation exact 保留 owner presence 及两组件各自的 null/empty、raw 空项、重复项和顺序。69-key canonical codec、detached snapshot 与 AI stable projection 的两个旧 flat key、相对顺序和数组类型不变。

生物分类的有序 `creature_type_tags` 由 plain C# `BattleUnitCreatureTypeState` 单独持有；它是已 materialize 到 battle unit 的 runtime fact，不与 movement、save 或 effective-trait 投影合并，也不在规则执行时回查 enemy template 或 progression catalog。normal replace/add 过滤空值、首次去重并保序，生产消费者只通过只读 view、`HasCreatureTypeTag(...)` 与 typed 写入口访问。strict codec 仍拒绝空项、重复项和错误类型，gameplay clone 仍将 null 归一为空并隔离集合引用；mutation exact 单独保留 owner presence、null/empty、raw 空项、重复项和顺序。69-key canonical codec、detached snapshot 与 AI stable projection 的 `creature_type_tags` flat key、相对顺序和数组类型不变。

unit 的 `action_progress`、`action_threshold` 与 runtime-only `action_progress_rate_remainder` 由 plain C# `BattleUnitActionClockState` 作为唯一存储；`BattleUnitState` 只保留 raw get/set、rate gain、threshold crossing、clone 与 exact gateway。`BattleTemporalStatusService` 决定 action progress rate，owner 保留余数并计算整数 gain；`BattleTimelineDriver` 决定静滞/读条跳过，归一并记录非法 threshold，随后让 owner 扣除所有 crossing，ready id 仍最多加入一次。零/负 rate 不消费 remainder，正 rate 才将负 raw remainder 按 `0` 参与计算；raw load/clone/exact 不归一 progress、threshold 或 remainder。69-key canonical codec 与 detached trace 的 `action_progress`、`action_threshold` flat key、顺序和类型不变，余数不进入 payload 且 load 后为 `0`；AI exact 继续使用原三个 stable key并区分 owner 丢失。`cast_progress_rate_remainder` 仍留在 unit-level casting 生命周期，不属于 action-clock owner。

known-skill 的 active ids、level map 与 lock-hit bonus map 由 plain C# `BattleUnitKnownSkillState` 作为唯一存储。active ids 保持首次插入顺序，首项仍是 main-skill 语义；level map 的 missing key 与显式 `0` 不合并，三个组件也不互相推导。AI/HUD/availability 等高频读路径消费 struct readonly view 或单项 gateway，唯一需要枚举 level map 的 AI cache-key 构建直接把 owner entries 复制进已有排序列表，不先创建 detached dictionary。canonical/detached 与 AI mutation snapshot 通过借用只读组合视图直接生成最终投影，避免先 capture raw 再二次复制；AI unit snapshot 仍只包含 active 与 level，显式 mutation-exact capture/restore seam 才创建拥有型 raw 副本。mutation stable projection 覆盖三组件独立 null/raw 状态和 owner presence，owner 缺失使用内部 diagnostic sentinel，保持旧三个 stable key 不变。

current hp/mp/stamina/aura/ap/move-points、hp 绑定的 alive fact 与 stamina recovery progress 由 plain C# `BattleUnitCombatResourceState` 作为唯一原子存储。`BattleUnitState` 保留单值读写、组合只读 view、damage/heal/dead/revive、caps、cost/refund、stamina recovery 与 mutation-exact gateway；Timeline 仍决定 TU tick、上限、constitution/装备加成及 resting 倍率，owner 只原子推进 stamina 与余数。默认态刻意保持 `hp = 0 / alive = true / move-points = 2`；normal HP 写同步 alive，但 strict codec、clone、detached snapshot 与 AI exact 不修正负 raw 值或 hp/alive 不一致，strict 仍只额外拒绝负 move-points。`is_resting` 由独立 `BattleUnitRestState` 提供，只作为恢复规则输入。69-key codec 与 AI stable projection 的旧八个 key 名称、原相对位置和类型均不变。

combat resource capability set 由独立 plain C# `BattleUnitCombatResourceUnlockState` 作为唯一存储，不与 current resource owner 合并。normal replace 保留有效 id 的首现顺序、过滤空值/未知值/重复项，并在末尾补齐缺失的 `hp`、`stamina`；canonical/detached 与 gameplay clone 继续先同步默认 capability，strict codec 继续拒绝缺默认项、非法项及重复项。生产消费者使用单项 gateway 或零副本 struct view，显式 mutation-exact seam 才深复制 raw list，并保留 null、原始顺序、重复/非法诊断值及 owner presence。69-key codec 的 `unlocked_combat_resource_ids` flat key、相对顺序和类型以及 AI mutation stable key 保持不变。

武器 profile/item/type/range/family、grip/attack range、单手/双手骰、versatile/双手使用与物理伤害标签由 plain C# `BattleUnitWeaponProjectionState` 作为唯一原子存储；`BattleUnitState` 只保留 apply/clear、不可变 read view、detached dice copy、canonical normalize 和 mutation-exact gateway。normal apply 会复制 mutable `WeaponDice` 并按既有 profile、grip、射程与双手规则规范化；canonical codec、detached snapshot 与 gameplay clone 继续先规范化 live unit。strict codec 只校验旧 schema 后安装 raw values，因此负射程或跨字段不一致仍保留到 canonical 边界；AI exact 额外区分 owner missing、nullable `StringName`、null dice 与 present-empty/invalid raw dice。69-key codec 的十二个 weapon flat key及 AI 的十二个 stable key均保持名称、顺序与类型不变，生产读取不再暴露 owner 内部 mutable dice。

## 战斗启动流程

1. World encounter anchor 命中后，GameRuntimeFacade 设置 battle save lock 并保存 world/player。
2. BattleSessionFacade 构建 battle start context；GameRuntimeFacade 以 anchor 的 `encounter_profile_id` 解析正式 `BattleEncounterDefinition`，取得 roster 与 objective。缺失 objective 时立即失败，不创建永久 pending 请求。
3. BattleRuntimeModule 以显式 `BattleObjectiveDefinition` 生成 BattleState：先生成地形并放置双方单位，再从 encounter 的 `scenario_actors` 构建 battle-only 友方 NPC、按类型化入口区放置，随后绑定依赖实际 actor/地图的 objective runtime，最后初始化 timeline 与 selection；绑定失败不得回退为歼灭。
4. 进入 BattleLoading modal；生成完成后进入 BattleStartConfirm，timeline frozen。
5. 玩家确认后清 pending prompt、unfreeze timeline，战斗 tick 开始推进。

确定失败时必须清 active battle id/name、pending generation、modal、battle state，并释放 save lock。只有地形生成器显式声明空结果代表异步 pending 时，空地形才可跨 frame 重试。

`BattleRuntimeModule.StartBattle*` 的成功判据不是“返回值非空”，而是返回的 state 与 `GetState()` 为同一引用。单位非法、普通地形/布阵尝试耗尽或出生不可达且所有重试均失败时，module 会清空 runtime-owned state，并通过 `BattleStartFailureSnapshot` 保留结构化原因；每次 placement attempt 必须覆盖上一次的瞬态原因，最终快照反映最新尝试。GameRuntime 将确定原因视为终端失败，不能保留永久 BattleLoading；只有显式 opt-in 的异步生成器可返回 `terrain_generation_pending`。后续 frame 才确定失败时，GameRuntime 在解锁后还必须执行 canonical world-sync flush。若前一轮出生可达性失败、后续轮次成功，成功返回前必须清空这份瞬态失败快照。模拟执行循环在进入推进前强制检查 state 引用身份；失败启动直接归类为 `invalid_runtime`，不得经过 idle guard 或污染 stalled 统计。

BattleSim 的内部开战单位通过一次性 `BattleStartUnitRoster` 移交：普通 scenario 每局创建 fresh ally/enemy `BattleUnitState`，runner 的借用 context 不再包含 `battle_party` / `enemy_units`；formal fixture 则由同一个 `BattleSimFormalRuntimeStartInput` 绑定 caller-owned context lease 与 fresh enemy-only roster，敌方不再经过 canonical codec，友方仍由 character gateway 构造。module 直接消费列表和 unit graph。typed overload 要求非空 roster；空阵营列表仍是显式输入并走既有单位合法性失败，同一阵营同时出现在 typed roster 与 context key 中会在启动前失败，避免双重真相。legacy 只传 context 的 overload 保持原路径。

地形启动链使用 `BattleTerrainLayout` 作为一次性 managed owner。生成器在 typed cells 上完成水域、坡度、边、prop、出生点和质量评分，不建立 `cells` / `cell_columns` Godot payload；factory 只应用 typed 出生点覆盖。runtime 确认 profile 有效后调用 `TakeCells()` 把 surface cell graph 移交给 `BattleState`，由 state 一次重建 cell columns；重试失败或异常时，尚未移交的 layout 负责释放 cell graph。固定 terrain seed 只复现地形，不得与命中、伤害、豁免或随机目标的 combat RNG 合并。

## 战斗目标与终局

当前正式内容支持歼灭、击败首领、拯救、逃离、护送、防守、截击、节点作业和区域占领九种目标。稳定 id、运行规则与原子结算边界见 [objective_runtime.md](./objective_runtime.md)；尚未实现的组合目标和模式扩展见 [multi_objective_modes.md](../../proposals/battle/multi_objective_modes.md)，不得当成当前可玩内容。

命令、timeline step、开战 reaction 与 promotion choice 都是 objective mutation 根。同步递归反应或多目标结算期间只标记 objective dirty，最外层 `EndObjectiveMutation` 才执行 `FlushBattleOutcomeEvaluation`。final decision 一旦锁存不可替换；存在 promotion/start-confirm modal 时先冻结 timeline，等 modal 完成后再从唯一 `CompleteBattle` 入口生成 result、奖励、phase/batch 和终局日志。`BattleResolutionResult`、Fate、掉落、任务、世界回写与 BattleSim 消费 typed `Outcome/EndReason`；winner 字符串只保留为输出投影。

## 命令模型

BattleCommand 应包含 actor/unit、command type、move target、skill id/variant、target coords/unit ids、facing/metadata。正式命令入口：

- select skill / cycle variant / clear skill。
- select target coord/unit。
- move direction / move to coord。
- wait or resolve。
- inspect cell。
- issue skill command。

命令必须返回 `BattleRefreshMode`：full、overlay 或 none；proxy 把 none direct command 提升为 full 以保证 UI 刷新。

## 时间线与 TU

BattleTimelineState 拥有单位行动顺序、frozen、当前 actor、tick 推进。确认开始前 frozen；确认后 TU 每秒按固定速率推进。等待、移动、施法、持续效果 tick 都必须通过 timeline driver 更新，不能由 UI 改 tick。timeline 负责调用 `BattleUnitTurnState` 的 activation 生命周期入口，但行动/移动/授权/施法耗尽事实由各自完成业务提交的 runtime service 标记，不能由 UI 或 snapshot 投影反向写入。

## 移动与地形

MovementQuery/Service 负责：边界、footprint、占用、terrain cost、reachability、path costs、previous map。大单位 footprint 必须整体合法。地形/prop 注入来自 battle terrain generator / profile，不在 WorldMapSystem 里写死。

## 技能执行

技能执行主线：

1. 校验 actor、skill known、cooldown/resource/cast condition、目标数量与 target team。
2. 收集目标：coord target、unit target、area/line/cone/random chain 等由 TargetCollectionService 计算。
3. 预览：hit/crit/save/damage range/status/ground effect。
4. 执行：扣资源、命中判定、伤害/治疗/状态/位移/护盾/屏障/地面效果。
5. 提交 outcome：写 BattleState、event batch、contribution、mastery。

特殊技能 resolver（meteor swarm、charge、barrier、shield、special profile）由 runtime service 负责，不放 UI。

`BattleDamageResolver` 主文件已按内聚拆分：装备耐久结算在新类 `BattleEquipmentDurabilityResolver`（主类保留 `SelectEquipmentForDurabilityDamage` / `ApplyEquipmentDurabilityDamageToSelection` internal 委托），伤害结果结算 / 伤害预览 / 豁免分支结算分别为主题 partial `BattleDamageResolver.DamageOutcome.cs` / `.Preview.cs` / `.SaveBranch.cs`（同类内聚，零接线变化）。装备耐久归零后的投影刷新由 `BattleDamageResolver` 在完整 `ResolveEffects(...)` / `ResolveAttackEffects(...)` 结束后统一触发，并委托 `BattleEquipmentAbilityRuntimeService.RefreshEquipmentProjectionAfterDurabilityDestruction(...)` 重建装备来源、清理失效目标标记和传播 changed unit id。直接装备耐久 action 在 commit 返回 destroyed 后调用同一 helper；`BattleSkillExecutionOrchestrator._apply_equipment_durability_result(...)` 只负责日志与 changed unit report。

## AI

BattleAiService 读取 BattleAiBrainDef、单位 snapshot、技能 affordance、位置评分，输出 BattleAiDecision。AI 执行必须通过与玩家相同的 BattleCommand/ActionPlan 提交通道，受 mutation guard 约束；AI 不应直接修改 BattleState 绕过规则。

## 战利品、成长与回写

战斗结束后：

- BattleRuntimeLootResolver 根据 defeated enemy template/drop table 生成 item/equipment loot，装备实例走 allocator。
- BattleSkillMasteryService/ContributionLedger 生成 mastery/progression delta。
- GameRuntimeBattleWritebackService 把战斗内角色状态回写到 party；GameRuntimeBattleLootCommitService 负责合并并提交战利品，成长、任务/成就事件继续由各自 progression owner 处理。
- 世界侧根据 typed outcome 选择 encounter 的 success/failure/draw resolution：preserve、clear 或 suppress。
- 清 battle save lock，恢复世界 UI。

`GameRuntimeBattleLootCommitService` 只弱借 `IGameRuntimeBattleLootCommitPort`。掉落合并、普通战灾厄碎片章节上限、固定物品/随机装备/装备实例分派、非致命坏条目丢弃和致命装备随机服务缺失判定归 service；facade port 才能访问 party、warehouse service、session item definitions 与 equipment drop service。每条掉落前捕获 opaque checkpoint，非致命失败只恢复该条；整批开始时另有 checkpoint，致命失败恢复全部已提交掉落和 fate flags。战斗终局外围 `RuntimeTransaction` 仍负责后续角色回写、world resolution、session staging 与 flush 失败时的完整 finalization rollback。

## UI 边界

BattleMapPanel/BattleBoardController 负责棋盘、HUD、hover preview、技能槽、command dock。它们可请求 preview，但不能改 BattleState。所有按钮信号经 WorldMapSystem/RuntimeProxy/BattleSessionFacade。

`BattleMapPanel` 在进入首帧 reveal 等待前，已把同步借用的 `BattleState` 投影为 detached `BattleHudSnapshot` 与 immutable `BattleBoardRenderSnapshot`；pending payload 只保存这两类展示快照和选择参数。`BattleHudAdapter` 只借 `IBattleHudContext`，不持有 runtime/session 根；facade adapter 只提供 snapshot 构建所需的 battle state、content、member facts、cast gating 与 preview 能力，session-only 测试 adapter 不获得 mutation 能力。`BattleBoard2D` / `BattleBoardController` 不借用 live battle graph，单位局部刷新通过 `BattleBoardUnitUpdateSnapshot` 合并到当前 board snapshot，地形与 edge facts 不被重建。`HideBattle()` 和 `_ExitTree()` 清空 pending/hover 展示状态；离树时只使异步 reveal ticket 失效并归零本地 loading progress，不再向正在退出的 UI 树发布 loading signal。

## 回归入口

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_world_map_battle_start_confirm_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_elimination_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_boss_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_escape_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_rescue_escort_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_intercept_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_defense_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_node_operation_objective_regression.cs
godot --headless -s res://tests/battle_runtime/objectives/run_battle_control_objective_regression.cs
python tests/run_regression_suite.py
```

battle simulation/balance runner 不属于常规全量回归，除非明确做数值/AI 平衡分析。

## 安全约束

- 不把 battle rule 写进 WorldMapSystem 或 UI。
- 不绕过 BattleSessionFacade 直接从 UI 改 BattleRuntimeModule state。
- 不从 dictionary fallback 恢复 typed skill/enemy/profile 内容。
- save lock 和战斗失败释放路径必须保留。

## 实现级补充：BattleRuntimeModule 内部服务

BattleRuntimeModule 重建时建议拆分以下 sidecar：

- `BattleUnitFactory`：party/enemy -> BattleUnitState。
- `BattleMovementQueryService` / `BattleMovementService`：reachable/path/move commit。
- `BattleTargetCollectionService`：根据 skill target config 收集 coord/unit。
- `BattleSkillAvailabilityService`：caller-scoped 的共享技能入口规则；preview 在当前时点校验 entry identity/selectability，execution 独立解析 entry level，不由 module 缓存，也不把 execution 反向转发给 preview owner。
- `BattleSkillExecutionOrchestrator`：技能执行主编排。
- `BattleSkillPreviewService` / `BattleSkillTargetValidationService` / `BattleChainDamageService` / `BattleRandomChainSkillService`：预览、目标校验、链式伤害与随机链子职责。
- `BattleDamageResolver` / `BattleHitResolver` / `BattleSaveResolver`：命中、豁免、伤害。
- `BattleTimelineDriver`：timeline phase、current TU、`tu_per_tick`、ready actor、action threshold、stamina、Control objective 分数与 turn start/end。
- `BattleRuntimeSkillTurnResolver`：基于 `BattleUnitCooldownState` 的 cooldown 惰性消费、5 TU 粒度/静滞/turn-start 编排、turn timer、状态周期 tick/duration/turn-start 规则、护盾 duration 调度，以及新应用状态的 `next_tick_at_tu` 初始化；cooldown map/anchor 存储归 `BattleUnitCooldownState`，护盾实际递减和到期清空归 `BattleUnitShieldState`。
- `BattleAiService`：AI decision -> command。
- `BattleRuntimeLootResolver`、`BattleSkillMasteryService`、`BattleContributionLedger`：结算。
- `BattleSpawnPlacementService`：spawn side、footprint、可达性与失败回滚。
- `BattleSpecialSkillGateService`：特殊技能门禁、状态写入与 resolver 桥接。
- `BattleMovementCommandService`：移动命令、路径成本与强制位移桥接。
- `BattleMetricsReportService`：指标、报告与 effect-origin scope；不拥有回合推进编排。
- `BattleAiDecisionBindingService`：私有持有 per-unit AI action-plan index，并拥有 plan build/ensure/query/clear、decision context/helper、评分输入与移动查询接线；module 不暴露其可变集合。
- `BattleContingencyBridgeService`：contingency hook、auto-cast、release queue、overlay 与 consumed 写回桥接，并实现 `IBattleContingencyRuntimePort`。
- `BattleCommandPreviewService`：只读 command preview、skill entry 校验与 issue blocking；不提交装备技能 usage 或 reaction。

这些 helper 优先保持 plain C# typed surface；Godot payload 只在最外层 UI/headless adapter 投影。由 module 持有且需要反向访问 module 的 service 使用弱 borrower；七个直属 split service 只登记在 owner-local `BattleRuntimeModuleBorrowerSet`，由同一拓扑负责初次绑定、重复 setup 与逆序 teardown。`BattleContingencySystem` 的端口绑定同样是弱引用，module teardown 先清除 system 的 capability，再断开 bridge 的 module borrower。AI callback consumer 先退出，随后断开该 set，最后释放它们依赖的 runtime sidecar。`BattleSkillExecutionOrchestrator` 与 `BattleGroundEffectService` 各自管理直属 child，并在 parent teardown 时清空 runtime、owner 与 sibling borrower。

## 实现级补充：BattleState 不变量

- unit id 唯一，units dictionary key 与 `BattleUnitState.unit_id` 一致。
- 每个 alive unit 的 footprint 坐标都在 map 内且不与其他 alive unit 重叠。
- timeline 中的 unit id 必须存在；死亡/移除单位要从可行动队列排除。
- battle ended 后不接受普通命令，只允许 resolve/writeback/inspect snapshot。
- modal state 为 StartConfirm/Loading 时 timeline frozen。

## 实现级补充：单位工厂

玩家单位来自 PartyState/CharacterManagement：属性快照、known skill map、profession/race traits、equipment/weapon projection。敌方单位来自 EnemyTemplateDef、EnemyAiBrainDef、WildEncounterRosterDef。单位工厂必须：

1. 分配 unit id。
2. 设置 faction、coord、footprint。
3. 初始化 hp/stamina/mp/aura 等 combat resources。
4. 注入 known skills 和 skill levels。
5. 注入 AI brain/template id。
6. 应用 battle-start passives/traits。

## 实现级补充：命令校验顺序

执行命令前统一校验：

1. battle active 且未 ended。
2. actor 存在、alive、当前可行动或命令允许非当前 actor。
3. command type 合法。
4. 移动/技能/等待各自校验。
5. 预览和执行使用同一目标收集逻辑，避免 preview/commit 不一致。

失败返回 status + refresh mode，不部分修改 BattleState。

## 实现级补充：技能 cost transaction

技能执行应先构建 `SkillCostTransaction`：资源、冷却、材料/弹药、cast time。需要读条的技能创建 `BattlePendingCastState`，不立即结算最终效果；instant 技能扣 cost 后立即 resolve。cost 扣除失败不得产生 event batch。

## 实现级补充：pending cast

pending cast 是 battle runtime-only state，不进入 save payload。timeline tick 推进读条；manual cancel 走 typed command path；stasis/slow 等 temporal 状态会影响读条推进率。读条完成后由 `BattleRuntimeSkillTurnResolver` 继续执行原技能。

## 实现级补充：目标与 area

TargetCollectionService 应支持：

- 单格、单位目标、多格目标。
- area pattern：diamond、square、line、cone、自定义 footprint。
- target team filter：self/ally/enemy/all。
- random chain candidate。
- ground skill preview unit ids。

收集结果保留 typed `List<Vector2I>` / `List<StringName>`，最外层再投影。

## 实现级补充：event batch

每次命令执行产生 `BattleEventBatch`：changed unit ids、changed coords、log lines、report entries、progression deltas。UI 根据 batch 决定刷新棋盘和 HUD；AI trace/headless snapshot也从 batch/trace DTO 投影。不要让 UI 解析伤害 resolver 内部对象。

## 实现级补充：AI 安全

AI 决策必须使用 snapshot/value object：

- ScoreInput 不持有 live BattleState/UnitState。
- command preview 不应 mutate state；`FullSnapshotDiagnostic` mutation guard 以同一组 typed snapshot 执行 capture、stable projection 与 restore，发现差异后先恢复再上报。
- guard 的单位权威面包含装备视图初始化标记、contingency 消耗、装备能力来源、时间进度修正、creature tags 与完整武器投影；effective trait/roll、pending cast、equipment entry/instance 等嵌套 owner 使用 mutation 专用 exact 深拷贝，不经过业务层的规范化 `DuplicateState`。状态效果 fingerprint 读取全部公共属性，强制位移免疫、debuff 判定和各类 lock 均在其中。
- 战斗级 objective runtime/final decision、target marks、temporary-edge state、cast allocator 和 temporary-edge allocator 都进入快照；objective runtime 按具体 subtype 显式投影，未知 subtype 失败关闭。最终裁决完整覆盖 objective mode、outcome、end reason 与 decision TU，派生 winner 不作为独立可写状态。原始集合按原顺序比较和恢复，重复项、非法哨兵、`null`、空集合及集合中的 `null` 元素不视为等价，也不在 rollback 时调用 gameplay cleanup。
- cell/column、terrain effect、attack-roll modifier 与 layered barrier 也属于 guard 的权威面。容器 canonical key 和对象内 id/coord 分开保存；blackboard 的 raw 数值与 presence flag 分开保存；rollback 调用 owner 的 mutation-exact seam，不通过会过滤无效值、夹断资源、规范化 body 或重算 attribute modifier 的 gameplay API。
- stable plain payload 同时编码 CLR/Godot 类型和完整分量，浮点按位精确比较；`null StringName`、空 `StringName`、数值类型相同文本以及不同 Godot struct 分量都不视为等价。
- skill definition 与 barrier profile definition index 对 evaluator 都只暴露 `ReadOnlyDictionary`；context 防御性复制调用方字典，guard 保存 canonical key、成员和 definition 引用 identity。作为 skill identity 校验成立的前提，`SkillDefinition`、`CombatSkillDefinition`、cast variant、effect、damage segment、target multiplier 与 contingency definition 的集合构造输入均防御性冻结；effect 的 mutable accuracy modifier 只返回 clone。
- 结构回归枚举 `BattleUnitState`、cell、terrain、barrier、blackboard 与嵌套 owner 的字段/属性，并以非默认及非法 raw 哨兵逐路径校验检测和 exact restore；私有 target mark、temporary-edge state 与两个 allocator 由独立行为回归覆盖。
- AI 输出 command 后通过正式 command committer 执行。
- AI failure policy 处理无动作、非法动作、fallback wait。

## 实现级补充：战斗结束与回写事务

1. BattleRuntime 在最外层 objective mutation flush 锁存 typed final decision 并完成 ended；winner 只是派生输出。
2. ResolveActiveBattle 收集 loot/mastery/contribution。
3. Writeback service 先 preview party/warehouse capacity。
4. 提交 party state、warehouse、quest/achievement progress。
5. 更新世界 encounter anchor。
6. 清 active battle state、selection、pending prompts、battle save lock。
7. 保存 GameSession；失败时保持 save lock 或给出明确错误，避免世界状态与战斗奖励半写。

## 实现级补充：回归映射

| 行为 | 回归 |
|---|---|
| world battle confirm/loading | `run_world_map_battle_start_confirm_regression.cs` / `run_world_map_battle_loading_overlay_regression.cs` |
| strict typed battle helper boundary | `run_world_map_low_level_defensive_regression.cs` |
| move path DTO | `tests/battle_runtime/runtime/run_battle_move_path_result_projection_regression.cs` |
| ground effect typed sets | `tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs` |
| magic backlash | `tests/battle_runtime/skills/run_magic_backlash_regression.cs` |
| shield service typed context | `tests/battle_runtime/runtime/run_battle_shield_service_typed_context_regression.cs` |
| battle runtime borrower-first teardown | `tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs` |

## 源码级重建清单：战斗运行时文件与 surface

以下清单用于弥补纯设计文档遗漏：重建时必须逐项恢复这些 owner 文件、公开/内部 typed surface 与职责边界；若实现拆文件，仍要保留等价 API 与行为。

### `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

- `internal static class BattleRuntimeDictionaryOptions`
- `internal static bool ReadBool(GDictionary source, string key, bool fallback = false)`
- `internal BattleEndOptions(bool commitProgression = false)`
- `public sealed class BattleStartFailureSnapshot`
- `internal static BattleStartFailureSnapshot FromDictionary(GDictionary source)`
- `public bool IsEmpty`
- `public partial class BattleRuntimeModule : RefCounted`
- `public BattleRuntimeModule()`
- `internal void _setup_special_profile_runtime()`
- `internal BattleStartFailureSnapshot GetLastStartFailureSnapshot() =>`
- `internal bool _validate_battle_units_for_start(GArray units, string side_label) =>`
- `internal void _build_ai_action_plans()`
- `internal void _ensure_ai_action_plan_for_unit(BattleUnitState unit_state)`
- `internal void _bind_ai_helper_services_for_decision(BattleUnitState unit_state, BattleAiContext ai_context)`
- `internal BattleAiContext _prepare_ai_context_for_decision(BattleUnitState activeUnit)`
- `internal int _get_ai_move_query_cost(StringName unit_id, Vector2I _from_coord, Vector2I to_coord)`
- `internal void _prepare_ai_turn(BattleUnitState unit_state)` / `internal void _cleanup_ai_turn(BattleUnitState unit_state)`
- `public BattleEventBatch advance(int tick_count)`
- `internal BattleContingencySystem GetContingencySystemTyped()`
- `internal StringName AllocateContingencySourceEventId(StringName prefix)`
- `internal void EmitContingencyHpAndStatusHooks(...)` / `EmitContingencySpellAffected(...)` / `EmitContingencyPositionChanged(...)`
- `internal IReadOnlyList<ContingencyTargetResolutionResult> ResolveContingencyStoredSpellTargetsForRelease(...)`
- `internal void OnBattleConfirmed(BattleEventBatch batch = null)` / `OnOwnerTurnStarted(...)`
- `internal void MarkAppliedStatusesForTurnTiming(...)`（三个 typed overload）
- `public BattlePreview PreviewCommand(BattleCommand command)`
- `internal bool CommitEquipmentSkillUsageIfNeeded(...)`
- `public BattleEventBatch IssueCommand(BattleCommand command)`
- `internal void _append_batch_logs_to_state(BattleEventBatch batch) =>`
- `internal void _append_result_report_entry(BattleEventBatch batch, GDictionary result)`
- `internal void _append_report_entry_to_batch(BattleEventBatch batch, GDictionary report_entry)`
- `internal void _keep_promotion_choice_modal_open(BattleEventBatch batch, string message = "")`
- `public BattleState GetState() => _state;`
- `internal IReadOnlyDictionary<StringName, int> GetCalamityByMemberIdSnapshot() =>`
- `internal int GetMemberCalamity(StringName member_id) =>`
- `internal int GetMemberCalamityCap(StringName member_id) =>`
- `internal int GetBlackStarBrandCastCost(StringName member_id) =>`
- `internal bool HasMisfortuneReason(StringName member_id, StringName reason_id) =>`
- `internal FateRuntimeModule GetFateRuntime() => _fate_runtime;`
- `internal BattleSkillCastBlockReasonKind GetSkillCastBlockReason(BattleUnitState active_unit, SkillDefinition skillDefinition) =>`
- `public bool IsUnitGuardLocked(BattleUnitState unit_state) =>`
- `public bool IsUnitCounterattackLocked(BattleUnitState unit_state) =>`
- `public bool IsUnitFollowUpLocked(BattleUnitState unit_state) =>`
- `internal void _ensure_sidecars_ready()`
- `internal WarehouseState _get_party_backpack_state(PartyState party_state)`
- `public bool IsBattleActive() =>`
- `internal IReadOnlyList<Vector2I> GetUnitReachableMoveCoordsTyped(BattleUnitState unit_state)`
- `internal void EndBattle(BattleEndOptions options)`
- `internal BattleResolutionResult GetBattleResolutionResult()`
- `internal BattleResolutionResult ConsumeBattleResolutionResult()`
- `public BattleGridService GetGridService() => _grid_service;`
- `internal IBattleRuntimeCharacterGateway GetCharacterGatewayTyped() => _characterGateway;`
- `public StringName AllocateEquipmentInstanceId()`
- `public BattleDamageResolver GetDamageResolver() => _damage_resolver;`
- `public void ConfigureDamageResolverForTests(BattleDamageResolver damage_resolver)`
- `internal BattleFateEventBus GetFateEventBus() =>`
- `public BattleHitResolver GetHitResolver() => _hit_resolver;`
- `internal BattleAttackCheckPolicyService GetAttackCheckPolicyService()`
- `internal BattleEquipmentAbilityRuntimeService GetEquipmentAbilityRuntimeService()`
- `public void ConfigureHitResolverForTests(BattleHitResolver hit_resolver)`
- `internal BattleTerrainGenerator GetTerrainGenerator() => _terrain_generator;`
- `internal SkillDefinition GetSkillDefinitionTyped(StringName skill_id)`
- `internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionIndexTyped() => _skillDefinitionIndex;`
- `internal IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplateIndexTyped() =>`
- `internal EnemyTemplateDef GetEnemyTemplateTyped(StringName templateId)`
- `internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainIndexTyped() =>`
- `internal IReadOnlyDictionary<StringName, ItemDef> GetItemDefIndexTyped() => _itemDefIndex;`
- `internal Dictionary<StringName, ItemDef> BuildItemDefIndexSnapshotTyped()`
- `internal int GetMinBattleSurfaceHeight() => MIN_BATTLE_SURFACE_HEIGHT;`
- `internal Dictionary<StringName, BattleRatingMemberStats> GetBattleRatingStatsTyped() =>`
- `internal GDictionary get_battle_rating_stats()`
- `internal BattleRatingSystem GetBattleRatingSystem() => _battle_rating_system;`
- `internal Godot.Collections.Array<PendingCharacterReward> get_pending_post_battle_character_rewards() =>`
- `internal void SetAiTraceEnabled(bool enabled)`
- `internal Godot.Collections.Array<GDictionary> GetAiTurnTraces()`
- `internal IReadOnlyList<BattleAiTurnTraceProjection> GetAiTurnTracesTyped() => _ai_turn_traces;`
- `internal void ClearAiTurnTraces() => _ai_turn_traces.Clear();`
- `internal string _format_ai_trace_coord(Vector2I coord) => $"({coord.X}, {coord.Y})";`
- `internal BattleMetricsState GetBattleMetricsTyped() => _battle_metrics ?? new BattleMetricsState();`
- `internal void SetAiScoreProfile(BattleAiScoreProfile profile) =>`
- `internal BattleAiScoreProfile GetAiScoreProfile() => _ai_service.GetScoreProfile();`
- `internal int GetTerrainEffectNonce() => _terrain_effect_nonce;`
- `internal int IncrementTerrainEffectNonce() => ++_terrain_effect_nonce;`
- `internal BattleEventBatch new_batch() => _new_batch();`
- `internal void MergeBatch(BattleEventBatch target_batch, BattleEventBatch source_batch) =>`
- `internal void AppendChangedCoord(BattleEventBatch batch, Vector2I coord) =>`
- `internal void AppendChangedCoords(BattleEventBatch batch, GVector2IArray coords) =>`
- `internal void AppendChangedUnitId(BattleEventBatch batch, StringName unit_id) =>`
- `internal void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unit_state) =>`
- `internal void AppendBatchLog(BattleEventBatch batch, string message) =>`
- `internal void AppendResultReportEntry(BattleEventBatch batch, AttackEffectResolutionResult result) =>`
- `internal void AppendReportEntry(BattleEventBatch batch, IReadOnlyDictionary<string, object> report_entry) =>`
- `internal void ClearDefeatedUnit(BattleUnitState unit_state, BattleEventBatch batch = null) =>`
- `internal GVector2IArray sort_coords(GArray target_coords) => _sort_coords(target_coords);`
- `internal GVector2IArray sort_coords(GVector2IArray target_coords) => _sort_coords(target_coords);`
- `internal bool is_unit_effect(CombatEffectDef effect_def) => _is_unit_effect(effect_def);`
- `internal int GetUnitSkillLevel(BattleUnitState unit_state, StringName skill_id) =>`
- `internal void _initialize_battle_metrics()`
- `internal void _record_turn_started(BattleUnitState unit_state, BattleEventBatch batch = null)`
- `internal void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)`
- `internal void _record_skill_success(BattleUnitState unit_state, StringName skill_id)`
- `internal void _record_unit_defeated(BattleUnitState unit_state)`
- `public void dispose()`
- `internal int _get_unit_hp_max(BattleUnitState unit_state) =>`
- `internal int _get_unit_stamina_max(BattleUnitState unit_state) =>`
- `internal GStringNameArray _normalize_target_unit_ids(BattleCommand command)`
- `internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)`

### `scripts/systems/battle/runtime/BattleRuntimeModuleBorrowerSet.cs`

- `internal sealed class BattleRuntimeModuleBorrowerSet`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime(ref Exception firstFailure)`
- 作为七个直属 split service 的唯一 owner-local 有序组合源；依赖方排在后面，绑定失败或 module teardown 时按逆序 best-effort 清理。
- `CaptureTopology(...)` 只报告 typed 注册拓扑与活动依赖数，供 borrower 生命周期回归验证，不统计 `WeakReference` 实例数量。

### `scripts/systems/battle/runtime/BattleSpawnPlacementService.cs`

- `internal sealed class BattleSpawnPlacementService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- `internal bool PlaceUnitsTyped(...)` / `internal bool PlaceUnitsForTestsTyped(...)`
- 拥有 spawn side 解析、footprint 放置、候选评分、可达性检查与失败回滚；module 只保留启动编排和测试窄入口。

### `scripts/systems/battle/runtime/BattleSpecialSkillGateService.cs`

- `internal sealed class BattleSpecialSkillGateService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- `public BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(...)`
- 拥有 doom、黑星印记、皇冠封印、黑契约等门禁与状态写入入口，并桥接 `BattleSpecialSkillResolver` / turn resolver。

### `scripts/systems/battle/runtime/BattleMovementCommandService.cs`

- `internal sealed class BattleMovementCommandService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- 拥有移动成本、移动命令、位置交换、强制位移候选与移动阻断桥接。

### `scripts/systems/battle/runtime/BattleMetricsReportService.cs`

- `internal sealed class BattleMetricsReportService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- 拥有 battle metrics 记录、rating/report 路由及 `BattleEffectOrigin` scope/栈。
- `_record_turn_started` 的 gameplay 编排归 `BattleRuntimeModule`；service 的 `RecordTurnStartedMetrics(...)` 只写指标。

### `scripts/systems/battle/runtime/BattleAiDecisionBindingService.cs`

- `internal sealed class BattleAiDecisionBindingService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- 私有持有 per-unit action-plan index，拥有 plan build/ensure/borrowed query/clear、decision helper/context 接线、三个 score-input builder、AI movement query/block 与 turn prepare/cleanup；只弱借用 module，module 不保存或暴露可变 plan map。
- action plan 是 battle-lifetime owner，decision context/helper 是其短期 consumer。content rebind、start failure 与 module teardown 必须先清 consumer，再从 index 摘除全部 plan 并逐个 `Dispose()`；单个关闭失败不阻止其余 plan 清理，service `DisposeRuntime()` 最终仍断开 weak module borrower，重复 clear/dispose 保持幂等。

### `scripts/systems/battle/runtime/BattleContingencyBridgeService.cs`

- `internal sealed class BattleContingencyBridgeService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- 显式实现 `IBattleContingencyRuntimePort`，拥有 current state/grid/skill 查询、玩家学习来源复核、source-event 编号、同步 auto-cast 和 owner overlay 刷新；source-event ordinal 由 bridge 按 runtime 实例生命周期持有。
- 其余职责是 HP/status/spell/position hook、stored-target/release queue、battle/turn hook 与 consumed validate/commit 桥接；只弱借用 module。
- auto-cast 保持原同步调用栈：先复核玩家已学来源，再建立覆盖完整 orchestrator 调用的 `BattleEffectOrigin.AutoCast` scope，使用调用方同一 `BattleEventBatch` 直接执行；不改成异步 event bus 或延迟队列。

### `scripts/systems/battle/runtime/IBattleContingencyRuntimePort.cs`

- `internal interface IBattleContingencyRuntimePort`
- 只向 `BattleContingencySystem` 暴露 state/grid/skill 查询、来源复核、source-event 编号、同步 auto-cast 与 overlay 刷新七项 capability。
- `BattleContingencySystem` 弱借用该端口，不持有或引用 `BattleRuntimeModule`；销毁时显式清除端口绑定。
- contingency lifecycle report 仍由 system 直接调用传入 `BattleEventBatch.AddReportEntry(...)`；不经 metrics/report bridge，避免给既有 report schema 注入额外 effect-origin 字段。

### `scripts/systems/battle/runtime/BattleCommandPreviewService.cs`

- `internal sealed class BattleCommandPreviewService`
- `internal void Setup(BattleRuntimeModule runtime)` / `internal void DisposeRuntime()`
- `public BattlePreview PreviewCommand(BattleCommand command)`
- `internal string _get_battle_interaction_block_message()`
- `internal bool _should_block_skill_issue_from_preview(...)`
- `internal void _preview_change_equipment_command(...)`
- 拥有只读 command preview、interaction/issue blocking 与 equipment-change preview；skill entry 校验直接调用 caller-scoped `BattleSkillAvailabilityService`，不为 execution 提供等级转发；装备技能 usage/reaction commit 不属于该 service。

### `scripts/systems/battle/runtime/BattleGroundEffectService.cs`

- `internal void Setup(BattleRuntimeModule runtime)` / `internal void Dispose()`
- parent 依次接线 `BattleGroundEffectCoordService`、`BattleGroundRelocationService`、`BattleGroundSkillValidationService`；关闭时按相反顺序清空 child 的 runtime、owner 与 sibling borrower，再清自身 runtime。
- `ActiveDependencyCount` 聚合整棵直属 child 子图，用于确认正常与异常 teardown 后没有残留依赖。

### `scripts/systems/game_runtime/BattleSessionFacade.cs`

- `public partial class BattleSessionFacade : RefCounted`
- `public void Setup(GameRuntimeFacade runtime)`
- `public new void Dispose()`
- `public string GetSelectedBattleSkillName()`
- `public string GetSelectedBattleSkillVariantName()`
- `public GVector2IArray GetSelectedBattleSkillTargetCoords()`
- `public GStringNameArray GetSelectedBattleSkillTargetUnitIds()`
- `public GVector2IArray GetSelectedBattleSkillValidTargetCoords()`
- `public int GetSelectedBattleSkillRequiredCoordCount()`
- `public GVector2IArray GetBattleMovementReachableCoords()`
- `public GVector2IArray GetBattleOverlayTargetCoords()`
- `public string GetBattleActiveUnitName()`
- `internal Dictionary GetBattleTerrainCounts()`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleTickTyped(int tickCount)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleSelectSkillTyped(int slotIndex)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleCycleVariantTyped(int step)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleClearSkillTyped()`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleMoveToTyped(Vector2I targetCoord)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleMoveDirectionTyped(Vector2I direction)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleWaitOrResolveTyped()`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleCancelCastTyped(StringName unitId)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandBattleInspectTyped(Vector2I coord)`
- `internal GameRuntimeFacade.RuntimeCommandResult ResetBattleFocusTyped()`
- `public bool HandleBattleInput(InputEventKey keyEvent)`
- `public void StartBattle(EncounterAnchorData encounterAnchor)`
- `internal GameRuntimeFacade.RuntimeCommandResult ResolveActiveBattleTyped()`
- `internal BattleResolutionResult GetBattleResolutionResult(BattleRuntimeModule battleRuntime)`
- `internal BattleResolutionResult ConsumeBattleResolutionResult(BattleRuntimeModule battleRuntime)`
- `internal BattleRefreshMode AttemptBattleMove(Vector2I direction)`
- `public void OnBattleCellClicked(Vector2I coord)`
- `public void OnBattleCellRightClicked(Vector2I coord)`
- `public void OnBattleSkillSlotSelected(int index)`
- `public void ApplyBattleBatch(BattleEventBatch batch)`
- `public void RefreshBattleRuntimeState()`
- `public int BuildBattleSeed(EncounterAnchorData encounterAnchor)`
- `public BattleState GetRuntimeBattleState()`
- `public bool IsBattleFinished()`
- `public BattleUnitState GetRuntimeActiveUnit()`
- `public BattleUnitState GetManualActiveUnit()`
- `public BattleUnitState GetRuntimeUnitAtCoord(Vector2I coord)`
- `public BattleCommand BuildWaitCommand()`
- `internal BattleRefreshMode IssueBattleCommand(BattleCommand command)`
- `internal void CapturePendingPromotionPrompt(Godot.Collections.Array progressionDeltas) =>`
- `public Vector2I GetDefaultBattleSelectedCoord()`
- `public BattleUnitState GetBattleUnitById(StringName unitId)`
- `public BattleUnitState GetBattleUnitAtCoord(Vector2I coord)`
- `public BattleUnitState GetBattleActiveUnit()`
- `public string GetBattleUnitTypeLabel(string unitId)`
- `internal Dictionary BuildBattleStartContext(EncounterAnchorData encounterAnchor)`
- `public StringName ResolveBattleTerrainProfile(EncounterAnchorData encounterAnchor)`

### `scripts/systems/battle/runtime/BattleUnitFactory.cs`

- `internal partial class BattleUnitFactory : RefCounted`
- `private sealed class AllyUnitDefaults`
- `public AllyUnitDefaults(Godot.Collections.Dictionary context)`
- `private sealed class EnemyUnitDefaults`
- `public EnemyUnitDefaults(Godot.Collections.Dictionary context)`
- `private sealed class EnemyWeaponDefaults`
- `public EnemyWeaponDefaults(Godot.Collections.Dictionary context)`
- `internal void Setup(BattleRuntimeModule runtime)`
- `internal void DisposeRuntime()`
- `internal void RefreshBattleUnit(BattleUnitState us)`
- `internal void RefreshKnownSkills(BattleUnitState us)`
- `internal void RefreshWeaponProjection(BattleUnitState us)`
- `internal void RefreshEquipmentProjection(BattleUnitState us)`

### `scripts/systems/battle/runtime/BattleMovementService.cs`

- `internal class BattleMovementService`
- `internal void Setup(BattleRuntimeModule runtime)`
- `internal void Dispose()`
- `internal IReadOnlyList<Vector2I> SortCoords(IEnumerable<Vector2I> target_coords)`
- `internal IReadOnlyList<Vector2I> GetUnitReachableMoveCoords(BattleUnitState unit_state)`
- `internal int GetMoveCostForUnitTarget(BattleUnitState unit_state, Vector2I target_coord)`
- `internal int GetMovePathCost(BattleUnitState unit_state, IReadOnlyList<Vector2I> anchor_path)`
- `internal int GetStatusMoveCostDelta(BattleUnitState unit_state)`
- `internal BattleMovePathResult ResolveMovePathResultTyped(BattleUnitState active_unit, Vector2I target_coord)`
- `internal int GetAvailableMovePoints(BattleUnitState unit_state)`
- `internal bool IsNormalMovementLocked(BattleUnitState unit_state)`
- `internal void HandleMoveCommand(BattleUnitState active_unit, BattleCommand command, BattleEventBatch batch)`
- `internal BattleValidatedMoveExecutionResult MoveUnitAlongValidatedPathTyped(BattleUnitState active_unit, IReadOnlyList<Vector2I> anchor_path, Vector2I target_coord, BattleEventBatch batch)`

### `scripts/systems/battle/runtime/BattleMovementQueryService.cs`

- `internal partial class BattleMovementQueryService : RefCounted`
- `public CellInfo(bool exists, StringName terrain, StringName occupant)`
- `private sealed class UnitInfo`
- `public EdgeInfo(bool blocksMove, bool blocksOccupancy, int heightDifference)`
- `public PathSearchBudgetSnapshot ToSnapshot()`
- `internal sealed class MovementQueryOptions`
- `public static PathSearchResult Failure(StringName reason, int visitedCount = 0)`
- `public static PathSearchResult Success(List<Vector2I> path, int cost, int visitedCount)`
- `private sealed class PathSearchTree`
- `private sealed class PathTargetSearchResult`
- `internal sealed class BattleDistanceBandPathTargetCandidate`
- `internal sealed class BattleDistanceBandPathTargetResult`
- `public static BattleDistanceBandPathTargetResult Failure(StringName rejectReason)`

### `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`

- `internal sealed partial class BattleSkillExecutionOrchestrator`
- `internal void Setup(BattleRuntimeModule runtime)`
- `internal void DisposeRuntime()`
- execution 在同步调用点通过 `BattleSkillAvailabilityService` 重新解析 entry level；availability result 不从 preview 缓存到 commit，装备授予等级继续进入原有 scoped skill-level execution 规则。
- `internal void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)`
- `internal void _record_unit_defeated(BattleUnitState unit_state)`
- `internal bool _is_doom_shift_skill(StringName skill_id)`
- `internal bool _is_black_crown_seal_skill(StringName skill_id)`
- `internal bool _is_crown_break_skill(StringName skill_id)`
- `internal bool _is_doom_sentence_skill(StringName skill_id)`
- `internal void _flush_last_stand_mastery_records(BattleEventBatch batch)`
- `internal void _append_changed_coord(BattleEventBatch batch, Vector2I coord)`
- `internal void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)`
- `internal void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)`
- `internal void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)`
- `internal GVector2IArray _sort_coords(GArray target_coords)`
- `internal GVector2IArray _sort_coords(GVector2IArray target_coords)`
- `internal int _get_effective_skill_range(BattleUnitState active_unit, SkillDefinition skillDefinition)`
- `internal int _get_effective_skill_range(BattleUnitReadView active_unit, SkillDefinition skillDefinition)`
- `internal void _append_damage_preview_line(BattlePreview preview)`
- `internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)`
- `internal void _apply_equipment_durability_result(BattleUnitState target_unit, AttackEffectResolutionResult result, BattleEventBatch batch)`
- `internal GVector2IArray _get_line_coords(Vector2I from, Vector2I to)`
- `internal bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)`
- `internal IReadOnlyList<BattleUnitState> _collect_units_in_coords_typed(GVector2IArray effect_coords)`
- `internal bool _is_unit_effect(CombatEffectDef effect_def)`
- `internal bool _is_terrain_effect(CombatEffectDef effect_def)`
- `private StringName ResolveEffectTargetFilter(SkillDefinition skillDefinition, CombatEffectDefinition effectDefinition)`
- `internal int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)`
- `internal string _format_skill_variant_label(SkillDefinition skillDefinition, CombatCastVariantDefinition castVariant)`
- `internal bool CommitEquipmentSkillUsageIfNeeded(...)`
- 装备技能 usage 与 granted-skill reaction 的真实提交归执行编排器；module 只保留测试/兄弟服务使用的窄门面。

### `scripts/systems/battle/runtime/BattleSkillPreviewService.cs`

- `internal sealed class BattleSkillPreviewService`
- `internal void Setup(...)` / `internal void DisposeRuntime()`
- 拥有技能 preview、unit/ground preview 分支、伤害预览与结果日志；弱借用 runtime，关闭时断开 orchestrator 与 target-validation borrower。

### `scripts/systems/battle/runtime/BattleSkillTargetValidationService.cs`

- `internal sealed class BattleSkillTargetValidationService`
- `internal void Setup(...)` / `internal void DisposeRuntime()`
- 拥有目标归一化/排序、unit/read-view 校验、dead/execute/体型规则与 target affordance；`_is_multi_unit_skill(...)` 只属于该 service。

### `scripts/systems/battle/runtime/BattleChainDamageService.cs`

- `internal readonly record struct ChainDamageParameters` / `internal readonly record struct ChainDamageHop`
- `internal sealed class BattleChainDamageService`
- `internal void Setup(...)` / `internal void DisposeRuntime()`
- 拥有 chain target 收集、逐跳 origin、半径/地形 bonus、屏障/路径检查与结算；关闭时断开 orchestrator 与 preview borrower。

### `scripts/systems/battle/runtime/BattleRandomChainSkillService.cs`

- `internal sealed class BattleRandomChainSkillService`
- `internal void Setup(...)` / `internal void DisposeRuntime()`
- 拥有随机链候选池、每目标命中上限、抽样与执行；`_shuffle_random_chain_pool(...)` 只属于该 service，关闭时断开 orchestrator 与 target-validation borrower。

### `scripts/systems/battle/runtime/BattleTargetCollectionService.cs`

- `internal sealed class BattleTargetCollectionService`

### `scripts/systems/battle/runtime/BattleTimelineDriver.cs`

- `internal sealed class BattleTimelineDriver`
- `internal void Setup(BattleRuntimeModule runtime)`
- `internal void Dispose()`
- `internal void AdvanceTimeline(int tickCount, BattleEventBatch batch)`
- `internal bool UseDiscreteTimelineTicks()`
- `internal void ApplyTimelineStep(BattleEventBatch batch, int tuDelta)`
- `internal void ResolveTimelineStatusPhase(BattleEventBatch batch, int tuDelta)`
- `internal bool ApplyStaminaRecovery(BattleUnitState unitState, int tuDelta)`
- `internal int GetUnitConstitution(BattleUnitState unitState)`
- `internal int ApplyStaminaRecoveryPercentBonus(BattleUnitState unitState, int baseProgressGain)`
- `internal int NormalizeUnitActionThreshold(int actionThreshold)`
- `internal void InitializeUnitActionThresholds()`
- `internal void InitializeUnitTraitHooks()`
- `internal int ResolveUnitActionThreshold(BattleUnitState unitState)`
- `internal int ResolveTimelineTuPerTick(GDictionary context)`
- `internal void EndActiveTurn(BattleEventBatch batch)`
- `internal void ActivateNextReadyUnit(BattleEventBatch batch)`
- `internal void SortReadyUnitIdsByActionPriority()`
- `internal bool IsLeftReadyUnitHigherPriority(StringName leftUnitId, StringName rightUnitId)`
- `internal int GetUnitTurnOrderAttribute(BattleUnitState unitState, StringName attributeId)`
- `internal int GetUnitTurnOrderActionPoints(BattleUnitState unitState)`
- `internal GStringNameArray GetUnitsInOrder()`
- 真实拥有 timeline phase、current TU、`tu_per_tick`、ready unit、action threshold、stamina 与 Control objective 分数推进，不经 module 或独立 bridge 反向转发。
- `ApplyTimelineStep(...)` 保持 current TU → Control 区域归属/计分 → 状态周期 tick/duration → 临时边/延迟区域/地形/屏障 → pending cast reconcile/advance/complete → ready 收集与排序的原同步顺序。
- `ActivateNextReadyUnit(...)` 保持 trait turn-start → cooldown/turn timer → module 的 metrics/contingency/sequential auto-cast 编排 → AP/移动点重置 → turn-start status/control 的原同步顺序。

### `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`

- `internal sealed class BattleRuntimeSkillTurnResolver`
- `internal void EnsureUnitTurnAnchor(...)` / `ConsumeTurnCooldownDelta(...)` / `AdvanceUnitTurnTimers(...)`
- `internal void InitializeAppliedStatusTimelineTicks(...)`（Godot collection 与 plain typed overload）
- `internal BattleStatusTickResult ApplyTurnStartStatusesResult(...)` / `ApplyUnitStatusPeriodicTicksResult(...)`
- `internal bool AdvanceUnitStatusDurations(...)`
- 真实拥有 cooldown 的 current-TU/静滞/turn-start 调度、非法粒度日志与 changed-unit 提交，以及 turn timer、状态周期 tick/duration/turn-start 规则和护盾 duration 调度；cooldown map/anchor 的唯一存储与原子判定/推进归 `BattleUnitCooldownState`。该 resolver 仍以状态应用时的 current TU 为基准初始化 `next_tick_at_tu`；护盾实际递减和到期六字段清空归 `BattleUnitShieldState`。
- `BattleRuntimeModule.MarkAppliedStatusesForTurnTiming(...)` 只负责先调用该 resolver 初始化 tick anchor，再通知 Fate runtime；不成为状态计时 owner。

### `scripts/systems/battle/runtime/BattleObjectiveEvaluationService.cs`

- `internal sealed class BattleObjectiveEvaluationService`
- `internal BattleObjectiveEvaluationResult Evaluate(BattleState state)`
- 当前 evaluator 实现歼灭、击败首领、拯救、逃离、护送、防守、截击、节点作业和区域占领；它读取正式 objective runtime state，并产出类型化 `BattleFinalDecision`，不直接修改阶段、日志或结算结果。

### `scripts/systems/battle/runtime/BattleRuntimeModule.Objectives.cs`

- `internal void BeginObjectiveMutation()` / `internal BattleOutcomeFlushResult EndObjectiveMutation(...)`
- `internal void MarkObjectiveEvaluationDirty()`
- `internal BattleOutcomeFlushResult FlushBattleOutcomeEvaluation(BattleEventBatch batch)`
- 原子变更最外层统一求值；`CompleteBattle(...)` 是阶段切换、评分收口、结果生成和终局日志的唯一 owner。

### `scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs`

- `internal class BattleRuntimeLootResolver`
- `internal void Setup(BattleRuntimeModule runtime)`
- `internal void Dispose()`
- `internal BattleResolutionResult BuildBattleResolutionResult()`

### `scripts/systems/battle/runtime/BattleSkillMasteryService.cs`

- `internal sealed class BattleSkillMasteryService : IDisposable`
- `internal void Clear()`
- `public void RecordTargetResult(BattleUnitState sourceUnit, BattleUnitState targetUnit, SkillDefinition skillDefinition, AttackEffectResolutionResult result, IReadOnlyList<CombatEffectDefinition> effectDefinitions = null)`
- `public void RecordBonus(BattleUnitState sourceUnit, BattleUnitState targetUnit, SkillDefinition skillDefinition, int baseAmount)`
- `public void RecordMasteryAmount(StringName skillId, int amount)`
- `public int ResolveActiveSkillMasteryAmount()`
- `public StringName ResolveMasteryRewardSkillId(BattleUnitState sourceUnit, StringName skillId)`
- `public int ResolveBattleRatingMasteryAmount(int score)`
- `internal static SkillMasteryResultSnapshot FromDictionary(GDictionary source)`
- `public static SkillMasteryResolutionEvent ForSkillAmount(StringName skillId, int amount)`

### `scripts/systems/battle/core/BattleState.cs`

- `public partial class BattleState : RefCounted`
- `public BattleCellEntry(Vector2I coord, BattleCellState cell)`
- `public BattleUnitEntry(StringName unitId, BattleUnitState unit)`
- `public static bool IsStrongAttackDisadvantageStatusId(StringName statusId) =>`
- `internal static IReadOnlyList<StringName> StrongAttackDisadvantageStatusIdsTyped() =>`
- `internal void MarkMovementGeometryChanged()`
- `public void ResetLogEntries(Godot.Collections.Array<string> entries)`
- `public void ClearLogEntries()`
- `public void AppendLogEntry(string entry)`
- `public int GetLogTextByteSize() => _log_text_byte_size;`
- `public int NextAttackRollNonce()`
- `internal ulong AllocateCastSequence()`
- `public string GetLogBudgetSummaryText() =>`
- `public bool IsAttackDisadvantage(BattleUnitState attacker, BattleUnitState defender = null)`
- `public bool IsEmpty() =>`
- `public WarehouseState GetPartyBackpackView()`
- `public void SetPartyBackpackView(WarehouseState backpackState)`
- `public EquipmentState GetUnitEquipmentView(StringName unitId)`
- `public bool SetUnitEquipmentView(StringName unitId, EquipmentState es)`
- `public void MarkRuntimeEdgesDirty()`
- `public void NormalizeUnitIdArrays()`
- `public List<StringName> GetAllyUnitIdsTyped() =>`
- `public List<StringName> GetEnemyUnitIdsTyped() =>`
- `internal List<StringName> GetUnitIdsTyped(bool sorted = false)`
- `internal List<BattleUnitState> GetUnitsTyped()`
- `internal List<BattleCellEntry> GetCellEntriesTyped()`
- `internal bool TryGetCellTyped(Vector2I coord, out BattleCellState cellState)`
- `internal List<BattleUnitEntry> GetUnitEntriesTyped()`
- `internal bool TryGetUnitTyped(StringName unitId, out BattleUnitState unitState)`

### `scripts/systems/battle/core/BattleUnitShieldState.cs`

- `internal readonly record struct BattleUnitShieldSnapshot(int CurrentHp, int MaxHp, int Duration, StringName Family, StringName SourceUnitId, StringName SourceSkillId)`
- `internal readonly record struct BattleUnitShieldDrainResult(int ActualDrain, bool Depleted)`
- `internal sealed class BattleUnitShieldState`
- `internal BattleUnitShieldSnapshot CaptureRaw()`
- `internal BattleUnitShieldSnapshot CaptureCanonical()`
- `internal bool HasActiveShield()`
- `internal void ReplaceAndNormalize(BattleUnitShieldSnapshot snapshot)`
- `internal void SetCurrentHpAndNormalize(int currentHp)`
- `internal bool AdvanceDuration(int elapsedTu)`
- `internal BattleUnitShieldDrainResult DrainCurrentHp(int requestedDrain)`
- `internal void Clear()`
- `internal void Normalize()`
- `internal BattleUnitShieldState DuplicateState()`
- `internal void RestoreRaw(BattleUnitShieldSnapshot snapshot)`
- `internal static BattleUnitShieldState FromRaw(BattleUnitShieldSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitCooldownState.cs`

- `internal readonly record struct BattleUnitCooldownSnapshot(BattleStringNameIntMap Cooldowns, int LastTurnTu)`
- `internal readonly record struct BattleUnitCooldownAdvanceResult(int ElapsedTu, bool AnchorChanged, bool CooldownMapChanged, bool InvalidGranularity)`
- `internal sealed class BattleUnitCooldownState`
- `internal int Get(StringName skillId, int fallback = 0)`
- `internal void Set(StringName skillId, int value)`
- `internal void ReplaceNormalized(IReadOnlyDictionary<StringName, int> values)`
- `internal Dictionary<StringName, int> Snapshot()`
- `internal int GetLastTurnTu()`
- `internal void SetLastTurnTu(int value)`
- `internal void EnsureAnchor(int currentTu)`
- `internal BattleUnitCooldownAdvanceResult AdvanceTo(int currentTu, int granularity)`
- `internal void AdvanceFrozenAnchor(int elapsedTu, int currentTu)`
- `internal BattleUnitCooldownSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitCooldownSnapshot snapshot)`
- `internal BattleUnitCooldownState DuplicateState()`
- `internal static BattleUnitCooldownState FromRaw(BattleUnitCooldownSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitActionClockState.cs`

- `internal readonly record struct BattleUnitActionClockSnapshot(bool OwnerPresent, int ActionProgress, int ActionThreshold, int ActionProgressRateRemainder)`
- `internal sealed class BattleUnitActionClockState`
- `internal int GetProgress()`
- `internal void SetProgressRaw(int value)`
- `internal int GetThreshold()`
- `internal void SetThresholdRaw(int value)`
- `internal int GetProgressRateRemainder()`
- `internal int ConsumeRateScaledGain(int baseProgressDelta, int ratePercent)`
- `internal bool AdvanceAndConsumeThresholds(int progressGain, int positiveThreshold)`
- `internal BattleUnitActionClockSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitActionClockSnapshot snapshot)`
- `internal BattleUnitActionClockState DuplicateState()`
- `internal static BattleUnitActionClockState FromRaw(BattleUnitActionClockSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitCombatResourceState.cs`

- `internal readonly record struct BattleUnitCombatResourceValues(int Hp, int Mp, int Stamina, int Aura, int Ap, int MovePoints, int StaminaRecoveryProgress, bool IsAlive)`
- `internal readonly record struct BattleUnitCombatResourceReadView(bool OwnerPresent, BattleUnitCombatResourceValues Values)`
- `internal readonly record struct BattleUnitCombatResourceSnapshot(bool OwnerPresent, BattleUnitCombatResourceValues Values)`
- `internal sealed class BattleUnitCombatResourceState`
- `internal BattleUnitCombatResourceReadView GetReadView()`
- `internal void SetCurrentHp(int value)`
- `internal void SetCurrentHpClamped(int value, int hpMax)`
- `internal int ApplyHpDamage(int damage)`
- `internal int ApplyHealing(int amount, int hpMax)`
- `internal void MarkDead()`
- `internal void ReviveWithHp(int hp, int hpMax)`
- `internal void SetAllNormalized(int hp, int mp, int stamina, int aura, int ap, int movePoints)`
- `internal void RestoreProjectionNormalized(int hp, int mp, int stamina, int aura, int ap, int movePoints, bool alive)`
- `internal void ClampToCaps(BattleResourceCaps caps)`
- `internal bool ApplyStaminaRecovery(int tickCount, int staminaMax, int progressGainPerTick, int progressDenominator)`
- `internal BattleUnitCombatResourceSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitCombatResourceSnapshot snapshot)`
- `internal BattleUnitCombatResourceState DuplicateState()`
- `internal static BattleUnitCombatResourceState FromRaw(BattleUnitCombatResourceSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitRestState.cs`

- `internal readonly record struct BattleUnitRestSnapshot(bool OwnerPresent, bool IsResting)`
- `internal sealed class BattleUnitRestState`
- `internal bool IsResting()`
- `internal void MarkResting()`
- `internal void ClearResting()`
- `internal BattleUnitRestSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitRestSnapshot snapshot)`
- `internal BattleUnitRestState DuplicateState()`
- `internal static BattleUnitRestState FromRaw(BattleUnitRestSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitCreatureTypeState.cs`

- `internal readonly struct BattleCreatureTypeTagReadView : IReadOnlyList<StringName>`
- `internal readonly record struct BattleUnitCreatureTypeReadView(bool OwnerPresent, BattleCreatureTypeTagReadView Tags)`
- `internal readonly record struct BattleUnitCreatureTypeSnapshot(bool OwnerPresent, StringNameList Tags)`
- `internal sealed class BattleUnitCreatureTypeState`
- `internal BattleUnitCreatureTypeReadView GetReadView()`
- `internal bool Contains(StringName tag)`
- `internal void ReplaceNormalized(IEnumerable<StringName> tags)`
- `internal bool AddNormalized(StringName tag)`
- `internal BattleUnitCreatureTypeSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitCreatureTypeSnapshot snapshot)`
- `internal BattleUnitCreatureTypeState DuplicateState()`
- `internal static BattleUnitCreatureTypeState FromRaw(BattleUnitCreatureTypeSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitMovementTagState.cs`

- `internal readonly struct BattleMovementTagReadView : IReadOnlyList<StringName>`
- `internal readonly record struct BattleUnitMovementTagReadView(bool OwnerPresent, BattleMovementTagReadView Tags)`
- `internal readonly record struct BattleUnitMovementTagSnapshot(bool OwnerPresent, StringNameList Tags)`
- `internal sealed class BattleUnitMovementTagState`
- `internal BattleUnitMovementTagReadView GetReadView()`
- `internal bool Contains(StringName tag)`
- `internal void ReplaceNormalized(IEnumerable<StringName> tags)`
- `internal bool AddNormalized(StringName tag)`
- `internal BattleUnitMovementTagSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitMovementTagSnapshot snapshot)`
- `internal BattleUnitMovementTagState DuplicateState()`
- `internal static BattleUnitMovementTagState FromRaw(BattleUnitMovementTagSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitVisionProficiencyState.cs`

- `internal readonly struct BattleVisionProficiencyTagReadView : IReadOnlyList<StringName>`
- `internal readonly record struct BattleUnitVisionProficiencyReadView(bool OwnerPresent, BattleVisionProficiencyTagReadView VisionTags, BattleVisionProficiencyTagReadView ProficiencyTags)`
- `internal readonly record struct BattleUnitVisionProficiencySnapshot(bool OwnerPresent, StringNameList VisionTags, StringNameList ProficiencyTags)`
- `internal sealed class BattleUnitVisionProficiencyState`
- `internal BattleUnitVisionProficiencyReadView GetReadView()`
- `internal bool ContainsVision(StringName tag)`
- `internal bool ContainsProficiency(StringName tag)`
- `internal void ReplaceNormalized(IEnumerable<StringName> visionTags, IEnumerable<StringName> proficiencyTags)`
- `internal void ResetNormalized()`
- `internal bool AddVisionNormalized(StringName tag)`
- `internal bool AddProficiencyNormalized(StringName tag)`
- `internal BattleUnitVisionProficiencySnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitVisionProficiencySnapshot snapshot)`
- `internal BattleUnitVisionProficiencyState DuplicateState()`
- `internal static BattleUnitVisionProficiencyState FromRaw(BattleUnitVisionProficiencySnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitCombatResourceUnlockState.cs`

- `internal readonly record struct BattleUnitCombatResourceUnlockSnapshot(bool OwnerPresent, StringNameList ResourceIds)`
- `internal readonly struct BattleCombatResourceUnlockReadView`
- `internal readonly record struct BattleUnitCombatResourceUnlockReadView(bool OwnerPresent, BattleCombatResourceUnlockReadView ResourceIds)`
- `internal sealed class BattleUnitCombatResourceUnlockState`
- `internal BattleUnitCombatResourceUnlockReadView GetReadView()`
- `internal bool Contains(StringName resourceId)`
- `internal bool Unlock(StringName resourceId)`
- `internal void ReplaceNormalized(IEnumerable<StringName> resourceIds)`
- `internal void SyncDefaults()`
- `internal BattleUnitCombatResourceUnlockSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitCombatResourceUnlockSnapshot snapshot)`
- `internal BattleUnitCombatResourceUnlockState DuplicateState()`
- `internal static BattleUnitCombatResourceUnlockState FromRaw(BattleUnitCombatResourceUnlockSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitKnownSkillState.cs`

- `internal readonly record struct BattleUnitKnownSkillSnapshot(bool OwnerPresent, StringNameList ActiveSkillIds, BattleStringNameIntMap SkillLevels, BattleStringNameIntMap LockHitBonuses)`
- `internal readonly struct BattleKnownActiveSkillReadView`
- `internal readonly struct BattleKnownSkillLevelReadView`
- `internal readonly record struct BattleUnitKnownSkillReadView(bool OwnerPresent, BattleKnownActiveSkillReadView ActiveSkills, BattleKnownSkillLevelReadView SkillLevels, BattleKnownSkillLevelReadView LockHitBonuses)`
- `internal sealed class BattleUnitKnownSkillState`
- `internal BattleUnitKnownSkillReadView GetReadView()`
- `internal BattleKnownActiveSkillReadView GetActiveSkillsView()`
- `internal bool KnowsActiveSkill(StringName skillId)`
- `internal bool TryGetFirstActiveSkill(out StringName skillId)`
- `internal void ReplaceActiveSkillsNormalized(IEnumerable<StringName> skillIds)`
- `internal void AddActiveSkillNormalized(StringName skillId)`
- `internal int GetSkillLevel(StringName skillId, int fallback = 0)`
- `internal bool HasSkillLevel(StringName skillId)`
- `internal void ReplaceSkillLevelsNormalized(IReadOnlyDictionary<StringName, int> values, bool preserveZero)`
- `internal void SetSkillLevelNormalized(StringName skillId, int level, bool preserveZero)`
- `internal void CopySkillLevelEntriesTo(List<KeyValuePair<StringName, int>> destination)`
- `internal int GetLockHitBonus(StringName skillId, int fallback = 0)`
- `internal void ReplaceLockHitBonusesNormalized(IReadOnlyDictionary<StringName, int> values)`
- `internal BattleUnitKnownSkillSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitKnownSkillSnapshot snapshot)`
- `internal BattleUnitKnownSkillState DuplicateState()`
- `internal static BattleUnitKnownSkillState FromRaw(BattleUnitKnownSkillSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitWeaponProjectionState.cs`

- `internal readonly record struct BattleWeaponDiceValues`
- `internal readonly record struct BattleWeaponProjectionValues`
- `internal readonly record struct BattleUnitWeaponProjectionReadView`
- `internal readonly record struct BattleUnitWeaponProjectionSnapshot`
- `internal sealed class BattleUnitWeaponProjectionState`
- `internal BattleUnitWeaponProjectionReadView GetReadView()`
- `internal void Clear()`
- `internal void ApplyNormalized(WeaponProjection projection)`
- `internal void NormalizeCanonicalInPlace()`
- `internal int GetAttackRangeClamped()`
- `internal WeaponDice CopyOneHandedDice()`
- `internal WeaponDice CopyTwoHandedDice()`
- `internal WeaponDice CopyActiveDice()`
- `internal BattleUnitWeaponProjectionSnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitWeaponProjectionSnapshot snapshot)`
- `internal BattleUnitWeaponProjectionState DuplicateState()`
- `internal static BattleUnitWeaponProjectionState FromRaw(BattleUnitWeaponProjectionSnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitGeometryState.cs`

- `internal readonly struct BattleOccupiedCoordReadView : IReadOnlyList<Vector2I>`
- `internal readonly record struct BattleUnitGeometryReadView`
- `internal readonly record struct BattleUnitGeometrySnapshot`
- `internal sealed class BattleUnitGeometryState`
- `internal BattleUnitGeometryReadView GetReadView()`
- `internal void SetAnchorCoord(Vector2I anchorCoord)`
- `internal bool SetBodySizeCategory(StringName category)`
- `internal bool SetBodySizeProjection(int size)`
- `internal void RestoreBodyShapeProjection(StringName category, int size, Vector2I footprint, IEnumerable<Vector2I> occupiedCoords)`
- `internal void NormalizeForOwnerWrite(StringName unitId)`
- `internal void EnsureProjectionInvariant(StringName unitId)`
- `internal BattleUnitGeometrySnapshot CaptureRaw()`
- `internal void RestoreRaw(BattleUnitGeometrySnapshot snapshot)`
- `internal BattleUnitGeometryState DuplicateState()`
- `internal static BattleUnitGeometryState FromRaw(BattleUnitGeometrySnapshot snapshot)`

### `scripts/systems/battle/core/BattleUnitState.cs`

- `public partial class BattleUnitState : RefCounted`
- `internal static GStringNameArray CreateDefaultUnlockedCombatResourceProjection() =>`
- `internal static bool IsValidCombatResourceId(StringName resourceId) =>`
- `internal static StringName ToStringName(BattleWeaponProfileKind kind)`
- `internal static BattleWeaponProfileKind ToWeaponProfileKind(StringName value)`
- `internal static StringName ToStringName(BattleWeaponGripKind kind)`
- `internal static BattleWeaponGripKind ToWeaponGripKind(StringName value)`
- `internal bool HasPendingCast() => pending_cast != null;`
- `internal bool IsCasting() => IsAlive() && pending_cast != null;`
- `internal void SetPendingCast(BattlePendingCastState pendingCast)`
- `internal BattlePendingCastState ClearPendingCast()`
- `internal void ClearCastingTurnFlags()`
- `internal BattleUnitRestSnapshot GetRestStateTyped()`
- `internal BattleUnitRestSnapshot CaptureRestForMutationSnapshotExact()`
- `internal void RestoreRestForMutationSnapshotExact(BattleUnitRestSnapshot snapshot)`
- `internal bool IsRestingTyped()`
- `internal void MarkRestingTyped()`
- `internal void ClearRestingTyped()`
- `internal BattleUnitCreatureTypeReadView GetCreatureTypeTagsReadViewTyped()`
- `internal BattleUnitCreatureTypeSnapshot CaptureCreatureTypesForMutationSnapshotExact()`
- `internal void RestoreCreatureTypesForMutationSnapshotExact(BattleUnitCreatureTypeSnapshot snapshot)`
- `internal bool HasCreatureTypeTag(StringName tag)`
- `internal void ReplaceCreatureTypeTagsTyped(IEnumerable<StringName> tags)`
- `internal bool AddCreatureTypeTagTyped(StringName tag)`
- `internal BattleUnitActionClockSnapshot GetActionClockStateTyped()`
- `internal BattleUnitActionClockSnapshot CaptureActionClockForMutationSnapshotExact()`
- `internal void RestoreActionClockForMutationSnapshotExact(BattleUnitActionClockSnapshot snapshot)`
- `internal int GetActionProgressTyped()`
- `internal void SetActionProgressTyped(int value)`
- `internal int GetActionThresholdTyped()`
- `internal void SetActionThresholdTyped(int value)`
- `internal int GetActionProgressRateRemainderTyped()`
- `internal int ConsumeActionProgressRateGainTyped(int baseProgressDelta, int ratePercent)`
- `internal bool AdvanceActionClockTyped(int progressGain, int positiveThreshold)`
- `internal BattleUnitGeometryReadView GetGeometryReadViewTyped()`
- `internal BattleUnitGeometrySnapshot CaptureGeometryForMutationSnapshotExact()`
- `internal void RestoreGeometryForMutationSnapshotExact(BattleUnitGeometrySnapshot snapshot)`
- `public Vector2I GetAnchorCoord()`
- `public int GetBodySize()`
- `public StringName GetBodySizeCategory()`
- `public Vector2I GetFootprintSize()`
- `internal BattleOccupiedCoordReadView GetOccupiedCoordsReadViewTyped()`
- `public void SetAnchorCoord(Vector2I anchor_coord)`
- `public bool OccupiesCoord(Vector2I target_coord)`
- `internal BattleUnitMovementTagReadView GetMovementTagsReadViewTyped()`
- `internal BattleUnitMovementTagSnapshot CaptureMovementTagsForMutationSnapshotExact()`
- `internal void RestoreMovementTagsForMutationSnapshotExact(BattleUnitMovementTagSnapshot snapshot)`
- `public bool HasMovementTag(StringName tag)`
- `internal void ReplaceMovementTagsTyped(IEnumerable<StringName> tags)`
- `internal bool AddMovementTagTyped(StringName tag)`
- `internal BattleUnitVisionProficiencyReadView GetVisionProficiencyReadViewTyped()`
- `internal BattleUnitVisionProficiencySnapshot CaptureVisionProficiencyForMutationSnapshotExact()`
- `internal void RestoreVisionProficiencyForMutationSnapshotExact(BattleUnitVisionProficiencySnapshot snapshot)`
- `internal void ResetVisionProficiencyTagsTyped()`
- `internal void ReplaceVisionProficiencyTagsTyped(IEnumerable<StringName> visionTags, IEnumerable<StringName> proficiencyTags)`
- `public bool HasVisionTag(StringName tag)`
- `public bool HasProficiencyTag(StringName tag)`
- `internal bool AddVisionTagTyped(StringName tag)`
- `internal bool AddProficiencyTagTyped(StringName tag)`
- `internal BattleUnitCombatResourceReadView GetCombatResourcesReadViewTyped()`
- `internal BattleUnitCombatResourceSnapshot CaptureCombatResourcesForMutationSnapshotExact()`
- `internal void RestoreCombatResourcesForMutationSnapshotExact(BattleUnitCombatResourceSnapshot snapshot)`
- `public int GetCurrentHp()`
- `public int GetCurrentMp()`
- `public int GetCurrentStamina()`
- `public int GetCurrentAura()`
- `public int GetCurrentAp()`
- `public int GetCurrentMovePoints()`
- `internal int GetStaminaRecoveryProgressTyped()`
- `public bool IsAlive()`
- `public void SetCurrentHp(int value)`
- `public void SetCurrentHpClamped(int value, int hpMax)`
- `public int ApplyHpDamage(int damage)`
- `public int ApplyHealing(int amount, int hpMax)`
- `public void MarkDead()`
- `public void ReviveWithHp(int hp, int hpMax)`
- `public void SetCurrentMp(int value)`
- `public void SetCurrentStamina(int value)`
- `public void SetCurrentAura(int value)`
- `public void SetCurrentAp(int value)`
- `public void SetCurrentMovePoints(int value)`
- `public void SetCombatResources(int hp, int mp, int stamina, int aura, int ap, int movePoints)`
- `internal void RestoreCombatResourceProjection(int hp, int mp, int stamina, int aura, int ap, int movePoints, bool alive)`
- `internal void ClampCombatResources(BattleResourceCaps caps)`
- `internal bool ApplyStaminaRecoveryTyped(int tickCount, int staminaMax, int progressGainPerTick, int progressDenominator)`
- `public IReadOnlyList<Vector2I> GetOccupiedCoordsTyped()`
- `public bool SetBodySizeCategory(StringName category)`
- `public bool SetBodySizeProjection(int size)`
- `internal void RestoreBodyShapeProjection(StringName category, int size, Vector2I footprint, IEnumerable<Vector2I> occupiedCoords)`
- `internal void NormalizeBodySizeProjectionForOwnerWrite()`
- `internal void EnsureBodySizeProjectionInvariant()`
- `public bool HasStatusEffect(StringName status_id)`
- `public bool HasShield()`
- `internal BattleUnitShieldSnapshot GetShieldStateTyped()`
- `internal BattleUnitShieldSnapshot CaptureShieldStateCanonical()`
- `internal BattleUnitShieldSnapshot CaptureShieldForMutationSnapshotExact()`
- `internal void ReplaceShieldStateTyped(int currentHp, int maxHp, int duration, StringName family, StringName sourceUnitId, StringName sourceSkillId)`
- `internal void RestoreShieldForMutationSnapshotExact(BattleUnitShieldSnapshot snapshot)`
- `internal void SetCurrentShieldHpAndNormalizeTyped(int currentHp)`
- `internal bool AdvanceShieldDurationTyped(int elapsedTu)`
- `internal BattleUnitShieldDrainResult DrainShieldTyped(int requestedDrain)`
- `public int GetAuraMax()`
- `public void SyncDefaultCombatResourceUnlocks()`
- `public bool HasCombatResourceUnlocked(StringName resource_id)`
- `internal BattleUnitCombatResourceUnlockReadView GetCombatResourceUnlocksReadViewTyped()`
- `internal BattleUnitCombatResourceUnlockSnapshot CaptureCombatResourceUnlocksForMutationSnapshotExact()`
- `internal void RestoreCombatResourceUnlocksForMutationSnapshotExact(BattleUnitCombatResourceUnlockSnapshot snapshot)`
- `internal int GetKnownSkillLevelTyped(StringName skillId, int fallback = 0)`
- `internal bool HasKnownSkillLevelTyped(StringName skillId)`
- `internal BattleUnitKnownSkillReadView GetKnownSkillsReadViewTyped()`
- `internal BattleKnownActiveSkillReadView GetKnownActiveSkillsViewTyped()`
- `internal bool KnowsActiveSkill(StringName skillId)`
- `internal bool TryGetFirstKnownActiveSkillIdTyped(out StringName skillId)`
- `internal void SetKnownActiveSkillIds(IEnumerable<StringName> skillIds)`
- `internal void AddKnownActiveSkill(StringName skillId)`
- `internal void CopyKnownSkillLevelEntriesTo(List<KeyValuePair<StringName, int>> destination)`
- `internal BattleUnitKnownSkillSnapshot CaptureKnownSkillsForMutationSnapshotExact()`
- `internal void RestoreKnownSkillsForMutationSnapshotExact(BattleUnitKnownSkillSnapshot snapshot)`
- `internal int GetCooldownTyped(StringName skillId, int fallback = 0)`
- `internal void SetCooldownTyped(StringName skillId, int value)`
- `internal void SetCooldownsTyped(IReadOnlyDictionary<StringName, int> values)`
- `internal BattleUnitCooldownSnapshot GetCooldownStateTyped()`
- `internal int GetCooldownAnchorTuTyped()`
- `internal void SetCooldownAnchorTuTyped(int value)`
- `internal void EnsureCooldownAnchorTyped(int currentTu)`
- `internal BattleUnitCooldownAdvanceResult AdvanceCooldownClockToTyped(int currentTu, int granularity)`
- `internal void AdvanceCooldownAnchorForStasisTyped(int elapsedTu, int currentTu)`
- `internal BattleUnitCooldownSnapshot CaptureCooldownForMutationSnapshotExact()`
- `internal void RestoreCooldownForMutationSnapshotExact(BattleUnitCooldownSnapshot snapshot)`
- `internal int GetPerBattleChargeTyped(StringName chargeKey, int fallback = 0)`
- `internal bool HasPerBattleChargeTyped(StringName chargeKey)`
- `internal void SetPerBattleChargeTyped(StringName chargeKey, int value)`
- `internal bool RemovePerBattleChargeTyped(StringName chargeKey)`
- `internal int GetPerTurnChargeTyped(StringName chargeKey, int fallback = 0)`
- `internal bool HasPerTurnChargeTyped(StringName chargeKey)`
- `internal void SetPerTurnChargeTyped(StringName chargeKey, int value)`
- `internal int GetPerTurnChargeLimitTyped(StringName chargeKey, int fallback = 0)`
- `internal bool HasPerTurnChargeLimitTyped(StringName chargeKey)`
- `internal void SetPerTurnChargeLimitTyped(StringName chargeKey, int value)`
- `internal bool RemovePerTurnChargeAndLimitTyped(StringName chargeKey)`
- `internal int GetFumbleProtectionUsedTyped(StringName skillId, int fallback = 0)`
- `internal void SetFumbleProtectionUsedTyped(StringName skillId, int value)`
- `internal Dictionary<StringName, int> GetKnownSkillLevelsTyped()`
- `internal Dictionary<StringName, int> GetKnownSkillLockHitBonusesTyped()`
- `internal Dictionary<StringName, int> GetCooldownsTyped()`
- `internal Dictionary<StringName, int> GetPerBattleChargesTyped()`
- `internal Dictionary<StringName, int> GetPerTurnChargesTyped()`
- `internal Dictionary<StringName, int> GetPerTurnChargeLimitsTyped()`
- `internal Dictionary<StringName, int> GetFumbleProtectionUsedTyped()`
- `internal Dictionary<StringName, StringName> GetDamageResistancesTyped()`
- `internal BattleUnitWeaponProjectionReadView GetWeaponProjectionReadViewTyped()`
- `internal BattleUnitWeaponProjectionSnapshot CaptureWeaponProjectionForMutationSnapshotExact()`
- `internal void RestoreWeaponProjectionForMutationSnapshotExact(BattleUnitWeaponProjectionSnapshot snapshot)`
- `internal WeaponDice GetWeaponOneHandedDiceTyped()`
- `internal WeaponDice GetWeaponTwoHandedDiceTyped()`
- `internal WeaponDice GetActiveWeaponDiceTyped()`
- `public bool UnlockCombatResource(StringName resource_id)`
- `public void SetUnlockedCombatResourceIds(IEnumerable<StringName> resource_ids)`
- `public void ClearShield()`
- `public void NormalizeShieldState()`
- `public EquipmentState GetEquipmentView()`
- `public void SetEquipmentView(EquipmentState source_equipment_state)`
- `public void ClearWeaponProjection()`
- `internal void ApplyWeaponProjectionTyped(WeaponProjection projection)`
- `public void ApplyWeaponProjection(GDictionary projection)`
- `public int GetWeaponAttackRange()`
- `public BattleStatusEffectState GetStatusEffect(StringName status_id)`
- `public List<BattleStatusEffectState> GetStatusEffectsTyped()`
- `public List<StringName> GetSortedStatusEffectIdsTyped()`
- `public void SetStatusEffect(BattleStatusEffectState effect_state)`
- `public void EraseStatusEffect(StringName status_id)`
- `public void ResetPerTurnCharges()`
- `public BattleUnitState clone()`
- `public static Vector2I GetFootprintSizeForBodySize(int size_value)`
- `public GDictionary ToDictionary()`
- `public static BattleUnitState FromDictionary(GDictionary payload)`

### `scripts/systems/battle/core/BattleCommand.cs`

- `public partial class BattleCommand : RefCounted`
- `public bool IsMove() => CommandKind == BattleCommandKind.Move;`
- `public bool IsSkill() => CommandKind == BattleCommandKind.Skill;`
- `public bool IsWait() => CommandKind == BattleCommandKind.Wait;`
- `public bool IsChangeEquipment() => CommandKind == BattleCommandKind.ChangeEquipment;`
- `public bool IsCancelCast() => CommandKind == BattleCommandKind.CancelCast;`
- `internal void SetEquipmentOccupiedSlotIds(IEnumerable<StringName> values)`
- `internal void SetTargetUnitIds(IEnumerable<StringName> values)`
- `internal void ClearTargetUnitIds()`
- `internal void AddTargetUnitId(StringName value)`
- `internal void SetTargetCoords(IEnumerable<Vector2I> values)`
- `internal void ClearTargetCoords()`
- `internal void AddTargetCoord(Vector2I value)`

### `scripts/systems/battle/core/BattleEventBatch.cs`

- `public partial class BattleEventBatch : RefCounted`
- `internal void SetChangedUnitIds(IEnumerable values)`
- `internal void ClearChangedUnitIds()`
- `internal void AddChangedUnitId(StringName unitId)`
- `internal bool ContainsChangedUnitId(StringName unitId)`
- `internal void SetChangedCoords(IEnumerable values)`
- `internal void ClearChangedCoords()`
- `internal void AddChangedCoord(Vector2I coord)`
- `internal bool ContainsChangedCoord(Vector2I coord)`
- `internal void SetLogLines(IEnumerable values)`
- `internal void ClearLogLines()`
- `internal void AddLogLine(string value)`
- `internal void InsertLogLine(int index, string value)`
- `internal bool ContainsLogLine(string value)`
- `internal void SetReportEntries(IEnumerable values)`
- `internal void ClearReportEntries()`
- `internal void AddReportEntry(GDictionary reportEntry)`
- `internal void SetProgressionDeltas(IEnumerable values)`
- `internal void ClearProgressionDeltas()`
- `internal void AddProgressionDelta(CharacterProgressionDelta delta)`
- `internal GArray BuildReportEntriesArray()`
- `internal GArray BuildProgressionDeltasArray()`
