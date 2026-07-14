# 战斗运行时模块可重建规格说明

更新日期：`2026-07-10`

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

装备耐久归零后的投影刷新由 `BattleDamageResolver` 在完整 `ResolveEffects(...)` / `ResolveAttackEffects(...)` 结束后统一触发，并委托 `BattleEquipmentAbilityRuntimeService.RefreshEquipmentProjectionAfterDurabilityDestruction(...)` 重建装备来源、清理失效目标标记和传播 changed unit id。直接装备耐久 action 在 commit 返回 destroyed 后调用同一 helper；`BattleSkillExecutionOrchestrator._apply_equipment_durability_result(...)` 只负责日志与 changed unit report。

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

## 源码级重建清单：战斗运行时文件与 surface

以下清单用于弥补纯设计文档遗漏：重建时必须逐项恢复这些 owner 文件、公开/内部 typed surface 与职责边界；若实现拆文件，仍要保留等价 API 与行为。

### `scripts/systems/battle/runtime/BattleRuntimeModule.cs`

- `internal static class BattleRuntimeDictionaryOptions`
- `internal static bool ReadBool(GDictionary source, string key, bool fallback = false)`
- `internal BattleEndOptions(bool commitProgression = false)`
- `public sealed class BattleStartFailureSnapshot`
- `internal static BattleStartFailureSnapshot FromDictionary(GDictionary source)`
- `internal GDictionary ToDictionary()`
- `public partial class BattleRuntimeModule : RefCounted`
- `public BattleRuntimeModule()`
- `internal void _setup_special_profile_runtime()`
- `internal BattleStartFailureSnapshot GetLastStartFailureSnapshot() =>`
- `internal bool _validate_battle_units_for_start(GArray units, string side_label) =>`
- `internal void _build_ai_action_plans()`
- `internal void _ensure_ai_action_plan_for_unit(BattleUnitState unit_state)`
- `internal int _get_ai_move_query_cost(StringName unit_id, Vector2I _from_coord, Vector2I to_coord)`
- `internal StringName _resolve_formal_terrain_profile_id(GDictionary terrain_data)`
- `public BattleEventBatch advance(int tick_count)`
- `internal bool _use_discrete_timeline_ticks()`
- `internal void _apply_timeline_step(BattleEventBatch batch, int tu_delta)`
- `internal void _resolve_timeline_status_phase(BattleEventBatch batch, int tu_delta)`
- `internal void _collect_timeline_ready_units(BattleEventBatch batch, int tu_delta)`
- `internal bool _apply_stamina_recovery(BattleUnitState unit_state, int tu_delta)`
- `internal int _get_unit_constitution(BattleUnitState unit_state)`
- `public BattlePreview PreviewCommand(BattleCommand command)`
- `public BattleEventBatch IssueCommand(BattleCommand command)`
- `internal string _get_battle_interaction_block_message()`
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
- `internal void AppendResultReportEntry(BattleEventBatch batch, GDictionary result) =>`
- `internal void AppendReportEntry(BattleEventBatch batch, GDictionary report_entry) =>`
- `internal void ClearDefeatedUnit(BattleUnitState unit_state, BattleEventBatch batch = null) =>`
- `internal GVector2IArray sort_coords(GArray target_coords) => _sort_coords(target_coords);`
- `internal GVector2IArray sort_coords(GVector2IArray target_coords) => _sort_coords(target_coords);`
- `internal bool is_unit_effect(CombatEffectDef effect_def) => _is_unit_effect(effect_def);`
- `internal int GetUnitSkillLevel(BattleUnitState unit_state, StringName skill_id) =>`
- `internal void _initialize_battle_metrics()`
- `internal void _record_turn_started(BattleUnitState unit_state)`
- `internal void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)`
- `internal void _record_skill_success(BattleUnitState unit_state, StringName skill_id)`
- `internal void _record_unit_defeated(BattleUnitState unit_state)`
- `public void dispose()`
- `internal bool _place_units(GArray units, GArray spawn_coords, bool is_ally) =>`
- `internal void _clear_spawn_placed_units(GBattleUnitArray placed_units, bool is_ally)`
- `internal bool _place_spawn_unit_at_anchor(BattleUnitState unit_state, Vector2I coord)`
- `internal StringName _resolve_spawn_side_from_coords(GArray spawn_coords)`
- `internal StringName _get_opposite_spawn_side(StringName spawn_side)`
- `internal bool _coord_matches_spawn_side(Vector2I coord, StringName spawn_side)`
- `internal int _get_long_edge_side_axis_value(Vector2I coord) =>`
- `internal int _get_long_edge_side_extent() =>`
- `internal int _score_spawn_anchor(BattleUnitState unit_state, Vector2I coord, int preferred_index)`
- `internal int _get_spawn_anchor_edge_clearance(BattleUnitState unit_state, Vector2I coord)`
- `internal int _get_spawn_anchor_center_bias(BattleUnitState unit_state, Vector2I coord)`
- `internal int _get_move_cost_for_unit_target(BattleUnitState unit_state, Vector2I target_coord)`
- `internal int _get_move_path_cost(BattleUnitState unit_state, GVector2IArray anchor_path)`
- `internal int _get_status_move_cost_delta(BattleUnitState unit_state)`
- `internal int _get_available_move_points(BattleUnitState unit_state)`
- `internal bool _is_normal_movement_locked(BattleUnitState unit_state)`
- `internal int _get_unit_hp_max(BattleUnitState unit_state) =>`
- `internal int _get_unit_stamina_max(BattleUnitState unit_state) =>`
- `internal GStringNameArray _normalize_target_unit_ids(BattleCommand command)`
- `internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)`

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

- `internal sealed class BattleSkillExecutionOrchestrator`
- `public static ChainDamageParameters FromEffect(CombatEffectDef effectDef)`
- `internal void Setup(BattleRuntimeModule runtime)`
- `internal void DisposeRuntime()`
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
- `internal void _shuffle_random_chain_pool(GArray chain_pool)`
- `internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)`
- `internal bool _is_multi_unit_skill(SkillDefinition skillDefinition)`
- `internal void _apply_equipment_durability_result(BattleUnitState target_unit, AttackEffectResolutionResult result, BattleEventBatch batch)`
- `internal GVector2IArray _get_line_coords(Vector2I from, Vector2I to)`
- `internal bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)`
- `internal IReadOnlyList<BattleUnitState> _collect_units_in_coords_typed(GVector2IArray effect_coords)`
- `internal bool _is_unit_effect(CombatEffectDef effect_def)`
- `internal bool _is_terrain_effect(CombatEffectDef effect_def)`
- `private StringName ResolveEffectTargetFilter(SkillDefinition skillDefinition, CombatEffectDefinition effectDefinition)`
- `internal int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)`
- `internal string _format_skill_variant_label(SkillDefinition skillDefinition, CombatCastVariantDefinition castVariant)`

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
- `internal bool CheckBattleEnd(BattleEventBatch batch)`
- `internal int CountLivingUnits(IEnumerable<StringName> unitIds)`
- `internal void EndActiveTurn(BattleEventBatch batch)`
- `internal void ActivateNextReadyUnit(BattleEventBatch batch)`
- `internal void SortReadyUnitIdsByActionPriority()`
- `internal bool IsLeftReadyUnitHigherPriority(StringName leftUnitId, StringName rightUnitId)`
- `internal int GetUnitTurnOrderAttribute(BattleUnitState unitState, StringName attributeId)`
- `internal int GetUnitTurnOrderActionPoints(BattleUnitState unitState)`
- `internal GStringNameArray GetUnitsInOrder()`

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

### `scripts/systems/battle/core/BattleUnitState.cs`

- `public partial class BattleUnitState : RefCounted`
- `internal static GStringNameArray CreateDefaultUnlockedCombatResourceProjection() =>`
- `internal static bool IsValidCombatResourceId(StringName resourceId) =>`
- `internal static StringName ToStringName(BattleWeaponProfileKind kind)`
- `internal static BattleWeaponProfileKind ToWeaponProfileKind(StringName value)`
- `internal static StringName ToStringName(BattleWeaponGripKind kind)`
- `internal static BattleWeaponGripKind ToWeaponGripKind(StringName value)`
- `public BattleUnitState()`
- `internal bool HasPendingCast() => pending_cast != null;`
- `internal bool IsCasting() => is_alive && pending_cast != null;`
- `internal void SetPendingCast(BattlePendingCastState pendingCast)`
- `internal BattlePendingCastState ClearPendingCast()`
- `internal void ClearCastingTurnFlags()`
- `public void SetAnchorCoord(Vector2I anchor_coord)`
- `public void RefreshFootprint()`
- `public bool OccupiesCoord(Vector2I target_coord)`
- `public bool HasMovementTag(StringName tag)`
- `public bool SetBodySizeCategory(StringName category)`
- `public void NormalizeBodySizeProjection()`
- `public bool HasStatusEffect(StringName status_id)`
- `public bool HasShield()`
- `public int GetAuraMax()`
- `public void SyncDefaultCombatResourceUnlocks()`
- `public bool HasCombatResourceUnlocked(StringName resource_id)`
- `internal int GetKnownSkillLevelTyped(StringName skillId, int fallback = 0)`
- `internal bool HasKnownSkillLevelTyped(StringName skillId)`
- `internal int GetCooldownTyped(StringName skillId, int fallback = 0)`
- `internal void SetCooldownTyped(StringName skillId, int value)`
- `internal void SetCooldownsTyped(IReadOnlyDictionary<StringName, int> values)`
- `internal int GetPerBattleChargeTyped(StringName chargeKey, int fallback = 0)`
- `internal bool HasPerBattleChargeTyped(StringName chargeKey)`
- `internal void SetPerBattleChargeTyped(StringName chargeKey, int value)`
- `internal int GetPerTurnChargeTyped(StringName chargeKey, int fallback = 0)`
- `internal bool HasPerTurnChargeTyped(StringName chargeKey)`
- `internal void SetPerTurnChargeTyped(StringName chargeKey, int value)`
- `internal int GetPerTurnChargeLimitTyped(StringName chargeKey, int fallback = 0)`
- `internal bool HasPerTurnChargeLimitTyped(StringName chargeKey)`
- `internal void SetPerTurnChargeLimitTyped(StringName chargeKey, int value)`
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
- `internal WeaponDice GetWeaponOneHandedDiceTyped()`
- `internal WeaponDice GetWeaponTwoHandedDiceTyped()`
- `internal WeaponDice GetActiveWeaponDiceTyped()`
- `public bool UnlockCombatResource(StringName resource_id)`
- `public void SetUnlockedCombatResourceIds(GStringNameArray resource_ids)`
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
