# 战斗运行时模块可重建规格说明

更新日期：`2026-06-17`

## 目标与边界

本文描述 `BattleRuntimeModule`、`BattleSessionFacade`、战斗 state、命令、时间线、单位工厂、移动、技能、AI、结算与世界回写的可重建规格。目标是代码丢失时能重建世界遭遇进入战斗、玩家/AI 行动、战斗结束写回的功能主线。

不覆盖：具体每个技能数值、每个 AI profile 的调参、战斗 UI 美术。

## 模块拓扑

```text
GameRuntimeFacade / BattleSessionFacade
  -> BattleRuntimeModule
    -> BattleState / BattleUnitState / BattleTimelineState
    -> BattleUnitFactory / BattleMovementService / BattleMovementQueryService
    -> BattleSkillExecutionOrchestrator / BattleDamageResolver / BattleHitResolver
    -> BattleAiService / BattleAiDecisionEngine
    -> BattleRuntimeLootResolver / BattleSkillMasteryService
  -> BattleMapPanel / BattleBoardController / BattleHudAdapter
  -> GameRuntimeBattleWritebackService
```

`BattleSessionFacade` 是世界 runtime 到 battle runtime 的窄门面；`BattleRuntimeModule` 拥有战斗规则与 state；UI 只展示 state 和发送 command。

## Setup 输入

BattleRuntime setup 必须注入：CharacterManagementModule、typed skill defs、enemy templates、enemy AI brains、EncounterRosterBuilder、EquipmentDropService、typed item defs、equipment instance id allocator、battle special profile registry snapshot、skill catalog typed view。

不要从 GameRuntimeFacade 或 UI dictionary 临时扫描技能/敌人内容；内容读取走 GameContentCatalog typed snapshot。

## BattleState 契约

BattleState 至少包含：battle id、map size、terrain/cells、units dictionary、timeline、round/tick、winner/ended 状态、loot/contribution、modal state、overlay data。BattleUnitState 包含 unit id、faction、coord、footprint、resources、attributes、known skills、equipment/weapon projection、status effects、AI brain/template id。

`known_skill_level_map`、damage resistance map 等 dictionary 边界必须严格类型化投影；runtime 内部优先 typed owner。

## 战斗启动流程

1. World encounter anchor 命中后，GameRuntimeFacade 设置 battle save lock 并保存 world/player。
2. BattleSessionFacade 构建 battle start context：encounter id/name、enemy roster template、terrain profile、active map 信息、party snapshot。
3. BattleRuntimeModule 生成 BattleState：地形、玩家单位、敌人单位、timeline、初始 selection。
4. 进入 BattleLoading modal；生成完成后进入 BattleStartConfirm，timeline frozen。
5. 玩家确认后清 pending prompt、unfreeze timeline，战斗 tick 开始推进。

失败时必须清 active battle id/name、pending generation、modal、battle state，并释放 save lock。

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

BattleTimelineState 拥有单位行动顺序、frozen、当前 actor、tick 推进。确认开始前 frozen；确认后 TU 每秒按固定速率推进。等待、移动、施法、持续效果 tick 都必须通过 timeline driver 更新，不能由 UI 改 tick。

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

## AI

BattleAiService 读取 BattleAiBrainDef、单位 snapshot、技能 affordance、位置评分，输出 BattleAiDecision。AI 执行必须通过与玩家相同的 BattleCommand/ActionPlan 提交通道，受 mutation guard 约束；AI 不应直接修改 BattleState 绕过规则。

## 战利品、成长与回写

战斗结束后：

- BattleRuntimeLootResolver 根据 defeated enemy template/drop table 生成 item/equipment loot，装备实例走 allocator。
- BattleSkillMasteryService/ContributionLedger 生成 mastery/progression delta。
- GameRuntimeBattleWritebackService 提交 loot、成长、任务/成就事件，更新 party state。
- 世界侧根据 winner 处理 encounter：single 删除，settlement encounter 增长/压制。
- 清 battle save lock，恢复世界 UI。

## UI 边界

BattleMapPanel/BattleBoardController 负责棋盘、HUD、hover preview、技能槽、command dock。它们可请求 preview，但不能改 BattleState。所有按钮信号经 WorldMapSystem/RuntimeProxy/BattleSessionFacade。

## 回归入口

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_world_map_battle_start_confirm_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.cs
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
- `BattleSkillExecutionOrchestrator`：技能执行主编排。
- `BattleDamageResolver` / `BattleHitResolver` / `BattleSaveResolver`：命中、豁免、伤害。
- `BattleTimelineDriver`：TU、ready actor、turn start/end。
- `BattleAiService`：AI decision -> command。
- `BattleRuntimeLootResolver`、`BattleSkillMasteryService`、`BattleContributionLedger`：结算。

这些 helper 优先保持 plain C# typed surface；Godot payload 只在最外层 UI/headless adapter 投影。

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
- command preview 不应 mutate state；mutation guard 比较执行前后 fingerprint。
- AI 输出 command 后通过正式 command committer 执行。
- AI failure policy 处理无动作、非法动作、fallback wait。

## 实现级补充：战斗结束与回写事务

1. BattleRuntime 判定 winner 和 ended。
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
