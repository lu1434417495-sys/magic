# 施法时间系统与时间静滞实现方案

最近核对：`2026-06-12`

## 状态

- 当前状态：`Active Implementation Plan`
- 范围：非瞬发施法读条、读条取消/中断、后续时间静滞状态族。
- 本文是编码前方案，已按当前 Godot 4.6 C# 主线重写。旧 GDScript 路径、裸 `Dictionary` 业务态、`TYPE_*` 字符串命令常量、owner 层撒网 hook、`time_stasis_cell_locks` 格锁方案不再采用。

## 目标拆分

### M1：只实现 typed pending cast

M1 交付非瞬发主动技能：

1. `CombatSkillDef.casting_time_tu > 0` 的技能开始后进入 runtime-only pending cast。
2. 时间轴继续推进；pending cast 到点后由 `BattleSkillExecutionOrchestrator` 的 no-cost 入口自动结算。
3. 玩家可通过 headless/runtime 命令取消自己的 pending cast。
4. pending cast 会因 HP 损失、死亡、离场、forced movement、control status 等通用中断条件失败。

M1 明确不交付：

- `time_stasis`、`time_slow`、`time_reverberation` 的完整状态语义。
- 静滞目标过滤、静滞解控技能、boss/elite 降级、防连控。
- AI 读条评分和敌方读条策略。
- battle item command。

M1 只保留一个通用状态语义占位：`BattleStatusSemanticTable.BlocksPendingCast(statusId)`。unit 级扫描放在 `BattleCastingTimeService.HasBlockingPendingCastState(unitState)`，不要把整单位扫描塞进 semantic table。未来 M2 的 `time_stasis` 可接入 casting service 的 unit 级判断，但 M1 不实现时间静滞。

### M2：实现 temporal 状态族

M2 单独交付：

- `time_stasis`：冻结个人时间线。
- `time_slow`：降低个人进度获取率。
- `time_reverberation`：防连控余波。
- temporal-only 解控技能、目标过滤、elite/boss 降级、豁免 degree、per-tag save bonus。

这样拆分后，M1 的代码面集中在 command/runtime/timeline/orchestrator/turn resolver/snapshot/headless，避免把读条系统和完整时间控制体系绑成一个难审的大 PR。

## 硬约束

- `BattleRuntimeModule.PreviewCommand(...)` / `IssueCommand(...)` 的 preview-first 门禁不拆。
- 技能效果仍由 `BattleSkillExecutionOrchestrator` 执行；timeline 只推进 pending cast，不直接执行技能 effect。
- `BattleDamageResolver` 继续只做规则计算，不持有 runtime callback。
- 不新增旧存档兼容、旧 payload 兼容、静默字段注入或 fallback migration。当前战斗中保存不支持，pending cast 不进入正式 save payload。
- 新业务态保持 C# typed owner，不把 runtime 业务链退回裸 `Godot.Collections.Dictionary`。

## 当前归属

必须先读的上下文单元：

- CU-13 / CU-14：`CombatSkillDef`、`SkillContentRegistry`、`BattleSaveContentRules`。
- CU-15：`BattleRuntimeModule`、`BattleTimelineDriver`、`BattleSkillExecutionOrchestrator`、`BattleRuntimeSkillTurnResolver`。
- CU-16：`BattleCommand`、`BattlePreview`、`BattleUnitState`、`BattleState`、`BattleStatusSemanticTable`、`BattleSaveResolver`、`BattleDamageResolver`、`BattleHitResolver`。
- CU-18 / CU-21：HUD、snapshot、text command、text renderer。
- CU-19：focused regression。

实现 PR 如果改变 runtime 关系、推荐读集或 CU 职责，必须同步更新 `docs/design/project_context_units.md`。只改本讨论文档不改上下文索引。

## M1 数据模型

### CombatSkillDef

文件：`scripts/player/progression/CombatSkillDef.cs`

新增字段：

```csharp
[Export]
public int casting_time_tu { get; set; }

[Export]
public int casting_maintenance_dc { get; set; }

[Export]
public int casting_spell_control_dc { get; set; }

[Export]
public StringName pending_cast_binding_mode { get; set; } = "";
```

新增读取方法：

```csharp
public int GetEffectiveCastingTimeTu(int skillLevel)
public int GetEffectiveCastingMaintenanceDc(int skillLevel)
public int GetEffectiveCastingSpellControlDc(int skillLevel)
public PendingCastBindingModeKind GetEffectivePendingCastBindingMode(int skillLevel)
```

`level_overrides` 支持同名 key，读取顺序沿用现有 `GetLevelOverride(...)` 合并规则。`casting_time_tu <= 0` 表示瞬发，走现有路径。

`pending_cast_binding_mode` 通过 enum/typed helper 解析，不在 runtime 中直接比较裸字符串。允许值：

| 值 | 语义 |
| --- | --- |
| `hard_anchor` | 任一绑定单位目标死亡、离场、变为不可作用，整次读条中断 |
| `soft_anchor` | 失效目标从 `TargetUnitIds` 剔除；全部失效才中断 |
| `ground_bind` | 只绑定开始时的 `TargetCoords`，单位目标失效不影响落地 |

### BattlePendingCastState

新增文件：`scripts/systems/battle/core/BattlePendingCastState.cs`

M1 直接使用 typed runtime state，不使用 pending cast 裸 `Dictionary`：

```csharp
internal sealed class BattlePendingCastState
{
    public StringName SkillId { get; init; }
    public StringName VariantId { get; init; }
    public BattleTargetMode Route { get; init; }
    public IReadOnlyList<StringName> TargetUnitIds { get; private set; }
    public IReadOnlyList<Vector2I> TargetCoords { get; init; }
    public int StartedAtTu { get; init; }
    public int BaseCastingTimeTu { get; init; }
    public int RemainingCastProgress { get; set; } // casting_time_tu * 100
    public SkillCostTransaction CastTransaction { get; init; }
    public BattleSpellControlResult SpellControlContext { get; init; }
    public int CastSequence { get; init; }
    public StringName SourceUnitId { get; init; }
    public int LastMaintenanceCheckpointHp { get; set; }
    public Vector2I StartedCoord { get; init; }
    public bool StartedAlive { get; init; }

    public BattlePendingCastState Clone()
}
```

`EstimatedCompleteAtTu` 不进 `BattlePendingCastState`，只由 HUD/snapshot 按当前进度实时估算。排序规则定义为：

```text
同一 timeline step 内完成的 pending casts 按 CastSequence 升序结算。
不同 step 自然由 current_tu 分批，不需要亚 tick completed_at_tu。
```

### SkillCostTransaction

新增文件：`scripts/systems/battle/runtime/SkillCostTransaction.cs`

M1 只承载读条已扣除的持久资源和冷却：

```csharp
internal sealed class SkillCostTransaction
{
    public StringName SkillId { get; init; }
    public int PaidMp { get; init; }
    public int PaidStamina { get; init; }
    public int PaidAura { get; init; }
    public int CooldownTu { get; init; }
    public PendingCastRefundPolicy RefundPolicy { get; init; }

    public static SkillCostTransaction ForCooldownOnly(StringName skillId, int cooldownTu)
}
```

`PendingCastRefundPolicy`、`PendingCastBindingModeKind` 放在 `scripts/systems/battle/_interop/BattleTypedEnums.cs`，或与 `BattlePendingCastState` 同文件但保持 internal typed enum；不要用字符串分支承载运行时策略。

M1 不支持 identity charges、misfortune gate、black contract、材料成本。这些留到后续 `SkillCostTransaction` 扩展。

### BattleUnitState

文件：`scripts/systems/battle/core/BattleUnitState.cs`

新增 runtime-only 字段，不加入 `ToDictFields`：

```csharp
internal BattlePendingCastState PendingCast { get; private set; }
public bool turn_casting_exhausted;
```

新增 helper：

```csharp
internal bool IsCasting()
internal void BeginPendingCast(BattlePendingCastState pendingCast)
internal BattlePendingCastState ClearPendingCast()
internal void ClearCastingTurnFlags()
```

`clone()` deep-copy `PendingCast`、`turn_casting_exhausted`、`per_battle_charges`、`per_turn_charges`、`per_turn_charge_limits`、`fumble_protection_used`。`ToDictionary()` / `FromDictionary(...)` 必须继续拒绝这些 runtime-only 字段。

`turn_casting_exhausted` 只约束当前 `UnitActing` 行动窗口。`BattleRuntimeModule.PreviewCommand(...)` / `IssueCommand(...)` 在 skill 与 change-equipment 分支前检查该标志：只允许 move / wait / cancel-cast，其余命令返回 typed block reason。`_end_active_turn(...)` 和下次单位进入行动窗口时都调用 `ClearCastingTurnFlags()`，避免标志跨回合残留。

### BattleState

文件：`scripts/systems/battle/core/BattleState.cs`

新增 runtime-only 字段：

```csharp
internal int next_cast_sequence = 1;
```

新增 helper：

```csharp
internal int AllocateCastSequence()
```

M1 删除旧方案中的 `time_stasis_cell_locks`。静滞是 M2 事项；即使 M2 实现，也优先通过 live unit footprint + `HasTimeStasis(unit)` 阻断位移，不维护平行格锁字典。

### BattleCommand

文件：`scripts/systems/battle/_interop/BattleTypedEnums.cs`、`scripts/systems/battle/core/BattleCommand.cs`

新增：

```csharp
internal enum BattleCommandKind
{
    ...
    CancelCast,
}
```

通过 `BattleTypedNames.ToCommandKind(...)` / `ToStringName(...)` 映射 `"cancel_cast"`。不恢复 `TYPE_CANCEL_CAST` 字符串常量。

## M1 内容校验

文件：`scripts/player/progression/SkillContentRegistry.cs`

在 `AppendCombatProfileValidationErrors(...)` 增加：

- `casting_time_tu`、`casting_maintenance_dc`、`casting_spell_control_dc` 必须为非负 int。
- `casting_time_tu > 0` 时必须是 `SkillContentRegistry` 现有 `TuGranularity` 常量的倍数，不引用 `BattleTimelineDriver` 的 private const。
- `casting_time_tu > 0` 时 `pending_cast_binding_mode` 必须可解析为 `hard_anchor` / `soft_anchor` / `ground_bind`。
- `casting_time_tu > 0` 时拒绝：
  - `special_resolution_profile_id != ""`
  - `target_selection_mode == "random_chain"`
  - 任一 effect / cast variant effect 的 `effect_type == "charge"`
  - 任一 effect / cast variant effect 的 `effect_type == "path_step_aoe"`
  - 任一 self relocation：`effect_type == "forced_move"` 且 `forced_move_mode in ["jump", "blink"]` 且作用对象是自己
  - `fumble_protection_curve` 非空或 `GetFumbleProtectionLimit(level) > 0`
- `level_overrides` 中的同名字段必须通过同一套校验。
- M1 暂不允许 identity-granted per-turn/per-battle charge 技能、misfortune gated 技能、black-contract-push 变体配置 `casting_time_tu > 0`。

M1 不从 `params` 读取 `incompatible_with_casting_time`。如果后续确实需要内容作者手动 opt-out，新增 typed `[Export] bool incompatible_with_casting_time` 到 `CombatSkillDef`，并走同一套校验。

M2 的 temporal tag、per-tag save bonus、temporal-only 解控校验不放进 M1。

## M1 Runtime 设计

### CastingTimeService

新增文件：`scripts/systems/battle/runtime/BattleCastingTimeService.cs`

职责只覆盖读条生命周期：

```csharp
internal sealed class BattleCastingTimeService
{
    public void Setup(BattleRuntimeModule runtime)
    public void Dispose()

    public bool IsCastingTimeSkill(SkillDef skillDef, BattleUnitState unitState)
    public string GetPreviewBlockReason(
        BattleUnitState unitState,
        SkillDef skillDef,
        BattleCommand command
    )
    public bool BeginCastingTimeSkill(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattleEventBatch batch
    )
    public bool CancelPendingCast(StringName unitId, BattleEventBatch batch)
    public void AdvancePendingCasts(int tuDelta, BattleEventBatch batch)
    public void CompletePendingCasts(BattleEventBatch batch)
    public void ReconcilePendingCastsAfterCommand(BattleEventBatch batch)
    public void ReconcilePendingCastsAfterTimelineStep(BattleEventBatch batch)
    public bool HasBlockingPendingCastState(BattleUnitState unitState)
    public PendingCastResolutionContext BuildPendingCastResolutionContext(
        BattleUnitState unitState,
        BattlePendingCastState pendingCast
    )
}
```

不再提供 `CaptureTemporalStatusSnapshot(...)` / `EmitTemporalStatusTransitions(...)`。这些是 M2 的 temporal status 服务职责。

### Pending Cast Reconciliation

M1 不在所有伤害/状态 owner 上撒 hook。改为集中对账：

- 在 pending cast 开始时，`BattlePendingCastState` 或 service 内部记录施法者 HP、coord、alive、unit-present、bound targets 快照。
- `BattleRuntimeModule.IssueCommand(...)` 每个 terminal path 都调用 `ReconcilePendingCastsAfterCommand(...)`，包括 `modal_requested` 早返回路径。
- `BattleTimelineDriver.ApplyTimelineStep(...)` 在 status / terrain / barrier 伤害结算之后、pending cast 推进和完成之前调用 `ReconcilePendingCastsAfterTimelineStep(...)`。
- 对账时只扫描正在读条的单位，数量通常极小，不进 AI 热循环。

对账规则：

- 施法者死亡、离场、从 `BattleState.units` 移除：清除 pending cast，不返还，不新启动冷却。
- 施法者坐标变化：按 forced movement 中断，清除 pending cast，不返还，启动冷却。
- 施法者获得 blocking cast 状态：中断，清除 pending cast，不返还，启动冷却。
- M1 的 blocking cast 状态必须显式枚举，默认包括 `petrified`、`madness`、`frozen`、`staggered`、`meteor_concussed`；不要直接复用 `IsMovementBlocked(...)`，因为 `pinned` / `rooted` / `tendon_cut` 只限制位移，不应自动打断读条。
- HP loss `<= 3`：不触发维持检定。
- HP loss `4..15`：DC 12。
- HP loss `> 15`：DC 15。
- 若 `casting_maintenance_dc > 0`，使用技能配置覆盖动态 DC。
- 维持检定失败：中断，清除 pending cast，不返还，启动冷却。
- HP loss 基线为“自读条开始或上次触发维持检定以来的累计损失”。`LastMaintenanceCheckpointHp` 在开始读条时设为当前 HP；只有触发维持检定后才重置为当前 HP。多次小伤害会累计到下一次检定，治疗不把 checkpoint 抬高。
- 绑定目标失效：
  - `hard_anchor`：中断。
  - `soft_anchor`：剔除失效目标；全部失效才中断。
  - `ground_bind`：不受单位目标失效影响。

这样未来新增伤害来源、DOT、terrain tick、barrier overflow、meteor、charge、fall damage 都会被统一对账覆盖。

### 开始读条

`BattleRuntimeModule.IssueCommand(...)` 的 skill 分支保持 preview-first。`BattleSkillExecutionOrchestrator._handle_skill_command(...)` 在现有 block reason 后、meteor/普通分支前插入：

```csharp
if (_runtime.CastingTimeService.IsCastingTimeSkill(skillDef, activeUnit))
{
    _runtime.CastingTimeService.BeginCastingTimeSkill(activeUnit, command, batch);
    return;
}
```

begin 流程：

1. 解析 unit / ground cast variant，复用现有 validation 锁定目标快照。
2. 执行 readied-cast 专用 spell control preflight。
3. ordinary failure：
   - 不创建 pending cast。
   - 不启动冷却。
   - 扣除 `max(1, skill_ap_cost)` AP，但不扣 MP/stamina/aura。
   - 设置 `turn_casting_exhausted = true`。
   - AP 归零时结束当前行动窗口；否则本行动窗口只允许 move / wait。
4. critical failure：
   - 不创建 pending cast。
   - 扣除当前 AP 的 50%，至少 1，最多扣到 0。
   - 用 `SkillCostTransaction.ForCooldownOnly(skillId, cooldownTu)` 创建 zero-cost transaction，`cooldownTu` 来自现有 `GetEffectiveResourceCostValues(...)`。
   - 先 `ConsumeTurnCooldownDelta(activeUnit)`，再 `StartSkillCooldownFromTransaction(activeUnit, transaction, batch)`。
   - 设置 `turn_casting_exhausted = true`。
5. success / critical success：
   - 调用 `BattleRuntimeSkillTurnResolver.ConsumeSkillCostsWithoutCooldown(...)`。
   - 只验证 AP 是否满足技能成本；不扣 AP，不把 AP 写入 transaction。
   - MP/stamina/aura 立即扣除。
   - 创建 `SkillCostTransaction`。
   - `BattleState.AllocateCastSequence()` 分配序号。
   - `BattleUnitState.BeginPendingCast(...)`。
   - `activeUnit.has_taken_action_this_turn = true`，`activeUnit.is_resting = false`，`activeUnit.current_ap = 0`，结束当前行动窗口。

ordinary failure 收 AP 成本，避免读条技能成为免费重掷门。

### 取消读条

`BattleCommandKind.CancelCast` 是 runtime interrupt command。

- `PreviewCommand(...)` 使用 `command.unit_id` 查 pending caster，不要求该单位是 active unit。
- `IssueCommand(...)` 在 state/null、battle-ended、modal-state 检查之后处理 `CancelCast`，并放在 `UnitActing` / active-unit gate 之前；不要允许晋升选择 modal 期间或战斗结束后 cancel。
- 现有 `IssueCommand(...)` 把 battle-ended 合在 `PhaseKind != UnitActing` 检查里；实现时需要为 `CancelCast` 拆出独立 battle-ended / modal 早返回，不能只在现有 active-unit gate 后插分支。
- 只允许玩家可控 manual party unit 取消自己的 pending cast。
- cancel 不恢复已结束行动窗口。
- cancel 不启动冷却。
- cancel 返还策略采用“有沉没成本”，避免硬锚失效前抢跳 cancel 成为最优微操：
  - 返还已付 MP/stamina/aura 的 50%，向下取整。
  - 不返还 AP。
  - report entry 写明 `refund_policy = "half_persistent_costs"`。

### Timeline 推进

`BattleTimelineDriver.ApplyTimelineStep(...)` 顺序改为：

1. `current_tu += tuDelta`
2. status phase
3. terrain timed effects
4. layered barrier durations
5. `CastingTimeService.ReconcilePendingCastsAfterTimelineStep(batch)`
6. `CastingTimeService.AdvancePendingCasts(tuDelta, batch)`
7. `CastingTimeService.CompletePendingCasts(batch)`
8. `CollectTimelineReadyUnits(batch, tuDelta)`
9. `SortReadyUnitIdsByActionPriority()`

`AdvancePendingCasts(...)` 只减少 `RemainingCastProgress`。M1 没有 `time_slow`，所以进度为 `tuDelta * 100`。

`CompletePendingCasts(...)` 每个完成项结算前必须查 live state：

- 施法者仍在 `BattleState.units`。
- 施法者仍 alive。
- 施法者仍有 pending cast。
- 施法者没有 blocking cast 状态。
- pending cast 绑定目标按 binding mode 清理后仍合法。

同一 step 内，完成项按 `CastSequence` 升序逐项结算；每结算一项后立刻重新对账 remaining pending casts，避免 cast A 杀死/控制 cast B 后 B 仍完成。

status phase、terrain tick、barrier overflow 等 step 内伤害必须在完成前经过第 5 步对账；不能依赖完成后的收尾对账，否则最后一个 tick 受伤的读条会绕过维持检定。

`CollectTimelineReadyUnits(...)` 必须跳过正在读条的单位：

- 不加入 ready 队列。
- 不推进 `action_progress`；读条已经消耗该单位个人时间线，完成瞬间不能连环 ready。
- 不推进 stamina recovery，避免读条期间白拿恢复。
- 不逐 step 推进 cooldown；已有 cooldown 在完成、中断、cancel、critical failure 启动新冷却前通过现有 `ConsumeTurnCooldownDelta(unitState)` 惰性消费。
- 不触发 turn start 状态、不重置 per-turn charges。
- 不调用 AI / control status turn resolution。

### Skill Turn Resolver

文件：`scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`

新增：

```csharp
public SkillCostTransaction ConsumeSkillCostsWithoutCooldown(
    BattleUnitState activeUnit,
    SkillDef skillDef,
    CombatCastVariantDef castVariant,
    BattleEventBatch batch
)

public void RefundSkillCostTransaction(
    BattleUnitState activeUnit,
    SkillCostTransaction transaction,
    PendingCastRefundPolicy policy,
    BattleEventBatch batch
)

public void StartSkillCooldownFromTransaction(
    BattleUnitState activeUnit,
    SkillCostTransaction transaction,
    BattleEventBatch batch
)
```

所有完成、中断、critical failure 启动新冷却前固定执行：

```text
ConsumeTurnCooldownDelta -> StartSkillCooldownFromTransaction
```

`ConsumeTurnCooldownDelta(unitState)` 是现有 API，已经负责消费 elapsed cooldown delta 并同步 anchor，且不触发 turn-start status。M1 不新增 `ConsumeCooldownDeltaWithoutTurnStart(...)` / `SyncCooldownAnchorToCurrentTu(...)`，避免只 sync 不 consume 造成静默丢进度。

### Pending Cast Completion

`BattleSkillExecutionOrchestrator` 新增：

```csharp
public void ResolvePendingCast(
    BattleUnitState sourceUnit,
    PendingCastResolutionContext context,
    BattleEventBatch batch
)
```

该入口不得调用：

- `ConsumeSkillCosts(...)`
- `ResolveUnitSpellControlAfterCostResult(...)`
- `ResolveGroundSpellControlAfterCostResult(...)`
- AP / range / LOS validation

允许复用 effect-only helper。读条完成不重新检查射程、视线或路径；绑定目标有效性只由 `PendingCastBindingModeKind` 处理。

## M1 Snapshot / Headless

`BattleHudAdapter`、`GameRuntimeSnapshotBuilder`、`GameTextSnapshotRenderer` 输出只读 pending cast 摘要：

- `skill_id`
- `variant_id`
- `remaining_cast_progress`
- `estimated_complete_at_tu`
- `can_cancel`
- `runtime_only = true`

text renderer 输出：

```text
[PENDING_CAST] unit=<id> skill=<skill_id> remaining=<progress> eta=<estimate> runtime_only=true
```

`GameTextCommandRunner` 新增：

```text
battle cancel_cast <unit_id>
```

经 `GameRuntimeFacade -> BattleSessionFacade -> BattleRuntimeModule` 触发 `BattleCommandKind.CancelCast`。

## M1 文件改动清单

新增：

- `scripts/systems/battle/core/BattlePendingCastState.cs`
- `scripts/systems/battle/runtime/BattleCastingTimeService.cs`
- `scripts/systems/battle/runtime/SkillCostTransaction.cs`
- `tests/battle_runtime/runtime/run_casting_time_core_regression.cs`
- `tests/battle_runtime/runtime/run_casting_time_interruption_regression.cs`
- `tests/text_runtime/commands/run_casting_time_text_command_regression.cs`

修改：

- `scripts/player/progression/CombatSkillDef.cs`
- `scripts/player/progression/SkillContentRegistry.cs`
- `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- `scripts/systems/battle/core/BattleCommand.cs`
- `scripts/systems/battle/core/BattlePreview.cs`
- `scripts/systems/battle/core/BattleState.cs`
- `scripts/systems/battle/core/BattleUnitState.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleTimelineDriver.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/game_runtime/BattleSessionFacade.cs`
- `scripts/utils/GameTextSnapshotRenderer.cs`

## M1 测试计划

新增 focused runner：

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_core_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_interruption_regression.cs
godot --headless -s res://tests/text_runtime/commands/run_casting_time_text_command_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
godot --headless -s res://tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs
```

核心断言：

- `casting_time_tu <= 0` 技能仍走瞬发路径。
- begin pending cast 扣 MP / stamina / aura，不扣 AP，不启动冷却；成功开始后 `current_ap = 0`。
- ordinary spell control failure 不建 pending、不扣 MP/stamina/aura、不启动冷却，但消耗 AP 并只允许 move / wait。
- critical failure 不建 pending，扣 AP 惩罚，并按固定顺序启动冷却。
- cancel 清 pending，按 half persistent costs 返还，不启动冷却，不恢复行动窗口。
- cancel 在他人行动窗口可执行，但在 modal / battle-ended 状态不可执行。
- pending cast 单位尝试 `ChangeEquipment` 被拒绝；ordinary spell control failure 后本行动窗口也只允许 move / wait，不允许换装。
- timeline 推进按 `RemainingCastProgress` 完成，不读 estimate。
- 同一 step 多个 pending cast 按 `CastSequence` 结算。
- cast A 完成后杀死/控制 cast B，cast B 同 step 不再结算。
- 同 step DOT / terrain / barrier 伤害发生在读条完成 tick；维持检定失败时该 step 不完成。
- casting unit 跨 action threshold 时不入 ready，`action_progress` 和 stamina recovery 不推进；已有冷却只在完成/中断/cancel 时经 `ConsumeTurnCooldownDelta` 惰性减少，且 `last_turn_tu` 不双算。
- HP loss `<=3` 不检定，`4..15` DC 12，`>15` DC 15；多次小伤害累计到阈值后检定；失败中断并启动冷却。
- forced movement、死亡、离场、blocking cast 状态会中断 pending cast。
- 命令触发伤害并进入 `modal_requested` 早返回时，仍执行 pending cast 对账。
- terrain tick、charge path/fall、meteor、barrier overflow 等任意 HP loss 经集中对账触发中断，不依赖各 owner 手写 hook。
- `hard_anchor` / `soft_anchor` / `ground_bind` 正确。
- `BattleUnitState.ToDictionary()` 不包含 pending cast；`clone()` 保留 pending cast。
- pending cast 存在时 battle save lock 仍阻止保存。
- `battle cancel_cast <unit_id>` 文本命令完整通过 facade 到 runtime，并在 snapshot/report 中可见。

实现 PR 完成前至少跑：

```powershell
dotnet build magic.csproj
python tests/run_regression_suite.py
godot --headless -s res://tests/battle_runtime/runtime/run_battle_runtime_attack_check_smoke.cs
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_core_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_interruption_regression.cs
godot --headless -s res://tests/text_runtime/commands/run_casting_time_text_command_regression.cs
```

不要把 battle simulation / balance runner 混进默认“全量测试”。

## M2 Temporal 状态设计修正

M2 不沿用旧方案中的 `time_stasis_cell_locks` 和 owner 撒网 hook。

### TemporalStatusService

新增 `BattleTemporalStatusService`，职责只覆盖 temporal 状态：

- `HasTimeStasis(unit)`
- `HasTimeSlow(unit)`
- `GetActionProgressRatePercent(unit)`
- `GetCastProgressRatePercent(unit)`
- `HasTemporalCastBlock(unit)`
- `CanTargetTimeStasis(...)`
- `ApplyTemporalReleaseEffects(...)`

Temporal status 不放在 `params` 字典里做业务态。优先扩展 typed fields：

- `BattleStatusEffectState.status_tags: IReadOnlyList<StringName>`
- `BattleStatusEffectState.save_bonus_by_tag: IReadOnlyDictionary<StringName, int>`
- `CombatEffectDef.effect_tags: IReadOnlyList<StringName>`

如果资源层仍以 `params` 导入，导入边界必须在 `SkillContentRegistry` / status construction 处转成 typed fields；runtime owner 不直接回读 `params`。

### time_stasis

语义：

- 单位不加入 ready 队列。
- 单位不推进 action progress。
- 单位不推进 stamina recovery。
- 单位不推进 cooldown；进入 stasis 时先消费一次 stasis 前的 cooldown delta，再冻结 anchor，避免丢失已有冷却进度。
- 单位不触发 turn start、per-turn charges reset、turn start status。
- 单位不结算普通 DOT/HOT/terrain tick。
- 单位不降低其他状态 duration。
- `time_stasis` 自身 duration 按战场时间减少。
- 位移/forced movement 对 stasis 单位 fail closed。

占用：

- 不维护额外格锁。
- 仍由 live unit footprint 占用格子。
- `BattleGridService` 和位移 owner 通过 `HasTimeStasis(unit)` 阻断移动、推拉、交换、跳跃等位移。

释放：

- `natural_expire` / `dispel` 添加或刷新 `time_reverberation`。
- `death` / `leave_battle` / `battle_end` / `scene_unload` / `cleanup` 不添加 reverberation。

### time_slow

`time_slow` 不改 `action_progress` 字段尺度。rate 由 `BattleTemporalStatusService` 从状态推导，不存到 `BattleUnitState`。

因为 `tuDelta * 50 / 100` 会产生小数，M2 必须新增 runtime-only 余数累加器：

```csharp
internal int action_progress_rate_remainder;
internal int cast_progress_rate_remainder;
```

carry 规则：

```text
raw = tuDelta * ratePercent + remainder
gain = raw / 100
remainder = raw % 100
```

测试必须证明 10 个 5-TU tick 在 50% slow 下总进度为 25，而不是逐 tick 截断成 20。

### save degree / per-tag bonus

`BattleSaveResult` 增加 typed `Degree`：

```csharp
public BattleSaveDegreeKind Degree { get; init; }
```

degree 规则：

- total < DC 为 failure，否则 success。
- natural 1 降一级，natural 20 升一级。
- 最低 `CriticalFailure`，最高 `CriticalSuccess`。

per-tag save bonus 不新建并行合成规则。它必须在 `BattleSaveResolver` 内部扩展现有 private `GetStatusSaveBonus(...)` 路径，并与 `save_bonus` / `control_save_bonus` 使用同一套 `Math.Max` 合成语义。不要设计成外部 service 调用 private method。测试覆盖 `time_reverberation + willpower_save_bonus_up` 共存。

### boss / elite 保护

M2 不只看 `boss_target`。使用现有 `BattleExecutionRules.IsEliteOrBossTarget(unitState)` 作为统一来源：

- elite 或 boss 不获得 `time_stasis`。
- failed / critical failed stasis 结果降级为 `time_slow`。
- critical success 仍无效果。

### M2 测试补充

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_time_stasis_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_save_resolver_regression.cs
godot --headless -s res://tests/battle_runtime/state_schema/run_battle_status_effect_state_schema_regression.cs
```

必须覆盖：

- 同 tick cast A 静滞/击杀 cast B，B 不 ready、不完成。
- stasis 下 action progress、cooldown、stamina recovery、DOT/HOT、status duration、shield duration 的冻结语义。
- time_slow 多 tick 累计精度。
- per-tag save bonus 与现有 save bonus/control save bonus 共存。
- `BattleStatusEffectState.status_tags` / `save_bonus_by_tag` 更新 schema 契约，不添加旧 payload fallback。
- elite/boss 降级。
- natural expire / dispel 添加 reverberation，death/cleanup 不添加。
- temporal-only 解控技能拒绝混入伤害、治疗、位移或普通状态。

## M3 / 后续

- AI 读条评分、威胁区读条风险、敌方读条技能。
- 读条 UI 的正式取消按钮。
- battle item command，以及 spell control failure 后允许非攻击性道具。
- 如果后续需要保存战斗中 pending cast，再设计 `BattlePendingCastState` 的 save payload；不得添加旧 payload fallback。

## 合并门槛

- 没有新增旧 schema fallback 或旧 string helper。
- 新命令走 `BattleCommandKind`，不是 `TYPE_*` 字符串常量。
- pending cast 是 typed runtime state，不是裸 `Dictionary` 业务态。
- `BattleDamageResolver` 无 runtime callback。
- M1 没有 temporal 状态撒网 hook。
- 中断检测通过集中 reconciliation 覆盖所有 HP loss / movement / status 后果。
- 同 tick 完成顺序和 live-state 重验有回归。
- cancel 与 hard-anchor 失效成本策略一致，不制造抢跳 cancel 微操最优解。
- 实现 PR 同步更新 `docs/design/project_context_units.md`，仅在实际 runtime owner / 推荐读集发生变化时更新。
