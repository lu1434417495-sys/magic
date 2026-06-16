# 施法时间系统与时间静滞实现对齐方案

最近核对：`2026-06-17`（可行性审查修订：WP1 death_ward 特例、M2 baseline 去重、维持检定 seam 命名、timeline 时序自查；架构加固修订：见「架构加固决策」A1–A6，含 `current_hp` 封装、命令对账单一收口、effect 核心共享、服务调用方向、fail-closed 白名单、preview 读取纪律;测试用例设计：见「测试用例设计」节,含 5 runner 用例矩阵、seam 依赖、A1–A6 覆盖映射;覆盖审查修订：修正 boss/elite「degree 降级」误增量(布尔 save 门已满足)、M2 测试多数已覆盖勿重写、snapshot/text 格式漂移、can_cancel 派生自 control_mode、AI4 条件化、identity-charge 经查证非 gap（learn_source 已全拒））

## 状态

- 当前状态：`Implementation Alignment / Hardening Plan`（关键设计岔路已于 `2026-06-16` 定稿，见下「已决关键设计」与「当前实现差距」）。
- 范围：非瞬发施法读条、读条取消/中断、敌方/AI 读条基础支持、时间静滞状态族加固。
- 当前主线已存在 `BattleCastingTimeService`、`BattlePendingCastState`、`SkillCostTransaction`、`BattleTemporalStatusService` 等实现，但仍停留在被本文件否决的旧做法上（详见「当前实现差距」）。本文件不是从零编码方案，而是把审查结论落成架构、代码实现和测试验收合同。
- 旧 GDScript 路径、裸 `Dictionary` 业务态、`TYPE_*` 字符串命令常量、owner 层撒网 hook、`time_stasis_cell_locks` 格锁方案不采用。

## 已决关键设计（2026-06-16）

实现前曾有三处开放岔路，现已定稿，作为硬合同：

1. **damage ledger 收口**：新增 `BattleUnitState.ApplyHpDelta(int loss)` 作为唯一掉血提交点。该 helper 扣减 `current_hp`，并在受伤单位有 pending cast 且 `loss > 0` 时累加 `DamageSinceMaintenanceCheck`。所有现存直写 `current_hp` 的掉血点（`BattleDamageResolver.ApplyDamageToTargetResult`、DOT/terrain tick、orchestrator 直写、barrier overflow、charge/fall/meteor）一律改走它。`ApplyHpDelta` 是 data-local 纯方法，`BattleDamageResolver` 不持有 runtime callback。治疗不经扣减分支，自然不减累计。**不采用 HP 快照差值**。
2. **维持检定走 `BattleSaveResolver`**：维持检定本质是 d20 save，复用 `BattleSaveResolver` 已有的 `BattleSaveContext.WithSaveRollOverride` / `SaveRollOverrides` 注入 seam（注意：`save_roll_override` 是 `BattleDamageResolver` / `BattleBarrierLayerState` 的 payload key，`self_save_roll_override` 是 `BattleStatusEffectState` 字段，都不是 `BattleSaveResolver` 上的入口；真正可复用的 seam 是 `BattleSaveContext`）、天然 1/20 degree（`ResolveSaveDegree` 已存在）。casting service 不再直调 `TrueRandomSeedService.RandiRange`。M1 维持检定是 willpower ability check，只用 willpower modifier，不接 per-tag save bonus（与 WP2 草图 `saveBonus = 0` 一致）。测试通过 `WithSaveRollOverride` 固定成功/失败路径。
3. **AI delayed-utility 线性模型（M1 最低）**：`score = projected_value * delay_factor * anchor_mult`，其中 `delay_factor = clamp(1 - 0.02 * casting_time_tu, 0.3, 1)`，`anchor_mult = hard_anchor ? 0.6 : soft_anchor ? (alive_targets / total_targets) : 1`（`ground_bind` 取 1，按落点价值评估）。`0.02`、下限 `0.3`、`0.6` 设为可调常量。M1 只打分 + 可发起，不做 AI cancel。

## 架构加固决策（2026-06-17）

架构审查暴露出「不变量靠约定守、收口哲学不一致、服务耦合面过宽」三类结构债，以下六条作为硬合同，凌驾本文其余叙述：

A1. **`current_hp` 封装,不再靠 grep 守约定**：`current_hp` 从 `public int` 改为 `public int current_hp { get; private set; }`。战斗内一切扣血只经 `ApplyHpDelta(loss)`，一切回血只经新增 `ApplyHeal(gain)`（治疗路径迁入），构造/反序列化经 `BattleUnitState` 内部 init helper（factory / `FromDictionary` / sim 用）。grep 守卫降级为「防御性回归」而非「唯一防线」——漏写在编译期即不可能。文档不再把 grep 等同于强不变量。详见 WP1a。

A2. **命令对账单一收口,与 damage 收口同哲学**：不在 `IssueCommand` 的每个 `modal_requested` / 早返回分支插 `ReconcilePendingCastsAfterCommand`。改为 `IssueCommand` 外层包裹 `IssueCommandCore` + `finally { ReconcilePendingCastsAfterCommand(batch); }`（或等价 dispatch wrapper），使对账无法被任何 return 分支漏掉。新增早返回分支不需要也不允许各自插桩。详见 WP1b。

A3. **orchestrator 抽共享 effect 核心,禁止双路径漂移**：瞬发路径与 pending completion 必须复用同一个「纯效果结算」内部入口（`ResolveSkillEffectsCore` 之类），差异只在外层 preflight（cost / spell-control / AP / range / LOS / precast-special）。`ResolvePendingCast` 不得复制效果结算逻辑。该抽取是 WP4/WP5 的前置 WP，见 WP0b。

A4. **服务调用方向无环,CastingTimeService 依赖收窄**：调用图钉死为单向——`RuntimeModule/Orchestrator → CastingTimeService → {SkillTurnResolver(冷却), SaveResolver(维持检定), Orchestrator(completion 效果)}`，禁止 `SkillTurnResolver → CastingTimeService`。冷却 owner 仍是 `BattleRuntimeSkillTurnResolver`；CastingTimeService 只是其调用方，不得自行实现冷却。CastingTimeService 对 module 的依赖收窄为它实际需要的子服务句柄（`_skill_turn_resolver` / `_skill_execution_orchestrator` / save 入口），不以 `WeakReference<BattleRuntimeModule>` 作为「想 reach 什么就 reach」的万能句柄；新增跨服务调用必须在 owner 表登记方向。

A5. **未枚举成本默认 fail-closed**：`CollectCastingUnsupportedCostKinds` 是单一真值源,语义为白名单——**只有显式建模为可安全返还/冷却的 typed 成本才放行,其余一律视为 unsupported 并拒绝开始读条**。casting time 在 M1 不是 orthogonal feature,它与 charge / identity / misfortune / black-contract / special profile 互斥。任何新增成本种类的 PR 必须显式评估与读条的兼容性（见合并门槛 checklist），否则默认被拒。

A6. **discriminated-union-by-flag 的读取纪律**：`BattlePreview` / AI score input 凡读 `hit_preview` / `damage_preview` 前必须先 gate on `!starts_pending_cast`；pending preview 的 hit/damage 投影必须为空,并有测试锁死。长期可演进为 typed 子类型（记为 M3 候选）。

### 实现批次

- 决策 2 让 M1 维持检定依赖 `BattleSaveResolver`，会顺带带出 M2 的 `BattleSaveDegreeKind` / per-tag bonus 路径。**M1 与 M2 不再完全可拆，按同一实现批次推进**，避免维持检定先接一套临时随机源、M2 再返工。
- 决策 1 的 `ApplyHpDelta` 是横切重构：必须有 schema/回归覆盖确认没有遗漏的掉血源直写 `current_hp`，否则 ledger 漏统计。这同时满足「terrain/charge/meteor/barrier overflow 任意 HP loss 经集中 ledger 触发中断」的验收断言。

## 当前实现差距（2026-06-16 核对）

主线 baseline 与本合同的实质差距，落地 PR 必须收敛：

| 合同项 | 当前代码 | 差距 |
| --- | --- | --- |
| damage ledger | `BattlePendingCastState.LastMaintenanceCheckpointHp` + `ShouldInterruptForHpLoss` 用快照差值 | 删除快照字段，改 `DamageSinceMaintenanceCheck` + `LastMaintenanceCheckTu` + `ApplyHpDelta` 收口 |
| 维持检定随机源 | `BattleCastingTimeService` 直调 `TrueRandomSeedService.RandiRange(1,20)` | 改走 `BattleSaveResolver` + override seam |
| `SkillCostTransaction` | 缺 `UnsupportedCostKinds`、`RefundPolicy` | 补两字段并在 begin 时 fail closed |
| `CastOriginCoord` | `BattlePendingCastState` 只有 `StartedCoord` | 新增锁定原点字段 |
| `BattlePreview` pending 语义 | 0 处 pending 字段 | 补 `StartsPendingCast` 等 5 字段并清空瞬发命中/伤害投影 |
| AI delayed-utility | 仅 `BattleAiMutationGuard` clone 读 pending；无评分 | `BattleAiScoreInput` + `BattleAiScoreService` 接入线性模型 |
| snapshot/text | `BuildPendingCastSnapshot` 缺 `estimated_complete_at_tu` / `can_cancel` / `runtime_only` | 补字段 + 文档 text 行格式 |
| `GetTemporalRemovalReason` | 未实现 typed removal reason | 移除入口加 typed reason |
| focused 测试 | 仅 `run_magic_backlash_regression` / `run_time_stasis` 等少数 | 按 M1 测试计划补 5 个 runner |

实际服务方法名与本文档目标名不同（现为 `TryHandleCastingSkillStart` / `ReconcilePendingCasts` / `AdvancePendingCasts` / `CompleteReadyPendingCasts`）。文档沿用目标契约名，落地按实际差异修改，不机械新增重复文件。

## 审查结论与处理口径

| 问题 | 处理口径 |
| --- | --- |
| 不应禁止敌方读条 | M1 明确支持玩家、敌人、AI 都可开始 pending cast；只是不交付 AI 主动取消和复杂威胁区策略。 |
| HP 快照差值无法覆盖治疗、多源小伤害和同 tick 伤害 | M1 改为 pending-cast damage ledger / counter；通过 `BattleUnitState.ApplyHpDelta(loss)` 单一收口统一累计 `DamageSinceMaintenanceCheck`，维持检定消费累计值。治疗不减累计。 |
| preview 仍像瞬发技能一样展示命中/伤害 | `BattlePreview` 必须有 pending-cast 语义：`starts_pending_cast`、`casting_time_tu`、`binding_mode`、锁定目标摘要；不得把未完成读条展示成已提交伤害。 |
| completion 调用 ground precast special effect 会重复/错位 | pending completion 禁止调用 `ApplyGroundPrecastSpecialEffects`；地面目标使用开始读条时锁定的 coords / origin。 |
| target binding 只看 missing/dead 不够 | 增加 typed binding validator，检查 alive/present/team/effect target filter；不重查 AP/range/LOS。 |
| hidden cost / 特殊 profile 只靠内容校验不够 | 内容校验拒绝，runtime transaction preflight 也必须 fail closed；未被 transaction 建模的成本不得进入读条。 |
| temporal 状态已在主线存在，旧文档仍写成未来项 | M2 改为 baseline hardening：统一 owner、冻结锚点、状态移除原因、测试补强。 |
| 随机维持检定不可测 | 维持检定走 `BattleSaveResolver`，复用其 `BattleSaveContext.WithSaveRollOverride` 注入 seam（非 `save_roll_override` payload key）；测试通过 override 固定结果，不依赖真实随机。 |
| snapshot/text schema 与实现漂移 | 文档固定只读摘要合同，测试锁住 `runtime_only`、ETA、cancel 可用性。 |
| battle save lock、clone、AI mutation guard 未端到端覆盖 | M1 测试必须覆盖 pending cast 存在时的保存阻断、clone 保留 runtime state、schema 不落盘。 |

## 目标拆分

### M1：typed pending cast 合同收紧

M1 交付非瞬发主动技能的稳定 runtime 合同：

1. `CombatSkillDef.casting_time_tu > 0` 的技能开始后进入 runtime-only pending cast。
2. pending cast 支持玩家单位、敌方单位和 AI 发起。
3. 时间轴继续推进；pending cast 到点后通过 no-cost / no-control / no-AP 入口自动结算。
4. 玩家可通过 headless/runtime 命令取消自己可控单位的 pending cast。
5. pending cast 会因 HP loss 维持失败、死亡、离场、forced movement、blocking control status、绑定目标失效而失败。
6. preview、snapshot、AI score input 必须把读条当作延迟结算行为，而不是瞬发技能。

M1 明确不交付：

- AI 主动取消读条、敌方读条高级策略、威胁区重定位、基于敌方打断概率的高级规划。
- battle item command。
- 战斗中 pending cast 保存 payload。

M1 不禁止敌方读条。敌方/AI 的最低合同是：AI 可以看到 `starts_pending_cast` 预览事实，能按延迟价值对读条技能打分并发起读条；AI cancel 策略留到 M3。

### M2：temporal 状态族 baseline hardening

M2 不是纯新增未来项；当前主线已有 temporal baseline，需要统一并补强：

- `time_stasis`：冻结个人时间线。
- `time_slow`：降低个人行动/读条进度获取率。
- `time_reverberation`：防连控余波。
- temporal-only 解控、目标过滤、elite/boss 降级、per-tag save bonus、状态移除原因。

M2 不沿用 `time_stasis_cell_locks`，也不把 temporal 语义拆散到各 owner 的 ad-hoc hook。

### M3：后续增强

- AI cancel 策略、读条风险评估、威胁区读条策略、打断/保护读条的战术规划。
- 读条 UI 的正式取消按钮。
- battle item command，以及 spell control failure 后允许非攻击性道具。
- 如需战斗中保存 pending cast，另行设计 typed save payload；不得添加旧 payload fallback。

## 硬约束

- `BattleRuntimeModule.PreviewCommand(...)` / `IssueCommand(...)` 的 preview-first 门禁不拆。
- 技能效果仍由 `BattleSkillExecutionOrchestrator` 执行；timeline 只推进 pending cast，不直接执行 skill effect。
- `BattleDamageResolver` 继续只做规则计算，不持有 runtime callback。
- 不新增旧存档兼容、旧 payload 兼容、静默字段注入或 fallback migration。pending cast 是 runtime-only state，不进入正式 save payload。
- 新业务态保持 C# typed owner，不把 runtime 业务链退回裸 `Godot.Collections.Dictionary`。
- 读条 completion 不重查 AP/range/LOS，不重新执行 spell control，不重复扣费。
- 未被 `SkillCostTransaction` 建模的资源、charge、identity、misfortune、black contract、特殊 profile 等成本或副作用必须 fail closed。
- 实现 PR 如果改变 runtime 关系、推荐读集或 CU 职责，必须同步更新 `docs/design/project_context_units.md`。仅修改本文档不需要更新上下文索引。

## 当前归属

必须先读的上下文单元：

- CU-13 / CU-14：`CombatSkillDef`、`SkillContentRegistry`、`BattleSaveContentRules`。
- CU-15：`BattleRuntimeModule`、`BattleTimelineDriver`、`BattleSkillExecutionOrchestrator`、`BattleRuntimeSkillTurnResolver`、`BattleCastingTimeService`、`BattleTemporalStatusService`。
- CU-16：`BattleCommand`、`BattlePreview`、`BattleUnitState`、`BattleState`、`BattleStatusSemanticTable`、`BattleSaveResolver`、`BattleDamageResolver`、`BattleHitResolver`。
- CU-18 / CU-21：HUD、snapshot、text command、text renderer。
- CU-19：focused regression。

## M1 数据模型合同

### CombatSkillDef

文件：`scripts/player/progression/CombatSkillDef.cs`

读条相关字段：

```csharp
[Export]
public int casting_time_tu { get; set; }

[Export]
public int casting_maintenance_dc { get; set; }

[Export]
public int casting_spell_control_dc { get; set; }

[Export]
public StringName pending_cast_binding_mode { get; set; } = "soft_anchor";
```

读取方法保持 typed 入口：

```csharp
public int GetEffectiveCastingTimeTu(int skillLevel)
public int GetEffectiveCastingMaintenanceDc(int skillLevel)
public int GetEffectiveCastingSpellControlDc(int skillLevel)
public PendingCastBindingModeKind GetEffectivePendingCastBindingMode(int skillLevel)
```

`level_overrides` 支持同名 key，读取顺序沿用现有 override 合并规则。`casting_time_tu <= 0` 表示瞬发，走现有路径。

`pending_cast_binding_mode` 通过 enum/typed helper 解析，runtime 不直接比较裸字符串。允许值：

| 值 | 语义 |
| --- | --- |
| `soft_anchor` | 失效目标从 `TargetUnitIds` 剔除；全部失效才中断。 |
| `hard_anchor` | 任一绑定单位目标死亡、离场、变为不可作用，整次读条中断。 |
| `ground_bind` | 绑定开始时的 `TargetCoords` / origin，单位目标失效不影响落地。 |

`PendingCastBindingModeKind` 必须有 `Unknown = 0`，解析失败在内容校验和 runtime preflight 中都 fail closed。

### BattlePendingCastState

文件：`scripts/systems/battle/core/BattlePendingCastState.cs`

pending cast 是 typed runtime state，不使用裸 `Dictionary`：

```csharp
internal sealed class BattlePendingCastState
{
    public StringName SourceUnitId { get; init; }
    public StringName SkillId { get; init; }
    public StringName VariantId { get; init; }
    public BattleTargetMode TargetMode { get; init; }
    public PendingCastBindingModeKind BindingMode { get; init; }
    public IReadOnlyList<StringName> TargetUnitIds { get; private set; }
    public IReadOnlyList<Vector2I> TargetCoords { get; init; }
    public Vector2I StartedCoord { get; init; }
    public Vector2I CastOriginCoord { get; init; }
    public int StartedAtTu { get; init; }
    public int BaseCastingTimeTu { get; init; }
    public int RemainingCastProgress { get; set; } // casting_time_tu * 100
    public SkillCostTransaction CostTransaction { get; init; }
    public BattleSpellControlMetadata SpellControlMetadata { get; init; }
    public ulong CastSequence { get; init; }
    public int DamageSinceMaintenanceCheck { get; private set; }
    public int LastMaintenanceCheckTu { get; set; }

    internal void AccrueMaintenanceDamage(int applied);   // ledger 累计归本类自己
    internal void ConsumeMaintenanceDamage(int atTu);     // 清零 + 更新 LastMaintenanceCheckTu

    public BattlePendingCastState Clone();
}
```

ledger 累计语义收归 `BattlePendingCastState`（点 8）：`DamageSinceMaintenanceCheck` set 私有，只由 `AccrueMaintenanceDamage` / `ConsumeMaintenanceDamage` 改，`BattleUnitState.ApplyHpDelta` 调用前者、维持检定调用后者。

`EstimatedCompleteAtTu` 不进 `BattlePendingCastState`，只由 HUD/snapshot 按当前进度和 temporal rate 实时估算。

排序规则：

```text
同一 timeline step 内完成的 pending casts 按 CastSequence 升序结算。
不同 step 自然由 current_tu 分批，不需要亚 tick completed_at_tu。
```

`CastSequence` 是 runtime-only 全局单调计数（`BattleState.next_cast_sequence`）。M1 pending cast 不入存档,故无需持久化;**若 M3 引入战斗中保存 pending cast,`next_cast_sequence` 必须随 typed save payload 一起持久化或在 load 后按现存 pending casts 重导出**,否则跨存档完成顺序不确定。此约束记入 M3。

### SkillCostTransaction

文件：`scripts/systems/battle/core/SkillCostTransaction.cs`

M1 transaction 只承载读条已扣除且能安全返还/冷却的 typed 成本：

```csharp
internal sealed class SkillCostTransaction
{
    public StringName SkillId { get; init; }
    public int SkillLevel { get; init; }
    public int ApCost { get; init; }
    public int MpCost { get; init; }
    public int StaminaCost { get; init; }
    public int AuraCost { get; init; }
    public int CooldownTurns { get; init; }
    public int PrecastDamage { get; init; }
    public PendingCastRefundPolicy RefundPolicy { get; init; }
    public IReadOnlyList<StringName> UnsupportedCostKinds { get; init; }
}
```

`SkillCostTransaction` 沿用现有 core 文件和字段名。M1 约束是：`ApCost` 只用于开始读条前的 AP 可用性和 failure 惩罚，不在成功读条开始时扣除；`MpCost` / `StaminaCost` / `AuraCost` 是已扣除且可按策略返还的持久成本；`CooldownTurns` 只在完成、中断或 critical failure 时按固定顺序启动；`UnsupportedCostKinds` 非空时不得开始 pending cast。runtime 不能假设内容校验已经拦住全部非法配置。

### BattleUnitState

文件：`scripts/systems/battle/core/BattleUnitState.cs`

runtime-only 字段不得加入 `ToDictFields`：

```csharp
internal BattlePendingCastState PendingCast { get; private set; }
public bool turn_casting_exhausted;
```

`current_hp` 封装（决策 A1）：

```csharp
public int current_hp { get; private set; }

internal void ApplyHpDelta(int loss);   // 战斗扣血唯一入口
internal void ApplyHeal(int gain);      // 战斗回血唯一入口
internal void InitHp(int value);        // 构造/反序列化/sim 专用，clamp 到 [0, hp_max]
```

helper 合同：

```csharp
internal bool IsCasting();
internal void BeginPendingCast(BattlePendingCastState pendingCast);
internal BattlePendingCastState ClearPendingCast();
internal void ClearCastingTurnFlags();
```

`ApplyHpDelta(loss)` 是唯一战斗扣血提交点：`current_hp = Math.Max(current_hp - loss, 0)`，并在 `PendingCast != null && applied > 0` 时调用 `PendingCast.AccrueMaintenanceDamage(applied)` 累计 ledger（累计语义归 pending state 自己，`BattleUnitState` 不直接写 `PendingCast.DamageSinceMaintenanceCheck` 字段）。它是 data-local 纯方法，不回调 runtime，不引用 `BattleDamageResolver`。`ApplyHeal(gain)` 走非累计分支（治疗不减 ledger）。`InitHp` 只用于构造/反序列化/sim，不进 ledger。因 `current_hp` set 私有，战斗内任何 `current_hp -=` 在编译期即不可能，grep 守卫退为防御性回归。`Clone()` 同时 deep-copy `DamageSinceMaintenanceCheck` / `LastMaintenanceCheckTu`。

`Clone()` 必须 deep-copy `PendingCast` 和 `turn_casting_exhausted`，AI mutation guard / preview clone 不能漏掉读条状态。`ToDictionary()` / `FromDictionary(...)` 必须继续拒绝 runtime-only 字段。

`turn_casting_exhausted` 只约束当前 `UnitActing` 行动窗口。skill 与 change-equipment 分支前检查该标志：只允许 move / wait / cancel-cast，其余命令返回 typed block reason。`_end_active_turn(...)` 和下次单位进入行动窗口时都调用 `ClearCastingTurnFlags()`。

### BattleState / BattleCommand

`BattleState` 保持 runtime-only sequence：

```csharp
internal ulong next_cast_sequence = 1;
internal ulong AllocateCastSequence();
```

`BattleCommandKind.CancelCast` 通过 `BattleTypedNames.ToCommandKind(...)` / `ToStringName(...)` 映射 `"cancel_cast"`。不恢复 `TYPE_CANCEL_CAST` 字符串常量。

## M1 内容校验

文件：`scripts/player/progression/SkillContentRegistry.cs`

`AppendCombatProfileValidationErrors(...)` 必须校验：

- `casting_time_tu`、`casting_maintenance_dc`、`casting_spell_control_dc` 为非负 int。
- `casting_time_tu > 0` 时必须是 `SkillContentRegistry` 现有 `TuGranularity` 常量的倍数，不引用 `BattleTimelineDriver` private const。
- `casting_time_tu > 0` 时 `pending_cast_binding_mode` 必须可解析为 `soft_anchor` / `hard_anchor` / `ground_bind`。
- `level_overrides` 中同名字段走同一套校验。

`casting_time_tu > 0` 时拒绝：

- `special_resolution_profile_id != ""`
- `target_selection_mode == "random_chain"`
- 任一 effect / cast variant effect 的 `effect_type == "charge"`
- 任一 effect / cast variant effect 的 `effect_type == "path_step_aoe"`
- 任一 ground relocation / jump / blink / precast-special-effect 依赖，尤其是 pending completion 会重复触发或依赖当前 caster coord 的配置。
- `fumble_protection_curve` 非空或 `GetFumbleProtectionLimit(level) > 0`
- identity-granted per-turn/per-battle charge 技能
- misfortune gated 技能
- black-contract-push 变体
- 任何 transaction 尚未 typed 建模的额外资源、材料、代价或副作用

M1 不从 `params` 读取 `incompatible_with_casting_time`。如果后续需要内容作者手动 opt-out，新增 typed `[Export] bool incompatible_with_casting_time` 到 `CombatSkillDef`，并走同一套校验。

## M1 Runtime 设计

### BattleCastingTimeService 职责

文件：`scripts/systems/battle/runtime/BattleCastingTimeService.cs`

service 只负责读条生命周期和读条对账，不吞并 orchestrator / turn resolver / damage resolver 的职责：

```csharp
internal sealed class BattleCastingTimeService
{
    public PendingCastPreview BuildPendingCastPreview(...);
    public PendingCastStartResult TryBeginPendingCast(PendingCastStartRequest request, BattleEventBatch batch);
    public bool CancelPendingCast(StringName unitId, BattleEventBatch batch);
    public void AdvancePendingCasts(int tuDelta, BattleEventBatch batch);
    public void CompletePendingCasts(BattleEventBatch batch);
    public void ReconcilePendingCastsAfterCommand(BattleEventBatch batch);
    public void ReconcilePendingCastsAfterTimelineStep(BattleEventBatch batch);
    public bool HasBlockingPendingCastState(BattleUnitState unitState);
}
```

建议边界：

- `BattleRuntimeModule`：command 门禁、preview-first、battle phase/modal gate、cancel command 路由。
- `BattleSkillExecutionOrchestrator`：正式 skill effect 结算和 pending completion no-cost 入口（两者共享 A3 的纯效果核心）。
- `BattleRuntimeSkillTurnResolver`：成本 transaction、返还、冷却启动（**冷却 owner**）。
- `BattleCastingTimeService`：pending state 生命周期、进度、取消、中断、完成队列、binding reconciliation。
- `BattleTemporalStatusService`：temporal 状态语义和 progress rate。

调用方向契约（决策 A4，无环，禁止反向）：

```text
RuntimeModule ──┐
Orchestrator ───┼─▶ CastingTimeService ─┬─▶ SkillTurnResolver   (冷却启动/消费,单向)
                │                       ├─▶ SaveResolver        (维持检定)
                │                       └─▶ Orchestrator        (completion 纯效果核心)
TimelineDriver ─┘
```

- 禁止 `SkillTurnResolver → CastingTimeService`。冷却语义只此一份,在 `SkillTurnResolver`;CastingTimeService 是调用方,不得自行实现冷却。
- `CastingTimeService` 不以 `WeakReference<BattleRuntimeModule>` 当万能句柄到处 reach;它依赖的是它真正需要的子服务（`_skill_turn_resolver` / `_skill_execution_orchestrator` / save 入口）。新增任何跨服务调用必须先在本契约登记方向,不得绕过。

### Pending Cast Reconciliation

M1 不在所有伤害/状态 owner 上撒 hook。读条中断通过集中对账和 damage ledger 覆盖：

- ledger 累计统一发生在 `BattleUnitState.ApplyHpDelta(loss)` 收口里：每次掉血 commit 时，若受伤单位有 pending cast 且 `loss > 0`，累加 `DamageSinceMaintenanceCheck`。不在各 owner 手写 hook。
- 治疗不减少该累计值。
- `ReconcilePendingCastsAfterCommand(...)` **单一收口**调用（决策 A2）：`IssueCommand` 外层包 `IssueCommandCore` 并在 `finally` 中调用一次，覆盖所有 return 分支（含 `modal_requested` 早返回）。**不**在各 terminal path 逐个插桩；新增早返回分支不得也不需要单独补对账。
- `ReconcilePendingCastsAfterTimelineStep(...)` 在 status / terrain / barrier 伤害结算之后、pending cast 推进和完成之前调用。
- 对账只扫描正在读条的单位，不进入 AI 热循环。

对账规则：

- 施法者死亡、离场、从 `BattleState.units` 移除：清除 pending cast，不返还，不新启动冷却。
- 施法者坐标变化：按 forced movement 中断，清除 pending cast，不返还，启动冷却。
- 施法者获得 blocking cast 状态：中断，清除 pending cast，不返还，启动冷却。
- blocking cast 状态显式枚举，默认包括 `petrified`、`madness`、`frozen`、`staggered`、`meteor_concussed`；不要直接复用 `IsMovementBlocked(...)`，因为 `pinned` / `rooted` / `tendon_cut` 只限制位移，不应自动打断读条。
- `DamageSinceMaintenanceCheck <= 3`：不触发维持检定。
- `4..15`：DC 12。
- `> 15`：DC 15。
- 若 `casting_maintenance_dc > 0`，使用技能配置覆盖动态 DC。
- 触发维持检定后无论成功失败都消费本轮累计（清零 `DamageSinceMaintenanceCheck` 并更新 `LastMaintenanceCheckTu`）；失败中断、清除 pending cast、不返还、启动冷却。
- 绑定目标失效按 `BindingMode` 处理。

维持检定通过 `BattleSaveResolver` 执行，不在 casting service 内直调 `TrueRandomSeedService`：

- DC 由上文阈值（`4..15` → 12，`>15` → 15）决定，`casting_maintenance_dc > 0` 时用技能配置覆盖。
- save 属性用施法者 willpower modifier；M1 维持检定不叠加 per-tag save bonus（`ResolveAbilityCheckResult` 传 `saveBonus = 0`）。
- 复用 `BattleSaveResolver` 已有的 `BattleSaveContext.WithSaveRollOverride` / `SaveRollOverrides` 注入 seam 固定测试 roll；天然 1/20 degree 走已存在的 `ResolveSaveDegree`。
- 因此维持检定实现复用 M2 已落地的 `BattleSaveDegreeKind` / `ResolveSaveDegree`（degree 已在 baseline），M1、M2 按同一批次推进以保持 save 路径一致。

维持检定随机源必须可测：测试通过 roll override 固定成功/失败，不依赖真实随机。

### Target Binding Validator

binding validator 必须 typed 化，至少覆盖：

- unit present in current state
- alive
- not left battle / not removed
- team / hostility / target filter 仍允许该 effect 作用
- `soft_anchor` 剔除失效目标后，更新 pending state 的 target list
- `hard_anchor` 任一失效即中断
- `ground_bind` 不受单位目标失效影响，但 coords 必须来自开始读条时锁定快照

completion 不重查 AP/range/LOS/path。读条开始时的合法性已经被锁定；completion 只处理绑定目标是否仍可作用。

### 开始读条

`BattleRuntimeModule.IssueCommand(...)` 的 skill 分支保持 preview-first。读条技能由 orchestrator 在普通瞬发执行前切入：

```csharp
if (_runtime.CastingTimeService.IsCastingTimeSkill(skillDef, activeUnit))
{
    _runtime.CastingTimeService.TryBeginPendingCast(request, batch);
    return;
}
```

begin 流程：

1. 解析 unit / ground cast variant，复用现有 validation 锁定目标快照。
2. 构建 pending-cast preview facts，供 UI / AI 使用。
3. 执行 readied-cast 专用 spell control preflight。
4. 内容校验和 transaction preflight 都通过后才能扣资源。
5. 成功 / critical success：
   - 调用 `BattleRuntimeSkillTurnResolver.ConsumeSkillCostsWithoutCooldown(...)`。
   - 只验证 AP 是否满足技能成本；不扣 AP，不把 AP 写入 transaction。
   - MP/stamina/aura 立即扣除。
   - `BattleState.AllocateCastSequence()` 分配序号。
   - `BattleUnitState.BeginPendingCast(...)`。
   - 显式设置 `activeUnit.has_taken_action_this_turn = true`、`activeUnit.is_resting = false`、`activeUnit.current_ap = 0`，不能依赖 `apCost == 0` 的 metrics side effect。
6. ordinary failure：
   - 不创建 pending cast。
   - 不启动冷却。
   - 扣除 `max(1, skill_ap_cost)` AP，但不扣 MP/stamina/aura。
   - 设置 `turn_casting_exhausted = true`。
   - AP 归零时结束当前行动窗口；否则本行动窗口只允许 move / wait / cancel-cast。
7. critical failure：
   - 不创建 pending cast。
   - 扣除 `ceil(当前 AP / 2)`（实现为 `Math.Max((current_ap + 1) / 2, 1)`，即 50% **向上取整**，至少 1，扣到 0 为止）。与现状代码 `BattleRuntimeSkillTurnResolver.cs:703` 一致。
   - 用 cooldown-only transaction 启动冷却。
   - 固定顺序：`ConsumeTurnCooldownDelta(activeUnit)` -> `StartSkillCooldownFromTransaction(...)`。
   - 设置 `turn_casting_exhausted = true`。

ordinary failure 收 AP 成本，避免读条技能成为免费重掷门。

### 敌方 / AI 基础合同

M1 必须允许敌方和 AI 开始 pending cast。

`BattlePreview` / AI score input 至少包含：

```csharp
public bool StartsPendingCast { get; init; }
public int CastingTimeTu { get; init; }
public PendingCastBindingModeKind PendingCastBindingMode { get; init; }
public IReadOnlyList<StringName> LockedTargetUnitIds { get; init; }
public IReadOnlyList<Vector2I> LockedTargetCoords { get; init; }
```

AI M1 scoring 最低要求采用线性 delayed-utility 模型：

```text
score      = projected_value * delay_factor * anchor_mult
delay_factor = clamp(1 - 0.02 * casting_time_tu, 0.3, 1)
anchor_mult  = hard_anchor ? 0.6
             : soft_anchor ? (alive_targets / total_targets)
             : 1            // ground_bind
```

- `0.02`（每 TU 折扣率）、下限 `0.3`、`hard_anchor` 系数 `0.6` 必须是可调常量，不写成魔法数字散落。
- `hard_anchor` 因任一目标失效即中断，给固定低折扣。
- `soft_anchor` 按当前存活/合法目标占比折扣。
- `ground_bind` 主要按地面落点价值评估，不因单位目标死亡直接归零（`anchor_mult = 1`）。
- 评分输入来自 `BattleAiScoreInput`，评分逻辑在 `BattleAiScoreService`；不得把 pending cast 未来伤害当作已提交伤害。读 `damage_preview` 前先 gate on `!starts_pending_cast`（决策 A6）。
- 读条期间 AI 不主动 cancel；cancel 策略留到 M3。

**M1 已知建模偏差(写明,非 bug)**：`delay_factor` 只折扣「延迟到手的价值」,并未建模「施法者被锁 `casting_time_tu` 期间损失的行动机会成本」。若 AI 评分是 per-action 无前瞻,M1 会系统性**略微高估**读条技能。M1 接受该偏差并把它合并近似进 `delay_factor`（`0.02`/TU 的折扣同时承担「延迟」与「锁定损失」两重含义,可调）。tuning 时遇到 AI 过度选择读条,先调 `PendingCastDelayPerTu` / `PendingCastDelayFloor`,不要当成 scoring bug 去追。真正的机会成本/前瞻建模留 M3。

preview 不能把 pending cast 的未来伤害当作当前已提交伤害，也不能让 AI 在同一 tick 同时获得“读条开始”和“瞬发命中”的双重收益。pending preview 的 `hit_preview` 为 `null`、`damage_preview` 为空,并由测试锁死（决策 A6）。

### 取消读条

`BattleCommandKind.CancelCast` 是 runtime interrupt command。

- `PreviewCommand(...)` 使用 `command.unit_id` 查 pending caster，不要求该单位是 active unit。
- `IssueCommand(...)` 在 state/null、battle-ended、modal-state 检查之后处理 `CancelCast`，并放在 `UnitActing` / active-unit gate 之前。
- 不允许晋升选择 modal 期间或战斗结束后 cancel。
- 只允许玩家可控 manual party unit 取消自己的 pending cast。
- cancel 不恢复已结束行动窗口。
- cancel 不启动冷却。
- cancel 返还策略采用“有沉没成本”：
  - 返还已付 MP/stamina/aura 的 50%，向下取整。
  - 不返还 AP。
  - report entry 写明 `refund_policy = "half_persistent_costs"`。

敌方/AI 可读条，但 M1 不提供 AI 主动 cancel。

### Timeline 推进

`BattleTimelineDriver.ApplyTimelineStep(...)` 顺序：

1. `current_tu += tuDelta`
2. status phase
3. terrain timed effects
4. layered barrier durations / overflow
5. `CastingTimeService.ReconcilePendingCastsAfterTimelineStep(batch)`
6. `CastingTimeService.AdvancePendingCasts(tuDelta, batch)`
7. `CastingTimeService.CompletePendingCasts(batch)`
8. `CollectTimelineReadyUnits(batch, tuDelta)`
9. `SortReadyUnitIdsByActionPriority()`

> 落地自查：主线实际序列（`BattleTimelineDriver.cs`）在 `current_tu += tuDelta` 后先 terrain、barrier，再 `ReconcilePendingCasts` → `AdvancePendingCasts` → `ReconcilePendingCasts`（advance 前后各一次，比本文目标序列更稳）→ `CompleteReadyPendingCasts` → `CollectTimelineReadyUnits`，方法实名为 `ReconcilePendingCasts` / `AdvancePendingCasts` / `CompleteReadyPendingCasts`。但**单位 status periodic tick 似乎发生在 `CollectTimelineReadyUnits` 内部，而非主序列靠前的「status phase」**。ledger 时序前提是「status/terrain/barrier 伤害结算之后再 reconcile + 维持检定」，因此落地前必须确认同 step 的 status DOT 伤害在 `ReconcilePendingCasts` 之前已经 commit 过 `ApplyHpDelta`；若 status tick 实际晚于 reconcile，需要把 status 伤害提前，否则同 step status 伤害会漏进当前维持检定窗口、推迟一个 step 才被统计。

`AdvancePendingCasts(...)` 使用 `BattleTemporalStatusService.GetCastProgressRatePercent(unit)`。`time_stasis` 为 0%，`time_slow` 使用 rate + remainder，普通状态为 100%。

`CompletePendingCasts(...)` 每个完成项结算前必须查 live state：

- 施法者仍在 `BattleState.units`。
- 施法者仍 alive。
- 施法者仍有 pending cast。
- 施法者没有 blocking cast 状态。
- pending cast 绑定目标按 binding mode 清理后仍合法。

同一 step 内，完成项按 `CastSequence` 升序逐项结算；每结算一项后立刻重新对账 remaining pending casts，避免 cast A 杀死/控制 cast B 后 B 仍完成。

正在读条的单位：

- 不加入 ready 队列。
- 不推进 `action_progress`。
- 不推进 stamina recovery。
- 不触发 turn start 状态。
- 不重置 per-turn charges。
- 不调用 AI / control status turn resolution。
- 已有 cooldown 只在完成、中断、cancel、critical failure 启动新冷却前通过 `ConsumeTurnCooldownDelta(unitState)` 惰性消费。

### Pending Cast Completion

`BattleSkillExecutionOrchestrator` 提供 no-cost pending completion 入口：

```csharp
public void ResolvePendingCast(
    BattleUnitState sourceUnit,
    PendingCastResolutionContext context,
    BattleEventBatch batch
);
```

该入口不得调用：

- `ConsumeSkillCosts(...)`
- `ResolveUnitSpellControlAfterCostResult(...)`
- `ResolveGroundSpellControlAfterCostResult(...)`
- AP validation
- range / LOS validation
- `ApplyGroundPrecastSpecialEffects`

允许复用 effect-only helper。读条完成不重新检查射程、视线或路径；绑定目标有效性只由 `PendingCastBindingModeKind` 和 binding validator 处理。

ground pending cast 使用开始读条时锁定的 `TargetCoords` / `CastOriginCoord`，不得使用 completion 时 caster 当前 coord 重新推导。

## M1 Snapshot / Headless

`BattleHudAdapter`、`GameRuntimeSnapshotBuilder`、`GameTextSnapshotRenderer` 输出只读 pending cast 摘要：

- `skill_id`
- `variant_id`
- `remaining_cast_progress`
- `remaining_cast_tu`
- `estimated_complete_at_tu`
- `can_cancel`
- `binding_mode`
- `runtime_only = true`

text renderer 输出合同：

```text
[PENDING_CAST] unit=<id> skill=<skill_id> remaining=<progress> eta=<estimate> runtime_only=true
```

`GameTextCommandRunner` 支持：

```text
battle cancel_cast <unit_id>
```

命令经 `GameRuntimeFacade -> BattleSessionFacade -> BattleRuntimeModule` 触发 typed `BattleCommandKind.CancelCast`。

## M1 文件改动清单

当前主线已有部分文件。后续 PR 按实际差异修改，不要机械新增重复文件。

核心文件：

- `scripts/player/progression/CombatSkillDef.cs`
- `scripts/player/progression/SkillContentRegistry.cs`
- `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- `scripts/systems/battle/core/BattleCommand.cs`
- `scripts/systems/battle/core/BattlePreview.cs`
- `scripts/systems/battle/core/BattleState.cs`
- `scripts/systems/battle/core/BattleUnitState.cs`
- `scripts/systems/battle/core/BattlePendingCastState.cs`
- `scripts/systems/battle/core/SkillCostTransaction.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleTimelineDriver.cs`
- `scripts/systems/battle/runtime/BattleCastingTimeService.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- `scripts/systems/battle/rules/BattleTemporalStatusService.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/game_runtime/BattleSessionFacade.cs`
- `scripts/utils/GameTextSnapshotRenderer.cs`

建议新增或拆分 focused tests：

- `tests/battle_runtime/runtime/run_casting_time_core_regression.cs`
- `tests/battle_runtime/runtime/run_casting_time_interruption_regression.cs`
- `tests/battle_runtime/ai/run_casting_time_ai_regression.cs`
- `tests/text_runtime/commands/run_casting_time_text_command_regression.cs`
- `tests/battle_runtime/skills/run_casting_time_content_validation_regression.cs`

现有 `tests/battle_runtime/skills/run_magic_backlash_regression.cs` 中的读条覆盖可以保留，但读条生命周期、AI、snapshot、save lock 不应长期埋在 magic-backlash runner 里。

## M1 测试计划

新增/补强 focused runner：

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_core_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_interruption_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_casting_time_ai_regression.cs
godot --headless -s res://tests/text_runtime/commands/run_casting_time_text_command_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_casting_time_content_validation_regression.cs
godot --headless -s res://tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs
```

核心断言：

- `casting_time_tu <= 0` 技能仍走瞬发路径。
- begin pending cast 扣 MP / stamina / aura，不扣 AP，不启动冷却；成功开始后 `current_ap = 0`，`has_taken_action_this_turn = true`，`is_resting = false`。
- preview 对读条技能输出 `starts_pending_cast`，不输出已提交命中/伤害。
- AI 可以选择敌方读条技能，并按 delayed utility 打分。
- ordinary spell control failure 不建 pending、不扣 MP/stamina/aura、不启动冷却，但消耗 AP 并只允许 move / wait / cancel-cast。
- critical failure 不建 pending，扣 AP 惩罚，并按固定顺序启动冷却。
- cancel 清 pending，按 half persistent costs 返还，不启动冷却，不恢复行动窗口。
- cancel 在他人行动窗口可执行，但在 modal / battle-ended 状态不可执行。
- pending cast 单位尝试 `ChangeEquipment` 被拒绝；ordinary spell control failure 后本行动窗口也只允许 move / wait / cancel-cast。
- timeline 推进按 `RemainingCastProgress` 完成，不读 estimate。
- 同一 step 多个 pending cast 按 `CastSequence` 结算。
- cast A 完成后杀死/控制 cast B，cast B 同 step 不再结算。
- 同 step DOT / terrain / barrier 伤害发生在读条完成 tick；维持检定失败时该 step 不完成。
- casting unit 跨 action threshold 时不入 ready，`action_progress` 和 stamina recovery 不推进；已有冷却只在完成/中断/cancel 时经 `ConsumeTurnCooldownDelta` 惰性减少。
- HP loss `<=3` 不检定，`4..15` DC 12，`>15` DC 15；多次小伤害累计到阈值后检定；治疗不降低累计伤害；失败中断并启动冷却。
- 维持检定经 `BattleSaveResolver`，测试用 `save_roll_override` 固定成功/失败路径。
- forced movement、死亡、离场、blocking cast 状态会中断 pending cast。
- 命令触发伤害并进入 `modal_requested` 早返回时，仍执行 pending cast 对账。
- terrain tick、charge path/fall、meteor、barrier overflow 等任意 HP loss 经 `ApplyHpDelta` 收口的集中 ledger 触发中断，不依赖各 owner 手写 hook；schema/回归确认无直写 `current_hp` 漏网。
- `hard_anchor` / `soft_anchor` / `ground_bind` 正确；binding validator 覆盖 team/filter 失效。
- pending ground completion 不调用 precast special effects，不使用 completion 时 mutable caster coord。
- `BattleUnitState.ToDictionary()` 不包含 pending cast；`Clone()` 保留 pending cast。
- pending cast 存在时 battle save lock 仍阻止保存。
- snapshot/text 输出 `runtime_only=true`、ETA、remaining、`can_cancel`。
- `battle cancel_cast <unit_id>` 文本命令完整通过 facade 到 runtime，并在 snapshot/report 中可见。
- invalid casting-time content 在 registry 校验失败；runtime transaction preflight 对未建模 cost fail closed。

实现 PR 完成前至少跑：

```powershell
dotnet build magic.csproj
python tests/run_regression_suite.py
godot --headless -s res://tests/battle_runtime/skills/run_magic_backlash_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_core_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_casting_time_interruption_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_casting_time_ai_regression.cs
godot --headless -s res://tests/text_runtime/commands/run_casting_time_text_command_regression.cs
```

不要把 battle simulation / balance runner 混进默认“全量测试”。

## 测试用例设计（2026-06-17）

本节把上面的「核心断言」展开成可落地的用例矩阵,标注层级、复用夹具、以及**每个用例依赖的实现 seam**。当前主线仍是 pre-WP baseline,故大量用例「Untestable-until-WP-adds-seam」——矩阵给出的是目标契约,随对应 WP 落地后才能跑通。

### 分层与归属

| Runner | 层 | CU | 夹具 |
| --- | --- | --- | --- |
| `run_casting_time_core_regression` | runtime（`BattleRuntimeModule` 直驱） | CU-15/16, CU-19 | 复用 magic_backlash builders |
| `run_casting_time_interruption_regression` | runtime | CU-15/16, CU-19 | + `DeterministicBattleDamageResolver` + maintenance roll override seam |
| `run_casting_time_ai_regression` | ai | CU-15, CU-19 | 仿 `run_battle_ai_score_context_adapter_regression` |
| `run_casting_time_text_command_regression` | text/headless（`GameTextCommandRunner`） | CU-21, CU-19 | 遵 `tests/text_runtime/README.md` |
| `run_casting_time_content_validation_regression` | 纯 registry | CU-13/14, CU-19 | `SkillContentRegistry` 直调 |
| 扩展 `run_battle_unit_state_schema_contract_regression` | state_schema | CU-16, CU-19 | 既有 runner 加断言 |
| 扩展 `run_battle_save_resolver_regression` | 纯 rule | CU-16, CU-19 | 既有 runner 加 `ResolveAbilityCheckResult` 用例 |

**共享夹具**：现有 casting builders（`BuildCastingTimeSkill / BuildRuntimeWithCastingSkill / BuildCastingTimeCommand / BuildCancelCastCommand / AdvanceTimelineTu`）埋在 `run_magic_backlash_regression.cs`。core+interruption 两个新 runner 都要用 → 按 test skill 规则抽到 `tests/battle_runtime/helpers/CastingTestFixtures.cs`,magic_backlash 改引用,不复制粘贴。

**实现 seam 依赖**（这些用例必须等对应 WP 加 seam 后才能写,不可先写假绿测试）：

| 阻塞的用例 | 缺失 seam（WP） | 当前 baseline |
| --- | --- | --- |
| SCHEMA A1（`ApplyHpDelta`/`ApplyHeal`/`InitHp` 值） | 三个 mutator（WP1a） | `BattleUnitState.cs:190` `public int current_hp;` 公有字段,无 mutator |
| I1/I2/I3/I5（ledger 桶/累计/治疗/全源） | `DamageSinceMaintenanceCheck` + `AccrueMaintenanceDamage`/`ConsumeMaintenanceDamage`（WP0/WP1） | `BattlePendingCastState.cs:19` 仍用 `LastMaintenanceCheckpointHp` 快照,无法区分多源累计/治疗不减 |
| I4 / SAVE-1/2（维持检定可测） | `BattleSaveResolver.ResolveAbilityCheckResult` + maintenance roll override（WP2） | `BattleCastingTimeService.cs:510` 直调 `TrueRandomSeedService.RandiRange`,无 override seam |
| I12（binding team/filter） | typed binding validator 深化（WP runtime） | 现 `ShouldInterruptForTargetBinding`（`BattleCastingTimeService.cs:521`）只查 present+alive,**无 team/filter** |
| A2（modal 早返回仍对账） | `IssueCommandCore` + `finally` 单一收口（WP1b） | `BattleRuntimeModule.cs:1288` reconcile **内联**,非 finally funnel;其上还有提前 `return batch` 分支会跳过 |
| A3（瞬发==completion） | 共享 `ResolveSkillEffectsCore`（WP0b） | 仅 `ResolvePendingCast`（`BattleSkillExecutionOrchestrator.cs:728`）,无共享核心——测「结果等价」可行,「同方法」不可测 |
| WP3 unsupported cost | `UnsupportedCostKinds`/`HasUnsupportedCost` + `CollectCastingUnsupportedCostKinds` | `SkillCostTransaction` 无该字段;registry 无该函数 |
| preview/AI 用例 | `BattlePreview.starts_pending_cast` + locked-target 字段 + AI 常量 | 均未实现 |

### RUNNER 1 — `run_casting_time_core_regression`（生命周期/成本/取消/完成）

| # | 契约 | 断言要点 |
| --- | --- | --- |
| C1 | 瞬发不回归 | `casting_time_tu<=0` 即时产出 hit/damage,`!HasPendingCast()` |
| C2 | begin 成功资源态 | MP/stamina/aura 扣;`current_ap==0`、`has_taken_action_this_turn`、`is_resting==false`;无冷却 |
| C3 | AP 只校验不写 transaction | begin 后 `current_ap==0` 来自显式置 0;**只断 begin 行为,不断 transaction 内部字段**（`ApCost` 是 internal,无公开观测） |
| C4 | 进度按 `RemainingCastProgress` 完成 | 推进到点清 pending 并产出效果,不读 estimate |
| C5 | ordinary spell-control 失败 | 不建 pending/不扣 MP/不冷却;扣 `max(1,ap_cost)`;`turn_casting_exhausted`;本窗口只准 move/wait/cancel |
| C6 | critical 失败 | AP 扣 `ceil(ap/2)`=`(ap+1)/2`（向上取整，≥1）;代码 `BattleRuntimeSkillTurnResolver.cs:703` 与 baseline 测试 `run_magic_backlash_regression.cs:242/253`（AP5→剩2）一致,文档措辞已校正——**无三方矛盾**（早先审查误读为 floor）;固定顺序启动冷却;`exhausted` |
| C7 | cancel 半返还 | MP/stamina/aura 返 50% 向下取整;不返 AP;不冷却;report `refund_policy=="half_persistent_costs"` |
| C8 | cancel 门禁 | 用 `command.unit_id` 查 caster（非 active 也可）;**modal / battle-ended 状态 REJECT 必须显式断言**（baseline 只证 allow,负向门禁未覆盖） |
| C9 | 同 step 顺序 | 按 `CastSequence` 升序结算,**断可观测 effect 先后**,不反射读私有计数器 |
| C10 | 完成不重查/不重扣 | caster AP=0 或移出射程仍完成,不再扣 MP/跑 spell control/校验 AP-range-LOS |
| C11 | save lock | pending 存在时 battle save lock 仍阻止保存 |

### RUNNER 2 — `run_casting_time_interruption_regression`（中断/ledger/维持/binding）

| # | 契约 | 断言要点 |
| --- | --- | --- |
| I1 | HP 桶阈值 | `<=3` 不检;`4..15` DC12;`>15` DC15;`maintenance_dc>0` 覆盖 |
| I2 | 多源小伤害累计 | 两次各 2 跨 tick → 累计 4 触发检定（证明非快照差值） |
| I3 | 治疗不减累计 | 累计 10 后治满,累计仍 10 仍触发（`ApplyHeal` 不进 ledger） |
| I4 | 维持成功/失败可测 | `WithSaveRollOverride` 固定:成功→继续+清零+更新 `LastMaintenanceCheckTu`;失败→中断+不返还+冷却 |
| I5 | ledger 收口全源 | DOT/terrain、charge/fall、meteor、barrier overflow 各造成掉血都能触发中断（**只能观测中断结果,funnel 本身靠 A1 编译保证 + 防御性 grep**） |
| I6 | forced movement 中断 | caster 坐标变化 → 中断+冷却 |
| I7 | 死亡/离场中断 | **断 no-cooldown 区分**：death/leave/removal 清 pending 不启动冷却（`ClearPendingCastNoCooldown` `:583`），区别于 forced-move/status/maintenance 的启动冷却 |
| I8 | blocking cast 状态 | petrified/madness/frozen/staggered/meteor_concussed 中断;pinned/rooted/tendon_cut **不**中断 |
| I9 | soft_anchor | 部分失效剔除并更新 list;全失效才中断 |
| I10 | hard_anchor | 任一失效整次中断 |
| I11 | ground_bind | 用开始锁定的 `TargetCoords`/`CastOriginCoord`,不用 completion 时 mutable caster coord |
| I12 | binding validator | **⚠ 需 WP 深化 validator**：team/hostility/effect target filter 失效也算失效（现仅 present+alive,用例会一直失败直到 validator 补全） |
| I13 | 同 step A 杀/控 B | A 完成后 B 同 step 不再 ready/完成（逐项重对账） |
| I14 | completion live-state 重验 | 到点前重查在场/存活/有 pending/无 blocking |
| A2 | modal 早返回仍对账 | **断可观测中断**（命中 modal 早返回的命令对在读条单位造成中断条件→仍中断）。⚠ 该用例必须与 WP1b 的 `IssueCommandCore` finally 同批落地,否则今天会「为错误的原因变绿」(reconcile 恰好内联在前) |

### RUNNER 3 — `run_casting_time_ai_regression`

| # | 契约 | 断言要点 |
| --- | --- | --- |
| AI1 | 输入装配（主断言） | preview→`BattleAiScoreInput` 拷入 `starts_pending_cast/casting_time_tu/pending_cast_binding_mode/locked_target_unit_ids/locked_target_coords` |
| AI2 | 敌方可发起 | 敌方/AI 单位能提交一次 pending cast start（决策可观测） |
| AI3 / A6 | 不双计（**High,correctness 不变量**） | pending preview `hit_preview==null`、`damage_preview` 空;score input 不带已提交伤害投影。**非 cosmetic**：现状无 `!starts_pending_cast` gate,读条 preview 会把未来伤害泄漏给 AI 当已提交伤害——此用例守的是真实正确性,不是格式 |
| AI4 | delay/anchor 单调性（仅纯函数层,条件性） | **当前无 `DelayFactor`/`PendingCastDelayPerTu`/`HardAnchorMult` helper 或常量**（scripts/systems/battle/ai 内 grep 空）。仅当 WP5 暴露**可测纯 helper** 时才写:`delay_factor` 随 `casting_time_tu` 单调不增且 clamp `[0.3,1]`,`hard_anchor(0.6)<ground_bind(1.0)`,不碰最终 score 总分。**若 WP5 把公式内联进 scorer 而无纯 helper,本用例必须删除——不得为验它而读总分**（test skill 禁断 AI score 总分/排序） |

### RUNNER 4 — `run_casting_time_text_command_regression`（CU-21）

| # | 契约 | 断言要点 |
| --- | --- | --- |
| T1 | 命令贯通 | **路由已存在,可立即写**：`battle cancel_cast` 已贯通 `GameTextCommandRunner.cs:739-743`→`GameRuntimeFacade.cs:2115`→`BattleSessionFacade.cs:293-310`(typed `CancelCast`)。断 facade→runtime 触发 + snapshot/report 可见(可见性依赖 T2 字段落地) |
| T2 | snapshot 字段 | **现状缺 3 字段(WP6)**：`GameRuntimeSnapshotBuilder.cs:719-740` 已出 skill_id/variant_id/binding_mode/remaining_cast_progress/remaining_cast_tu,**缺 `estimated_complete_at_tu`/`can_cancel`/`runtime_only`**。补齐后再断全字段 |
| T3 | text 行片段 | **⚠ 格式漂移(WP6 须先调和)**：现 `GameTextSnapshotRenderer.cs:727-731` 出 `pending_cast=… \| skill=… \| …`,**无 `[PENDING_CAST]` 前缀、无 `eta=`、无 `runtime_only=`**,与文档目标行不符。先决定 canonical 格式(改 renderer 对齐文档,或改文档采现状),再断稳定片段,不断全文 |
| T4 | can_cancel 语义 | 玩家可控=`true`;敌方/AI=`false`。**seam 可派生**：`BattleUnitState.control_mode`(默认 `"manual"`,`:177`)即玩家可控判据,`can_cancel` 由其派生;WP6 须把该字段加进 snapshot |

### RUNNER 5 — `run_casting_time_content_validation_regression`（纯 registry）

只调 `SkillContentRegistry` 生产 API,断**错误存在 + domain/count**,不在测试里复制 allowlist。

| # | 契约 | 断言要点 |
| --- | --- | --- |
| V1 | 字段合法性 | 负值;`casting_time_tu>0` 非 `TuGranularity` 倍数 → 报错 |
| V2 | binding 解析 | 不可解析 → `Unknown` fail closed 报错 |
| V3 | 互斥拒绝（A5） | special_profile(`SkillContentRegistry.cs:1761`)/random_chain(1765)/fumble(1769)/identity learn_source(1775)/misfortune(1779)/black_contract(1784)/charge+path_step_aoe(1824)/self relocation(1833) 各报错。**✅ 已查证非 GAP**：「identity-granted per-turn/per-battle charge 技能」无需独立规则——racial-charge(`per_turn_charges`/`per_battle_charges` 键 `racial_skill_{skill_id}`)只可能挂在 identity learn_source 技能上(grant 路径 `ProgressionService.cs:163,173` 强制 `skillDef.LearnSourceKind == {Race/Subrace/Ascension/Bloodline}`),而 1775 已拒**所有** identity learn_source 技能(比 charge 更宽)。故 V3 按 learn_source 规则写即可,**不要**期望独立的 "identity-charge" 报错 |
| V4 | level_overrides 同校验 | override 内同名字段走同一校验 |
| V5 | 单一真值源 | `CollectCastingUnsupportedCostKinds` 同输入下 content 校验与 runtime preflight 结论一致 |

### 既有 runner 扩展（schema / save / M2）

- **`run_battle_unit_state_schema_contract_regression`**：A1 三 mutator 值正确;`ToDictionary()` 不含 pending/`turn_casting_exhausted`/ledger;`Clone()` deep-copy pending+ledger+exhausted（baseline `:130-141` 已覆盖 pending+exhausted+txn,**ledger 字段待补**）;`FromDictionary` 拒绝 runtime-only（baseline `:126`）;`CastOriginCoord`（WP0 新字段）随 `Clone()` round-trip（Low,现仅经 ground completion 间接用到）。
- **`run_battle_save_resolver_regression`**：`ResolveAbilityCheckResult` dc<=0/null→`Empty`;`WithSaveRollOverride`→`Degree` 正确;maintenance 用 ability-check 入口、`saveBonus=0`（**勿重复 baseline `:202` 的 effect-keyed per-tag 共存测试**）。degree 规则 baseline `:98` 已覆盖,不重写。顺手清理该 runner 内未被任何用例引用的 dead helper `RequireNull`/`IsGodotPayloadType`（`:644-681`,cosmetic）。
- **M2**（见「M2 测试补充」）：绝大多数已被 4 个既有 runner 覆盖（子代理核对 ~24 项已覆盖,见各 file:line）,**勿重写**。genuinely 新/缺的仅：HOT 在 stasis 下冻结(现仅 DOT 覆盖)、同 step cast A 静滞/击杀 cast B(任何 runner 都没有,与 M1 I13 同源)、typed removal reason(WP7,且需与既有 `TemporalStatusReleaseKind` 调和)、维持检定 ability-check(WP2)。**boss/elite「degree 降级」不是增量也不是 gap**——已被 `run_time_stasis_regression.cs:34-99` 覆盖(布尔 save 门),不新增。

### 架构决策覆盖（A1–A6）

| 决策 | 覆盖用例 | 备注 |
| --- | --- | --- |
| A1 封装 | SCHEMA A1 + I3/I5 | `current_hp` 私有 set 由编译保证,不测可见性本身 |
| A2 单一收口 | I-A2 | 断可观测中断,不断 call order;与 WP1b 同批 |
| A3 共享 effect 核心 | C10 + 一条结果等价断言 | 比对瞬发与 completion 同输入的可观测 effect,不断「同方法」 |
| A4 调用方向无环 | 不单测 | 无行为 seam,靠 review + owner 表;test skill 禁止断 call order |
| A5 fail-closed 白名单 | V3/V5 | content + runtime 双层 |
| A6 preview 读取纪律 | AI3/T2 | pending hit/damage 为空 + consumer gate |

### 明确不测（test skill 反模式）

- 不断 AI score 总分/目标排序/RNG 具体结果（AI4 只在公式纯函数层）。
- 不断 `next_cast_sequence` 私有数值（C9 用可观测完成顺序）。
- 不断 `current_hp` 字段可见性、helper 调用顺序、内部方法身份（A1/A3/A4）。
- 不断全文 text dump,只断稳定片段（T3）。
- 不在测试里复制 binding/cost allowlist（V3/V5 调生产 API）。
- 不混入 simulation/balance runner。

### 落地建议

**今天即可写(seam 已存在)**：RUNNER 5 的 V1/V2/V3/V4(registry 校验 `SkillContentRegistry.cs:551-561/623-653/1761-1845` 已在,V3 按 learn_source 规则写——identity-charge 已查证非独立 gap);RUNNER 4 的 T1 路由(facade→runtime 已贯通)。**须等 WP 落地**：RUNNER 3 全部(WP4/WP5 preview+score-input 字段未建)、T2/T3/T4(WP6 snapshot/renderer)、V5(WP0/WP3 `CollectCastingUnsupportedCostKinds`)、SCHEMA A1 与 I1-I5/I12/A2/A3(各自 seam)。

先抽 `CastingTestFixtures.cs` + 扩展 `run_battle_save_resolver_regression`（WP2 可独立验证）+ 写 RUNNER 5 registry 用例,其余 runner 随对应 WP 落地逐个补;**严禁在 seam 缺失时先写恒绿用例**（尤其 A2/I12,及被布尔门「假装」覆盖的 boss/elite degree）。

## M2 Temporal 状态设计修正

### M2 baseline 现状（2026-06-17 核对）

主线已落地的 temporal baseline 比旧文档读起来多得多，下列**已存在，不重新实现，只验证/补强**：

| 项 | 主线现状 |
| --- | --- |
| `BattleSaveResult.Degree` + `BattleSaveDegreeKind`(含 `Unknown = 0`) + `ResolveSaveDegree` | 已实现（`BattleSaveResolver.cs`），degree 规则已与本节描述一致 |
| `action_progress_rate_remainder` / `cast_progress_rate_remainder` 余数字段 | 已在 `BattleUnitState.cs`，`BattleTemporalStatusService` 的 carry 规则已实现 |
| `HasTimeStasis` / `HasTimeSlow` / `HasTemporalCastBlock` / `GetActionProgressRatePercent` / `GetCastProgressRatePercent` / `ConsumeActionProgressGain` / `ConsumeCastProgressGain` / `CanTargetTimeStasis` / `ApplyTemporalReleaseEffects` / `ApplyTimeReverberation` / `HandleTemporalStatusRemoved` | 已在 `BattleTemporalStatusService` |
| `ApplyEliteBossStasisDowngrade` | 已实现 elite/boss→`time_slow` 无条件降级 |

M2 真实增量收敛为：(a) per-tag save bonus 扩展 `GetStatusSaveBonus`；(b) typed `GetTemporalRemovalReason` / removal reason（但注意已有 `TemporalStatusReleaseKind` enum,`BattleTemporalStatusService.cs:5-14`,WP7 应是「正式化/统一命名」而非新建并行 reason）；(c) 上述已存在语义的回归补强。**boss/elite 降级不在增量内**——desired 行为已被无条件降级 + 布尔 save 门满足（见「boss / elite 保护」修正）,不引入 degree 逻辑。下面各小节以此为准，凡标注「已存在」的不要新建重复代码。

### BattleTemporalStatusService

文件：`scripts/systems/battle/rules/BattleTemporalStatusService.cs`

职责只覆盖 temporal 状态：

- `HasTimeStasis(unit)`
- `HasTimeSlow(unit)`
- `GetActionProgressRatePercent(unit)`
- `GetCastProgressRatePercent(unit)`
- `HasTemporalCastBlock(unit)`
- `CanTargetTimeStasis(...)`
- `ApplyTemporalReleaseEffects(...)`
- `GetTemporalRemovalReason(...)`

Temporal status 不放在 `params` 字典里做业务态。资源层如仍以 `params` 导入，导入边界必须在 `SkillContentRegistry` / status construction 处转成 typed fields；runtime owner 不直接回读 `params`。

### time_stasis

语义：

- 单位不加入 ready 队列。
- 单位不推进 action progress。
- 单位不推进 cast progress。
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
- 状态移除必须携带 typed removal reason，不能靠调用点猜测。

### time_slow

`time_slow` 不改 `action_progress` 字段尺度。rate 由 `BattleTemporalStatusService` 从状态推导，不存到 `BattleUnitState`。

因为 `tuDelta * 50 / 100` 会产生小数，需要 runtime-only 余数累加器。**这两个字段及其 carry 规则已在 baseline 实现**（`BattleUnitState.cs` 的字段 + `BattleTemporalStatusService.ConsumeActionProgressGain` / `ConsumeCastProgressGain`），M2 只验证精度并补回归，不重新引入：

```csharp
internal int action_progress_rate_remainder;
internal int cast_progress_rate_remainder;
```

已实现的 carry 规则：

```text
raw = tuDelta * ratePercent + remainder
gain = raw / 100
remainder = raw % 100
```

测试必须证明 10 个 5-TU tick 在 50% slow 下总进度为 25，而不是逐 tick 截断成 20。

### save degree / per-tag bonus

`BattleSaveResult.Degree` 与 `BattleSaveDegreeKind` / `ResolveSaveDegree` **已在 baseline 实现**，M2 不新增该字段，只确认规则。degree 的消费方仅维持检定（M1 WP2）需要;boss/elite 降级**不消费 degree**（用布尔 save 门,见「boss / elite 保护」修正）：

```csharp
public BattleSaveDegreeKind Degree { get; init; } // 已存在
```

已实现的 degree 规则（核对一致，勿改）：

- total < DC 为 failure，否则 success。
- natural 1 降一级，natural 20 升一级。
- 最低 `CriticalFailure`，最高 `CriticalSuccess`（枚举含 `Unknown = 0`）。

per-tag save bonus 不新建并行合成规则。它必须在 `BattleSaveResolver` 内部扩展现有 private `GetStatusSaveBonus(...)` 路径，并与 `save_bonus` / `control_save_bonus` 使用同一套 `Math.Max` 合成语义。不要设计成外部 service 调用 private method。测试覆盖 `time_reverberation + willpower_save_bonus_up` 共存。

### boss / elite 保护

M2 不只看 `boss_target`。统一来源 `BattleExecutionRules.IsEliteOrBossTarget(unitState)` 与 `ApplyEliteBossStasisDowngrade`（`BattleTemporalStatusService.cs:159-172`，无条件 elite/boss→`time_slow`）已存在。期望行为如下：

- elite 或 boss 不获得 `time_stasis`。
- save 失败 → `time_slow`。
- save 成功（含 crit success）→ 无效果。

**修正（2026-06-17，原「degree 维度降级」是错误增量）**：上述三条**已被现状代码完整满足,无需也不应引入 degree 逻辑**。机制是「无条件降级」+「布尔 save 门」叠加:
- `ApplyEliteBossStasisDowngrade` 把 elite/boss 的 stasis 无条件降为 slow;
- `DoesSaveBlockEffect = HasSave && Success`（`BattleDamageResolver.cs:2643`，布尔门,不读 degree）——save 成功（无论 success / crit-success）整个状态不施加 = 无效果;save 失败（无论 fail / crit-fail）才进入降级路径。

因此「fail/crit-fail→slow、success/crit-success→无效果」是布尔门的自然结果,degree 三/四级粒度在此处**没有语义需求**。`ApplyEliteBossStasisDowngrade` 不需要 degree 参数。该行为**已被 `run_time_stasis_regression.cs:34-99` 覆盖**,既非新增、也非 gap——M2 在 boss/elite 维度无代码增量,只保留既有回归。

### M2 测试补充

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_time_stasis_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_save_resolver_regression.cs
godot --headless -s res://tests/battle_runtime/state_schema/run_battle_status_effect_state_schema_regression.cs
```

覆盖清单（标注 ✅=既有 runner 已覆盖,勿重写；🆕=genuinely 新/缺,需补）：

- 🆕 同 tick cast A 静滞/击杀 cast B，B 不 ready、不完成。（任何 runner 都没有,与 M1 I13 同源）
- 🆕 **HOT** 在 stasis 下冻结。（现仅 DOT 覆盖于 `run_temporal_status_semantics_regression.cs:49-50`,HOT 缺）
- ✅ stasis 下 action progress / cast progress / cooldown / stamina recovery / DOT / status duration / shield duration 冻结。（`run_temporal_status_semantics_regression.cs:51-78,109-138`、`run_time_stasis_regression.cs:257-285`）
- ✅ time_slow 多 tick 累计精度（10×5TU@50%=25）。（`…semantics…cs:140-167`、`run_time_stasis_regression.cs:287-316`）
- ✅ per-tag save bonus 与现有 save bonus/control save bonus 共存。（`run_battle_save_resolver_regression.cs:202-261`）
- ✅ `BattleStatusEffectState.status_tags` / `save_bonus_by_tag` schema 契约,无旧 payload fallback。（`run_battle_status_effect_state_schema_regression.cs:911-979`）
- ✅ elite/boss 降级（布尔 save 门,**非 degree**）。（`run_time_stasis_regression.cs:34-99`）
- ✅ natural expire / dispel 添加 reverberation，death/cleanup 不添加。（`…semantics…cs:305-360`、`run_time_stasis_regression.cs:133-211`）
- ✅ temporal-only 解控技能拒绝混入伤害/治疗/位移/普通状态。（`run_time_stasis_regression.cs:318-492`）
- 🆕 typed removal reason（WP7）——但需与既有 `TemporalStatusReleaseKind`（`BattleTemporalStatusService.cs:5-14`）调和,不建并行 reason。
- ⚠ `run_time_stasis_regression.cs` 无 ObjectDB leak / unsafe-ref warning——当前仅靠进程退出码,无显式断言,属 runner hygiene gap。

## 合并门槛

- 没有新增旧 schema fallback 或旧 string helper。
- 新命令走 `BattleCommandKind`，不是 `TYPE_*` 字符串常量。
- pending cast 是 typed runtime state，不是裸 `Dictionary` 业务态。
- 玩家和敌方/AI 都可开始 pending cast；M1 只不交付 AI cancel 高级策略。
- `BattleDamageResolver` 无 runtime callback。
- **（A1）`current_hp` 为 `{ get; private set; }`,战斗扣血只经 `ApplyHpDelta`、回血只经 `ApplyHeal`、构造/回滚只经 `InitHp`;不靠 grep 守 public 字段。**
- 中断检测通过集中 reconciliation + damage ledger 覆盖所有 HP loss / movement / status 后果；所有掉血源经 `BattleUnitState.ApplyHpDelta(loss)` 收口，无 owner 手写 hook，无直写 `current_hp` 漏网。
- **（A2）命令对账 `ReconcilePendingCastsAfterCommand` 单一收口（`IssueCommand` finally）,不在各早返回分支逐个插桩。**
- **（A3）瞬发路径与 pending completion 共享同一 effect 结算核心,无复制双路径。**
- **（A4）服务调用图无环且方向登记在 owner 表;冷却 owner 唯一为 `BattleRuntimeSkillTurnResolver`;CastingTimeService 不以 module 万能句柄越界 reach。**
- 维持检定走 `BattleSaveResolver` 且随机源可经 `BattleSaveContext.WithSaveRollOverride` 注入固定，不直调 `TrueRandomSeedService`。
- preview 有 pending-cast 语义，不把未完成读条当瞬发伤害。
- unsupported hidden cost 在内容校验和 runtime preflight 双层 fail closed;`CollectCastingUnsupportedCostKinds` 为白名单语义,未枚举成本默认拒绝（A5）。
- **（A6）pending preview 的 `hit_preview`/`damage_preview` 为空,consumer 读 hit/damage 前 gate on `!starts_pending_cast`,有测试锁死。**
- pending completion 不调用 ground precast special effects，不使用 mutable current coord 重新推导地面效果。
- target binding validator 覆盖 alive/present/team/filter，并按 binding mode 更新或中断。
- 同 tick 完成顺序和 live-state 重验有回归。
- cancel 与 hard-anchor 失效成本策略一致，不制造抢跳 cancel 微操最优解。
- snapshot/text/headless 输出合同有回归。
- battle save lock、clone、schema runtime-only 合同有回归。
- temporal 状态归属 `BattleTemporalStatusService`，不恢复 `time_stasis_cell_locks` 或 owner 撒网 hook。
- 实现 PR 同步更新 `docs/design/project_context_units.md`，仅在实际 runtime owner / 推荐读集发生变化时更新。

## 实现工作包（2026-06-16 代码层级分解）

按依赖顺序落地，每个 WP 后跑 `dotnet build magic.csproj`。文档沿用目标契约名；实际服务方法名（`TryHandleCastingSkillStart` / `ReconcilePendingCasts` / `AdvancePendingCasts` / `CompleteReadyPendingCasts`）按现状改造，不机械新增重复文件。

代码层级补充的两个小决策：

- `ApplyHpDelta` floor 为 0；black-contract 自损用 `Min(cost, current_hp - 1)` 保留 min-1 语义。
- 维持检定走**新增的** `BattleSaveResolver.ResolveAbilityCheckResult`，不改造 effect-keyed 的 `ResolveSaveResult`（后者强依赖 `CombatEffectDef.save_tag/save_ability`，维持检定无 effect_def 且 DC 来自伤害桶）。

### WP0 — 类型/字段（无行为变更）

`scripts/systems/battle/core/BattlePendingCastState.cs`：

- 删除 `LastMaintenanceCheckpointHp`。
- 新增 `int DamageSinceMaintenanceCheck`、`int LastMaintenanceCheckTu`、`Vector2I CastOriginCoord`。
- `Clone()` 带上三字段，移除 checkpoint 行。

`scripts/systems/battle/core/SkillCostTransaction.cs`：

- 新增 `IReadOnlyList<StringName> UnsupportedCostKinds`（默认空）、`PendingCastRefundPolicy RefundPolicy`（默认 `HalfPersistentCosts`）、`bool HasUnsupportedCost`。
- `Clone()` / `CooldownOnly(...)` 带上新字段。

### WP0b — orchestrator 共享 effect 核心（决策 A3，WP4/WP5 前置）

`scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`：把瞬发路径里「纯效果结算」从「preflight（cost / spell-control / AP / range / LOS / precast-special）」剥离成一个被两条路径共享的内部入口（如 `ResolveSkillEffectsCore(source, context, batch)`）。瞬发路径 = preflight + core;`ResolvePendingCast`（WP 完成入口）= 仅 core + binding validator,**不复制效果逻辑**。这是无行为变更重构,做在 begin/completion 接线之前,避免双路径漂移。合并门槛新增「瞬发与 completion 共享同一 effect 核心,无复制分支」。

### WP1b — 命令对账单一收口（决策 A2）

`scripts/systems/battle/runtime/BattleRuntimeModule.cs`：把 `IssueCommand` 主体改名 `IssueCommandCore`,外层 `IssueCommand` 包 `try { return IssueCommandCore(command); } finally { _casting_time_service.ReconcilePendingCastsAfterCommand(batch); }`（batch 需在外层可见,按现状取 batch 的方式调整）。删除散落在各 `modal_requested` / 早返回分支里的逐个对账意图——对账只此一处。回归：构造一条命中 `modal_requested` 早返回且同时对在读条单位造成中断条件的用例,验证仍触发对账。

### WP1a — `current_hp` 封装 + mutator（决策 A1，先做）

`scripts/systems/battle/core/BattleUnitState.cs`：

- `public int current_hp;` → `public int current_hp { get; private set; }`。
- 新增 `internal void ApplyHpDelta(int loss)`（扣血,见下）、`internal void ApplyHeal(int gain)`（回血 clamp 到 hp_max,不进 ledger）、`internal void InitHp(int value)`（构造/反序列化/rollback/换装 clamp 专用,clamp 到 `[0, hp_max]`）。
- `BattlePendingCastState`：`DamageSinceMaintenanceCheck` set 私有,加 `AccrueMaintenanceDamage(applied)` / `ConsumeMaintenanceDamage(atTu)`。

封装后所有跨类的 `current_hp` 写都必须改走 mutator——含原「白名单」外部写点：换装 clamp（orchestrator `2807` / `BattleChangeEquipmentResolver:314/319`）改 `InitHp(...)`;AI 投影回滚（`BattleAiMutationGuard:1367` `unit.current_hp = _currentHp`）改 `InitHp(_currentHp)`;治疗（`Effects.cs:786`）改 `ApplyHeal(...)`;factory/sim/`FromDictionary` 改 `InitHp(...)`。编译器会替你找出所有外部直写点（这正是封装相对 grep 的优势）。

### WP1 — `ApplyHpDelta` 掉血收口

`scripts/systems/battle/core/BattleUnitState.cs`（锚点：`SetPendingCast` 区附近）：

```csharp
internal void ApplyHpDelta(int loss)
{
    if (loss <= 0)
        return;
    int before = current_hp;
    current_hp = Math.Max(current_hp - loss, 0);
    int applied = before - current_hp;
    if (applied > 0 && pending_cast != null)
        pending_cast.AccrueMaintenanceDamage(applied);   // 累计语义归 pending state
}
```

改路的掉血点：

1. `BattleDamageResolver.cs:2192-2251`：该块当前有约 7 处 `targetUnit.current_hp = X`（不要按写死个数核对，以 WP1 末尾 grep/schema 断言为准）。**非致命分支**（`minHpAfterDamage` clamp、`bypassDeathPrevention`、`projectedHp > minHpAfterDamage` 正常扣血、fatal-trait `ClampToHp` 分支）改写入局部 `int finalHp`（默认 `= current_hp`），块尾一次 `targetUnit.ApplyHpDelta(targetUnit.current_hp - finalHp)`；这些分支内对 `current_hp` 的读取因延迟写入仍读原值，语义不变。fatal-trait 分支只计算 `ClampToHp` 不直写，已可纳入延迟写入。

   **`death_ward` / `TriggerLastStand` 是延迟写入特例，不套用通用 recipe**：`TriggerLastStand`（`BattleDamageResolver.Effects.cs:1238`）内部经 `ResolveEffects` 施加最后一搏治疗、**直接写 `current_hp`**，并依赖「调用前 `current_hp` 已置 0」来用 `current_hp > 0` 判定是否触发。若延迟写入，进入时 `current_hp` 仍是原值，治疗叠加其上、`triggered` 恒真，最后一搏语义被破坏，且块尾 `current_hp - finalHp` 把治疗值算进 ledger 也错。正确做法：进入该分支时先对致命伤调用一次 `targetUnit.ApplyHpDelta(致命伤 loss)`（把单位扣到 0、同时让 ledger 记到这次伤害），再调用 `TriggerLastStand`，其内部治疗走原有白名单治疗路径（`Effects.cs:786`，不进 ledger 累计分支）。该分支不参与块尾的 `finalHp` 单次提交。
2. `BattleRuntimeSkillTurnResolver.cs:1292`（DOT/terrain tick）：`int loss = Math.Min(tickDamage, previousHp); unit_state.ApplyHpDelta(loss); unit_state.is_alive = unit_state.current_hp > 0;`。
3. `BattleRuntimeSkillTurnResolver.cs:1050`（black-contract 自损）：`active_unit.ApplyHpDelta(Math.Min(BLACK_CONTRACT_PUSH_HP_COST, active_unit.current_hp - 1));`。

非战斗 HP 变更改走 mutator（封装后不再有「直写白名单」）：`Effects.cs:786` 治疗 → `ApplyHeal`;换装 clamp（orchestrator `2807` / `BattleChangeEquipmentResolver`）、AI 投影回滚（`BattleAiMutationGuard:1367`）、`FromDictionary` / persistence / settlement / creation / EncounterRoster / sim / factory → `InitHp`。

回归：因 `current_hp` set 私有,战斗内直写已编译期不可能;仍保留 grep/schema 断言作为防御性回归——`scripts/systems/battle` 内 `current_hp` 的扣减只经 `ApplyHpDelta`。

### WP2 — 维持检定走 `BattleSaveResolver`

`scripts/systems/battle/rules/BattleSaveResolver.cs` 新增 effect-free 入口：

```csharp
public static BattleSaveResult ResolveAbilityCheckResult(
    BattleUnitState target_unit,
    StringName saveAbility,
    int dc,
    BattleSaveContext context = default)
{
    if (target_unit == null || dc <= 0)
        return BattleSaveResult.Empty(AdvantageStateNormal);
    int naturalRoll = RollSaveDie(AdvantageStateNormal, context); // 消费 context.SaveRollOverrides
    int abilityMod = GetTargetAbilityModifier(target_unit, saveAbility);
    int total = naturalRoll + abilityMod;
    bool success = total >= dc;
    return new BattleSaveResult(true, false, success, naturalRoll, total, dc,
        saveAbility, "", AdvantageStateNormal,
        GetTargetAbilityValue(target_unit, saveAbility), abilityMod, 0,
        System.Array.Empty<BattleSaveSource>())
    { Degree = ResolveSaveDegree(naturalRoll, total, dc) };
}
```

`scripts/systems/battle/runtime/BattleCastingTimeService.cs` `ShouldInterruptForHpLoss` 重写：

- 改用 `DamageSinceMaintenanceCheck` 判桶（`<=3` 不检定，`4..15` DC 12，`>15` DC 15，`casting_maintenance_dc > 0` 覆盖）。
- 通过 `BattleSaveResolver.ResolveAbilityCheckResult(unitState, WillpowerAbility, dc, ctx)` 检定，`ctx` 由 `MaintenanceRollOverride`（挂 `PendingCastResolutionContext` / service 测试 seam）经 `BattleSaveContext.WithSaveRollOverride` 注入。
- 检定后清零 `DamageSinceMaintenanceCheck`、更新 `LastMaintenanceCheckTu = current_tu`。
- 删除 `TrueRandomSeedService.RandiRange` 与快照逻辑；确认 willpower modifier 与 `GetTargetAbilityModifier("willpower")` 口径一致。

begin 路径（`BuildStartPayload` / 旧 `LastMaintenanceCheckpointHp = current_hp` 处）：改设 `DamageSinceMaintenanceCheck = 0`、`LastMaintenanceCheckTu = StartedTu`、`CastOriginCoord = 开始锁定 origin`。

### WP3 — runtime fail-closed（unsupported cost）

`scripts/player/progression/SkillContentRegistry.cs`：把读条拒绝判定（现 1761-1843）抽成共享静态 `CollectCastingUnsupportedCostKinds(skill, profile, level)`，内容校验与 runtime 共用，单一真值源。

**白名单语义（决策 A5）**：该函数按白名单工作——只有显式建模为可安全返还/冷却的 typed 成本（`SkillCostTransaction` 已建模的 AP/MP/stamina/aura/cooldown）才不计入 unsupported,其余一切成本/charge/identity/misfortune/black-contract/special profile 默认进 `UnsupportedCostKinds`。新增成本种类时,若不在该函数显式放行,则**默认被拒**（fail closed,安全方向）。

`BattleCastingTimeService` begin：构建 transaction 时填 `UnsupportedCostKinds`；`HasUnsupportedCost` 为真则返回 block payload，不扣资源、不建 pending。

### WP4 — preview pending 语义

`scripts/systems/battle/core/BattlePreview.cs` 新增：

```csharp
public bool starts_pending_cast { get; set; } = false;
public int casting_time_tu { get; set; } = 0;
public PendingCastBindingModeKind pending_cast_binding_mode { get; set; } = PendingCastBindingModeKind.Unknown;
public Godot.Collections.Array<StringName> locked_target_unit_ids { get; set; } = new();
public Godot.Collections.Array<Vector2I> locked_target_coords { get; set; } = new();
```

读条技能 preview 注解处置 `starts_pending_cast = true` 并填锁定目标，同时清空 `hit_preview = null` / `damage_preview.Clear()`，杜绝同 tick 双收益。

### WP5 — AI delayed-utility

`scripts/systems/battle/ai/BattleAiScoreInput.cs` 新增 `starts_pending_cast / casting_time_tu / pending_cast_binding_mode / locked_target_unit_ids / locked_target_coords`，在 `BattleAiScoreContextAdapter` 装配处从 preview 拷入。

`scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`（投影价值算出后）：

```csharp
if (input.starts_pending_cast)
{
    float delay = Math.Clamp(1f - PendingCastDelayPerTu * input.casting_time_tu, PendingCastDelayFloor, 1f);
    float anchor = input.pending_cast_binding_mode switch
    {
        PendingCastBindingModeKind.HardAnchor => HardAnchorMult,
        PendingCastBindingModeKind.SoftAnchor => SafeRatio(aliveLocked, totalLocked),
        _ => 1f
    };
    score = (int)Math.Round(score * delay * anchor);
}
```

`PendingCastDelayPerTu = 0.02f`、`PendingCastDelayFloor = 0.3f`、`HardAnchorMult = 0.6f` 放 `BattleAiScoreProfile`。

### WP6 — snapshot / text

- `GameRuntimeSnapshotBuilder.cs` `BuildPendingCastSnapshot`：加 `estimated_complete_at_tu`（`current_tu + ceil(remaining / rate)`）、`can_cancel`（玩家可控 manual unit）、`runtime_only = true`。
- `GameTextSnapshotRenderer.cs`：输出 `[PENDING_CAST] unit=… skill=… remaining=… eta=… runtime_only=true`。

### WP7（M2）— removal reason

`scripts/systems/battle/rules/BattleTemporalStatusService.cs` 加 `GetTemporalRemovalReason(...)`；状态移除入口加 typed `BattleTemporalRemovalReason`；仅 `NaturalExpire` / `Dispel` 触发 `time_reverberation`。

### WP8 — 测试

5 个 M1 runner（core / interruption / ai / text_command / content_validation）+ 在 `run_temporal_status_semantics` / `run_time_stasis` / `run_battle_save_resolver` 补 degree / per-tag / removal-reason 断言。`ApplyHpDelta` 覆盖断言：DOT / terrain / charge / meteor / barrier 任意掉血都累计 ledger。补架构回归：(A2) `modal_requested` 早返回仍对账;(A3) 瞬发与 completion 共享 effect 核心;(A6) pending preview 的 hit/damage 为空。

### 落地顺序

WP0 → WP0b（effect 核心抽取,无行为变更）→ WP1a（`current_hp` 封装,编译器找出全部外部写点）→ WP1（`ApplyHpDelta` 收口 +回归确认无漏网掉血）→ WP1b（命令对账单一收口）→ WP2 → WP3 → WP4 / WP5 → WP6 → WP7 → WP8。
