# 反击系统设计提案

> 状态：`Proposal / Not implemented`
> 更新日期：`2026-07-25`
> 关联上下文单元：CU-13（内容定义）、CU-15（战斗运行时总编排）、CU-16（战斗规则 / 伤害 / AI）

## 一、当前事实（已核对源码）

设计前先固定基线，避免把"内容已声明"误当成"引擎已实现"。

### 1.1 已经存在的接口，但没有实现

| 事实 | 位置 | 状态 |
|---|---|---|
| `lock_counterattack` typed 状态字段 | `BattleStatusEffectState.cs:184`、`OptionalSchemaFields` | 已定义、可编解码 |
| 从内容定义投影写入 | `BattleStatusSemanticTable.cs:433` ← `CombatEffectDefinition.LockCounterattack` | 已接线 |
| 运行时读面 | `BattleRuntimeModule.IsUnitCounterattackLocked(...)`(1433)、`BattleRuntimeSkillTurnResolver.HasCounterattackLockStatus(...)`(2537) | 已实现 |
| 内容侧施加者 | 黑星烙印（`BattleSpecialSkillResolver.cs:531`）、厄命宣判、折手分支、黑冠封印·禁反击、九霄终锤 terminal lock | 已投放 |
| **消费者** | — | **不存在** |

`lock_counterattack` 目前只有 4 个 fate 回归在断言"标记已设上"，没有任何规则读它来阻止什么。这是本提案要闭合的第一个洞。

### 1.2 描述与实现不符的既有技能

| skill_id | display_name | 现有 description | 实际实现 |
|---|---|---|---|
| `warrior_counter_slash` | 反击斩 | 本回合首次被近战攻击后自动反击 | 普通近战 damage 技能，1 AP / 18 stamina / 20 TU CD |
| `warrior_moment_counter` | 刹那反击 | 闪避近战后自动反击 | 普通近战 damage 技能，80 TU CD |
| `warrior_phantom_blade_bait` | 虚刃诱反 | 诱发目标反击，反击失败则失衡 | 普通近战 damage 技能 |
| `warrior_mind_eye_counter` | 心眼反制 | 本回合受远程攻击时命中检定降低 | `range_value = 0` 自我 buff（这条实际最接近描述） |

前三条是"描述承诺了反应式行为、实现是主动技能"。它们是本系统落地后的内容迁移目标，不是新增内容。

### 1.3 可复用的反应基础设施

工程里已经有**两套**反应机制，反击不应该成为第三套独立实现：

**A. Contingency（玩家编排的储存法术矩阵）**
- `BattleContingencySystem`（1195 行）+ `BattleContingencyBridgeService` + `IBattleContingencyRuntimePort`
- 关键契约：`_releaseQueue` 入队 → `ExecuteQueuedReleaseContexts(...)` 排空；同一调用栈、同一 `BattleEventBatch`
- 递归保护：`BattleEffectOrigin.AutoCast(...)` 的 `CanTriggerContingencies = false`
- 触发事实冻结：`ContingencyHookFact` → `ContingencyFrozenTriggerFacts`

**B. 装备能力 reaction**
- `IBattleEquipmentCombatReactionSink.ResolveHitReceived(...)`，authoring timing 已有 `on_hit_received` / `after_hit_received` / `on_attack_check` / `on_damage_applied`
- `ImmediateWeaponAttackActionPayloadDef` 已经能"立即发起一次武器攻击"（`skill_id` 默认 `basic_attack`）；执行体 `BattleEquipmentAbilityRuntimeService.ResolveImmediateWeaponAttackAction(...)`（cs:1448）是本系统执行层的直接先例
- `EquipmentAbilityReactionDef.once_scope` 已有 `turn` 语义（`BattleEquipmentAbilityStateResolver.cs:19`）

**结论：反击系统的执行层几乎不需要新东西。缺的是"防御方视角的触发事实 + 反应预算 + 通用资格规则"。**

### 1.4 时序与几何基线

- 战斗是 **TU 时间线**，不是离散回合。`BattleUnitTurnState` 只表达**单次 activation** 内的事实，由 `BattleTimelineDriver.ActivateNextReadyUnit(...)` 调用 `ResetForTurnStart()` 重置。
- **`action_threshold` 直接就是 TU**：`BattleTemporalStatusService.ConsumeActionProgressGain(...)` 在 100% 速率下返回 `tuDelta`，所以 threshold 30 = 每 30 TU 行动一次。
- 生产尺度：`AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD = 30`；40 个敌人模板的 `action_threshold` 分布 **25–60**（25×3、30×11、35×11、40×5、45×4、50×4、55×1、60×1），密集在 30/35。
- **相邻是正交的**：`BattleState._are_units_adjacent(...)`（cs:1570）用 Manhattan == 1，不含对角。中体型单位同时最多被 4 个近战单位接触。
- 敌人模板 agility 分布 6–16，密集在 12（11 个）、14（7 个）、15（6 个）。按 `floor((v-10)/2)`，实际 `agility_modifier` 范围 -2 ~ +3；玩家成长加装备可到 +5 ~ +7。

### 1.5 存档影响：无

`SaveSerializer` 不序列化任何 battle 字段，`GameSession._battle_save_lock_enabled` 在战斗期间锁存档。**战斗局部新增 owner 不需要 bump `SaveSchemaVersions.SaveVersion`（当前 17）**。仍需覆盖 codec / clone / AI mutation stable projection，那三条是 CU-16 的硬要求，与存档无关。

---

## 二、设计目标与非目标

### 目标

1. 反击是**通用规则**，不是某几个技能的特例分支。任何单位（玩家、敌人、召唤物）通过同一条链获得反击。
2. 消费 `lock_counterattack`，让已投放的封锁类内容真正生效。
3. 复用现有 hit / range / damage / barrier / status owner，不复制规则。
4. 攻击方在**出手前**能看到"这一击可能吃反击"——策略游戏节奏下这是必需信息（见 `project_combat_pacing_philosophy`）。
5. 不产生无限递归：反击不能触发反击。

### 非目标（本期明确不做）

- **借机攻击 / 威胁区（AoO）**。需要在 `BattleMovementService` 的逐步移动上开钩子，牵动寻路、AI 路径评分、大单位 footprint 与 barrier 边界穿越。列为 P3。
- 反击链（A 反击 B，B 再反击 A）。
- **技能反击**。反击固定打出一次基础武器攻击，姿态不能声明"用某个技能反击"。理由见 §13 决策 3。
- 玩家逐次确认反击时机的交互模式（§8.3）。
- 格挡 / 招架作为独立机制。`guard_block` 已是减伤字段，不与反击合并。

---

## 三、核心模型

**反击 = 状态声明（做什么） × 反应预算（能做几次） × 通用资格规则（这次能不能做）**

```text
CombatEffectDef.counter_*            （authoring，CU-13）
  -> CombatEffectDefinition           （immutable definition）
  -> BattleStatusSemanticTable        （写入状态 typed 字段）
  -> BattleStatusEffectState.counter_* （战斗局部状态声明）

BattleUnitReactionState              （反应预算，CU-16 core）
BattleCounterattackRules             （资格判定，CU-16 rules，无状态）
IBattleAttackResolvedSink            （注入端口，CU-16 rules）
BattleCounterattackSystem            （入队/排空编排，CU-15 runtime）
```

### 3.1 为什么用"状态驱动"而不是新的技能类别

1. **既有内容天然就是姿态**。"反击斩：本回合首次被近战攻击后自动反击" = 主动技能消耗 1 AP 挂一个持续到本次 activation 结束的姿态状态，与现有 `effect_type = "status"` 完全同构。
2. **天生反击的怪物也走同一条路**。`SkillPassiveResolver` / `PassiveStatusOrchestrator` / `RaceTraitResolver` 已能挂永续被动状态，怪物的"永远反击"就是 `duration = -1` 的姿态状态。
3. **`lock_counterattack` 已经是状态字段**。封锁与授予同层，语义对称。

### 3.2 新增 owner：`BattleUnitReactionState`

`scripts/systems/battle/core/BattleUnitReactionState.cs`，plain C#，对齐 `BattleUnitTurnState` / `BattleUnitCooldownState` 的写法。

```csharp
internal readonly record struct BattleUnitReactionSnapshot(
    bool OwnerPresent,
    int ChargesRemaining,
    int ChargeCapacity,
    int RechargeIntervalTu,
    int NextRechargeAtTu
);

internal sealed class BattleUnitReactionState
{
    internal int GetChargesRemaining();
    internal int GetChargeCapacity();
    internal int GetRechargeIntervalTu();
    internal int GetNextRechargeAtTu();

    // 原子：判定余量 > 0 并扣减，返回是否成功。禁止 caller 先读后写。
    internal bool TryConsumeCharge();

    // 原子：anchor 按整间隔推进到 currentTu 之后；跨过任意个间隔都回满。
    internal bool AdvanceAndRefill(int currentTu);

    // 容量变化：升不补、降夹紧（§4.2）。
    internal void ApplyCapacityRaw(int chargeCapacity);
    internal void SetRechargeIntervalTuRaw(int value);

    // 战斗开始时由 timeline driver 播种，并置为满电。
    internal void InitializeAnchor(int currentTu, int rechargeIntervalTu, int chargeCapacity);

    internal BattleUnitReactionSnapshot CaptureRaw();
    internal void RestoreRaw(BattleUnitReactionSnapshot snapshot);
    internal BattleUnitReactionState DuplicateState();
    internal static BattleUnitReactionState FromRaw(BattleUnitReactionSnapshot snapshot);
}
```

不变量：
- `ChargesRemaining` 的唯一存储在此。`BattleCounterattackRules` 只读，`BattleCounterattackSystem` 通过 `TryConsumeCharge()` 原子扣减——**不允许"先判定后扣减"的两段式**，否则同一 batch 内多个防御方触发时会超发。
- **anchor 按整间隔推进，不得重置为 `currentTu`**。否则 timeline step 与间隔不整除时 anchor 会持续右漂，反击频率随 step 粒度变化。这与 `BattleUnitActionClockState.ConsumeRateScaledGain / AdvanceAndConsumeThresholds` 的余数处理同源。
- 不参与存档（§1.5），但必须进入 `BattleUnitStatePlainSnapshot`、`DuplicateState()` 与 AI mutation stable projection。

### 3.3 新增规则类：`BattleCounterattackRules`

`scripts/systems/battle/rules/BattleCounterattackRules.cs`，**无状态、caller-scoped**，对齐 `BattleSkillAvailabilityService` 的定位（不缓存进 `BattleRuntimeModule`）。

这是**唯一**的资格判定 owner。preview、正式执行、AI 风险估值三方共同消费同一份实现，禁止任一方近似复制。

```csharp
internal readonly record struct CounterattackEligibility(
    bool Eligible,
    BattleCounterattackBlockReasonKind BlockReason,   // typed 枚举
    StringName SourceStatusId,     // 命中的姿态状态，扣次数时要用
    int ChancePercent,
    int AttackRollBonus,
    int StaminaCost
);

internal static CounterattackEligibility Evaluate(
    CounterattackHookFact fact,
    BattleUnitState defender,
    BattleUnitState attacker,
    BattleState state,
    BattleBarrierService barrierService,
    bool consumeRandomness   // preview = false
);
```

### 3.4 触发事实：`CounterattackHookFact` 与 `CounterattackReleaseContext`

对齐 `ContingencyHookFact` → `ContingencyFrozenTriggerFacts` 的两段式。放 `scripts/systems/battle/core/`。

```csharp
internal sealed class CounterattackHookFact
{
    internal BattleCounterattackTriggerKind TriggerKind { get; init; }
    internal StringName SourceEventId { get; init; } = "";   // bridge 分配，去重用
    internal StringName AttackerUnitId { get; init; } = "";
    internal StringName DefenderUnitId { get; init; } = "";
    internal Vector2I AttackerCell { get; init; }            // 入队时的坐标，仅供日志
    internal Vector2I DefenderCell { get; init; }
    internal bool AttackSucceeded { get; init; }
    internal bool CriticalHit { get; init; }
    internal bool IncludesWeaponDamage { get; init; }
    internal StringName AttackSkillId { get; init; } = "";
    internal IReadOnlyList<StringName> EffectCategories { get; init; } // 近战/投射判定输入
    internal BattleEffectOrigin Origin { get; init; }
}

internal sealed class CounterattackReleaseContext
{
    internal CounterattackHookFact Fact { get; init; }
    internal StringName SourceStatusId { get; init; } = "";
    internal int AttackRollBonus { get; init; }
    internal int StaminaCost { get; init; }
}
```

**坐标只冻结用于日志，不用于判定。** 射程与屏障必须在排空时按当前坐标重新求值（§5 第 10/11 条）。这与 contingency 的 `ToFrozenFacts(...)` 相反——contingency 冻结坐标是因为储存法术的目标在触发瞬间就该确定；反击是即时近身还击，必须看当下。

### 3.5 新增注入端口：`IBattleAttackResolvedSink`

`scripts/systems/battle/rules/IBattleAttackResolvedSink.cs`。

`BattleDamageResolver` 是纯规则层，不能直接持有 runtime 的 `BattleCounterattackSystem`。既有两个端口都不合用：`IBattleDamageApplicationHook` 是 `BeforeDamageResolved`（在伤害之前，看不到 miss 也看不到死亡结果），`IBattleEquipmentCombatReactionSink` 是装备专用且 gate 在"命中 + 含武器伤害"。反击需要一个"本次攻击已完全结算"的端口。

```csharp
internal interface IBattleAttackResolvedSink
{
    void OnAttackResolved(BattleAttackResolvedContext context);
}

internal sealed class BattleAttackResolvedContext
{
    internal BattleUnitState AttackerUnit { get; init; }
    internal BattleUnitState DefenderUnit { get; init; }
    internal BattleState BattleState { get; init; }
    internal bool AttackSucceeded { get; init; }
    internal bool CriticalHit { get; init; }
    internal bool IncludesWeaponDamage { get; init; }
    internal StringName SkillId { get; init; } = "";
    internal IReadOnlyList<StringName> EffectCategories { get; init; }
    internal BattleEffectOrigin Origin { get; init; }
    internal BattleEventBatch Batch { get; init; }
    internal bool IsRepeatAttackStage { get; init; }
}
```

#### 调用位置：**两条出口都要挂**

`ResolveAttackEffects(...)` 有**两个 return 点**，sink 必须都覆盖，否则会丢 trigger：

```csharp
// 出口 A —— miss 提前返回（cs:537-559）
if (!attackMetadata.AttackSuccess)
{
    ...
    DispatchAttackResolutionEvents(...);
    ClearComboStackOnMiss(source_unit);
    ConsumeOneShotAttackCheckStatuses(source_unit);
    ★ OnAttackResolved(AttackSucceeded: false)      // ← 必须在这里，且在上面两个 Consume 之后
    return FinalizeTypedResult(failedResult);
}

// 出口 B —— 命中路径（cs:618-623 之后）
DispatchAttackResolutionEvents(...);
★ OnAttackResolved(AttackSucceeded: true)
```

**这是 P1 最容易漏掉的一处。** `melee_attack_evaded`（刹那反击）**只在出口 A 触发**——只挂出口 B 的话，P1 两个 trigger 里有一个完全是死的，而且不会报错，只是永远不生效。

两个出口的状态成熟度不同，但都满足反击的需要：

- **出口 A**：没有伤害、没有死亡结算，防御方必然存活。`ClearComboStackOnMiss` / `ConsumeOneShotAttackCheckStatuses` 必须先跑完，否则反击会看到未结算的一次性状态。
- **出口 B**：死亡结算已在 `ResolveEffectsDefinitionCore(...)`（cs:584）内部完成（`MarkDead()` 在 cs:2186 / 2210 / 2219 / 2224），`ApplyEquipmentAbilityAfterHitResult(...)`（cs:600）也已跑完。所以出口 B 能同时看到命中结果与存活结果。

实现上应抽一个私有 helper 供两处调用，不要复制两份 context 构造。

#### 装配点

绑在 `BattleRuntimeModule.BindDamageResolver()`（cs:2802）——contingency 的 `SetDamageApplicationHook(_contingency_system)` 就在那里，两者形状完全相同（runtime sidecar → damage resolver）。

**不要绑在 `BindEquipmentRulePorts()`（cs:2811）**。那个方法绑的是 AttackCheckQuery / DamageQuery / ReactionSink 三个**装备**端口；equipment_ability_runtime.md 说的"三端口唯一正式装配点"只针对装备端口，不是所有规则端口的总装配点——damage hook 从来就不在那里。

**不改任何方法名。** 重命名会波及全部调用方与文档引用，且无收益。

### 3.6 新增编排：`BattleCounterattackSystem` + bridge

`scripts/systems/battle/runtime/BattleCounterattackSystem.cs` + `BattleCounterattackBridgeService : BattleRuntimeModuleBorrower`。

- system 实现 `IBattleAttackResolvedSink`，持有 `Queue<CounterattackReleaseContext>`，弱借用 `IBattleCounterattackRuntimePort`，不反向依赖 `BattleRuntimeModule`
- bridge 实现该端口，保留同步调用栈、调用方 `BattleEventBatch` 与 origin scope，并分配 `SourceEventId`
- 排空后 `ClearBattleState()`；`DisposeRuntime()` 断开 runtime / owner / sibling borrower（对齐 c3469ca 的既有整治方向）

---

## 四、触发时序与反应预算

### 4.1 入队与排空分离

直接照搬 contingency 已验证的 `_releaseQueue` + `ExecuteQueuedReleaseContexts` 模式。

```text
BattleSkillExecutionOrchestrator 执行一次攻击技能
  └─ BattleDamageResolver.ResolveAttackEffects(...)
       对每个目标：
         命中判定 → 伤害/状态/位移 → 死亡结算（cs:584 内完成）
         装备 after-hit / hit-received（cs:600）
         DispatchAttackResolutionEvents（cs:618）
         ★ IBattleAttackResolvedSink.OnAttackResolved → 入队（只入队，不执行）
  └─ 全部目标 effect 结算完成
  └─ ★ 排空点：BattleCounterattackSystem.ExecuteQueuedCounterattacks(batch)
  └─ BattleSkillOutcomeCommitter 提交 outcome
```

**为什么必须入队而不是就地执行：**

1. AoE 打中 3 个能反击的目标时，就地执行会让第 1 个目标的反击在第 2、3 个目标的伤害结算**之前**发生，攻击方的 HP / 状态在同一次技能内被反复穿插修改，结果依赖遍历顺序。
2. 防御方可能在本次攻击中**死亡**。入队 + 排空时复核存活，语义干净："被打死了就不能反击"（免死状态生效时它还活着，反击成立——这正是想要的）。
3. `BattleDamageResolver` 是纯规则层，不应反向调用 orchestrator。入队让依赖方向保持单向。

**排空顺序**：按入队顺序（由 `BattleTargetCollectionService` 的目标顺序决定，确定性），以 `unit_id` ordinal 作稳定 tiebreak（复用 `BattleContingencySystem.CompareStringNamesOrdinal`）。

**重复攻击去重**：`BattleRepeatAttackResolver.ApplyRepeatAttackSkillResult(...)` 逐段调用 `ResolveRepeatAttackStageResult(...)`，每段都会触发 sink。**同一 `(AttackerUnitId, DefenderUnitId)` 对在一次排空周期内只入队一次**，由 `SourceEventId` 去重（对齐 `BattleContingencySystem.ReserveSourceEventQueueSlot(...)` 的既有做法）。三段攻击 = 一次反击，不是三次。

### 4.2 反应预算：容量与间隔双双由敏捷驱动

```
ChargeCapacity     = clamp( 1 + agility_modifier, 1, 4 )
RechargeIntervalTu = clamp( round5( 60 - agility_modifier × 5 ), 25, 120 )
补充语义           = 每跨一个间隔，池子回满（不是 +1）
```

两条轴都吃敏捷是**有意的设计选择**（§13 决策 5）：本作其它模块加值本就强势，反应维度不需要额外保守。回满而非递增，是为了对齐 3.5e 战斗反射"每轮刷新全部 AoO"的语义。

#### 两个 clamp 是硬天花板，且都有物理理由

- **容量上限 4** = 中体型单位的正交邻居数（§1.4 已核实 `_are_units_adjacent` 是 Manhattan == 1）。**超过 4 的容量在近战场景下物理上花不出去**。这与仓库既有的 `ARMOR_MAX_DEX_BONUS` 封顶敏捷对 AC 的贡献是同一种形状，不是新发明的规则。下限 1 保底——再笨重的单位也能反击一次。
- **间隔下限 25** = 战场最快的行动节奏（敌人模板 `action_threshold` 最小值）。原则是**反应节奏永不快于战场最快的行动节奏**。上限 120 只是防装备/状态病态叠加的安全钳。

所以整体天花板硬定在 `4 次 / 25 TU`，敏捷再堆也到不了更高。

#### 常数来历

- **基线间隔 60 = 2 × `DEFAULT_CHARACTER_ACTION_THRESHOLD`**，设计陈述是"标准角色平均每两次自己的行动获得一次反击机会"。锚在既有常数上。
- **每点敏捷 5 TU** = 一个 `TuGranularity` 步长，天然对齐，无需额外取整。

#### 实际展开

| agility | mod | 容量 | 间隔 | 每 100 TU 反击次数 | 模板数 |
|---|---|---|---|---|---|
| 6 | -2 | 1 | 70 | 1.4 | 1 |
| 8 | -1 | 1 | 65 | 1.5 | 5 |
| 10–11 | 0 | 1 | 60 | 1.7 | 6 |
| **12–13** | +1 | 2 | 55 | **3.6** | 13 |
| **14–15** | +2 | 3 | 50 | **6.0** | 13 |
| 16 | +3 | 4 | 45 | 8.9 | 2 |
| 20（玩家高敏） | +5 | 4 | 35 | 11.4 | — |
| 24（极限） | +7 | 4 | 25 | 16.0 | — |

敌人模板常见区间（mod 0 → +3）差 **5.3 倍**，到玩家极限 **9.6 倍**。

**表中"每 100 TU 反击次数" = 容量 ÷ 间隔 × 100，是渐近吞吐上限，不是实战期望值。** 它假设每个补充周期内都有足够多的独立攻击动作把池子吃干。实战中单体挨打频率通常吃不满容量（§4.2 "容量只在真被围殴时才兑现"），所以绝对值偏乐观。该列用于**档位间相对比较**成立，不能拿去直接估算一场战斗的反击总数。

**现存 40 个敌人模板一个都不用改**，全部自动获得与敏捷相称的反应深度与速率。

#### 容量变化语义

- **升不补**：容量从 1 涨到 3，当前电量不变，靠回充填进去。同"最大生命上涨不回血"。
- **降夹紧**：容量掉回 1 时当前电量立即夹到 1，多余丢弃。
- 由 `ApplyCapacityRaw(...)` 原子处理，caller 不得自己读改写。

#### 配置链（对齐 `action_threshold` 的既有同构链路）

| 层 | `action_threshold` 现状 | 反应新增 |
|---|---|---|
| 派生属性 | `AttributeService.ACTION_THRESHOLD` | `REACTION_RECHARGE_INTERVAL_TU`、`REACTION_CHARGE_CAPACITY`、`REACTION_WINDOW_TU` |
| 属性层归一化 | `AttributeService.NormalizeActionThreshold(...)`(878) | `NormalizeReactionRechargeInterval(...)`（取整到 5 + clamp[25,120]）、`NormalizeReactionChargeCapacity(...)`（clamp[1,4]）、`NormalizeReactionWindow(...)`（0 或取整到 5 的正数） |
| 敌人 authoring | `EnemyTemplateDef.action_threshold` | `reaction_recharge_interval_tu` / `reaction_charge_capacity`（**默认 `0` = 从敏捷派生**）、`reaction_window_tu`（**默认 `0` = 跟随补充间隔**） |
| 战斗上下文默认 | `default_ally_action_threshold` | `default_ally_/default_enemy_reaction_*` |
| 工厂读取 | `BattleUnitFactory._resolve_action_threshold_from_snapshot(...)`(1228) | `_resolve_reaction_recharge_interval_from_snapshot(...)` / `_resolve_reaction_charge_capacity_from_snapshot(...)` |
| roster 写入 | `EncounterRosterBuilder.cs:731` | 同处追加 |
| 战斗层归一化 | `BattleTimelineDriver.NormalizeUnitActionThreshold(...)`(393) | `NormalizeUnitReactionRechargeIntervalTu(...)`，越界 `GameLog.Error` 回落 |
| 战斗开始播种 | `BattleTimelineDriver.InitializeUnitActionThresholds()`(412) | `InitializeUnitReactionBudget()` |
| 兜底常数 | `BattleUnitState.DefaultActionThreshold = 120` | `DefaultReactionRechargeIntervalTu = 60`、`DefaultReactionChargeCapacity = 1` |

**模板默认 0 = 派生**这条有实际好处：现存模板零改动。想做"反击专精 boss"再显式写值覆盖。

因为是派生属性，装备、trait、状态可经既有属性修正链改它，反击系统不再自建修正面——这也是"战斗反射 +1 容量"类内容的落地口，不需要新字段。

#### 推进时机

挂在 `BattleTimelineDriver.ApplyTimelineStep(...)` 的状态周期 tick 阶段之后：

```text
ApplyTimelineStep(batch, tuDelta)
  current TU 推进
  Control 区域归属/计分
  状态周期 tick / duration          ← 现有
  ★ 反应预算 AdvanceAndRefill        ← 新增，逐单位原子结算
  临时边 / 延迟区域 / 地形 / 屏障
  pending cast reconcile/advance/complete
  ready 收集与排序
```

放在状态 tick **之后**是有意的：状态可经属性链改容量，补充必须看到本 step 已生效的容量。

静滞（`time_stasis`）单位在该 step 内**冻结**补充，anchor 一并冻结不推进——与护盾 duration 的既有处理一致。

**加速 / 减速不影响反应补充。** `BattleTemporalStatusService` 已在改行动进度速率；若反应也吃同一倍率，haste 就在两条轴上双重加成，而 haste 本身已强。需要"加速也提升反应"的内容，直接经属性修正链改 `REACTION_RECHARGE_INTERVAL_TU`，不由系统内建。

#### 战斗开局

`InitializeUnitReactionBudget()` 播种间隔、容量与首个 anchor，且**开局即满**。开局空池会让先手方的第一击永远免疫反击。

### 4.3 触发种类

`BattleCounterattackTriggerKind`（typed 枚举，非自由 `StringName`，对齐 `BattleDamageBonusConditionKind` 的既有做法）：

| Trigger | 语义 | 对应内容 |
|---|---|---|
| `melee_hit_received` | 被近战武器攻击命中后 | 反击斩 |
| `melee_attack_evaded` | 近战攻击对自己未命中后 | 刹那反击 |
| `ranged_hit_received` | 被远程/投射攻击命中后 | 弓手后跃类（P2） |
| `any_attack_resolved` | 命中与否都触发 | 怪物天生反击（P2） |

近战 / 远程判定**必须**委托 `BattleEffectCategoryResolver`（它已从 `projectile_kind` 派生 `projectile` / `magical_projectile` / `nonmagical_projectile`），不得从技能 tag、射程或技能 id 推断——这是 skill_runtime.md 已写死的约束。

---

## 五、资格判定链

`BattleCounterattackRules.Evaluate(...)` 内部**严格按序**、fail-closed。顺序本身是合同，因为它决定了哪些失败要扣费（§7.3）。

| # | 判定 | 委托 owner | 失败原因 |
|---|---|---|---|
| 1 | 防御方存活**且 HP > 0** | `IsAlive()` + `GetCurrentHp()` | `defender_down` |
| 2 | **未被封锁反击** | `IsUnitCounterattackLocked(...)` | `counterattack_locked` |
| 3 | 无硬控阻断（paralyzed / stunned / time_stasis） | `BattleStatusSemanticTable` | `hard_controlled` |
| 4 | 存在有效反击姿态状态 | `BattleStatusEffectCollection` | `no_counter_stance` |
| 5 | 该姿态本窗口未用尽 | 状态 `counter_used_in_window`（§5.1） | `stance_exhausted` |
| 6 | 反应预算有余量 | `BattleUnitReactionState`（读，不扣） | `no_reaction_charge` |
| 7 | origin 允许反应 | `BattleEffectOrigin.CanTriggerReactions` | `reaction_origin_blocked` |
| 8 | 攻击方仍存活且在场 | `BattleState.TryGetUnitTyped` | `attacker_gone` |
| 9 | trigger 类型匹配本次攻击事实 | `BattleEffectCategoryResolver` | `trigger_mismatch` |
| 10 | 防御方→攻击方射程合法 | `BattleRangeService` | `out_of_reach` |
| 11 | 两者之间无阻断屏障 | `BattleBarrierService` | `barrier_blocked` |
| 12 | 资源足够（stamina） | `BattleUnitCombatResourceState` | `insufficient_resource` |
| 13 | 概率骰通过（`consumeRandomness = true` 时才掷） | 正式 RNG | `chance_failed` |

**第 1 条为什么要额外查 HP > 0**：CU-16 明确"默认态仍允许 `hp = 0 / alive = true`"。只查 `IsAlive()` 会让 0 血单位照常反击。反击是主动挥击，倒地单位不该能做，所以显式要求 HP > 0。

**第 10 条必须在排空时重新求值。** `vault_behind_target`、强制位移、`warrior_over_shoulder` 会在攻击结算中改变站位。入队时相邻不代表排空时仍相邻——这也是 §3.4 冻结坐标只用于日志的原因。

**第 11 条容易被漏。** 棱光球一类分层屏障隔开双方时反击不能穿过去。屏障边界穿越的唯一 owner 是 `BattleBarrierService`，不得按锚点坐标复制判断。

### 5.0 RNG 纪律与可重现性

`TrueRandomSeedService.RandiRange(...)` 是全仓库唯一的战斗随机入口——命中（`BattleHitResolver.cs:1023/1151`）、伤害骰（`BattleDamageResolver.cs:1958`）、豁免（`BattleSaveResolver.cs:585/590`）、耐久（`BattleEquipmentDurabilityResolver.cs:505`）共用同一条流。**没有分流设计**。

推论：**确定性完全来自调用顺序**。这让 §4.1 的排空顺序契约从"整洁"升级为"可重现性的承重墙"，两条硬规则：

1. **概率骰（第 13 条）必须先掷，失败则不构造攻击检定、不掷攻击骰。** 若失败后仍走一遍检定构造，会多消耗一次 RNG，同一局面的重放会分叉。
2. **排空顺序必须完全确定**：入队顺序 + `unit_id` ordinal tiebreak（§4.1）。任何依赖字典遍历顺序或引用地址的排序都会破坏重放。

反击不引入新的随机流，也不为反击单独播种。

### 5.1 姿态触发次数的窗口与存储

**窗口不再是 activation。** 反击的三条节流轴（容量、间隔、姿态窗口）全部脱离 `action_threshold`，这是决策 2 的彻底化——只要还有一条轴键在 activation 上，补充频率就仍隐式耦合行动节奏。

#### 窗口定义

新增单位级派生属性 `REACTION_WINDOW_TU`：

- **默认 `0` = 跟随 `RechargeIntervalTu`**。绝大多数内容不需要独立周期，此时姿态窗口与池子补充周期同相位、共用同一个 anchor，**不产生第三条 TU 轴**。
- **显式正值 = 独立周期**，用于"池子每 55 TU 转一圈，但这个姿态 110 TU 才能用一次"的慢循环强姿态。此时该姿态持有自己的 anchor。

归一化同 `RechargeIntervalTu`：正数、`TuGranularity`（5）整数倍，越界 `GameLog.Error` 回落。`EnemyTemplateDef.reaction_window_tu` 同样 `0 = 跟随`。

这个"0 = 跟随"模式与配置链里 `reaction_recharge_interval_tu` / `reaction_charge_capacity` 的"0 = 派生"一致，不是新形状。

#### 存储与清零

计数**存在状态自己身上**：`BattleStatusEffectState` 新增两个 runtime-only 字段（非 authoring，不出现在 `CombatEffectDef`）：

| 字段 | 语义 |
|---|---|
| `counter_used_in_window` | 本窗口内该姿态已触发次数 |
| `counter_window_next_reset_at_tu` | 该姿态窗口的下一次清零时点 |

清零由 `BattleRuntimeSkillTurnResolver` 在 timeline 状态周期 tick 阶段随 `next_tick_at_tu` 一并推进——它已经在遍历状态并处理 TU anchor，是唯一不需要新增遍历的落点。**anchor 按整窗口推进，不重置为 current TU**，与 §3.2 反应预算同一条余数纪律。

跟随模式（`REACTION_WINDOW_TU = 0`）下，`counter_window_next_reset_at_tu` 直接镜像 `BattleUnitReactionState.NextRechargeAtTu`，不独立推进——保证"池子回满"与"姿态次数清零"严格同刻，不会出现池子满了但姿态还锁着的割裂状态。

两个字段进 codec / clone / `BattleUnitStatePlainSnapshot` / AI mutation exact，不进 authoring schema。

#### 与 activation 的关系

彻底解耦后，`BattleUnitTurnState` 与反击系统**没有任何耦合**。反击不读 `HasTakenActionThisTurn()`，不挂 `ResetForTurnStart()`，activation 的快慢只通过"你多久被攻击一次"间接影响反击频率，不再通过任何直接通道。

---

## 六、Authoring 契约

### 6.1 `CombatEffectDef` 新增字段

放在 `effect_type = "status"` 的效果上，与既有 `guard_block` / `lock_counterattack` 同层：

| 字段 | 类型 | 默认 | 语义 |
|---|---|---|---|
| `counter_trigger` | `StringName` | `""` | 空 = 该状态不授予反击。非空必须是 §4.3 闭集之一，未知值加载期拒绝 |
| `counter_max_per_window` | `int` | `0` | `0` = 不额外限制（由容量与间隔节流）。正值用于长持续姿态限制每窗口次数，窗口定义见 §5.1 |
| `counter_chance_percent` | `int` | `100` | 触发概率 |
| `counter_attack_roll_bonus` | `int` | `0` | 反击这一击的命中修正 |
| `counter_stamina_cost` | `int` | `-1` | `-1` = 继承 `basic_attack` 自身的 `stamina_cost` |
| `counter_consume_status` | `bool` | `false` | 触发后立即移除该姿态（一次性反击） |

**`counter_max_per_window` 默认必须是 0 而不是 1。** 默认 1 会把容量彻底锁死——容量 3 配上"每窗口最多 1 次"，容量永远花不出去。

**不设 `counter_charge_capacity_bonus`。** 容量由敏捷派生（§4.2），"战斗反射 +1"类内容通过既有属性修正链改 `REACTION_CHARGE_CAPACITY`，不需要反击系统自建字段。

### 6.2 投影链

沿用既有五段式，每一段都要改，缺一段就会在加载期或运行期静默丢字段：

```text
CombatEffectDef                       新增 [Export] × 6
  -> SkillCombatProfileValidator      字段名映射表新增 6 项（对齐 cs:75 的 lock_counterattack 写法）
  -> CombatEffectDefinition           新增 immutable 属性 + 构造参数 + 克隆
  -> BattleStatusSemanticTable        写入 statusEntry（对齐 cs:433）
  -> BattleStatusEffectState          typed 字段 + counter_used_in_window
                                      + counter_window_next_reset_at_tu
                                      + OptionalSchemaFields/FormalParamKeys + 编解码 + Duplicate
  -> BattleStateReadView              只读读面（对齐 cs:398 的 LockCounterattack）
  -> BattleUnitStatePlainSnapshot     快照投影（对齐 cs:246）
```

`BattleStateReadView` 需要的读面：`CounterTriggerKind`、`CounterMaxPerWindow`、`CounterChancePercent`、`CounterAttackRollBonus`、`CounterStaminaCost`、`CounterConsumeStatus`、`CounterUsedInWindow`、`CounterWindowNextResetAtTu`。

装备来源同理需要 `EquipmentAbilityStatusDeclarationCatalog` 声明 + `EquipmentAbilityDefinitionProjection` 投影，否则装备授予的反击姿态过不了 membership 校验。

### 6.3 叠加语义

- **各姿态独立判定、独立计 `counter_max_per_window`**，但共享同一个 `BattleUnitReactionState` 预算池。这是防止叠姿态叠出无限反击的关键。
- 同一次触发事实匹配到多个姿态时，按状态 `power` 降序、`status_id` ordinal 升序取**第一个**，不逐个结算。命中的姿态 id 由 `CounterattackEligibility.SourceStatusId` 带出，扣次数时用它。
- **这里有一处刻意的不对称，不是 bug**：`counter_attack_roll_bonus` 累加**全部**姿态，但触发判定、概率骰与次数扣减只算选中的那一个。后果是高 `power` 姿态若 `counter_chance_percent = 50` 而低 `power` 姿态是 100，概率骰失败时低 power 姿态**不做补偿**——玩家直觉可能是"我叠了两层反击"。这是有意为之：触发是姿态各自独立的事件，不做"任一成功即成功"的合并。P2 内容变多后若被当 bug 报，回到本条。
- `counter_attack_roll_bonus` 跨状态**累加**（对齐既有约定：命中修正跨状态累加，惩罚默认累加，取大只走语义表 `TakeMax` 集合配置）。

---

## 七、执行与递归安全

### 7.1 执行出口

反击固定打出一次基础武器攻击（§13 决策 3），执行方式对齐既有先例 `BattleEquipmentAbilityRuntimeService.ResolveImmediateWeaponAttackAction(...)`（cs:1448）：

```text
取 basic_attack 的 SkillDefinition.CombatProfile.EffectDefinitions
  -> BattleAttackCheckPolicyService.BuildSkillDefinitionAttackContext / BuildAttackCheck
     （追加 counter_attack_roll_bonus）
  -> BattleDamageResolver.ResolveAttackEffects(defender, attacker, effects, check, AttackContext)
```

**不经过 `BattleSkillExecutionOrchestrator`，不构造 `AutoCastRequest`。** 顺带消掉两个问题：

- 不需要放宽 `BattleContingencyBridgeService.IsContingencyAutoCastSourcePlayerLearned(...)` 的"玩家已学"门禁。那条门禁对反击是错的（敌人和召唤物必须能反击），但改它会牵动 contingency。现在不碰。
- 不需要回答"反击是否吃技能的 AP / 冷却 / 读条"。没有借来的技能语义，就没有这些冲突。

**一处需要澄清的措辞**："不借技能语义"不是绝对的。`counter_stamina_cost = -1` 时仍要读 `basic_attack` 定义里的 `stamina_cost`，效果列表本身也来自它的 `CombatProfile`。准确说法是：**只借效果列表与体力基数，不借 AP / 冷却 / 熟练度 / 读条 / 目标形状**。内容若想完全脱钩，显式写 `counter_stamina_cost` 即可。

来源复核也随之简化：**姿态状态本身就是唯一权威**。`basic_attack` 由 `BattleUnitFactory._ensure_basic_attack_skill(...)` 与 `EncounterRosterBuilder.EnsureBasicAttackSkill(...)` 保证每个单位都有。

`AttackContext.SkillId` 仍填 `basic_attack`；反击身份由 `BattleEffectOrigin`（§7.2）承载，不塞进 skill id。

### 7.2 反击自身产生的连锁

反击是一次真实的武器攻击，会走完整 `ResolveAttackEffects`。以下连锁**明确允许**，不额外拦截：

| 连锁 | 结论 | 理由 |
|---|---|---|
| 攻击方身上的装备 `on_hit_received` / `after_hit_received` 被反击触发 | **允许** | 反击是真实攻击，装备反应本就该响应。级联深度由装备自身的 `once_scope` 约束 |
| 反击伤害触发攻击方的 **contingency** | **允许**（`canTriggerContingencies = true`） | 反击把人打到半血，对方"低血自动护盾"应当生效。这与 AutoCast 设 `false` 的理由不同——那是防 contingency 自递归 |
| 反击伤害计入 `BattleContributionLedger` / `BattleRatingSystem` / `BattleMetricsCollector` | **计入** | 反击是该单位的真实战斗贡献，影响战利品与评分是正确的 |
| 反击的攻击检定参与 Fate / misfortune | **参与** | 走 `BuildFateAwareAttackCheckPreview` 同一条链，不为反击开特例 |
| 反击**不发放技能熟练度** | **不发放** | `_skill_mastery_service` 只在 orchestrator 内调用，反击绕开 orchestrator，天然不触发。这是**想要的**——否则挨打就能刷熟练度。实现时不要"补上"这个看似的遗漏 |

### 7.3 递归保护

`BattleEffectOrigin` 新增：

```csharp
internal bool CanTriggerReactions { get; }

internal static BattleEffectOrigin Counterattack(CounterattackReleaseContext context) =>
    new("counterattack",
        canTriggerContingencies: true,    // 见 §7.2
        canTriggerReactions: false,
        ...);
```

`PlayerCommand()` / `AutoCast(...)` 都显式给出 `canTriggerReactions = true`。contingency 释放的法术打到人，对方应该能反击。

`CanTriggerReactions = false` 是**唯一**的反击递归闸门，配合 §5 第 7 条判定。不引入深度计数器——单一闸门比"深度 ≤ N"更容易证明终止，也更容易在回归里断言。

### 7.4 失败扣费语义

对齐已确立的规则（validation 期拒绝不扣费；执行期失败扣费是设计惩罚）：

- §5 第 1–12 条任一失败 → **不扣** stamina、**不扣** 反应预算、**不消耗** 姿态次数。这些都是 validation。
- 第 13 条概率骰失败 → 执行期。**扣 stamina、扣反应预算、计入姿态次数**。日志显式说明"反击未能抓住时机"。
- 反击的攻击检定 miss → 同上，全额扣费。设计惩罚，不是 bug。

### 7.5 AI mutation guard

反击在 AI 决策**提交阶段**发生，不在评估阶段。`BattleAiMutationSnapshot` 必须新增覆盖 `BattleUnitReactionState`（owner presence + 五个字段的 raw 语义）与状态上的 `counter_used_in_window` / `counter_window_next_reset_at_tu`。评估期任何路径都不得改动反应预算——否则 guard 在决策边界抛 `BattleAiMutationViolationException`，且按既有约定**不做状态回滚，fixture 直接废弃**。

---

## 八、预览与呈现

### 8.1 攻击方 preview 必须暴露反击风险

`BattlePreview` / `AttackPreviewData` 新增只读字段：

```csharp
public bool TargetMayCounterattack;
public int  TargetCounterChancePercent;
public BattleDamagePreviewRange TargetCounterDamageRange;
```

由 `BattleCounterattackRules.Evaluate(..., consumeRandomness: false)` 产出；伤害区间走 `BattleDamagePreviewRangeService` 对防御方的 `basic_attack` 求值。**preview 不掷骰、不扣预算、不消耗姿态次数、不写 store**。

呈现链路：`BattlePreviewProjection` → `BattlePresentationDelta` → `BattleHudAdapter`，三段都要带上新字段，否则 UI 拿不到。

理由：这是策略游戏，反击风险是出手前的决策输入。隐藏它会让"该不该打这个举盾的战士"退化成试错。

#### AoE 的聚合

上面三个字段是**单目标**语义。AoE / 线 / 锥技能会命中多个目标，其中可能有若干个能反击，preview 必须回答"我这一发会吃几次反击"。

规则：

- **逐目标求值**，不做提前收敛。每个目标独立跑一次 `Evaluate(..., consumeRandomness: false)`。
- **`BattlePreview` 增加一个聚合层**：`CounterattackRiskEntry[]`（每项含 `TargetUnitId` + 上述三字段）+ 一个汇总 `ExpectedCounterattackCount`（各目标 `ChancePercent / 100` 之和）。单目标技能退化为长度 1 的数组，不走两套路径。
- **聚合值必须考虑攻击方视角的重复**：同一个防御方在一次攻击动作里只反击一次（§4.1 去重），所以聚合按目标去重，不按命中次数累计。
- **UI 呈现**：单目标显示具体概率；多目标显示"预计 N 次反击"，逐目标明细可展开。具体布局归 UI 层，但**数据必须逐目标可用**，不能只给一个标量。

这条 P1 就要做——AoE 技能在现有内容里是常态，只做单目标 preview 会让 preview 在最需要它的场合失效。

### 8.2 反应预算的可见性

**自军单位**：当前电量 / 容量与下次补充 TU 必须在 HUD 可见。回满是锯齿形的（周期边界后满电、边界前空电），不可见就是隐藏信息。

**敌方单位**：不可见。否则玩家能精确卡在对方补充前出手，把策略退化成读表。

### 8.3 战斗日志

排空时逐条写入调用方 `BattleEventBatch`，格式对齐 `BattleReportFormatter`。被 `lock_counterattack` 封锁时**必须**输出显式行——否则玩家永远不知道黑星烙印做了什么，那个内容至今没有可感知效果。

### 8.4 P1 不做逐次确认

反击姿态是玩家在**更早的一次主动决策**中花 AP 挂上的，选择已经发生过一次；再要求逐次确认是把一个决策拆成 N 次点击，与"节奏慢 ≠ 操作多"的既定取向相反。

---

## 九、AI

`BattleAiScoreService` 新增风险项：候选攻击的期望收益减去目标反击的期望损失。

- 必须调用 `BattleCounterattackRules.Evaluate(..., consumeRandomness: false)`，不得在 evaluator 里近似复制资格链。反击不改变**合法目标集合**，所以可用 typed 快速估值而非完整 canonical preview——但**资格判定本身**必须是同一份实现。
- 新增评分参数进 `BattleAiScoreProfileDefinition`，并在 `docs/design/battle/ai_score_parameters.md` 登记。
- `BattleAiMoveToRangeActionEvaluator` 里已有的 `CanCounterattack` / `screening_can_counterattack` 是**位置层的"我方能否还手"启发式**，与本系统无关，不要合并或改名。

---

## 十、内容迁移

落地后把 §1.2 的三条技能从"伤害技能"改写为"姿态技能"：

| skill_id | 迁移后 |
|---|---|
| `warrior_counter_slash` | `effect_type = "status"`，`counter_trigger = melee_hit_received`，duration 到本次 activation 结束 |
| `warrior_moment_counter` | `counter_trigger = melee_attack_evaded`，`counter_consume_status = true`（一次性） |
| `warrior_phantom_blade_bait` | 保留伤害，追加"命中后给目标挂诱反 debuff"；诱反 = `counter_chance_percent = 100` + 反击 miss 时自身挂 `staggered`。属于 P2 |

`warrior_mind_eye_counter` 不迁移，实现与描述一致。`warrior_counter_flag`（反攻旗帜）是团队 buff，不属于本系统。

**表现力限制**：反击固定是基础武器攻击（§13 决策 3），姿态之间的差异只来自 `counter_trigger` / `counter_attack_roll_bonus` / `counter_chance_percent` / `counter_consume_status`。"反击斩"和"刹那反击"打出来的都是一次普通挥击，区别在触发条件与命中修正，不在招式本身。迁移时描述文案要如实改写，不要保留暗示特殊招式的措辞。

**注意**：按仓库兼容策略，改写既有 `.tres` 的字段语义前需要确认——这些技能已在职业技能树里，改法会改变已有角色的技能行为（技能定义来自内容快照而非存档，所以不破档，但会改变手感）。

---

## 十一、分期

### P1：规则内核

- `BattleUnitReactionState`、`BattleCounterattackRules`、`CounterattackHookFact` / `CounterattackReleaseContext`、`IBattleAttackResolvedSink` + `BattleAttackResolvedContext`、`BattleCounterattackSystem` + bridge
- 端口在 `BindDamageResolver()`（cs:2802）装配，**不改任何方法名**；sink 调用点是 `ResolveAttackEffects` 的**两条出口**——miss 提前返回（cs:559 前）与命中路径（cs:623 后），见 §3.5
- 反应预算配置链：`REACTION_RECHARGE_INTERVAL_TU` / `REACTION_CHARGE_CAPACITY` 两个派生属性、两层归一化、`EnemyTemplateDef` 两个字段（默认 0 = 派生）、battle context 默认值、工厂/roster 写入、`InitializeUnitReactionBudget()`、`ApplyTimelineStep` 推进
- `CombatEffectDef` 6 个字段 + 2 个 runtime 字段的完整投影链（§6.2）；`REACTION_WINDOW_TU` 派生属性与 `EnemyTemplateDef.reaction_window_tu`（0 = 跟随补充间隔）
- `BattleEffectOrigin.CanTriggerReactions`
- `melee_hit_received` + `melee_attack_evaded` 两个 trigger
- **消费 `lock_counterattack`**
- preview 字段 + 呈现链路 + 预算可见性 + 战斗日志
- codec / clone / AI mutation stable projection 覆盖

### P2：内容与 AI

- §10 的三条技能迁移
- `ranged_hit_received` + `any_attack_resolved`
- AI 反击风险评分项
- 怪物天生反击（trait passive 姿态状态）
- "战斗反射"类内容（经属性修正链加 `REACTION_CHARGE_CAPACITY`）

### P3：威胁区与借机攻击

**独立提案，不在本文范围。** 需要 `BattleMovementService` 逐步位置钩子、威胁区几何（大单位 footprint）、`BattleMovementQueryService` 路径评分计入 AoO 代价、AI 绕行决策。爆炸半径至少是 P1 的两倍。

---

## 十二、回归清单

新增 headless runner，放 `tests/battle_runtime/`：

| 文件 | 覆盖 |
|---|---|
| `rules/run_counterattack_eligibility_regression.cs` | §5 十三条判定逐条，含顺序与扣费边界；`hp = 0 / alive = true` 不反击 |
| `runtime/run_counterattack_queue_order_regression.cs` | AoE 多目标入队/排空顺序；防御方在触发攻击中死亡时不反击；免死后反击成立；重复攻击三段只反击一次 |
| `runtime/run_counterattack_recursion_guard_regression.cs` | 反击不触发反击；contingency 释放的攻击**能**被反击；反击伤害**能**触发攻击方 contingency |
| `runtime/run_counterattack_cascade_regression.cs` | 反击触发攻击方装备 `on_hit_received` 且级联不失控；反击**不**发放技能熟练度；反击伤害计入 contribution / rating / metrics |
| `runtime/run_counterattack_reaction_budget_regression.cs` | 预算原子扣减；多姿态共享同一池；开局即满；容量升不补 / 降夹紧 |
| `runtime/run_counterattack_recharge_anchor_regression.cs` | 跨多间隔一次回满；anchor 按整间隔推进不右漂；`time_stasis` 冻结补充与 anchor；非 5 TU 倍数间隔被归一化并报错；haste 不影响补充 |
| `rules/run_counterattack_agility_derivation_regression.cs` | 容量/间隔按 §4.2 表逐档正确；两个 clamp 生效；模板 `0` 走派生、正值走覆盖 |
| `rules/run_counterattack_lock_regression.cs` | 黑星烙印 / 厄命宣判 / 折手 / 黑冠封印四条既有内容真正阻止反击 |
| `rules/run_counterattack_reach_revalidation_regression.cs` | `vault_behind_target` / 强制位移后排空时重新求值射程 |
| `rules/run_counterattack_barrier_regression.cs` | 分层屏障隔断反击 |
| `rules/run_counterattack_stance_window_regression.cs` | 跟随模式下姿态清零与池子回满严格同刻；独立窗口模式下按自身 anchor 整窗口推进不右漂；反击链路完全不读 `BattleUnitTurnState` |
| `rules/run_counterattack_preview_parity_regression.cs` | preview 不掷骰不扣费，且与正式判定结论一致 |
| `runtime/run_counterattack_miss_path_sink_regression.cs` | **`melee_attack_evaded` 在 miss 提前返回路径（出口 A）被正确入队**；出口 A 的 sink 在 `ClearComboStackOnMiss` / `ConsumeOneShotAttackCheckStatuses` 之后触发 |
| `runtime/run_counterattack_rng_order_regression.cs` | 概率骰失败时**不**消耗攻击检定 RNG；同一 seed + 同一局面重放结果一致；排空顺序不依赖字典遍历顺序 |
| `rules/run_counterattack_aoe_preview_regression.cs` | AoE 逐目标产出 `CounterattackRiskEntry`；同一防御方不重复计数；单目标退化为长度 1 数组走同一路径 |
| `ai/run_counterattack_ai_mutation_guard_regression.cs` | 评估期不动反应预算与姿态计数 |

按 AGENTS.md，战斗数值模拟 runner 不进常规全量回归，仅在显式做平衡分析时跑。

**模拟场景注意**：`data/configs/battle_sim/scenarios/*.tres` 的 `action_threshold` 是 110/120，与生产内容的 25–60 不是一个尺度。用 sim 分析反击 build 时，场景必须显式写 `reaction_recharge_interval_tu` 对齐自身尺度，否则"每次行动能反击两次"的结论无效。

---

## 十三、决策记录

### 已定（2026-07-25）

1. **反击不消耗 AP**，只消耗 stamina + 反应预算。AP 属于单位自己的 activation，让反击吃 AP 会使"被打"直接削弱下一次主动行动，反馈链过长。

2. **反应预算按单位级 TU 间隔补充**，不绑 activation。绑 activation 会让补充频率隐式耦合 `action_threshold`，且该耦合在内容侧不可见、不可单独调。

3. **反击固定打出一次基础武器攻击。不引入 `counter_skill_id`，技能反击本期不设计。**
   考虑过让姿态声明任意技能（盾反触发 `warrior_shield_bash` 自带击晕），但技能自带 AP、stamina、冷却、熟练度、读条与目标形状六套语义，反击场景下每条都要单独裁决。收益不足以抵消六条裁决与对应回归的成本，**且不加只有一个合法取值的字段**。将来若确需技能反击，按 §7.1 的"只借效果列表"路线新增字段。

4. **容量与间隔均由敏捷派生**，公式与常数见 §4.2。容量上限 4 与间隔下限 25 都有物理理由（正交相邻数、战场最快行动节奏），不是平衡拍数。

5. **允许敏捷在容量与间隔上双重加成。** 曾提议把敏捷只绑一条轴以避免吞吐相乘（mod 0 → +3 相差 5.3 倍，到玩家极限 9.6 倍），但本作其它模块加值本就强势，反应维度不需要额外保守。天花板由两个 clamp 硬钳在 `4 次 / 25 TU`，不会失控。

6. **姿态窗口也脱离 activation，改为单位级 TU 配置。** `REACTION_WINDOW_TU` 默认 `0 = 跟随补充间隔`（不产生第三条 TU 轴），显式正值表达慢循环姿态。这是决策 2 的彻底化——只要还有一条节流轴键在 activation 上，反击频率就仍隐式耦合 `action_threshold`。完成后反击链路与 `BattleUnitTurnState` 零耦合。

### 待决

暂无。

---

## 十四、必须保持的不变量

- 反击资格判定只有一个 owner（`BattleCounterattackRules`），preview / execution / AI 三方共同消费，禁止近似复制。
- 射程、屏障、命中、伤害、死亡、状态语义全部委托既有 canonical service，不在反击链里复制规则或按 skill_id 建特例。
- 入队与排空严格分离；排空发生在完整攻击 effect 结算之后、outcome 提交之前，同一调用栈、同一 `BattleEventBatch`。
- `ResolveAttackEffects` 的**两个 return 点都要挂 sink**（miss 提前返回 + 命中路径）。只挂命中路径会让 `melee_attack_evaded` 静默失效。
- 全仓库共用 `TrueRandomSeedService` 单一随机流，确定性只来自调用顺序：概率骰先掷、失败不构造攻击检定；排空顺序不得依赖字典遍历或引用地址。
- 触发事实冻结的坐标**只用于日志**；射程与屏障在排空时按当前坐标重新求值。
- 同一 `(攻击方, 防御方)` 对在一次排空周期内只入队一次，重复攻击多段不产生多次反击。
- `CanTriggerReactions = false` 是唯一反击递归闸门。反击**不**关闭 contingency，也不关闭装备反应。
- 反应预算的判定与扣减必须原子（`TryConsumeCharge()`），容量变化必须原子（`ApplyCapacityRaw(...)`），禁止两段式。
- 补充 anchor 按整间隔推进，不重置为 current TU；跨任意个间隔都回满。补充规则归 `BattleUnitReactionState`，`BattleTimelineDriver` 只提供 current TU 与调用时机。
- 反应间隔必须为正且是 `TuGranularity`（5）的整数倍，容量必须在 [1, 4]，违反时报错回落默认值。
- 反击不发放技能熟练度；反击伤害计入 contribution / rating / metrics。
- preview 不消费 RNG、不扣预算、不消耗姿态次数、不写 store。
- 新增战斗局部 owner 不进存档，但必须进 codec / clone / AI mutation stable projection。
- 不新增旧 payload / schema 兼容路径。
