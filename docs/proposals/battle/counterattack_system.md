# 反击系统架构提案

> 状态：`Proposal / Architecture review findings resolved / Not implemented`
> 更新日期：`2026-07-28`
> 关联上下文单元：CU-12（成长桥接）、CU-14（职业 / 技能进阶规则）、CU-15（战斗运行时总编排）、CU-16（战斗状态 / 规则 / 伤害）、CU-18（战斗展示）、CU-19（回归）
> 源码锚点：`eeae4ba85d9ef85cf3ee37aebfe0c61bcf0baf02`

本文所有 `file:line` 都指向上述源码提交；实施期间若行号漂移，以同一方法名和列出的语句 token 为准。提案中的新类型、目标代码骨架与迁移动作仍是未实现设计，不能把代码块误读为当前仓库已有 API。

## 一、范围与结论

本文只定义反击系统的运行时架构，不设计或迁移任何具体技能、trait、装备、敌人模板或 `.tres` 内容。

本轮要闭合的是以下架构问题：

1. 一次逻辑攻击如何获得稳定身份，并跨普通攻击、重复攻击、冲锋、地面攻击、装备即时攻击与 AutoCast 传播。
2. `BattleDamageResolver` 如何只发布已结算的攻击事实，而不反向依赖 runtime。
3. 多目标、嵌套攻击和同步装备反应存在时，反击何时入队、何时排空、如何去重。
4. 反击如何复用完整武器攻击语义，而不丢失装备反应、耐久、死亡、contingency、贡献、评分与 metrics。
5. 反应预算如何拥有 TU anchor，并在 `time_stasis` 下真正冻结而不解除后追赶补充。
6. preview、HUD 与未来 AI 如何复用同一资格规则且不消费 RNG、不修改战斗状态。

本文修订后，P1A 的运行时内核可以按明确 owner 实施；内容来源、具体数值和平衡属于后续独立设计。

---

## 二、当前事实与现有缺口

### 2.1 可复用的现有 owner

| 能力 | 当前 owner | 可复用范围 |
|---|---|---|
| 命中、暴击、伤害、状态、装备 attack reaction | `BattleDamageResolver` 及其现有 query / sink 端口 | 反击的真实攻击结算 |
| 射程 | `BattleRangeService` | 反击执行前的正式射程查询 |
| 分层屏障 | `BattleLayeredBarrierService`（`BattleBarrierService` 的正式 runtime subtype） | 反击执行前的 `PreviewSkillBarrierInteractionResult(...)` 查询 |
| current stamina | `BattleUnitCombatResourceState` | 反击尝试成本 |
| TU 与静滞调度 | `BattleTimelineDriver`、`BattleRuntimeSkillTurnResolver` | 反应预算推进 |
| effect origin | `BattleEffectOrigin` 与当前 runtime origin scope | 递归控制与报告来源 |
| 异步式反应队列先例 | `BattleContingencySystem` | FIFO、同 batch、同步排空的形状 |
| 展示读取 | `BattlePreview`、`BattleHudAdapter`、`BattleHudSnapshot` | 风险与预算的 detached projection |

现有 `lock_counterattack` 已有 typed 状态字段、投影和运行时查询，但没有实际反击消费者。P1A 只消费这个现有事实，不扩展其内容来源。

### 2.2 不能直接沿用旧提案的地方

| 缺口 | 当前事实 | 若不修正的后果 |
|---|---|---|
| 攻击没有稳定逻辑 ID | `AttackContext` 只有 state、skill id、batch 与骰子覆盖 | 重复攻击阶段无法可靠去重 |
| origin 没有进入攻击上下文 | origin 主要由 runtime ambient stack 提供 | sink 无法证明本次攻击能否触发反击 |
| 没有正式 delivery kind | `BattleEffectCategoryResolver` 不等于近战/远程武器分类 | `melee_*` trigger 会误判或静默失效 |
| 排空点只描述 orchestrator | `ResolveAttackEffects` 还有 repeat、charge、equipment、ground 等调用方 | 队列可能泄漏到下一动作或错序执行 |
| charge 使用局部事件 batch | `BattleChargeResolver` 当前创建 `chargeBatch`，结束后才 merge 到 caller batch | 与“一个 reaction boundary 只有一个 batch”冲突 |
| AutoCast 可隐式创建 batch | `BattleContingencyBridgeService.ExecuteAutoCast(...)` 当前存在 `batch ?? _new_batch()` | AutoCast 可能脱离正式 boundary，反击队列无人排空 |
| 即时武器攻击先例不完整 | 装备即时攻击没有完整 contribution/rating/metrics 提交 | 反击结果与普通真实攻击不等价 |
| 标准攻击 outcome 尚未模块化 | `_apply_unit_skill_result(...)` 把通用提交与 mastery、shield、special effect 等技能语义交织在同一方法 | 新 committer 若整段复制会误带技能专属行为 |
| 静滞只“跳过补充”不够 | current TU 继续前进，现有冷却会显式平移 anchor | 静滞解除后会追赶冻结期补充 |
| rules API 直接依赖 runtime service | 射程、屏障、成本又需要正式攻击 definition/query | rules/runtime 分层反转且 preview 难复用 |
| preview 走错数据链 | `BattlePresentationDelta` 只表达 dirty facts | HUD 拿不到或重复保存 preview 业务数据 |

因此不能把反击实现成“在伤害 resolver 加一个回调，再从 orchestrator 排一次队”。需要显式的攻击动作身份与反应边界。

---

## 三、目标、约束与非目标

### 3.1 目标

1. 反击是战斗运行时的 typed 机制，不读取或分支任何具体 `skill_id`。
2. 所有正式攻击入口共用同一个攻击事实契约。
3. 伤害 resolver 只发布事实；counterattack runtime 负责候选捕获、排队、资格复核、扣费和执行。
4. 同一逻辑攻击中，同一 `(attacker, defender)` 最多产生一次反击机会。
5. 多目标原始动作全部完成正式后处理后，才开始执行反击。
6. 反击是真实武器攻击，但不消费 AP、不产生读条/冷却；它不发放反击动作 definition
   自身的技能熟练度，但符合正式 `weapon_attack_quality` 条件时，必须给实际使用的
   `sword_training` / `bow_training` / `unarmed_training` 发放武器精通熟练度；任何
   `weapon_training` 类型只增长熟练度与技能等级，不能成为职业升级触发技能，也不能由
   本次 grant 请求职业晋升弹窗。
7. 反击可触发现有装备反应与 contingency；反击的直接攻击事实不能再次触发反击。
8. battle-local 状态可被 clone、codec、canonical/detached snapshot 与 AI mutation exact 完整观察。
9. runtime query、preview 与未来 AI 使用同一组确定性资格规则。

### 3.2 硬约束

- 保留装备 reaction 当前的同步嵌套调用顺序，不把已有装备反应改成延迟事件。
- 不在 `BattleDamageResolver` 中持有 `BattleRuntimeModule` 或 `BattleCounterattackSystem`。
- 不以 batch、技能 ID、日志文本、单位坐标或 drain 周期推导逻辑攻击身份。
- 不在 preview / AI evaluation 中消费 RNG、stamina、反应次数或修改队列。
- 武器精通的职业升级禁用规则按现有 `weapon_training` 内容标签统一判定，不按
  `sword_training/bow_training/unarmed_training` 三个具体 ID 复制白名单。
- 不新增旧 payload/schema 兼容路径；本系统尚未落地，没有历史反击数据需要迁移。

### 3.3 本文非目标

- 任何具体技能及其 authoring 字段、持续时间、描述或迁移。
- trait、装备、敌人模板如何授予反击能力。
- 敏捷或其他属性如何派生容量、恢复间隔。
- 反击数值、概率、默认容量与恢复间隔的最终平衡。
- 借机攻击、威胁区、移动中断。
- AI 评分参数与具体策略权重。
- 玩家逐次确认是否反击的交互模式。

这些非目标不得通过临时 `skill_id` 分支、状态 `params` 字典或 runtime 白名单偷偷进入 P1A。

---

## 四、方案比较

### 方案 A：只在 `BattleSkillExecutionOrchestrator` 挂钩和排空

优点：

- 改动最小。
- 普通单位目标技能容易找到“全部目标结束”的位置。

缺点：

- 无法覆盖 `BattleRepeatAttackResolver`、`BattleChargeResolver`、`BattleEquipmentAbilityRuntimeService`、`BattleGroundEffectService` 和独立 AutoCast。
- pending cast、timeline effect 与嵌套即时攻击没有统一边界。
- 新增攻击入口时容易漏接且不会编译失败。

结论：不采用。它把“当前主路径”误当成“全部攻击 owner”。

### 方案 B：在 `BattleDamageResolver` 出口立即执行反击

优点：

- 所有调用方天然覆盖。
- 不需要额外排空入口。

缺点：

- AoE 第一个目标会在后续目标尚未结算前反击，结果依赖目标遍历顺序。
- 反击会插入原攻击的状态、位移、死亡、贡献与 outcome 后处理之间。
- 规则层会反向控制 runtime，递归和生命周期无法证明。

结论：不采用。resolver 只能发布事实，不能执行反击。

### 方案 C：攻击事实 sink + 显式攻击动作/反应边界 + FIFO 排空

形状：

```text
攻击 producer
  -> BeginReactionBoundary(batch)
  -> PushEffectOrigin(origin)
  -> BeginLogicalAttack(deliveryKind) -> BattleAttackActionContext
  -> ResolveAttackEffects(..., AttackContext.Action)
       -> IBattleAttackResolutionSink.OnAttackResolved(fact, EventBatch)
       -> 只捕获候选并入队
  -> producer 完成状态、位移、死亡、贡献、outcome 等后处理
  -> CompleteLogicalAttack()
  -> CompleteReactionBoundary()
       -> FIFO 排空直到队列为空
```

优点：

- resolver 的所有正式调用方都能被强制接入同一 action contract。
- 多段复用同一 action id，独立 follow-up 获得新 action id。
- 原动作的全部后处理先完成，反击不会穿插 AoE。
- nested AutoCast / equipment attack 仍保持同步，但新反击候选只在边界尾部排空。
- action id、origin、delivery kind、batch 与队列生命周期都有唯一 owner。

代价：

- 需要修改所有正式攻击 producer 以传播 typed action context。
- 需要把 charge 的局部 batch 与 AutoCast 的隐式 batch fallback 收敛到单一 root batch contract。
- 需要抽出完整即时武器攻击服务，而不是复制装备即时攻击实现。
- 需要从当前 orchestrator 精确拆出通用 outcome phases，并用等价回归保护标准路径。

结论：采用方案 C。

---

## 五、ownership 模型

```text
BattleEffectExecutionContextService            CU-15 runtime
  └─ BattleEffectOrigin stack

BattleAttackActionCoordinator                  CU-15 runtime
  ├─ reaction-boundary depth / single root batch
  ├─ nested-depth / work-item safety guard
  ├─ logical action id allocator
  ├─ snapshots current effect origin into each action
  └─ boundary completion -> drain callback

BattleDamageResolver                           CU-16 rules
  └─ IBattleAttackResolutionSink
       └─ publishes BattleAttackResolutionFact only

BattleCounterattackSystem                      CU-15 runtime
  ├─ candidate capture
  ├─ dedupe set + FIFO queue
  ├─ attempt transaction / RNG ordering
  └─ calls query + immediate attack service

BattleCounterattackQueryService                CU-15 runtime query
  ├─ unit/capability read
  ├─ lock / hard control / hostility
  └─ canonical immediate weapon attack query

BattleCounterattackRules                       CU-16 pure rules
  └─ ordered fail-closed evaluation

BattleCounterattackPreviewService              CU-18 read-only projection
  ├─ reuses query readiness + immediate attack query
  ├─ per-defender stage/dedupe probability
  └─ immutable risk projection + typed coverage

BattleImmediateWeaponAttackService             CU-15 runtime
  ├─ canonical attack plan
  ├─ BattleAttackCheckPolicyService
  ├─ BattleDamageResolver
  ├─ BattleWeaponAttackOutcomeCommitter
  │    ├─ resolver surface
  │    ├─ post-producer hooks
  │    ├─ result surface
  │    ├─ terminal outcome
  │    └─ BattleEquipmentDurabilityResultProjector
  └─ BattleSkillMasteryService
       └─ stateless weapon-training grant after counterattack terminal

SkillProfessionPromotionRules                  CU-14 progression rules
  └─ weapon_training never becomes a profession level trigger

CharacterManagementModule                     CU-12 progression bridge
  └─ weapon-training mastery delta never requests promotion modal

BattleUnitCounterattackCapabilityState         CU-16 unit component
  └─ active typed capability instances

BattleUnitReactionState                        CU-16 unit component
  └─ shared charge budget + TU anchor
```

### 5.1 owner 边界

- `BattleEffectExecutionContextService` 是 effect origin stack 的唯一 owner，普通攻击、非攻击效果、AutoCast 与反击都通过它建立 origin scope。
- `BattleAttackActionCoordinator` 只拥有攻击 ID、reaction boundary、单一 root batch 与 boundary work guard，不判断反击资格。
- `BattleCounterattackSystem` 只拥有反击队列和反击 attempt，不复制命中、范围、屏障或伤害规则。
- `BattleCounterattackQueryService` 只收集 canonical facts，不消费 RNG、不写状态。
- `BattleCounterattackRules` 只解释输入事实与失败顺序，不持有 runtime service。
- `BattleCounterattackPreviewService` 只拥有当前状态条件风险的只读聚合，不伪造
  attack fact、action/root scope 或未知 producer 的概率。
- `BattleImmediateWeaponAttackService` 是“即时真实武器攻击”的唯一执行入口，并拥有
  counterattack terminal outcome 之后调用武器精通 grant 的准确时序。
- `BattleWeaponAttackOutcomeCommitter` 只拥有正式武器攻击的四个代码时序阶段与 typed outcome policy，不拥有 mastery、shield 或技能 special effect。
- `BattleSkillMasteryService` 继续拥有 `weapon_attack_quality`、武器 family → training
  skill 映射与目标等级倍率；counterattack 只能调用新增的 stateless grant builder，
  不得读写 command 级 `_resolutionEvents` accumulator。
- `SkillProfessionPromotionRules` 是“某技能能否成为职业升级触发技能”的唯一规则 owner；
  `weapon_training` 标签固定返回 false，`LevelGrowthEvaluationService` 与
  `ProgressionService`、`ProfessionRuleService` 共同消费，不允许 battle runtime 自己判断。
- `CharacterManagementModule` 仍是 mastery grant → `CharacterProgressionDelta` 的桥接
  owner；对 `weapon_training` grant 保留 mastery/skill-level/achievement delta，但从
  本次 delta 中清除 pending profession choices 并固定
  `needs_promotion_modal=false`。它不能删除 `UnitProgress` 中由其他技能产生的既有 pending
  choice。
- `BattleEquipmentDurabilityResultProjector` 只把 resolver 已经提交的 `EquipmentDurabilityEvents` 投影为 batch 日志与 destroyed-equipment dirty facts；它不再次修改耐久，也不判断本次攻击是否为武器攻击。标准武器、反击与仍留在 orchestrator 的非武器技能都调用这一份实现。
- `BattleReactionBoundarySafetyRules` 只提供 production 技术熔断值，不成为内容、平衡或 authoring owner。
- `BattleUnitReactionState` 是反应 charge 与 recharge anchor 的唯一存储。
- `BattleUnitCounterattackCapabilityState` 是当前有效反击能力实例的唯一战斗局部存储。

---

## 六、typed 攻击契约

### 6.1 逻辑攻击 ID

新增 plain value object：

```csharp
internal readonly record struct BattleAttackActionId(long Value)
{
    internal bool IsValid => Value > 0;
}
```

规则：

1. ID 只由 `BattleAttackActionCoordinator` 单调分配。
2. ID 只在当前 battle runtime 内有意义，不进入存档或内容 schema。
3. 重复攻击的所有 stage 复用同一 ID。
4. 同一动作的多个目标复用同一 ID。
5. 独立触发的 follow-up、装备即时攻击、AutoCast 和反击各自获得新 ID。
6. 延迟到未来 timeline 才结算的攻击，在实际结算时获得新 ID。
7. 不允许用 `skill_id + unit_id`、batch identity 或当前 TU 拼接 ID。

`BattleAttackActionId` 是 core contract，固定声明在
`scripts/systems/battle/core/BattleAttackActionContext.cs`；不能随 coordinator 放进 runtime
文件，否则 core fact/context 会反向依赖 runtime。

repeat、charge、random-chain、Nine Echo terminal hit 与同一次立即 ground effect 不重新打开
logical scope。创建 scope 的最外层 producer 保持 scope 存活，并把同一个非空
`BattleAttackActionContext` 逐层传到每个 resolver call。这样“复用父 ID”不是一个可被晚些时候
重新打开的 API，也不存在把旧 context 带入后续 root/timeline 的合法路径。

### 6.2 origin 的正式 owner

`BattleEffectOrigin` 新增：

```csharp
internal bool CanTriggerReactions { get; }
```

存在四种正式 origin：

| origin | CanTriggerContingencies | CanTriggerReactions |
|---|---:|---:|
| player/AI command | true | true |
| timeline mutation segment | true | true |
| contingency AutoCast | false | true |
| counterattack | true | false |

`BattleEffectOrigin` constructor 增加 `canTriggerReactions` 与
`triggeringAttackActionId`，四个 factory 的精确值为：

```csharp
private BattleEffectOrigin(
    StringName originKind,
    bool canTriggerContingencies,
    bool canTriggerReactions,
    StringName ownerMemberId = default,
    StringName setupId = default,
    StringName instanceId = default,
    StringName skillEntryId = default,
    StringName storedSkillId = default,
    StringName triggerType = default,
    long triggeringAttackActionId = 0
)
{
    OriginKind = Normalize(originKind);
    if (OriginKind == new StringName(""))
        throw new ArgumentException("origin kind is required");
    if (triggeringAttackActionId < 0)
        throw new ArgumentOutOfRangeException(
            nameof(triggeringAttackActionId)
        );
    CanTriggerContingencies = canTriggerContingencies;
    CanTriggerReactions = canTriggerReactions;
    OwnerMemberId = Normalize(ownerMemberId);
    SetupId = Normalize(setupId);
    InstanceId = Normalize(instanceId);
    SkillEntryId = Normalize(skillEntryId);
    StoredSkillId = Normalize(storedSkillId);
    TriggerType = Normalize(triggerType);
    TriggeringAttackActionId = triggeringAttackActionId;
}

internal long TriggeringAttackActionId { get; }
internal bool CanTriggerReactions { get; }

internal static BattleEffectOrigin PlayerCommand() =>
    new(
        "player_command",
        canTriggerContingencies: true,
        canTriggerReactions: true
    );

internal static BattleEffectOrigin AutoCast(AutoCastRequest request) =>
    new(
        "contingency_auto_cast",
        canTriggerContingencies: false,
        canTriggerReactions: true,
        ownerMemberId: request?.OwnerMemberId ?? "",
        setupId: request?.SetupId ?? "",
        instanceId: request?.InstanceId ?? "",
        skillEntryId: request?.SkillEntryId ?? "",
        storedSkillId: request?.StoredSkillId ?? "",
        triggerType: request?.ReleaseContext?.TriggerType ?? ""
    );

internal static BattleEffectOrigin Timeline(StringName segmentKind)
{
    if (segmentKind == new StringName(""))
        throw new ArgumentException("timeline segment kind is required");
    return new BattleEffectOrigin(
        "timeline",
        canTriggerContingencies: true,
        canTriggerReactions: true,
        triggerType: segmentKind
    );
}

internal static BattleEffectOrigin Counterattack(
    BattleAttackActionId triggeringActionId,
    StringName capabilityInstanceId
)
{
    if (!triggeringActionId.IsValid)
        throw new ArgumentException("triggering action id is invalid");
    if (capabilityInstanceId == new StringName(""))
        throw new ArgumentException("capability instance id is required");
    return new BattleEffectOrigin(
        "counterattack",
        canTriggerContingencies: true,
        canTriggerReactions: false,
        instanceId: capabilityInstanceId,
        triggerType: "counterattack",
        triggeringAttackActionId: triggeringActionId.Value
    );
}
```

`ToPlainDictionary()` 无条件新增 `["can_trigger_reactions"]` 与 `["triggering_attack_action_id"]`。普通 command/timeline/AutoCast 的 triggering id 为 `0`；counterattack 必须为正数。不能把 action id 塞入 `trigger_type` 或 `instance_id` 字符串。

当前 origin stack 不应继续由 metrics service 作为业务真相 owner。P1A 将其迁入独立的
`BattleEffectExecutionContextService`。攻击读取与非攻击 report 读取必须是两个命名不同的
API：攻击禁止空栈回落，非攻击 report 维持当前默认 `PlayerCommand` 展示语义。目标实现固定为：

```csharp
internal sealed class BattleEffectExecutionContextService
{
    private readonly List<Frame> _frames = new();
    private long _nextScopeId = 1;
    private long _generation = 1;

    internal int Depth => _frames.Count;

    internal BattleEffectOrigin CurrentForReporting =>
        _frames.Count > 0
            ? _frames[^1].Origin
            : BattleEffectOrigin.PlayerCommand();

    internal BattleEffectOrigin RequireCurrentForAttack()
    {
        if (_frames.Count == 0)
        {
            throw new InvalidOperationException(
                "logical attack requires an explicit effect origin"
            );
        }
        return _frames[^1].Origin;
    }

    internal IDisposable Push(BattleEffectOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        long scopeId = checked(_nextScopeId++);
        long generation = _generation;
        _frames.Add(new Frame(scopeId, origin));
        return new Scope(this, scopeId, generation);
    }

    internal void Clear()
    {
        _frames.Clear();
        _generation = checked(_generation + 1);
        _nextScopeId = 1;
    }

    private void Pop(long scopeId, long generation)
    {
        // ClearBattleState/teardown 已经清过的旧 handle 不得弹出新 battle 的 frame。
        if (generation != _generation)
            return;
        if (_frames.Count == 0 || _frames[^1].ScopeId != scopeId)
        {
            throw new InvalidOperationException(
                "effect origin scopes must be disposed in LIFO order"
            );
        }
        _frames.RemoveAt(_frames.Count - 1);
    }

    private readonly record struct Frame(
        long ScopeId,
        BattleEffectOrigin Origin
    );

    private sealed class Scope : IDisposable
    {
        private BattleEffectExecutionContextService _owner;
        private readonly long _scopeId;
        private readonly long _generation;

        internal Scope(
            BattleEffectExecutionContextService owner,
            long scopeId,
            long generation
        )
        {
            _owner = owner;
            _scopeId = scopeId;
            _generation = generation;
        }

        public void Dispose()
        {
            BattleEffectExecutionContextService owner = _owner;
            if (owner == null)
                return;
            _owner = null;
            owner.Pop(_scopeId, _generation);
        }
    }
}
```

`CurrentForReporting` 只为保持现有“无显式 scope 的非攻击 report 归为
`PlayerCommand`”语义；`BattleAttackActionCoordinator.BeginLogicalAttack(...)` 必须且只能调用
`RequireCurrentForAttack()`，因此任何正式攻击都不能借该展示回落逃过显式 origin 接线。

- `BattleMetricsReportService` 删除 `_effectOriginStack`、`PushEffectOrigin(...)`、
  `PopEffectOrigin()` 与内部 `EffectOriginScope`；`BuildAutoCastEffectResultReport(...)` 和
  `AttachCurrentEffectOrigin(...)` 改读
  `_runtime.EffectExecutionContext.CurrentForReporting`。
- `BattleRuntimeModule.CurrentEffectOriginForContingency` 精确改为
  `EffectExecutionContext.CurrentForReporting`；它不再使用
  `_metricsReportService.CurrentEffectOrigin ?? PlayerCommand()`。
- `IssueCommandCore(...)` root 显式 push `PlayerCommand()`；AI/madness 仍委托该 command root。
- timeline tick、dead-active cleanup、ready-unit activation 分别显式 push `Timeline("timeline_tick")`、`Timeline("dead_active_cleanup")`、`Timeline("ready_unit_activation")`。
- `BattleContingencyBridgeService.ExecuteAutoCast(request, batch)` 要求非空 batch 和 active reaction boundary，在执行整个 AutoCast effect graph 时 push AutoCast origin；P1A 删除 `batch ?? _new_batch()` fallback。
- `BattleCounterattackSystem` 在执行整个反击 effect graph 时 push Counterattack origin。
- `BattleAttackActionCoordinator.BeginLogicalAttack(...)` 从
  `RequireCurrentForAttack()` 读取并冻结 origin；caller 不自行复制默认回落逻辑。
- metrics/report 读取 action context 中已经冻结的 origin；非攻击 effect report 读取当前 effect scope。
- effect origin scope 必须包住对应 logical attack scope；嵌套 AutoCast 临时覆盖 origin，返回后恢复父 origin。

对应的现有调用点不是保留兼容代理，而是直接改 owner：

```csharp
// BattleRuntimeModule.cs
internal BattleEffectOrigin CurrentEffectOriginForContingency =>
    EffectExecutionContext.CurrentForReporting;

// BattleMetricsReportService.cs
private BattleEffectOrigin CurrentEffectOrigin =>
    _runtime.EffectExecutionContext.CurrentForReporting;

private void AttachCurrentEffectOrigin(
    Dictionary<string, object> reportEntry
)
{
    if (reportEntry == null || reportEntry.Count == 0)
        return;
    reportEntry["effect_origin"] =
        CurrentEffectOrigin.ToPlainDictionary();
}
```

所有当前 `_metricsReportService.PushEffectOrigin(...)` caller 改为
`EffectExecutionContext.Push(...)`；旧 stack 和旧 push method 在同一提交删除，不保留
转发 alias。

`BattleEffectOrigin.ToPlainDictionary()` 的目标字段集合固定为：

```csharp
internal Dictionary<string, object> ToPlainDictionary() =>
    new(StringComparer.Ordinal)
    {
        ["origin_kind"] = OriginKind.ToString(),
        ["can_trigger_contingencies"] = CanTriggerContingencies,
        ["can_trigger_reactions"] = CanTriggerReactions,
        ["owner_member_id"] = OwnerMemberId.ToString(),
        ["setup_id"] = SetupId.ToString(),
        ["instance_id"] = InstanceId.ToString(),
        ["skill_entry_id"] = SkillEntryId.ToString(),
        ["stored_skill_id"] = StoredSkillId.ToString(),
        ["trigger_type"] = TriggerType.ToString(),
        ["triggering_attack_action_id"] =
            TriggeringAttackActionId,
    };
```

旧字段不改名；两个新字段供 report、trace 与回归使用。

### 6.3 delivery kind

武器射程类型已经同时进入 item authoring、敌人模板投影和 battle unit projection，但当前
没有共享的闭集 owner。P1A 先新增
`scripts/systems/battle/core/BattleWeaponRangeType.cs`，由内容校验、投影 producer 与
delivery rules 共同消费：

```csharp
internal enum BattleWeaponRangeTypeKind
{
    Unknown = 0,
    Melee,
    Ranged
}

internal static class BattleWeaponRangeTypeNames
{
    internal static BattleWeaponRangeTypeKind Parse(StringName value)
    {
        if (value == new StringName("melee"))
            return BattleWeaponRangeTypeKind.Melee;
        if (value == new StringName("ranged"))
            return BattleWeaponRangeTypeKind.Ranged;
        return BattleWeaponRangeTypeKind.Unknown;
    }

    internal static StringName ToStringName(
        BattleWeaponRangeTypeKind value
    ) => value switch
    {
        BattleWeaponRangeTypeKind.Melee =>
            new StringName("melee"),
        BattleWeaponRangeTypeKind.Ranged =>
            new StringName("ranged"),
        _ => new StringName(""),
    };
}
```

delivery kind 是另一个闭集：

```csharp
internal enum BattleAttackDeliveryKind
{
    Unknown = 0,
    MeleeWeapon,
    RangedWeapon,
    NonWeapon
}
```

`BattleAttackDeliveryKind` 固定与 `BattleWeaponRangeTypeKind` 一起声明在
`scripts/systems/battle/core/BattleWeaponRangeType.cs`。rules 层的
`BattleAttackDeliveryRules.cs` 只放静态分类规则；core 的 action context、resolution fact 与
preview contract 只能依赖这个 core enum，不能依赖 rules/runtime 私有类型。

`BattleAttackDeliveryRules` 是唯一分类 owner：

- 从本次正式攻击 definition、`IncludesWeaponDamage` 和攻击开始时的 weapon projection 计算。
- `current weapon` 必须在 action 创建时解析并冻结，不能在排空时重新读取后再分类。
- `Unknown` 只允许作为尚未形成 executable plan 的 query 值；production
  `ResolveAttackEffects(...)` 出口遇到它直接抛 action-contract 错误，不发布事实。它不能
  静默降级为“不触发反击”，否则漏接 weapon projection 会被掩盖。
- delivery kind 是本次攻击事实，不从展示 tag、文本描述或具体 skill id 推导。

目标 rule 直接使用现有 `BattleUnitWeaponProjectionReadView`，签名和闭集映射固定为：

```csharp
internal static class BattleAttackDeliveryRules
{
    internal static bool IncludesWeaponDamage(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    ) =>
        BattleUnitSkillDefinitionExecutionRules
            .IncludesWeaponDamage(effectDefinitions);

    internal static BattleAttackDeliveryKind Resolve(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleUnitWeaponProjectionReadView weaponProjection
    )
    {
        if (!IncludesWeaponDamage(effectDefinitions))
            return BattleAttackDeliveryKind.NonWeapon;
        if (
            !weaponProjection.OwnerPresent
            || weaponProjection.Values.AttackRange <= 0
        )
        {
            return BattleAttackDeliveryKind.Unknown;
        }
        return BattleWeaponRangeTypeNames.Parse(
            weaponProjection.Values.RangeType
        ) switch
        {
            BattleWeaponRangeTypeKind.Melee =>
                BattleAttackDeliveryKind.MeleeWeapon,
            BattleWeaponRangeTypeKind.Ranged =>
                BattleAttackDeliveryKind.RangedWeapon,
            _ => BattleAttackDeliveryKind.Unknown,
        };
    }
}
```

`ActiveDice.HasUsableDice` 不是 delivery 分类条件。零骰或 present-empty dice 可能令伤害结算
不产生武器骰，但不会把已经明确为 melee/ranged 的自然武器、空手或装备攻击改成
non-weapon；骰子合法性继续归现有 weapon content/damage owner。

当前敌人 definition → runtime 路径会原样传输
`EnemyTemplateDef.GetWeaponProjectionTyped(...)` 的 `weapon_range_type`，而
`GetNaturalWeaponProjectionTyped()`、`GetUnarmedWeaponProjectionTyped()` 和
`_build_weapon_projection_from_item_definition(...)` 都漏写了该字段。P1A 必须与
delivery fail-fast **在同一切片原子落地**，精确补齐：

```csharp
// EnemyTemplateDef.GetNaturalWeaponProjectionTyped()
weapon_range_type = BattleWeaponRangeTypeNames.ToStringName(
    BattleWeaponRangeTypeKind.Melee
),

// EnemyTemplateDef.GetUnarmedWeaponProjectionTyped()
weapon_range_type = BattleWeaponRangeTypeNames.ToStringName(
    BattleWeaponRangeTypeKind.Melee
),

// EnemyTemplateDef._build_weapon_projection_from_item_definition(...)
weapon_range_type = itemDefinition.GetWeaponRangeType(),
```

加载期校验也必须消费同一个 typed owner。`ItemContentRegistry` 当前只检查
`resolvedProfile.RangeType != ""`，改成：

```csharp
if (
    BattleWeaponRangeTypeNames.Parse(resolvedProfile.RangeType)
    == BattleWeaponRangeTypeKind.Unknown
)
{
    _validationErrors.Add(
        $"Weapon item {(string)itemDef.ItemId} "
        + "weapon_profile.range_type must be melee or ranged."
    );
    return false;
}
```

`EnemyTemplateDef.ValidateSchemaTyped(...)` 在完成 beast/non-beast weapon 校验后，再校验最终投影，
防止 natural/unarmed/equipped 三个 producer 任一路径漏字段：

```csharp
WeaponProjection projectedWeapon =
    GetWeaponProjectionTyped(itemDefinitions);
if (
    projectedWeapon == null
    || projectedWeapon.IsEmpty()
    || BattleWeaponRangeTypeNames.Parse(
        projectedWeapon.weapon_range_type
    ) == BattleWeaponRangeTypeKind.Unknown
)
{
    errors.Add(
        $"Enemy template {template_id} must project "
        + "weapon_range_type melee or ranged."
    );
}
```

这里不采用“未知投影临时降级为 `NonWeapon`”的兼容路径：P1A 不是可在内容校验前单独开启
delivery fail-fast 的 feature flag。合并门要求上述 producer 修复、item/enemy 加载期校验与
全模板回归先通过，再启用 production `Unknown` 硬失败；这样既不让现有敌人普攻崩溃，也
不把未来漏接投影静默伪装成 non-weapon。

共享 predicate 与 `BattleDamageResolver.cs:705-719` 当前
`(AddWeaponDice || RequiresWeapon)` 判定逐句一致；旧 private helper 删除并改调
`BattleAttackDeliveryRules.IncludesWeaponDamage(...)`，后者只转发 lower content-rule
owner。每个 producer 在
`BeginLogicalAttack(deliveryKind)` 前捕获一次
`sourceUnit.GetWeaponProjectionReadViewTyped()` 并计算 delivery；coordinator 把 delivery
冻结进 action context。同一个 logical action 的 repeat/charge stages 复用该 context，不在
每 stage 重新读取换装结果。

### 6.4 action context

新增：

```csharp
internal sealed class BattleAttackActionContext
{
    internal BattleAttackActionContext(
        BattleAttackActionId actionId,
        long rootBoundaryId,
        BattleEffectOrigin origin,
        BattleAttackDeliveryKind deliveryKind
    )
    {
        if (!actionId.IsValid)
            throw new ArgumentException("attack action id is invalid");
        if (rootBoundaryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(rootBoundaryId));
        if (deliveryKind == BattleAttackDeliveryKind.Unknown)
            throw new ArgumentException("attack delivery kind is required");
        ActionId = actionId;
        RootBoundaryId = rootBoundaryId;
        Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        DeliveryKind = deliveryKind;
    }

    internal BattleAttackActionId ActionId { get; }
    internal long RootBoundaryId { get; }
    internal BattleEffectOrigin Origin { get; }
    internal BattleAttackDeliveryKind DeliveryKind { get; }
}
```

扩展现有 `AttackContext`：

```csharp
internal BattleAttackActionContext Action { get; init; }
```

`AttackContext` 当前是 `public sealed class`，而
`BattleAttackActionContext` 保持 `internal`，所以这里必须使用 internal member；写成
public 会直接触发 C# inconsistent accessibility（CS0053），不是可选风格。

`EventBatch` 仍用于 resolver 的同步事件追加，但 batch ownership 由当前 reaction boundary 校验：

- root boundary 固定一个 `BattleEventBatch`。
- 所有嵌套 logical action 必须使用同一 batch。
- 发现不同 batch 时 fail fast；不能把一个边界中的反击写入另一个 batch。
- `BeginLogicalAttack(...)` 要求已有 active reaction boundary；不允许隐式创建 boundary，因为那会让 producer 忘记覆盖自身后处理范围。
- descendant producer 只能借用其 caller 传入的 `BattleAttackActionContext`，不能调用
  coordinator “按旧 ID 重开” scope。sink 再用 `RootBoundaryId`、action id、origin
  reference 与 delivery 对照当前 stack top；已经完成/释放的 context 即使仍被某对象引用，
  再次发布 fact 也会 fail root。
- 不支持“合法 child batch”“batch 谱系”或 merge-before-drain 例外；P1A 只有同一实例这一条规则。
- `BattleEventBatch` 不是 gameplay transaction 或回滚快照。producer 若只需要暂存局部判断，使用 typed result/counter，不创建第二个 batch。

`BattleChargeResolver` 是现状中必须显式迁移的冲突点：

1. 删除局部 `new BattleEventBatch()` 与结尾 `MergeBatch(...)`。
2. 移动、屏障、路径步攻击、日志、changed facts 与伤害 resolver 全部直接接收 caller 的 root batch。
3. “是否移动过”“是否在起步阶段被阻断”“是否产生过日志”等分支改由局部 typed 计数/结果表达，不再读取局部 batch 作为控制流。
4. 该迁移不改变 gameplay 原子性：当前局部 batch 也没有回滚已经发生的移动、伤害或状态，只是延迟合并展示 facts。

### 6.5 resolver 发布的事实

把旧名称“attack fully resolved context”改为准确的 `BattleAttackResolutionFact`：

```csharp
internal readonly record struct BattleAttackResolutionFact(
    BattleAttackActionId ActionId,
    long RootBoundaryId,
    StringName AttackerUnitId,
    StringName DefenderUnitId,
    Vector2I AttackerCellAtResolution,
    Vector2I DefenderCellAtResolution,
    BattleAttackDeliveryKind DeliveryKind,
    bool AttackSucceeded,
    bool CriticalHit,
    bool IncludesWeaponDamage,
    BattleEffectOrigin Origin
);

internal interface IBattleAttackResolutionSink
{
    void OnAttackResolved(
        in BattleAttackResolutionFact fact,
        BattleEventBatch batch
    );
}
```

约束：

- fact 不持有 `BattleUnitState`、`BattleState`、`SkillDefinition` 或 mutable collection。
- fact 不持有 batch；sink 只在同步 callback 参数中接收 batch，调用 coordinator 校验它与 active root 是同一实例后立即丢弃，queue entry 不保存 batch。
- `RootBoundaryId` 只用于同步 active-action contract 校验，不进入存档、report 或 dedupe key。
- 两个坐标只用于日志和诊断；执行资格在排空时用当前坐标重新查询。
- sink 不执行攻击、不扣资源、不掷骰。
- production 中 sink 已绑定但 action id/origin/delivery 缺失时 fail fast；isolated rules test 可选择不绑定 sink。

### 6.6 `BattleDamageResolver` 的调用点

当前存在两个 overload。`BattleDamageResolver.cs:481-495` 的四参 convenience overload 会
隐式构造 `new AttackContext()`；P1A 删除它。`:497-625` 是唯一保留的正式 overload，并把
最后一个参数从 `AttackContext attack_context = null` 改为 required
`AttackContext attack_context`。所有 override 同步删除 optional default，所有 caller 必须
显式传 context；这样漏传 context 才是真正的编译期错误，而不是 sink 绑定后才运行期失败。
唯一保留的 overload 有三个 return 类别，P1A 必须逐个处理，不能只按“miss/hit 两出口”
模糊修改：

| 当前代码锚点 | 当前行为 | P1A 精确动作 |
|---|---|---|
| `BattleDamageResolver.cs:505-512` | source/target 为 null 时构造 empty result 并返回 | 保留为 invalid-input guard，不发布 fact；production logical attack 在到达 resolver 前已禁止 null unit |
| `BattleDamageResolver.cs:515-536` | 规范化 effect/context、构建 metadata、计算 `attackIncludesWeaponDamage`、同步提交 equipment attack-check reaction | 原序保留；把 `EffectDefinitionsIncludeWeaponDamage(...)`（当前 `:705-719`）迁到唯一 owner `BattleAttackDeliveryRules.IncludesWeaponDamage(...)`，resolver 与 producer 共同调用 |
| `BattleDamageResolver.cs:539-555` | miss result → `AttachAttackReportEntry` → `DispatchAttackResolutionEvents` | 原序保留 |
| `BattleDamageResolver.cs:556-558` | clear combo → consume one-shot → finalize/return | 只替换 `:558`：在 `:557` 后调用 `FinalizeAndPublishAttackResolution(...)`，该 helper finalize、复制 immutable fact、同步入队并返回同一个 finalized result |
| `BattleDamageResolver.cs:561-606` | secondary hit → `ResolveEffectsDefinitionCore` → weapon combo → equipment after-hit | 原序保留 |
| `BattleDamageResolver.cs:607-623` | durability projection reconcile → attach report → dispatch | 原序保留 |
| `BattleDamageResolver.cs:624-625` | consume one-shot → finalize/return | 只替换 `:625`：在 `:624` 后调用同一个 `FinalizeAndPublishAttackResolution(...)` |

删除四参 overload 后，现有无 context 调用并不是 production producer，而是三个 isolated
rules/skill runner：

- `tests/battle_runtime/rules/run_weapon_hit_combo_stack_regression.cs`
- `tests/battle_runtime/skills/run_hunter_mark_skill_regression.cs`
- `tests/battle_runtime/skills/run_warrior_perfect_rhythm_regression.cs`

这些 call site 统一在第五参显式传 `new AttackContext()`，继续保持“不绑定 sink 的纯 resolver
fixture”语义。`run_trait_trigger_regression.cs:47-53` 当前已经传入显式
`attackContext`，不属于待修 caller。

下面六个 override 也必须在同一次提交删除 `AttackContext ... = null` 的 default；即使方法体
不读取 context，也不能让 subclass 静态类型继续提供隐式四参入口：

- `tests/shared/FixedHitOneDamageResolver.cs`
- `tests/shared/FixedSuccessOneDamageResolver.cs`
- `tests/shared/StageOutcomeDamageResolver.cs`
- `tests/battle_runtime/skills/run_warrior_nine_echo_final_hammer_regression.cs`
- `tests/battle_runtime/skills/run_warrior_overhead_chop_regression.cs`
- `tests/battle_runtime/runtime/run_plague_tongue_weapon_ability_regression.cs`

完成后以 Roslyn/编译检查所有 `ResolveAttackEffects(...)` invocation 都有第五个
`AttackContext` 实参，并检查所有 override 的第五参都没有 default；不能只用文本统计括号或
逗号数。

`BattleDamageResolver` 字段区新增且只新增 sink reference：

```csharp
private IBattleAttackResolutionSink _attack_resolution_sink;
```

setter 的单 owner 实现在 §13.3；目标 helper 固定为 rules 内私有同步方法，不向 runtime
反向查询：

```csharp
private AttackEffectResolutionResult FinalizeAndPublishAttackResolution(
    BattleUnitState sourceUnit,
    BattleUnitState targetUnit,
    AttackContext attackContext,
    AttackEffectResolutionResult result,
    bool includesWeaponDamage
)
{
    AttackEffectResolutionResult finalized =
        AttackEffectResolutionResultReader.FinalizeTypedResult(result);
    if (_attack_resolution_sink == null)
        return finalized;

    BattleAttackActionContext action = attackContext?.Action
        ?? throw new InvalidOperationException("attack action context is required");
    if (!action.ActionId.IsValid || action.Origin == null)
        throw new InvalidOperationException("attack action id/origin is invalid");
    if (attackContext.EventBatch == null)
        throw new InvalidOperationException("attack event batch is required");
    if (action.DeliveryKind == BattleAttackDeliveryKind.Unknown)
        throw new InvalidOperationException("attack delivery kind is required");

    var fact = new BattleAttackResolutionFact(
        action.ActionId,
        action.RootBoundaryId,
        sourceUnit.unit_id,
        targetUnit.unit_id,
        sourceUnit.GetAnchorCoord(),
        targetUnit.GetAnchorCoord(),
        action.DeliveryKind,
        finalized.AttackSuccess,
        finalized.CriticalHit,
        includesWeaponDamage,
        action.Origin
    );
    _attack_resolution_sink.OnAttackResolved(
        in fact,
        attackContext.EventBatch
    );
    return finalized;
}
```

上面是目标代码形状，不允许 helper 调用 `BattleRuntimeModule`、查询当前 ambient origin、创建 batch、打开 boundary、执行反击或修改 fact 中的单位。`_attack_resolution_sink == null` 只服务 isolated rules tests；production setup 绑定 sink 后，缺 `Action`、batch 或 delivery 一律抛错。

miss 尾部目标替换是：

```csharp
ClearComboStackOnMiss(source_unit);
ConsumeOneShotAttackCheckStatuses(source_unit);
return FinalizeAndPublishAttackResolution(
    source_unit,
    target_unit,
    normalizedAttackContext,
    failedResult,
    attackIncludesWeaponDamage
);
```

hit 尾部目标替换是：

```csharp
DispatchAttackResolutionEvents(
    source_unit,
    target_unit,
    attackMetadata,
    normalizedAttackContext
);
ConsumeOneShotAttackCheckStatuses(source_unit);
return FinalizeAndPublishAttackResolution(
    source_unit,
    target_unit,
    normalizedAttackContext,
    resolvedResult,
    attackIncludesWeaponDamage
);
```

这里的“resolved”只表示 `BattleDamageResolver` 自己的攻击结算结束；producer 的特殊效果、位移、死亡提交、贡献与 outcome 后处理可能仍未结束，因此 sink 只能入队。

---

## 七、反击能力与反应预算

### 7.1 battle-local capability owner

新增窄 typed component：

```csharp
internal readonly record struct BattleCounterattackCapability(
    StringName InstanceId,
    BattleCounterattackTriggerKind TriggerKind,
    int SelectionPriority,
    int ChancePercent,
    int AttackRollBonus,
    StringName WeaponActionDefinitionId
);

internal readonly record struct BattleUnitCounterattackCapabilitySnapshot(
    bool OwnerPresent,
    IReadOnlyList<BattleCounterattackCapability> Values
)
{
    internal static BattleUnitCounterattackCapabilitySnapshot MissingOwner =>
        new(false, Array.Empty<BattleCounterattackCapability>());
}

internal sealed class BattleUnitCounterattackCapabilityState
{
    private BattleCounterattackCapability[] _ordered =
        Array.Empty<BattleCounterattackCapability>();
    private Dictionary<StringName, BattleCounterattackCapability> _byId =
        new();
    private Dictionary<
        BattleCounterattackTriggerKind,
        IReadOnlyList<BattleCounterattackCapability>
    > _byTrigger = new();

    internal void ReplaceAll(
        IReadOnlyList<BattleCounterattackCapability> values
    )
    {
        var byId =
            new Dictionary<StringName, BattleCounterattackCapability>();
        foreach (
            BattleCounterattackCapability value
                in values ?? Array.Empty<BattleCounterattackCapability>()
        )
        {
            if (
                value.InstanceId == new StringName("")
                || value.WeaponActionDefinitionId == new StringName("")
                || !Enum.IsDefined(value.TriggerKind)
                || value.ChancePercent < 0
                || value.ChancePercent > 100
            )
            {
                throw new ArgumentException(
                    "counterattack capability is invalid",
                    nameof(values)
                );
            }
            if (!byId.TryAdd(value.InstanceId, value))
            {
                throw new ArgumentException(
                    $"duplicate counterattack capability {value.InstanceId}",
                    nameof(values)
                );
            }
        }

        BattleCounterattackCapability[] ordered =
            byId.Values.ToArray();
        Array.Sort(
            ordered,
            static (left, right) =>
            {
                int priorityOrder =
                    right.SelectionPriority.CompareTo(
                        left.SelectionPriority
                    );
                return priorityOrder != 0
                    ? priorityOrder
                    : string.Compare(
                        left.InstanceId.ToString(),
                        right.InstanceId.ToString(),
                        StringComparison.Ordinal
                    );
            }
        );
        var byTrigger = new Dictionary<
            BattleCounterattackTriggerKind,
            IReadOnlyList<BattleCounterattackCapability>
        >();
        foreach (
            BattleCounterattackTriggerKind triggerKind
                in Enum.GetValues<BattleCounterattackTriggerKind>()
        )
        {
            byTrigger[triggerKind] = Array.AsReadOnly(
                ordered
                    .Where(value => value.TriggerKind == triggerKind)
                    .ToArray()
            );
        }
        _byId = byId;
        _ordered = ordered;
        _byTrigger = byTrigger;
    }

    internal bool TryGetCapability(
        StringName instanceId,
        out BattleCounterattackCapability capability
    )
    {
        if (instanceId == new StringName(""))
        {
            capability = default;
            return false;
        }
        return _byId.TryGetValue(instanceId, out capability);
    }

    internal IReadOnlyList<BattleCounterattackCapability> GetCandidates(
        BattleCounterattackTriggerKind triggerKind
    )
    {
        if (!Enum.IsDefined(triggerKind))
            return Array.Empty<BattleCounterattackCapability>();
        return _byTrigger.TryGetValue(
            triggerKind,
            out IReadOnlyList<BattleCounterattackCapability> candidates
        )
            ? candidates
            : Array.Empty<BattleCounterattackCapability>();
    }

    internal BattleUnitCounterattackCapabilitySnapshot CaptureRaw() =>
        new(true, Array.AsReadOnly(_ordered.ToArray()));

    internal void RestoreRaw(
        BattleUnitCounterattackCapabilitySnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            throw new ArgumentException(
                "missing-owner snapshot must be restored by BattleUnitState"
            );
        }
        ReplaceAll(snapshot.Values);
    }

    internal BattleUnitCounterattackCapabilityState DuplicateState()
    {
        var duplicate = new BattleUnitCounterattackCapabilityState();
        duplicate.ReplaceAll(_ordered);
        return duplicate;
    }
}
```

`BattleUnitState` 只暴露下面两个 capability gateway；runtime 不直接拿 `_counterattackCapabilityState`：

```csharp
private BattleUnitCounterattackCapabilityState
    _counterattackCapabilityState;

internal IReadOnlyList<BattleCounterattackCapability>
    GetCounterattackCandidatesTyped(
        BattleCounterattackTriggerKind triggerKind
    ) =>
        _counterattackCapabilityState?.GetCandidates(triggerKind)
        ?? Array.Empty<BattleCounterattackCapability>();

internal bool TryGetCounterattackCapabilityTyped(
    StringName instanceId,
    out BattleCounterattackCapability capability
)
{
    if (_counterattackCapabilityState == null)
    {
        capability = default;
        return false;
    }
    return _counterattackCapabilityState.TryGetCapability(
        instanceId,
        out capability
    );
}

internal void ReplaceCounterattackCapabilitiesTyped(
    IReadOnlyList<BattleCounterattackCapability> values
)
{
    _counterattackCapabilityState ??=
        new BattleUnitCounterattackCapabilityState();
    _counterattackCapabilityState.ReplaceAll(values);
}

internal BattleUnitCounterattackCapabilitySnapshot
    CaptureCounterattackCapabilitiesRawTyped() =>
        _counterattackCapabilityState?.CaptureRaw()
        ?? BattleUnitCounterattackCapabilitySnapshot.MissingOwner;

internal void RestoreCounterattackCapabilitiesRawTyped(
    BattleUnitCounterattackCapabilitySnapshot snapshot
)
{
    if (!snapshot.OwnerPresent)
    {
        _counterattackCapabilityState = null;
        return;
    }
    _counterattackCapabilityState ??=
        new BattleUnitCounterattackCapabilityState();
    _counterattackCapabilityState.RestoreRaw(snapshot);
}
```

P1A 只实现 runtime owner 与测试注入，不定义生产内容如何安装这些实例。

`BattleUnitReactionState` 与 capability state 统一采用显式 `OwnerPresent`，不使用 null collection 或隐式 sentinel 表示缺失 owner。这样 present-empty capability list、present-zero reaction budget 与 missing owner 在 strict codec / mutation exact 中可区分。

不变量：

- `InstanceId` 非空且在单位内唯一。
- `WeaponActionDefinitionId` 非空；它是 generic typed reference，不允许在
  counterattack system/query/rules 中与 `"basic_attack"` 或任何其他具体值比较。
- `ChancePercent` 必须在 `[0, 100]`。
- 候选顺序固定为 `SelectionPriority` 降序，再按 `InstanceId` ordinal 升序。
- 同一事实只选择第一个匹配实例；不跨实例叠加 attack bonus。
- 概率失败不回退到第二个实例。
- capability 被移除，或相同 instance id 的完整 immutable 值发生变化后，已经排队但尚未执行的 entry 在排空复核时失效。

这避免把计数和生命周期塞进 `BattleStatusEffectState`，也避免反击 runtime 直接依赖任何具体内容来源。

### 7.2 trigger 闭集

P1A 只实现：

```csharp
internal enum BattleCounterattackTriggerKind
{
    MeleeHitReceived = 0,
    MeleeAttackEvaded
}

internal static class BattleCounterattackTriggerNames
{
    internal static StringName ToStringName(
        BattleCounterattackTriggerKind value
    ) => value switch
    {
        BattleCounterattackTriggerKind.MeleeHitReceived =>
            new StringName("melee_hit_received"),
        BattleCounterattackTriggerKind.MeleeAttackEvaded =>
            new StringName("melee_attack_evaded"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            null
        ),
    };

    internal static bool TryParse(
        StringName value,
        out BattleCounterattackTriggerKind result
    )
    {
        if (value == new StringName("melee_hit_received"))
        {
            result = BattleCounterattackTriggerKind.MeleeHitReceived;
            return true;
        }
        if (value == new StringName("melee_attack_evaded"))
        {
            result = BattleCounterattackTriggerKind.MeleeAttackEvaded;
            return true;
        }
        result = default;
        return false;
    }
}
```

`BattleCounterattackTriggerKind` 与 `BattleCounterattackTriggerNames` 落在
`scripts/systems/battle/core/BattleCounterattackContracts.cs`，不能声明在
`BattleCounterattackSystem.cs`。它们同时被 capability state、rules、runtime 与 report
读取，是 domain contract，不是 runtime service 的私有实现。

映射规则：

| delivery | result | trigger |
|---|---|---|
| `MeleeWeapon` | hit | `MeleeHitReceived` |
| `MeleeWeapon` | miss | `MeleeAttackEvaded` |
| 其他 | 任意 | 无 |

扩展 ranged/any trigger 属于未来功能，新增 enum value 时必须同步 query、preview、AI seam 与回归。

### 7.3 reaction budget owner

```csharp
internal readonly record struct BattleReactionBudgetConfig(
    int ChargeCapacity,
    int RechargeIntervalTu
);

internal readonly record struct BattleUnitReactionSnapshot(
    bool OwnerPresent,
    int ChargesRemaining,
    int ChargeCapacity,
    int RechargeIntervalTu,
    int NextRechargeAtTu
)
{
    internal static BattleUnitReactionSnapshot MissingOwner =>
        new(false, 0, 0, 0, 0);
}

internal sealed class BattleUnitReactionState
{
    private int _chargesRemaining;
    private int _chargeCapacity;
    private int _rechargeIntervalTu;
    private int _nextRechargeAtTu;

    internal void Initialize(
        int currentTu,
        BattleReactionBudgetConfig config,
        bool startFull
    )
    {
        if (currentTu < 0)
            throw new ArgumentOutOfRangeException(nameof(currentTu));
        if (config.ChargeCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(config));
        if (config.RechargeIntervalTu <= 0)
            throw new ArgumentOutOfRangeException(nameof(config));
        _chargeCapacity = config.ChargeCapacity;
        _rechargeIntervalTu = config.RechargeIntervalTu;
        _chargesRemaining = startFull ? _chargeCapacity : 0;
        _nextRechargeAtTu = checked(
            currentTu + _rechargeIntervalTu
        );
    }

    internal bool HasCharge() => _chargesRemaining > 0;

    internal void ValidateConsumeChargeKnownAvailable()
    {
        if (_chargesRemaining <= 0)
        {
            throw new InvalidOperationException(
                "reaction charge is not available"
            );
        }
    }

    internal void CommitConsumeChargeKnownAvailable()
    {
        // aggregate gateway 已经调用 Validate；这里故意只有不会失败的 assignment。
        _chargesRemaining -= 1;
    }

    internal bool AdvanceAndRefill(int currentTu)
    {
        if (currentTu < 0)
            throw new ArgumentOutOfRangeException(nameof(currentTu));
        if (_rechargeIntervalTu <= 0)
            throw new InvalidOperationException("reaction state is not initialized");
        if (currentTu < _nextRechargeAtTu)
            return false;

        long crossed =
            ((long)currentTu - _nextRechargeAtTu)
            / _rechargeIntervalTu
            + 1L;
        int nextRechargeAtTu = checked(
            (int)(
                (long)_nextRechargeAtTu
                + crossed * _rechargeIntervalTu
            )
        );
        bool changed =
            nextRechargeAtTu != _nextRechargeAtTu
            || _chargesRemaining != _chargeCapacity;
        _nextRechargeAtTu = nextRechargeAtTu;
        _chargesRemaining = _chargeCapacity;
        return changed;
    }

    internal bool AdvanceFrozenAnchor(int elapsedTu)
    {
        if (elapsedTu < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedTu));
        if (elapsedTu == 0)
            return false;
        if (_rechargeIntervalTu <= 0)
            throw new InvalidOperationException("reaction state is not initialized");
        _nextRechargeAtTu = checked(
            (int)((long)_nextRechargeAtTu + elapsedTu)
        );
        return true;
    }

    internal BattleUnitReactionSnapshot CaptureRaw() =>
        new(
            true,
            _chargesRemaining,
            _chargeCapacity,
            _rechargeIntervalTu,
            _nextRechargeAtTu
        );

    internal void RestoreRaw(BattleUnitReactionSnapshot snapshot)
    {
        if (
            !snapshot.OwnerPresent
            || snapshot.ChargeCapacity < 0
            || snapshot.ChargesRemaining < 0
            || snapshot.ChargesRemaining > snapshot.ChargeCapacity
            || snapshot.RechargeIntervalTu <= 0
            || snapshot.NextRechargeAtTu < 0
        )
        {
            throw new ArgumentException(
                "reaction snapshot is invalid",
                nameof(snapshot)
            );
        }
        _chargesRemaining = snapshot.ChargesRemaining;
        _chargeCapacity = snapshot.ChargeCapacity;
        _rechargeIntervalTu = snapshot.RechargeIntervalTu;
        _nextRechargeAtTu = snapshot.NextRechargeAtTu;
    }

    internal BattleUnitReactionState DuplicateState()
    {
        var duplicate = new BattleUnitReactionState();
        duplicate.RestoreRaw(CaptureRaw());
        return duplicate;
    }
}
```

`BattleUnitCombatResourceState` 同步新增 `ValidateSpendStaminaKnownAvailable(int)` 与 `CommitSpendStaminaKnownAvailable(int)`；前者只验证 next stamina，后者只做已经验证后的 assignment。

这两个方法直接加在现有 `_values` owner 内，不能经
`SetCurrentStamina(...)` 再做 clamp：

```csharp
internal void ValidateSpendStaminaKnownAvailable(int staminaCost)
{
    if (staminaCost < 0)
        throw new ArgumentOutOfRangeException(nameof(staminaCost));
    if (_values.Stamina < staminaCost)
        throw new InvalidOperationException("stamina is not available");
}

internal void CommitSpendStaminaKnownAvailable(int staminaCost)
{
    // aggregate gateway 已验证 0 <= cost <= current；这里不会 clamp 或二次失败。
    _values = _values with
    {
        Stamina = _values.Stamina - staminaCost,
    };
}
```

`BattleUnitState` 的只读 budget gateway 固定为：

```csharp
private BattleUnitReactionState _reactionState;

internal bool HasReactionChargeTyped() =>
    _reactionState?.HasCharge() == true;

internal void InitializeReactionBudgetTyped(
    int currentTu,
    BattleReactionBudgetConfig config,
    bool startFull
)
{
    _reactionState ??= new BattleUnitReactionState();
    _reactionState.Initialize(currentTu, config, startFull);
}

internal BattleUnitReactionSnapshot CaptureReactionRawTyped() =>
    _reactionState?.CaptureRaw()
    ?? BattleUnitReactionSnapshot.MissingOwner;

internal void RestoreReactionRawTyped(
    BattleUnitReactionSnapshot snapshot
)
{
    if (!snapshot.OwnerPresent)
    {
        _reactionState = null;
        return;
    }
    _reactionState ??= new BattleUnitReactionState();
    _reactionState.RestoreRaw(snapshot);
}

internal bool AdvanceReactionBudgetTyped(int currentTu) =>
    _reactionState?.AdvanceAndRefill(currentTu) == true;

internal bool AdvanceFrozenReactionAnchorTyped(int elapsedTu) =>
    _reactionState?.AdvanceFrozenAnchor(elapsedTu) == true;
```

P1A 的 config 在 battle start 后不可动态修改。属性、装备或状态导致的动态重配置不在本文范围，避免未定义的容量补偿和 anchor 重基准。

config 约束：

- capacity `>= 0`。
- interval `> 0` 且是 `BattleTimelineState.TuGranularity` 的整数倍。
- `BattleReactionBudgetRules` 提供 P1A 的 provisional engine default：capacity `1`、interval `60 TU`。它只保证架构与测试有合法启动值，不表达属性派生或最终平衡。
- runtime setup 为每个单位复制一份已经验证的 config；未来 typed provider 可以在 battle start 前替换该值。
- 显式配置在 battle assembly 边界验证；非法值阻止 runtime start，不在 owner 内静默 clamp。
- 不设“容量最多 4”之类的架构硬上限；平衡层以后可以在自己的 typed provider 中约束。

仓库已有 core owner `BattleTimelineState.cs:9` 的
`internal const int TuGranularity = 5`。P1A 删除
`BattleTimelineDriver.cs:9` 的重复 private 常量，并把该文件当前
`:180/:183/:344/:400/:403/:458/:460/:461/:464/:468` 的
`TuGranularity` 全部改为 `BattleTimelineState.TuGranularity`。同时删除
`BattleRuntimeSkillTurnResolver.cs:45` 的 `TU_GRANULARITY`，把当前
`:1627/:1635/:1945` 三处引用也改为 `BattleTimelineState.TuGranularity`。

`scripts/systems/battle` 范围内还要覆盖另外三个现有重复 owner，不能只迁移上面两个
runtime 文件：

- 删除 `BattleTerrainEffectSystem.cs:11` 的 `TuGranularity`，把当前
  `:902/:908/:911` 改为 `BattleTimelineState.TuGranularity`。
- 删除 `BattleAiWaitActionEvaluator.cs:7` 的 `TuGranularity`，把当前
  `:263` 改为 `BattleTimelineState.TuGranularity`。
- 删除 `BattleStatusSemanticTable.cs:39` 多声明符语句中的
  `TU_GRANULARITY = 5`，保留同一句的
  `DEFAULT_BLIND_ATTACK_ROLL_PENALTY = 4`；把当前 `:806/:808/:810` 三处引用改为
  `BattleTimelineState.TuGranularity`。

完成后
`rg -n "const int\s+(TuGranularity|TU_GRANULARITY)\s*=" scripts/systems/battle`
必须只剩 `scripts/systems/battle/core/BattleTimelineState.cs:9` 这一条 core owner；不能把
可见性限定为 `private`，否则会漏掉 `BattleStatusSemanticTable` 当前的 `internal` 多声明符。
新增反应规则也只引用这个 core 常量。默认配置与统一验证 owner 为：

`scripts/player/progression/SkillContentRegistry.cs:9` 与
`scripts/player/progression/equipment_abilities/EquipmentAbilityPayloadValidators.cs:8` 也有
同名 `TuGranularity = 5`，但它们属于 progression/content authoring 的独立验证 owner，不在
本提案的 `scripts/systems/battle` 去重范围；验收不得无范围扩成仓库根并把这两处误报为
反击系统残留。

```csharp
internal static class BattleReactionBudgetRules
{
    internal static BattleReactionBudgetConfig EngineDefault =>
        new(ChargeCapacity: 1, RechargeIntervalTu: 60);

    internal static void Validate(
        BattleReactionBudgetConfig config
    )
    {
        if (config.ChargeCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(config));
        if (
            config.RechargeIntervalTu <= 0
            || config.RechargeIntervalTu
                % BattleTimelineState.TuGranularity
                != 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                "reaction interval must use timeline granularity"
            );
        }
    }
}
```

`EnsureCounterattackUnitOwnersInitializedForAdmission(...)` 在任何 owner 写入前调用
`BattleReactionBudgetRules.Validate(config)`；全量遍历与动态召唤走同一条验证路径。
`BattleUnitReactionState.Initialize(...)` 仍保留自身的非负/正数 invariant guard，但不反向
依赖 runtime 的 timeline class。

### 7.4 补充算法

`NextRechargeAtTu` 始终表示严格晚于最后一次已处理边界的下一个补充点。

```text
if currentTu < next:
    no change
else:
    crossed = floor((currentTu - next) / interval) + 1
    next += crossed * interval
    charges = capacity
```

要求：

- 用 `long` 中间值计算并做 checked/fail-fast，避免长战斗整数溢出。
- anchor 按完整间隔前进，不重置成 `currentTu`。
- 跨多个间隔仍只回满，不累计超过 capacity。
- capacity 为 0 时 charges 始终为 0，但 anchor 仍保持合法。

### 7.5 `time_stasis`

仅跳过 `AdvanceAndRefill` 不足以冻结个人时间。当前 TU 在静滞期间继续前进，解除后会追赶全部错过的补充点。

正式语义：

- `BattleTimelineDriver.ResolveTimelineStatusPhase(...)` 保持当前逐单位现场调用
  `BattleTemporalStatusService.HasTimeStasis(unitState)` 的判定；step 开始时捕获的
  stasis unit set 只继续服务 pending cast 与 ready-unit 收集，不作为 status phase 的
  判定输入。
- `BattleRuntimeSkillTurnResolver.AdvanceTimeStasisFrozenTimers(...)` 在平移 cooldown anchor 的同一位置调用 `BattleUnitReactionState.AdvanceFrozenAnchor(elapsedTu)`。
- 即使静滞在当前 step 末自然到期，本 step 仍完整冻结。
- `AdvanceFrozenAnchor` 只平移 `NextRechargeAtTu`，不改变 charge。
- 非静滞单位在 status phase 结束后执行 `AdvanceAndRefill(currentTu)`。

这里刻意不复用 `BattleUnitCooldownState.AdvanceFrozenAnchor(elapsedTu, currentTu)` 的双参数签名。cooldown 的 `_lastTurnTu` 是“已经消费到的过去 anchor”，所以必须 clamp 到 `currentTu`；reaction 的 `NextRechargeAtTu` 是未来阈值，合法值本来就可以大于 `currentTu`，若 clamp 会把冻结期错误地吃掉。reaction 方法只用 checked `long` 计算 `NextRechargeAtTu + elapsedTu`，溢出时 fail fast，不按 `currentTu` 截断。

这样静滞结束后不会立刻补回冻结期间的反应机会。

`BattleTimelineDriver.ResolveTimelineStatusPhase(...)` 的非静滞分支在现有
`_AdvanceUnitStatusDurations(...)` 后新增，不放进 `CollectTimelineReadyUnits(...)`（后者会
跳过 casting unit，反应补充不应因此暂停）：

```csharp
if (_AdvanceUnitStatusDurations(unitState, tuDelta, batch))
    _AppendChangedUnitId(batch, unitState.unit_id);
if (
    unitState.IsAlive()
    && unitState.AdvanceReactionBudgetTyped(
        state.timeline.current_tu
    )
)
{
    _AppendChangedUnitId(batch, unitState.unit_id);
}
```

`BattleRuntimeSkillTurnResolver.AdvanceTimeStasisFrozenTimers(...)` 的起始与三个
return 改为合并 `reactionAnchorChanged`，完整控制形状为：

```csharp
if (unit_state == null || elapsed_tu <= 0)
    return false;

int currentTu = _runtime?._state?.timeline?.current_tu ?? 0;
unit_state.AdvanceCooldownAnchorForStasisTyped(
    elapsed_tu,
    currentTu
);
bool reactionAnchorChanged =
    unit_state.AdvanceFrozenReactionAnchorTyped(elapsed_tu);
BattleStatusEffectState stasisEntry = GetStatusEffect(
    unit_state,
    BattleStatusSemanticTable.STATUS_TIME_STASIS
);
if (stasisEntry == null)
    return reactionAnchorChanged;

BattleStatusDurationAdvanceResult durationResult =
    BattleStatusSemanticTable.AdvanceTimelineDurationResult(
        stasisEntry,
        elapsed_tu
    );
if (durationResult.Expired)
{
    EraseStatusEffect(
        unit_state,
        BattleStatusSemanticTable.STATUS_TIME_STASIS
    );
    BattleTemporalStatusService.HandleTemporalStatusRemoved(
        unit_state,
        BattleStatusSemanticTable.STATUS_TIME_STASIS,
        TemporalStatusReleaseKind.NaturalExpire
    );
    AppendLog(
        batch,
        $"{DisplayName(unit_state)} 的时间静滞消散，残留时间余波。"
    );
    return true;
}
if (durationResult.Changed)
{
    unit_state.SetStatusEffect(stasisEntry);
    return true;
}
return reactionAnchorChanged;
```

开头的现有 guard 必须原样保留：null unit、零或负 elapsed 都继续安静返回 false，不进入
cooldown/reaction anchor API。stasis caller 已在 `true` 时追加 changed unit；anchor 变化即使
stasis duration 本 step 未变化也必须返回 true，HUD 才能观察新的
`next_recharge_at_tu`。

---

## 八、动作边界、入队、去重与排空

### 8.1 reaction boundary 与 logical action 是两层概念

`BattleAttackActionCoordinator` 提供两个 scope：

```csharp
internal BattleReactionBoundaryScope BeginReactionBoundary(BattleEventBatch batch);

internal BattleLogicalAttackScope BeginLogicalAttack(
    BattleAttackDeliveryKind deliveryKind
);

internal void RequireActiveRootBatch(BattleEventBatch batch);
internal void RequireActiveBoundary();
internal void RequireActiveLogicalAttack(
    BattleAttackActionId actionId,
    long rootBoundaryId,
    BattleEffectOrigin origin,
    BattleAttackDeliveryKind deliveryKind
);
internal void ConsumeWorkItem(BattleReactionWorkItemKind kind);

internal sealed class BattleReactionBoundaryScope : IDisposable
{
    internal void Complete();
    public void Dispose();
}

internal sealed class BattleLogicalAttackScope : IDisposable
{
    internal BattleAttackActionContext Context { get; }
    internal void Complete();
    public void Dispose();
}
```

上述不是只给实现者看的接口草图。`BattleAttackActionCoordinator.cs` 的状态机和 scope
实现固定如下；实施时允许机械拆分 private helper，但不得改变 complete/drain/dispose
顺序或增加隐式 root：

```csharp
internal sealed class BattleAttackActionCoordinator : IDisposable
{
    private readonly BattleEffectExecutionContextService _effectContext;
    private readonly BattleReactionBoundaryLimits _limits;
    private readonly List<BoundaryFrame> _boundaries = new();
    private readonly List<LogicalFrame> _logicalAttacks = new();

    private IBattleReactionDrainOwner _drainOwner;
    private BattleEventBatch _rootBatch;
    private long _activeRootBoundaryId;
    private long _nextRootBoundaryId = 1;
    private long _nextActionId = 1;
    private long _nextScopeId = 1;
    private long _generation = 1;
    private int _workItemCount;
    private bool _accepting = true;
    private bool _rootFailed;

    internal BattleAttackActionCoordinator(
        BattleEffectExecutionContextService effectContext,
        BattleReactionBoundaryLimits limits
    )
    {
        _effectContext = effectContext
            ?? throw new ArgumentNullException(nameof(effectContext));
        if (limits.MaxNestedBoundaryDepth <= 0 || limits.MaxWorkItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(limits));
        _limits = limits;
    }

    internal bool HasActiveBoundary => _boundaries.Count > 0;

    internal void BindDrainOwner(IBattleReactionDrainOwner drainOwner)
    {
        ArgumentNullException.ThrowIfNull(drainOwner);
        if (ReferenceEquals(_drainOwner, drainOwner))
            return;
        if (HasActiveBoundary)
            throw new InvalidOperationException("cannot bind drain owner in a boundary");
        if (_drainOwner != null)
            throw new InvalidOperationException("reaction drain owner is already bound");
        _drainOwner = drainOwner;
    }

    internal BattleReactionBoundaryScope BeginReactionBoundary(
        BattleEventBatch batch
    )
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!_accepting)
            throw new ObjectDisposedException(nameof(BattleAttackActionCoordinator));
        if (_rootFailed)
            throw new InvalidOperationException("active reaction root has failed");

        int nextDepth = checked(_boundaries.Count + 1);
        if (nextDepth > _limits.MaxNestedBoundaryDepth)
        {
            FailRoot();
            throw new InvalidOperationException(
                $"reaction boundary depth {nextDepth} exceeds "
                + $"{_limits.MaxNestedBoundaryDepth}"
            );
        }

        if (_boundaries.Count == 0)
        {
            _rootBatch = batch;
            _activeRootBoundaryId = checked(_nextRootBoundaryId++);
            _workItemCount = 0;
            _rootFailed = false;
        }
        else
        {
            RequireActiveRootBatch(batch);
        }

        long scopeId = checked(_nextScopeId++);
        long generation = _generation;
        _boundaries.Add(
            new BoundaryFrame(
                scopeId,
                _effectContext.Depth,
                _logicalAttacks.Count
            )
        );
        return new BattleReactionBoundaryScope(
            this,
            scopeId,
            _activeRootBoundaryId,
            generation
        );
    }

    internal BattleLogicalAttackScope BeginLogicalAttack(
        BattleAttackDeliveryKind deliveryKind
    )
    {
        RequireActiveBoundary();
        if (_rootFailed)
            throw new InvalidOperationException("active reaction root has failed");
        if (deliveryKind == BattleAttackDeliveryKind.Unknown)
            throw new ArgumentException("attack delivery kind is required");
        ConsumeWorkItem(BattleReactionWorkItemKind.LogicalAttack);

        BattleEffectOrigin currentOrigin =
            _effectContext.RequireCurrentForAttack();
        var context = new BattleAttackActionContext(
            new BattleAttackActionId(checked(_nextActionId++)),
            _activeRootBoundaryId,
            currentOrigin,
            deliveryKind
        );

        long scopeId = checked(_nextScopeId++);
        long generation = _generation;
        _logicalAttacks.Add(new LogicalFrame(scopeId, context));
        return new BattleLogicalAttackScope(
            this,
            scopeId,
            generation,
            context
        );
    }

    internal void RequireActiveRootBatch(BattleEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        RequireActiveBoundary();
        if (!ReferenceEquals(_rootBatch, batch))
        {
            FailRoot();
            throw new InvalidOperationException(
                "reaction boundary received a different event batch"
            );
        }
    }

    internal void RequireActiveBoundary()
    {
        if (_boundaries.Count == 0 || _rootBatch == null)
            throw new InvalidOperationException("no active reaction boundary");
    }

    internal void RequireActiveLogicalAttack(
        BattleAttackActionId actionId,
        long rootBoundaryId,
        BattleEffectOrigin origin,
        BattleAttackDeliveryKind deliveryKind
    )
    {
        RequireActiveBoundary();
        if (_rootFailed)
            throw new InvalidOperationException("active reaction root has failed");
        if (_logicalAttacks.Count == 0)
        {
            FailRoot();
            throw new InvalidOperationException(
                "attack fact was published without an active logical attack"
            );
        }
        BattleAttackActionContext current =
            _logicalAttacks[^1].Context;
        if (
            !actionId.IsValid
            || rootBoundaryId != _activeRootBoundaryId
            || current.ActionId != actionId
            || current.RootBoundaryId != rootBoundaryId
            || !ReferenceEquals(current.Origin, origin)
            || current.DeliveryKind != deliveryKind
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "attack fact does not match the active logical attack"
            );
        }
    }

    internal void ConsumeWorkItem(BattleReactionWorkItemKind kind)
    {
        RequireActiveBoundary();
        int nextCount = checked(_workItemCount + 1);
        if (nextCount > _limits.MaxWorkItems)
        {
            FailRoot();
            throw new InvalidOperationException(
                $"reaction root {_activeRootBoundaryId} exceeded "
                + $"{_limits.MaxWorkItems} work items before {kind}; "
                + $"depth={_boundaries.Count}, consumed={_workItemCount}"
            );
        }
        _workItemCount = nextCount;
    }

    internal void StopAcceptingAndAbort()
    {
        _accepting = false;
        _rootFailed = true;
        _drainOwner?.AbortBoundary();
        _logicalAttacks.Clear();
        _boundaries.Clear();
        _generation = checked(_generation + 1);
        ResetRootFields();
    }

    internal void ResetForBattle()
    {
        if (_boundaries.Count != 0 || _logicalAttacks.Count != 0)
        {
            throw new InvalidOperationException(
                "cannot reset reaction coordinator with active scopes"
            );
        }
        _drainOwner?.AbortBoundary();
        ResetRootFields();
        _generation = checked(_generation + 1);
        _nextRootBoundaryId = 1;
        _nextActionId = 1;
        _nextScopeId = 1;
        _accepting = true;
        _rootFailed = false;
    }

    public void Dispose()
    {
        StopAcceptingAndAbort();
        _drainOwner = null;
    }

    internal void CompleteBoundary(
        long scopeId,
        long rootBoundaryId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        BoundaryFrame frame = RequireTopBoundary(scopeId, rootBoundaryId);
        if (frame.Completed)
            throw new InvalidOperationException("reaction boundary completed twice");
        if (_rootFailed)
            throw new InvalidOperationException("failed reaction root cannot complete");
        if (_logicalAttacks.Count != frame.LogicalDepthAtEntry)
        {
            FailRoot();
            throw new InvalidOperationException(
                "logical attack scope escaped its reaction boundary"
            );
        }

        if (_boundaries.Count == 1)
        {
            IBattleReactionDrainOwner drainOwner = _drainOwner
                ?? throw new InvalidOperationException(
                    "reaction drain owner is not bound"
                );
            try
            {
                drainOwner.Drain(_rootBatch);
            }
            catch
            {
                FailRoot();
                throw;
            }
            if (_logicalAttacks.Count != 0)
            {
                FailRoot();
                throw new InvalidOperationException(
                    "logical attack stack is not empty after reaction drain"
                );
            }
        }
        frame.Completed = true;
    }

    internal void DisposeBoundary(
        long scopeId,
        long rootBoundaryId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        BoundaryFrame frame = RequireTopBoundary(scopeId, rootBoundaryId);
        bool rootAlreadyFailed = _rootFailed;
        bool originDepthMismatch =
            _effectContext.Depth != frame.OriginDepthAtEntry;
        bool logicalDepthMismatch =
            _logicalAttacks.Count != frame.LogicalDepthAtEntry;
        bool failed =
            !frame.Completed
            || originDepthMismatch
            || logicalDepthMismatch;
        if (failed)
            FailRoot();

        _boundaries.RemoveAt(_boundaries.Count - 1);
        bool exitedRoot = _boundaries.Count == 0;
        if (exitedRoot)
        {
            _logicalAttacks.Clear();
            ResetRootFields();
        }

        // CompleteBoundary/drain 已经记录并抛出原始 root failure 时，
        // Dispose 只负责清理，不能用 scope-contract 异常覆盖原异常。
        if (rootAlreadyFailed && !frame.Completed)
            return;
        if (originDepthMismatch || logicalDepthMismatch)
        {
            throw new InvalidOperationException(
                "reaction boundary did not restore origin/action depth"
            );
        }
        if (!frame.Completed)
        {
            throw new InvalidOperationException(
                "reaction boundary disposed without Complete()"
            );
        }
    }

    internal void CompleteLogicalAttack(
        long scopeId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        LogicalFrame frame = RequireTopLogical(scopeId);
        if (frame.Completed)
            throw new InvalidOperationException("logical attack completed twice");
        frame.Completed = true;
    }

    internal void DisposeLogicalAttack(
        long scopeId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        LogicalFrame frame = RequireTopLogical(scopeId);
        bool rootAlreadyFailed = _rootFailed;
        bool completed = frame.Completed;
        _logicalAttacks.RemoveAt(_logicalAttacks.Count - 1);
        if (completed)
            return;
        FailRoot();
        // coordinator guard 已经抛出原始 root failure 时，Dispose 只清理；
        // 不能用 scope-contract 异常覆盖 root id/depth/count 诊断。
        if (rootAlreadyFailed)
            return;
        throw new InvalidOperationException(
            "logical attack disposed without Complete()"
        );
    }

    private BoundaryFrame RequireTopBoundary(
        long scopeId,
        long rootBoundaryId
    )
    {
        RequireActiveBoundary();
        if (
            rootBoundaryId != _activeRootBoundaryId
            || _boundaries[^1].ScopeId != scopeId
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "reaction boundaries must complete/dispose in LIFO order"
            );
        }
        return _boundaries[^1];
    }

    private LogicalFrame RequireTopLogical(long scopeId)
    {
        if (
            _logicalAttacks.Count == 0
            || _logicalAttacks[^1].ScopeId != scopeId
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "logical attacks must complete/dispose in LIFO order"
            );
        }
        return _logicalAttacks[^1];
    }

    private void FailRoot()
    {
        _rootFailed = true;
        _drainOwner?.AbortBoundary();
    }

    private void ResetRootFields()
    {
        _rootBatch = null;
        _activeRootBoundaryId = 0;
        _workItemCount = 0;
        _rootFailed = false;
    }

    private sealed class BoundaryFrame
    {
        internal BoundaryFrame(
            long scopeId,
            int originDepthAtEntry,
            int logicalDepthAtEntry
        )
        {
            ScopeId = scopeId;
            OriginDepthAtEntry = originDepthAtEntry;
            LogicalDepthAtEntry = logicalDepthAtEntry;
        }

        internal long ScopeId { get; }
        internal int OriginDepthAtEntry { get; }
        internal int LogicalDepthAtEntry { get; }
        internal bool Completed { get; set; }
    }

    private sealed class LogicalFrame
    {
        internal LogicalFrame(
            long scopeId,
            BattleAttackActionContext context
        )
        {
            ScopeId = scopeId;
            Context = context;
        }

        internal long ScopeId { get; }
        internal BattleAttackActionContext Context { get; }
        internal bool Completed { get; set; }
    }
}

internal sealed class BattleReactionBoundaryScope : IDisposable
    {
        private BattleAttackActionCoordinator _owner;
        private readonly long _scopeId;
        private readonly long _rootBoundaryId;
        private readonly long _generation;

        internal BattleReactionBoundaryScope(
            BattleAttackActionCoordinator owner,
            long scopeId,
            long rootBoundaryId,
            long generation
        )
        {
            _owner = owner;
            _scopeId = scopeId;
            _rootBoundaryId = rootBoundaryId;
            _generation = generation;
        }

        internal void Complete()
        {
            BattleAttackActionCoordinator owner = _owner
                ?? throw new ObjectDisposedException(GetType().Name);
            owner.CompleteBoundary(
                _scopeId,
                _rootBoundaryId,
                _generation
            );
        }

        public void Dispose()
        {
            BattleAttackActionCoordinator owner = _owner;
            if (owner == null)
                return;
            _owner = null;
            owner.DisposeBoundary(
                _scopeId,
                _rootBoundaryId,
                _generation
            );
        }
    }

internal sealed class BattleLogicalAttackScope : IDisposable
    {
        private BattleAttackActionCoordinator _owner;
        private readonly long _scopeId;
        private readonly long _generation;

        internal BattleLogicalAttackScope(
            BattleAttackActionCoordinator owner,
            long scopeId,
            long generation,
            BattleAttackActionContext context
        )
        {
            _owner = owner;
            _scopeId = scopeId;
            _generation = generation;
            Context = context
                ?? throw new ArgumentNullException(nameof(context));
        }

        internal BattleAttackActionContext Context { get; }

        internal void Complete()
        {
            BattleAttackActionCoordinator owner = _owner
                ?? throw new ObjectDisposedException(GetType().Name);
            owner.CompleteLogicalAttack(_scopeId, _generation);
        }

        public void Dispose()
        {
            BattleAttackActionCoordinator owner = _owner;
            if (owner == null)
                return;
            _owner = null;
            owner.DisposeLogicalAttack(_scopeId, _generation);
        }
}
```

上面两个 scope 若在正常控制流离开时未 `Complete()`，`Dispose()` 会先调用
`FailRoot()` / `AbortBoundary()`，再抛出 scope-contract 错误；若
`CompleteBoundary(...)`、drain、`RequireActiveLogicalAttack(...)`、
`ConsumeWorkItem(...)` 或 nested depth guard 已先将 root 标记失败并抛出带相应
root/work/depth 诊断的原始异常，boundary/logical dispose 都只清理且不得再抛。两者必须在
本次 `Dispose()` 调用 `FailRoot()` 前冻结 `rootAlreadyFailed`，分别保证原始诊断不被
`"reaction boundary disposed without Complete()"` 或
`"logical attack disposed without Complete()"` 覆盖。production entry point 必须把这类
异常视为 runtime invariant failure，不能捕获后继续同一 battle。

`StopAcceptingAndAbort()` / `ResetForBattle()` 是更高层的 battle-generation invalidation：
两者递增 coordinator generation。旧 generation 的 boundary/logical handle 随后调用
`Complete()` 或 `Dispose()` 必须静默 no-op，既不能访问已经清空的 stack，也不能因
scope id 在新 battle 从 1 重新分配而误完成/弹出新 generation 的 frame。这条规则只覆盖
teardown/transition 已明确失效的旧 handle；同一 generation 内的漏 `Complete()`、LIFO
破坏与 root failure 仍按上文 fail fast。

`BattleTimelineDriver`、contingency bridge 等 borrower 不直接取 coordinator 字段；`BattleRuntimeModule` 新增且只新增下面这个内部转发入口。它是正式 active-root/batch 校验入口，不创建 batch，也不吞 coordinator 的异常：

```csharp
internal BattleReactionBoundaryScope BeginReactionBoundary(BattleEventBatch batch)
{
    _ensure_sidecars_ready();
    if (batch == null)
        throw new ArgumentNullException(nameof(batch));
    return _attackActionCoordinator.BeginReactionBoundary(batch);
}

internal void RequireActiveReactionBatch(BattleEventBatch batch)
{
    _ensure_sidecars_ready();
    _attackActionCoordinator.RequireActiveRootBatch(batch);
}

internal BattleLogicalAttackScope BeginLogicalAttack(
    BattleAttackDeliveryKind deliveryKind
)
{
    _ensure_sidecars_ready();
    return _attackActionCoordinator.BeginLogicalAttack(deliveryKind);
}

internal BattleEffectExecutionContextService EffectExecutionContext
{
    get
    {
        _ensure_sidecars_ready();
        return _effectExecutionContext;
    }
}
```

- reaction boundary 决定队列何时可以排空。
- logical attack 决定去重所需的 `BattleAttackActionId`。
- logical attack 的 origin 从当前 `BattleEffectExecutionContextService` scope 冻结。
- 一个 boundary 可包含多个独立 logical attack。
- nested boundary 只增加 depth，必须复用 root batch。
- 只有最外层 boundary 正常完成时才触发排空。

正式 root boundary 只由已经拥有用户可见 `BattleEventBatch` 的顶层 mutation segment 打开。这里的“single root”表示任一时刻最多一个 active root，且其所有 nested action 只使用同一个 batch；不表示 `advance(tick_count > 1)` 整个调用只能有一个长生命周期 scope。timeline 必须逐 tick 在 objective mutation 内完成反击并结算胜负，不能把第一个 tick 的反击拖到后续 tick 之后。

| 生产 segment | 当前代码锚点 | root batch | P1A 插入点 |
|---|---|---|---|
| command | `BattleRuntimeModule.cs:1164-1178` → `IssueCommandCore:1181-1282` | `:1184` 创建的 batch | public `IssueCommand(...)` 已在外层打开 objective mutation；`IssueCommandCore` 在创建 batch 后打开 root，完整 command body 结束后先 drain，再返回给 `IssueCommand(...)` 的 `EndObjectiveMutation(...)` |
| `advance(...)` 清理失效 active unit | `BattleRuntimeModule.cs:1023-1033` | `advance:1010` 的 batch | 在现有 `BeginObjectiveMutation` 内包住 `_end_active_turn(batch)`；完成/drain 后才执行 `EndObjectiveMutation` |
| 每个 timeline tick | `BattleTimelineDriver.cs:36-50` | caller 传入的 `advance:1010` batch | 在每次现有 `BeginObjectiveMutation` 之后、`ApplyTimelineStep(...)` 之前打开 root；`ApplyTimelineStep` 后完成/drain，再执行该 tick 的 `EndObjectiveMutation` |
| ready-unit activation | `BattleRuntimeModule.cs:1144-1157` → `BattleTimelineDriver.ActivateNextReadyUnit:524-605` | 同一个 `advance:1010` batch | 在现有 activation objective mutation 内包住 `_activate_next_ready_unit(batch)`；turn-start contingency/sequential AutoCast 与其 nested attack 全部 drain 后再结束 objective mutation |
| AI/失控 command | `BattleRuntimeModule.cs:1044-1047`、`:1102-1125` | `IssueCommand(...)` 返回的 command batch | 不为 `advance:1010` 的临时空 batch 打开 root；直接委托 command segment，避免双 root |
| battle confirm | `BattleRuntimeModule.cs:2159-2180` | `:2161` 创建的 batch | `OnBattleConfirmed(...)` 在 `BeginObjectiveMutation` 后打开 root，包住 `BattleContingencyBridgeService.OnBattleConfirmed(batch)`；drain 后执行 `:2174` 的 batch log/report flush，再结束 objective mutation |
| direct runtime regression fixture | fixture 显式创建的 batch | fixture batch | fixture 必须按 boundary → `Push(PlayerCommand())` → logical/action → `Complete()` 顺序显式建立并逆序释放；不提供 production fallback |

`IssueCommandCore(...)` 当前有大量 early return，不能在每个 return 前人工复制
`Complete()`。目标改造是保留 public `IssueCommand(...)` 的 objective wrapper，并保留
当前 `_state == null || command == null` 这两个无 mutation guard 在 root **之前**：
`_ensure_sidecars_ready()` 在 state 为 null 时会把 coordinator 设为 stopped，因此不能先
调用 `BeginReactionBoundary(...)` 再指望搬入的 body 返回空 batch。只有通过这两个 guard
后，才把当前 `IssueCommandCore:1187-1281` 的其余主体搬入返回 `void` 的
`ExecuteCommandCoreIntoBatch(command, batch)`；原 `return batch` 改为 `return`，其余语句
与现有内部 log flush 顺序不动。唯一 root wrapper 固定为：

```csharp
private BattleEventBatch IssueCommandCore(BattleCommand command)
{
    _ensure_sidecars_ready();
    BattleEventBatch batch = _new_batch();
    if (_state == null || command == null)
        return batch;

    using BattleReactionBoundaryScope boundary =
        _attackActionCoordinator.BeginReactionBoundary(batch);
    using IDisposable originScope =
        _effectExecutionContext.Push(BattleEffectOrigin.PlayerCommand());

    ExecuteCommandCoreIntoBatch(command, batch);
    int logCountBeforeDrain = batch.LogLinesTyped.Count;
    int reportCountBeforeDrain = batch.ReportEntriesTyped.Count;
    boundary.Complete();
    _append_batch_logs_to_state_from(
        batch,
        logCountBeforeDrain,
        reportCountBeforeDrain
    );
    return batch;
}
```

最后一次 `_append_batch_logs_to_state_from(...)` 只 flush drain 新增的日志/报告，不能从 0 重放，否则会重复 `ExecuteCommandCoreIntoBatch(...)` 已按现状提交的 command 日志。若 command body 或 drain 抛错，`Complete()` 不会执行，scope dispose 按 §8.6 清 transient reaction state，public `IssueCommand(...)` 的 `mutationCompleted` 仍为 false。

timeline tick 的目标骨架必须放在现有 objective scope 内，而不是包在整个 `advance(...)` 外：

```csharp
internal void AdvanceTimeline(int tickCount, BattleEventBatch batch)
{
    BattleRuntimeModule runtime = _ResolveRuntime();
    BattleState state = _ResolveState();
    if (
        runtime == null
        || state == null
        || state.timeline == null
        || tickCount <= 0
    )
    {
        return;
    }
    if (batch == null)
        throw new ArgumentNullException(nameof(batch));

    int resolvedTickCount = Mathf.Max(tickCount, 0);
    for (int i = 0; i < resolvedTickCount; i++)
    {
        runtime.BeginObjectiveMutation();
        bool mutationCompleted = false;
        try
        {
            using BattleReactionBoundaryScope boundary =
                runtime.BeginReactionBoundary(batch);
            using IDisposable originScope =
                runtime.EffectExecutionContext.Push(
                    BattleEffectOrigin.Timeline("timeline_tick")
                );
            ApplyTimelineStep(batch, state.timeline.tu_per_tick);
            int logCountBeforeDrain = batch.LogLinesTyped.Count;
            int reportCountBeforeDrain = batch.ReportEntriesTyped.Count;
            boundary.Complete();
            runtime._append_batch_logs_to_state_from(
                batch,
                logCountBeforeDrain,
                reportCountBeforeDrain
            );
            mutationCompleted = true;
        }
        finally
        {
            runtime.EndObjectiveMutation(batch, mutationCompleted);
        }
        if (state.FinalDecision != null)
            return;
    }
}
```

`advance:1023-1033` 的 dead-active cleanup 精确替换为：

```csharp
BeginObjectiveMutation();
bool mutationCompleted = false;
try
{
    using BattleReactionBoundaryScope boundary =
        BeginReactionBoundary(batch);
    using IDisposable originScope =
        EffectExecutionContext.Push(
            BattleEffectOrigin.Timeline("dead_active_cleanup")
        );
    _end_active_turn(batch);
    int logCountBeforeDrain = batch.LogLinesTyped.Count;
    int reportCountBeforeDrain = batch.ReportEntriesTyped.Count;
    boundary.Complete();
    _append_batch_logs_to_state_from(
        batch,
        logCountBeforeDrain,
        reportCountBeforeDrain
    );
    mutationCompleted = true;
}
finally
{
    EndObjectiveMutation(batch, mutationCompleted);
}
return batch;
```

`advance:1144-1157` 的 activation 不引用“同一代码”占位，精确替换为：

```csharp
BeginObjectiveMutation();
bool mutationCompleted = false;
try
{
    using BattleReactionBoundaryScope boundary =
        BeginReactionBoundary(batch);
    using IDisposable originScope =
        EffectExecutionContext.Push(
            BattleEffectOrigin.Timeline("ready_unit_activation")
        );
    _activate_next_ready_unit(batch);
    int logCountBeforeDrain = batch.LogLinesTyped.Count;
    int reportCountBeforeDrain = batch.ReportEntriesTyped.Count;
    boundary.Complete();
    _append_batch_logs_to_state_from(
        batch,
        logCountBeforeDrain,
        reportCountBeforeDrain
    );
    mutationCompleted = true;
}
finally
{
    EndObjectiveMutation(batch, mutationCompleted);
}
```

这段不带 `return batch`，继续落到方法现有 `:1158`。这样 counterattack 造成的击倒在本
tick/activation 的 `EndObjectiveMutation(...)` 中可见，`BattleTimelineDriver.cs:49-50`
仍能在正式终局后停止剩余 tick；若把一个 root 包在整个
`AdvanceTimeline(...)` 外，现有逐 tick objective flush 会先于反击发生，属于错误实现。

四个入口不能为了“统一”而都从 index `0` flush。基线代码已经给出了逐入口合同：

| 入口 | 基线已提交的主体 surface | root 完成后的精确提交 |
|---|---|---|
| command | `IssueCommandCore:1191-1261` 的多个 early-return flush，以及 `:1278` 的 end-turn delta flush | 仅从 `logCountBeforeDrain/reportCountBeforeDrain` 提交 drain 新增项 |
| timeline tick | `BattleTimelineDriver.ApplyTimelineStep:174-213` 不统一提交 batch log/report | 仍不重放 step 主体，只提交 drain 新增项，保持基线主体 surface 不变 |
| dead-active cleanup | `BattleTimelineDriver.EndActiveTurn:473-522` 不提交 batch log/report | 仅提交 drain 新增项 |
| activation | `BattleTimelineDriver.ActivateNextReadyUnit:576` 和 `:604` 只提交它自己当前已经提交的两条日志 | 不从 `0` 重放 activation batch；仅提交 drain 新增项 |

因此三个 delta wrapper 中，计数必须在各自 producer 返回后、`boundary.Complete()` 前读取。
把计数移到 producer 前会把 timeline/activation 的历史 batch 内容或 activation 已单独提交的
日志再次写入 state；把计数移到 `Complete()` 后则会漏掉全部反击日志与 report。这里的
`_append_batch_logs_to_state_from(...)` 只负责 state 的历史投影，batch 中原有和 drain 新增的
entries 始终全部保留给本次返回值。

`OnBattleConfirmed(...)` 没有先前局部 flush，目标代码不使用 delta：

```csharp
internal void OnBattleConfirmed(BattleEventBatch batch)
{
    ArgumentNullException.ThrowIfNull(batch);
    BeginObjectiveMutation();
    bool mutationCompleted = false;
    try
    {
        using BattleReactionBoundaryScope boundary =
            BeginReactionBoundary(batch);
        using IDisposable originScope =
            EffectExecutionContext.Push(
                BattleEffectOrigin.Timeline("battle_confirm")
            );
        _contingencyBridgeService.OnBattleConfirmed(batch);
        boundary.Complete();
        _append_batch_logs_to_state(batch);
        mutationCompleted = true;
    }
    finally
    {
        EndObjectiveMutation(batch, mutationCompleted);
    }
}
```

两个直接 resolver fixture 和装备能力直接 fixture 共用下面的测试 helper；禁止每个测试各写一
种顺序：

```csharp
// tests/shared/BattleReactionRootTestHelper.cs
internal static class BattleReactionRootTestHelper
{
    internal static void ExecuteInReactionRoot(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        Action body
    )
    {
        ExecuteInReactionRoot(
            runtime,
            batch,
            BattleEffectOrigin.PlayerCommand(),
            body
        );
    }

    internal static void ExecuteInReactionRoot(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        BattleEffectOrigin origin,
        Action body
    )
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(body);
        using BattleReactionBoundaryScope boundary =
            runtime.BeginReactionBoundary(batch);
        using IDisposable originScope =
            runtime.EffectExecutionContext.Push(origin);
        body();
        boundary.Complete();
    }
}
```

`body` 内若直接调用 resolver，还必须自己打开并完成 logical scope；helper 只拥有 root
boundary/origin，不伪造 action context。三参 overload 只用于 player-command/direct
resolver fixture；直接 timeline/AutoCast fixture 必须调用四参 overload并传正式
`Timeline(...)`/`AutoCast(request)` origin，不能为了省参数把所有 fixture 伪装成
`PlayerCommand`。

P1A 对下面这些 test-only direct entry 做一次完整静态审计。它们绕过 production root
wrapper，不能只修当前恰好触发攻击的 case：

| direct entry | 当前 fixture | 固定改法 |
|---|---|---|
| `_apply_ground_unit_effects_result(...)` | `run_battle_ground_effect_typed_sets_regression.cs:63` | `ExecuteInReactionRoot(..., PlayerCommand)` 内按 effect/projection 解析 delivery，打开一个 logical scope，把 `Context` 插在 batch 后，再 complete logical |
| `handle_charge_skill_command_result(...)` | `run_battle_ai_charge_path_aoe_behavior_regression.cs:321/:352` | source/state setup 后用三参 `ExecuteInReactionRoot(...)`；charge wrapper 自己拥有 logical scope |
| `_skill_orchestrator.ExecuteAutoCast(...)` | `run_prismatic_sphere_special_entry_regression.cs:185`、`run_prismatic_sphere_regression.cs:1313` | 四参 helper 传 `BattleEffectOrigin.AutoCast(request)`；orchestrator 自己拥有 logical scope |
| `_skill_orchestrator.ResolvePendingCast(...)` | `run_prismatic_sphere_special_entry_regression.cs:222`、`run_prismatic_sphere_regression.cs:1496` | 四参 helper 传 `Timeline("ready_unit_activation")`；pending owner 自己拥有 logical scope |
| `_skill_orchestrator._handle_skill_command(...)` | `run_meteor_swarm_special_profile_regression.cs:256/:267` | 用三参 helper；command owner 自己拥有 logical scope |
| `ApplyTimelineStep(...)` | `run_time_stasis_regression.cs:747`、`run_temporal_status_semantics_regression.cs:652`、`run_plague_tongue_weapon_ability_regression.cs:322/:351/:357/:370/:407`、`run_void_axe_weapon_ability_regression.cs:153/:161/:163` | 四参 helper 传 `Timeline("timeline_tick")`；即使当前 fixture 未触发 pending/AutoCast，也必须建立 production-equivalent root |
| `ActivateNextReadyUnit(...)` | `run_sands_time_weapon_ability_regression.cs:218`、`run_temporal_status_semantics_regression.cs:204/:338` | 四参 helper 传 `Timeline("ready_unit_activation")` |

以后新增直接调用 `ApplyTimelineStep`、`ActivateNextReadyUnit`、`EndActiveTurn`、
`ResolvePendingCast`、`_handle_skill_command`、`handle_charge_skill_command_result`、
`_apply_ground_unit_effects_result` 或 orchestrator `ExecuteAutoCast` 的 fixture，都必须在
同一次修改中声明 production-equivalent root/origin；静态测试枚举这些 invocation，并拒绝
不在 `ExecuteInReactionRoot(...)` body 内的调用。

ground fixture 的目标形态不能只“补一个 null context”，固定为：

```csharp
BattleGroundUnitEffectsResult result = null;
BattleReactionRootTestHelper.ExecuteInReactionRoot(
    fixture.Runtime,
    batch,
    () =>
    {
        BattleAttackDeliveryKind deliveryKind =
            BattleAttackDeliveryRules.Resolve(
                new[] { fixture.WindPushEffect },
                fixture.Source.GetWeaponProjectionReadViewTyped()
            );
        using BattleLogicalAttackScope logicalAttack =
            fixture.Runtime.BeginLogicalAttack(deliveryKind);
        result =
            fixture.Runtime._ground_effect_service
                ._apply_ground_unit_effects_result(
                    fixture.Source,
                    fixture.Skill,
                    null,
                    new[] { fixture.WindPushEffect },
                    new List<Vector2I> { new(1, 0) },
                    batch,
                    logicalAttack.Context,
                    new List<Vector2I> { new(1, 0) }
                );
        logicalAttack.Complete();
    }
);
```

上面的“静态审计”不是人工 checklist。新增
`tests/static_analysis/run_battle_reaction_contract_static_regression.cs`，由
`tests/run_regression_suite.py` 现有 `tests_root.rglob("run_*.cs")` 自动发现。runner 固定包含
三个检查：

1. `TestDirectEntryRootManifest()` 枚举 `tests/**/*.cs`（排除自身和
   `tests/shared/BattleReactionRootTestHelper.cs`）中的八个 direct-entry method token；任何
   新文件/方法未出现在 §8.1 manifest 都失败。对已登记 method 提取 balanced method body，
   要求包含 `BattleReactionRootTestHelper.ExecuteInReactionRoot(`；timeline/AutoCast 项还
   分别要求对应 `BattleEffectOrigin.Timeline(...)` /
   `BattleEffectOrigin.AutoCast(request)` token，ground 项要求
   `BeginLogicalAttack(`、`logicalAttack.Context` 与 `logicalAttack.Complete()`。
2. `TestDamageResolverRequiredContextContract()` 对
   `BattleDamageResolver.cs` 的 balanced parameter list 断言
   `ResolveAttackEffects` declaration 恰好一个、第五参为无 default 的
   `AttackContext attack_context`；再扫描 `tests/**/*.cs` 的 override，拒绝
   `AttackContext ... = null`。三个旧四参 runner 必须包含显式 `new AttackContext()`；
   最终 `dotnet build magic.csproj` 负责对所有 invocation 做编译期 arity 验收。
3. `TestBattleTuGranularityOwner()` 在 `scripts/systems/battle/**/*.cs` 使用
   `const int\s+(TuGranularity|TU_GRANULARITY)\s*=`，断言匹配数为 1 且规范化路径严格等于
   `scripts/systems/battle/core/BattleTimelineState.cs`。

runner 的源码提取必须忽略字符串与行/块注释，并使用 balanced brace/parenthesis，不允许用
“统计一行逗号数”判断 invocation/parameter arity。focused 命令固定为：

```bash
godot --headless -s res://tests/static_analysis/run_battle_reaction_contract_static_regression.cs
```

当前所有 production batch 创建/逃逸点必须按下表一次性处理：

| 当前代码 | 分类 | P1A 结论 |
|---|---|---|
| `BattleRuntimeModule.cs:1010` | `advance(...)` 用户可见 batch | 保留；只在上表列出的 mutation segment 打开顺序 root |
| `BattleRuntimeModule.cs:1184` | command 用户可见 batch | 保留；成为 command root batch |
| `BattleRuntimeModule.cs:1340` | promotion 用户可见 batch | 保留但明确不打开 reaction boundary |
| `BattleRuntimeModule.cs:2161` | battle-confirm 用户可见 batch | 保留；成为 confirm root batch |
| `BattleRuntimeModule.cs:2869` / `new_batch():1788` | factory / 当前无 caller 的薄包装 | 不作为新入口；反击实现不得调用它隐式造 batch |
| `BattleChargeResolver.cs:80`、`:199`、`MergeBatch:2160-2179` | producer 私有 child batch 与 merge | 删除；整条 charge 路径直接使用 caller root batch |
| `BattleContingencyBridgeService.cs:129` | `batch ?? _runtime._new_batch()` fallback | 删除；null 或 batch 不等于 active root 时抛错 |
| `BattleEventBatchProjection.cs:40` | Godot 同步投影的 null normalization | 保留；它只投影展示对象，不可到达 attack resolver |

`SubmitPromotionChoiceCore(...)` 的排除不是口头假设，而由当前调用体固定：`BattleRuntimeModule.cs:1343-1347` 只调用 `_characterGateway.PromoteProfession(...)`，`:1353-1359` 只写 progression delta、刷新 unit projection、changed-unit/log，`:1361-1373` 只维护 promotion modal/timeline frozen。该方法及其 callees在本基线内不调用 `ResolveAttackEffects`、damage hook、`ExecuteAutoCast`、equipment action resolver 或 ground/repeat/charge producer，所以 `:1340` 的 batch 不打开 boundary。未来若在这条链上新增任一上述调用，必须同时把 promotion 注册为正式 root segment并添加 action-contract 回归；否则由 `BeginLogicalAttack` / AutoCast active-root 前置条件 fail fast。

现有 AutoCast 路径的 boundary 责任固定如下：

| AutoCast 来源 | 当前执行位置 | P1A 规则 |
|---|---|---|
| `combat_started` release | `OnBattleConfirmed(...)` | 使用 battle-confirm root boundary/batch |
| `owner_turn_started` release 与 sequential release | `_record_turn_started(...)` / timeline activation | 使用当前 timeline root boundary/batch；正式路径移除 nullable batch |
| `OnHookFact(...)` 形成的 queued release | 后续正式 release pump | 使用执行该 pump 的 root boundary/batch，不沿用触发发生时已经结束的 batch |
| damage hook immediate release | `BeforeDamageResolved(...)` | 已在父 effect graph 内，打开 nested boundary 并复用 `context.Batch` |
| test-only direct `ExecuteAutoCast(...)` | regression fixture | fixture 先显式打开 root boundary |

`BattleContingencyBridgeService.ExecuteAutoCast(request, batch)`、`BattleContingencySystem.ExecuteQueuedReleaseContexts(...)` 与 `ExecuteNextSequentialAutoCastForOwner(...)` 都要求非空 batch。bridge 先校验 coordinator 存在 active root 且 batch 与 root 为同一实例，再为整个 AutoCast graph 打开/完成一个 nested reaction boundary，并在其内 push AutoCast origin；不允许在 bridge/system 内调用 `_new_batch()`。`_record_turn_started(...)` 的正式 contingency 路径也不再接受 null batch；只测 metrics 的回归应传入 batch 或直接调用 metrics owner，不能借 nullable 参数绕过 boundary。该方法的目标签名和 null contract 固定为：

```csharp
internal void _record_turn_started(
    BattleUnitState unit_state,
    BattleEventBatch batch
)
{
    ArgumentNullException.ThrowIfNull(batch);
    _metricsReportService.RecordTurnStartedMetrics(unit_state);
    _contingency_system.OnOwnerTurnStarted(unit_state, batch);
    _contingency_system.ExecuteQueuedReleaseContexts(
        new ContingencyFrozenTriggerFacts
        {
            TriggerSourceUnitId = unit_state?.unit_id ?? "",
            TriggerTargetUnitId = unit_state?.unit_id ?? "",
            TriggerSourceCell =
                unit_state?.GetAnchorCoord() ?? new Vector2I(-1, -1),
            TriggerTargetCell =
                unit_state?.GetAnchorCoord() ?? new Vector2I(-1, -1),
            TriggerCell =
                unit_state?.GetAnchorCoord() ?? new Vector2I(-1, -1),
        },
        batch
    );
    _contingency_system.ExecuteNextSequentialAutoCastForOwner(
        unit_state?.unit_id ?? "",
        batch
    );
}
```

删除默认参数与 `if (batch == null) return;`。所有 production caller 都在 active root 内传入
同一 root batch；测试若直接调用该入口，也必须先打开 root boundary 并在断言前完成它。

`BattleContingencyBridgeService.cs:121-130` 的目标方法体为：

```csharp
internal bool ExecuteAutoCast(
    AutoCastRequest request,
    BattleEventBatch batch
)
{
    _runtime._ensure_sidecars_ready();
    ArgumentNullException.ThrowIfNull(batch);
    _runtime.RequireActiveReactionBatch(batch);
    if (request?.IsValid != true || _runtime._state == null)
        return false;
    if (!IsContingencyAutoCastSourcePlayerLearned(request))
        return false;

    using BattleReactionBoundaryScope boundary =
        _runtime.BeginReactionBoundary(batch);
    using IDisposable originScope =
        _runtime.EffectExecutionContext.Push(
            BattleEffectOrigin.AutoCast(request)
        );
    bool executed =
        _runtime._skill_orchestrator.ExecuteAutoCast(request, batch);
    boundary.Complete();
    return executed;
}
```

`BattleRuntimeModule.EffectExecutionContext` 是 internal read-only property，返回已经 setup 的唯一 context service；它不允许 setter。`originScope` 在 `boundary` 之前 dispose（C# using declaration 逆序），因此 nested logical attack 完成时仍能看到 AutoCast origin，而返回父 producer 后 origin 已恢复。

除表中 entry 外，不允许再出现可以到达 damage hook、AutoCast 或正式攻击 resolver 的 production 顶层入口。以后新增这类入口时，必须同时登记 root batch/boundary owner 并增加 action-contract 回归，否则 fail closed。

### 8.2 各 producer 的 ID 规则

| producer | action id 语义 |
|---|---|
| 普通单/多目标攻击动作 | 整个逻辑攻击共用一个 ID |
| repeat attack stages | 外层 scope 保持打开；所有 stage 显式传同一个 parent context |
| charge 内属于同一攻击意图的 stages | charge producer 只开一次 scope；所有 path step 显式传同一个 context |
| 同一动作即时结算的 ground weapon effects | ground producer 只开一次 scope；所有单位/格子显式传同一个 context |
| 延迟到后续 timeline 的 ground attack | 实际触发时新建 ID |
| equipment immediate weapon attack | `ResolveImmediateWeaponAttackAction` 的每个 target execution 新建 ID；同一 payload 的多个 target 不共享 |
| contingency AutoCast | 每个 request 新建 ID |
| counterattack | 每次实际反击新建 ID |

“是否复用”必须在 producer 的 typed 调用点显式表达。缺少 context 不允许回退为自动分配，因为自动分配会让 repeat silently 失去去重。

当前五个 production `ResolveAttackEffects(...)` call site 的参数迁移必须一次性完成：

| 当前 resolver call | action 创建点 | 目标签名/传播 |
|---|---|---|
| `BattleSkillExecutionOrchestrator.cs:1829-1835` | `_handle_unit_skill_command:1488-1600` 每次 command 一个；`ResolvePendingUnitCast:1237-1297` 每次 release 一个；`ExecuteAutoUnitSkill:297-383` 每次 AutoCast 一个 | `_apply_unit_skill_result(...)` 与 `_resolve_unit_skill_effect_resolution(...)` 新增 non-null `BattleAttackActionContext actionContext`；delivery 已在 context 内 |
| `BattleRepeatAttackResolver.cs:813-819` | 不创建；复用上述 command/release/AutoCast context | `ApplyRepeatAttackSkillResult(...)`、stage helper 全链新增同一个 context 参数；所有 stage/target 写入同一 context |
| `BattleChargeResolver.cs:979-990` | charge command producer 每次完整 charge 一个 | path-step helper 接收同一 context；所有 step/target 复用 |
| `BattleGroundEffectService.cs:925-936` | 立即 ground command/release/AutoCast 在 ground producer 创建一个；未来 delayed tick 在实际 tick producer 新建一个 | `ApplyGroundUnitEffectsResultTyped(...)` → `_apply_ground_unit_effects_result(...)` → `_resolve_ground_unit_effect_resolution(...)` 全链新增同一个 context 参数；立即多格/多目标复用，delayed 不复用旧 action |
| `BattleEquipmentAbilityRuntimeService.cs:1505-1516` | 删除此 call；由 `BattleImmediateWeaponAttackService.Execute(...)` 每个 equipment action/target 新建 | 使用 §10.3 的完整 context |

标准 producer 的创建形状固定为：

```csharp
BattleAttackDeliveryKind deliveryKind =
    BattleAttackDeliveryRules.Resolve(
        resolvedEffectDefinitions,
        active_unit.GetWeaponProjectionReadViewTyped()
    );
using BattleLogicalAttackScope logicalAttack =
    Runtime.BeginLogicalAttack(deliveryKind);

// 整个既有 target/stage loop；每次调用都只传
// logicalAttack.Context；delivery 从 context 读取。

logicalAttack.Complete();
return applied;
```

scope 只能在 validation/cost/control 已允许进入 effect graph 后创建，并必须覆盖 target loop
之后的 producer-specific outcome；不能在每个 target 或 repeat stage 内创建。为了让这一约束成为
编译期约束，下面列出的 context 参数全部是 required、non-null，不能保留 optional/default
overload。

#### 8.2.1 unit-target 调用链的逐方法改签

`BattleSkillExecutionOrchestrator` 新增唯一的 producer helper；它只负责冻结 weapon projection
和开 scope，不执行 target loop：

```csharp
private BattleLogicalAttackScope BeginLogicalAttackForEffects(
    BattleUnitState sourceUnit,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions
)
{
    ArgumentNullException.ThrowIfNull(sourceUnit);
    BattleRuntimeModule runtime = Runtime
        ?? throw new InvalidOperationException("battle runtime is not bound");
    BattleAttackDeliveryKind deliveryKind =
        BattleAttackDeliveryRules.Resolve(
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            sourceUnit.GetWeaponProjectionReadViewTyped()
        );
    return runtime.BeginLogicalAttack(deliveryKind);
}
```

三个 unit-target scope owner 及插入点固定为：

| scope owner | 在当前方法中的前置 token | scope 创建位置 | 正常完成位置 |
|---|---|---|---|
| `_handle_unit_skill_command(...)` | `if (spellControlContext.SkipEffects) return true;` | 该 guard 之后、读取 `_repeat_attack_resolver` 之前 | random-chain 分支返回前或当前 `return applied;` 前 |
| `ResolvePendingUnitCast(...)` | `if (state == null) return false;` | state guard 之后、读取 `_repeat_attack_resolver` 之前 | 当前 `return applied;` 前 |
| `ExecuteAutoUnitSkill(...)` | `CanApplyUnitSkillOrRepeatResultFromDefinitions(...)` guard | guard 之后、读取 `_repeat_attack_resolver` 之前 | random-chain 分支返回前或当前 `return applied;` 前 |

以 `_handle_unit_skill_command(...)` 为准，`BattleSkillExecutionOrchestrator.cs:1544-1600`
精确改成下面的控制流；pending/AutoCast 使用相同的 scope 形状，只保留各自现有 validation：

```csharp
using BattleLogicalAttackScope logicalAttack =
    BeginLogicalAttackForEffects(active_unit, resolvedEffectDefinitions);
BattleAttackActionContext actionContext = logicalAttack.Context;

BattleRepeatAttackResolver repeatAttackResolver =
    Runtime?._repeat_attack_resolver;
CombatEffectDefinition repeatAttackEffect =
    repeatAttackResolver?.get_repeat_attack_effect_def(
        resolvedEffectDefinitions
    );
if (isRandomChain)
{
    bool randomChainApplied =
        _randomChainSkillService._handle_random_chain_unit_skill_command(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            batch,
            actionContext,
            resolvedEffectDefinitions,
            repeatAttackEffect,
            spellControlContext
        );
    logicalAttack.Complete();
    return randomChainApplied;
}

bool applied = false;
foreach (BattleUnitState targetUnit in validation.TargetUnits)
{
    if (targetUnit == null)
        continue;
    if (repeatAttackEffect != null)
    {
        if (
            repeatAttackResolver != null
            && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                active_unit,
                targetUnit,
                skillDefinition,
                resolvedEffectDefinitions,
                repeatAttackEffect,
                batch,
                actionContext,
                castVariantDefinition
            )
        )
        {
            applied = true;
        }
        continue;
    }
    if (
        _apply_unit_skill_result(
            active_unit,
            targetUnit,
            skillDefinition,
            castVariantDefinition,
            resolvedEffectDefinitions,
            batch,
            actionContext,
            spellControlContext
        )
    )
    {
        applied = true;
    }
}
logicalAttack.Complete();
return applied;
```

`ResolvePendingUnitCast(...)` 和 `ExecuteAutoUnitSkill(...)` 中的 random-chain、repeat 与
normal 三种 call 使用完全相同的 argument order；区别只在它们现有的
`pendingCast.TargetUnitIds` / `validation.TargetUnits` 遍历和 spell-control 值。不能抽一个
新的 gameplay owner 来合并这三个方法。

从这三个 owner 向 resolver 的目标签名固定如下：

```csharp
// BattleSkillExecutionOrchestrator.cs
internal UnitSkillEffectResolution ResolveUnitSkillEffectResult(
    BattleUnitState active_unit,
    BattleUnitState target_unit,
    SkillDefinition skillDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    CombatCastVariantDefinition castVariantDefinition = null
);

private UnitSkillEffectResolution _resolve_unit_skill_effect_resolution(
    BattleUnitState active_unit,
    BattleUnitState target_unit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    bool forceHitAllowCrit = false
);

internal bool _apply_unit_skill_result(
    BattleUnitState active_unit,
    BattleUnitState target_unit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    BattleSpellControlResult spell_control_context = default,
    bool force_hit_allow_crit = false
);

// BattleRepeatAttackResolver.cs
internal bool ApplyRepeatAttackSkillResult(
    BattleUnitState active_unit,
    BattleUnitState target_unit,
    SkillDefinition skill_definition,
    IEnumerable<CombatEffectDefinition> effect_definitions,
    CombatEffectDefinition repeat_attack_effect,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    CombatCastVariantDefinition castVariantDefinition = null
);

private AttackEffectResolutionResult ResolveRepeatAttackStageResult(
    BattleUnitState active_unit,
    BattleUnitState target_unit,
    SkillDefinition skill_definition,
    CombatEffectDefinition repeat_attack_effect,
    BattleRepeatAttackStageSpec stage_spec,
    int stage_index,
    IEnumerable<CombatEffectDefinition> stage_effects,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext
);

// BattleRandomChainSkillService.cs
internal bool _handle_random_chain_unit_skill_command(
    BattleUnitState active_unit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    IReadOnlyList<CombatEffectDefinition> effect_definitions,
    CombatEffectDefinition repeat_attack_effect,
    BattleSpellControlResult spell_control_context
);

// BattleNineEchoFinalHammerResolver.cs
internal void ApplySuccessfulHitReward(
    BattleUnitState activeUnit,
    BattleUnitState targetUnit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    int successfulHitCount,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext
);

private void ApplyTerminalReward(
    BattleUnitState activeUnit,
    BattleUnitState targetUnit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext
);
```

上述每个入口第一条业务 guard 都执行
`ArgumentNullException.ThrowIfNull(actionContext)`。两个真正构造 `AttackContext` 的位置
逐字新增同一 member：

```csharp
// BattleSkillExecutionOrchestrator.cs:1818-1824
var attackContext = new AttackContext
{
    BattleState = RtState(),
    SkillId = skillDefinition?.SkillId ?? new StringName(""),
    EventBatch = batch,
    ForceHitAllowCrit = forceHitAllowCrit,
    Action = actionContext,
};

// BattleRepeatAttackResolver.cs:807-812
var attackResolutionContext = new AttackContext
{
    BattleState = battleState,
    SkillId =
        skill_definition?.SkillId ?? new StringName(""),
    EventBatch = batch,
    Action = actionContext,
};
```

`BattleRandomChainSkillService.cs:114-121` 与 `:126-134` 分别把 `actionContext` 传给
repeat/normal path；`:145-153` 再把它传给
`BattleNineEchoFinalHammerResolver.ApplySuccessfulHitReward(...)`。
`BattleNineEchoFinalHammerResolver.cs:95-102` 继续传给 `ApplyTerminalReward(...)`，
最终 `:141-149` 的 `_apply_unit_skill_result(...)` 在 `batch` 后、named
`force_hit_allow_crit` 前传 `actionContext`。因此 terminal hit 仍属于原 random-chain
logical action，而不是第十个独立 action。

`ResolveUnitSkillEffectResult(...)` 当前只被两个 regression 直接调用且传 null batch；P1A
删除旧 convenience 形状，改为上面的 required `batch/actionContext`。两个 fixture 自己打开
root + logical scope，不提供 production fallback。

#### 8.2.2 ground 与 charge 调用链的逐方法改签

普通 ground、pending ground 与 AutoCast ground 都在已经得到 `barrierClip` 后、调用
`ApplyGroundUnitEffectsResultTyped(...)` 前创建一次 logical scope，并保持到 terrain result、
汇总日志和方法返回前：

```csharp
using BattleLogicalAttackScope logicalAttack =
    BeginLogicalAttackForEffects(
        activeUnit,
        barrierClip.UnitEffectDefinitions
    );
BattleGroundUnitEffectsResult unitResult =
    Runtime.ApplyGroundUnitEffectsResultTyped(
        activeUnit,
        skillDefinition,
        castVariantDefinition,
        barrierClip.UnitEffectDefinitions,
        barrierClip.UnitEffectCoords,
        batch,
        logicalAttack.Context,
        targetCoords,
        barrierClip.VisibleEffectCoords
    );

// 保留当前 terrainResult、applied 与 log 代码。
logicalAttack.Complete();
return applied;
```

三处变量名按当前方法机械替换：command 使用 `active_unit`，pending 使用 `activeUnit`，
AutoCast 使用 `caster`；三处 target coords 分别使用当前的 `targetCoords`。目标 gateway 与
service 签名固定为：

```csharp
// BattleRuntimeModule.RuntimeEffects.cs
internal BattleGroundUnitEffectsResult ApplyGroundUnitEffectsResultTyped(
    BattleUnitState source_unit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    IReadOnlyList<Vector2I> effect_coords,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    IReadOnlyList<Vector2I> target_coords = null,
    IReadOnlyList<Vector2I> contingency_effect_coords = null
);

// BattleGroundEffectService.cs
internal BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
    BattleUnitState sourceUnit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    IReadOnlyList<Vector2I> effectCoords,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext,
    IReadOnlyList<Vector2I> targetCoords,
    IReadOnlyList<Vector2I> contingencyEffectCoords = null
);

internal AttackEffectResolutionResult ResolveGroundUnitEffectResult(
    BattleUnitState sourceUnit,
    BattleUnitState targetUnit,
    SkillDefinition skillDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext
);

private GroundUnitEffectResolution _resolve_ground_unit_effect_resolution(
    BattleUnitState sourceUnit,
    BattleUnitState targetUnit,
    SkillDefinition skillDefinition,
    IReadOnlyList<CombatEffectDefinition> effectDefinitions,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext
);
```

`_apply_ground_unit_effects_result(...)` 在进入 target loop 前校验 context；`:666-672` 调
`_resolve_ground_unit_effect_resolution(...)` 时把 context 放在 batch 后。该 private method
的 `AttackContext` initializer 固定为：

```csharp
new AttackContext
{
    BattleState = State,
    SkillId = skillDefinition?.SkillId ?? Empty,
    EventBatch = batch,
    Action = actionContext,
}
```

charge 不在 orchestrator 外层开 scope，因为只有
`BattleChargeResolver` 能看到 `path_step_aoe` 转成的真正 `stageEffect`。当前
`handle_charge_skill_command_result(...)` 在 direction/distance guard 后改为薄 wrapper：

```csharp
CombatEffectDefinition pathStepAoeEffect =
    GetChargePathStepAoeEffectDefinition(
        castVariantDefinition,
        skillDefinition,
        active_unit
    );
CombatEffectDefinition stageEffect =
    pathStepAoeEffect?.WithEffectType(DamageEffectType);
IReadOnlyList<CombatEffectDefinition> attackEffects =
    stageEffect != null
        ? new[] { stageEffect }
        : Array.Empty<CombatEffectDefinition>();
BattleAttackDeliveryKind deliveryKind =
    pathStepAoeEffect?.ResolveAsWeaponAttack == true
        ? BattleAttackDeliveryRules.Resolve(
            attackEffects,
            active_unit.GetWeaponProjectionReadViewTyped()
        )
        : BattleAttackDeliveryKind.NonWeapon;
using BattleLogicalAttackScope logicalAttack =
    Runtime.BeginLogicalAttack(deliveryKind);
bool applied = ExecuteChargeIntoRootBatch(
    active_unit,
    skillDefinition,
    castVariantDefinition,
    validation,
    batch,
    logicalAttack.Context
);
logicalAttack.Complete();
return applied;
```

原 `BattleChargeResolver.cs:80-240` 主体机械移动为：

```csharp
private bool ExecuteChargeIntoRootBatch(
    BattleUnitState activeUnit,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    BattleGroundSkillValidationResult validation,
    BattleEventBatch batch,
    BattleAttackActionContext actionContext
)
```

该主体删除 `var chargeBatch = new BattleEventBatch()` 与 `MergeBatch(batch, chargeBatch)`，
并把其余所有 `chargeBatch` token 改成 `batch`。`ApplyChargePathStepAoeEffects(...)` 新增
最后一个 required `BattleAttackActionContext actionContext` 参数，`:984-989` 的 initializer
新增 `Action = actionContext`。原 private `MergeBatch(...)` 在全仓零调用后删除。

这里即使 charge 没有 weapon path-step，也会得到 `NonWeapon` logical action；这是为了让
一个 charge 的 movement/barrier/contingency graph 始终由一个 scope 包住。只有
`ResolveAsWeaponAttack == true` 的分支调用 `ResolveAttackEffects(...)` 并发布 fact。
`Unknown` 仍会在 scope 创建时 fail fast，不能把缺失武器投影降级为 `NonWeapon`。

五个 resolver call 的 `AttackContext` initializer 都必须新增：

```csharp
Action = actionContext,
```

`EventBatch` 继续是同一 root batch。source weapon projection 在 logical scope 创建前只读取一次；
同一 action 中即使同步装备 reaction 改变装备，context 中已有 delivery 不重算。

### 8.3 捕获阶段

`BattleCounterattackSystem.OnAttackResolved(...)` 同步执行：

1. 验证 action id 与 origin。
2. 若 `Origin.CanTriggerReactions == false`，立即返回。
3. 将 delivery + hit result 映射为 trigger；无 trigger 则返回。
4. 从 defender 的 capability owner 选择当前时刻第一个匹配实例。
5. 生成 key：

   ```text
   (ActionId, AttackerUnitId, DefenderUnitId)
   ```

6. 只有找到 capability 后才 reserve key 并入队。
7. queue entry 冻结 capability instance id、chance、attack bonus 与攻击 fact。

dedupe key 不重复存 `RootBoundaryId`：action id allocator 在同一 battle 内单调且不跨 root
复用，而 `_dedupe` 在每个 root 正常完成或异常 abort 时都清空。`RootBoundaryId` 仍保留在
fact 中，专门用于 sink 的 active-root contract 校验；把它再加入 key 不会改变等价关系，
反而会混淆“身份校验”和“玩法去重”两个 owner。

queue key、entry 与 sink 方法的目标代码为：

```csharp
// scripts/systems/battle/core/BattleCounterattackContracts.cs
internal readonly record struct BattleCounterattackDedupeKey(
    BattleAttackActionId ActionId,
    StringName AttackerUnitId,
    StringName DefenderUnitId
);

internal readonly record struct BattleCounterattackQueueEntry(
    BattleAttackResolutionFact Fact,
    BattleCounterattackCapability Capability
);

// scripts/systems/battle/runtime/BattleCounterattackSystem.cs
internal sealed class BattleCounterattackSystem
    : IBattleAttackResolutionSink,
        IBattleReactionDrainOwner
{
    private readonly BattleRuntimeModule _runtime;
    private readonly BattleAttackActionCoordinator
        _attackActionCoordinator;
    private readonly BattleEffectExecutionContextService
        _effectExecutionContext;
    private readonly BattleCounterattackQueryService _queryService;
    private readonly BattleImmediateWeaponAttackService
        _immediateWeaponAttackService;
    private readonly IBattleCounterattackChanceRoller _chanceRoller;
    private readonly Queue<BattleCounterattackQueueEntry> _queue =
        new();
    private readonly HashSet<BattleCounterattackDedupeKey> _dedupe =
        new();
    private bool _isDraining;
    private bool _disposed;

    internal BattleCounterattackSystem(
        BattleRuntimeModule runtime,
        BattleAttackActionCoordinator attackActionCoordinator,
        BattleEffectExecutionContextService effectExecutionContext,
        BattleCounterattackQueryService queryService,
        BattleImmediateWeaponAttackService immediateWeaponAttackService,
        IBattleCounterattackChanceRoller chanceRoller
    )
    {
        _runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        _attackActionCoordinator = attackActionCoordinator
            ?? throw new ArgumentNullException(
                nameof(attackActionCoordinator)
            );
        _effectExecutionContext = effectExecutionContext
            ?? throw new ArgumentNullException(
                nameof(effectExecutionContext)
            );
        _queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        _immediateWeaponAttackService = immediateWeaponAttackService
            ?? throw new ArgumentNullException(
                nameof(immediateWeaponAttackService)
            );
        _chanceRoller = chanceRoller
            ?? throw new ArgumentNullException(nameof(chanceRoller));
    }

    internal void DisposeRuntime()
    {
        if (_disposed)
            return;
        AbortBoundaryCore();
        _disposed = true;
    }

    private void RequireUsable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BattleCounterattackSystem));
    }

void IBattleAttackResolutionSink.OnAttackResolved(
    in BattleAttackResolutionFact fact,
    BattleEventBatch batch
)
{
    RequireUsable();
    _attackActionCoordinator.RequireActiveRootBatch(batch);
    _attackActionCoordinator.RequireActiveLogicalAttack(
        fact.ActionId,
        fact.RootBoundaryId,
        fact.Origin,
        fact.DeliveryKind
    );
    _attackActionCoordinator.ConsumeWorkItem(
        BattleReactionWorkItemKind.AttackFact
    );
    if (
        !fact.ActionId.IsValid
        || fact.RootBoundaryId <= 0
        || fact.Origin == null
        || fact.AttackerUnitId == new StringName("")
        || fact.DefenderUnitId == new StringName("")
    )
    {
        throw new InvalidOperationException("invalid attack resolution fact");
    }
    if (!fact.Origin.CanTriggerReactions)
        return;
    if (
        !BattleCounterattackRules.TryMapTrigger(
            fact,
            out BattleCounterattackTriggerKind triggerKind
        )
    )
        return;

    BattleState state = _runtime.GetState()
        ?? throw new InvalidOperationException("battle state is not bound");
    if (
        !state.TryGetUnitTyped(
            fact.DefenderUnitId,
            out BattleUnitState defender
        )
        || defender == null
    )
    {
        return;
    }
    IReadOnlyList<BattleCounterattackCapability> candidates =
        defender.GetCounterattackCandidatesTyped(triggerKind);
    if (candidates.Count == 0)
        return;

    BattleCounterattackCapability capability = candidates[0];
    var key = new BattleCounterattackDedupeKey(
        fact.ActionId,
        fact.AttackerUnitId,
        fact.DefenderUnitId
    );
    if (!_dedupe.Add(key))
        return;
    _queue.Enqueue(new BattleCounterattackQueueEntry(fact, capability));
}
}
```

为便于按语义审阅，`IBattleReactionDrainOwner.Drain(...)` /
`IBattleReactionDrainOwner.AbortBoundary()` 及其 private core 在 §8.4 展开、
`TryExecute(...)` 在 §9.5 展开；实施时这些 member 都放在上面
`BattleCounterattackSystem` 最后一个 `}` 之前，不另建 partial 或第二份 queue owner。

`BattleUnitState.GetCounterattackCandidatesTyped(...)` 只是 capability owner 的只读 gateway，直接返回 §7.1 已按 priority/id 排序的 detached immutable list；它不创建 missing owner，也不在读取时排序。

捕获 capability 的原因：

- 后续同一原动作新授予的能力不能追溯响应更早的攻击。
- 排空前能力若被移除，仍通过 instance presence 复核而失效。
- 多 stage 的第一个有效匹配事实占用本动作唯一机会，后续 stage 不重试。

### 8.4 FIFO 与 nested enqueue

- queue order 就是攻击事实的实际发布顺序。
- 不在排空时按 unit id 二次排序；那会改变已经发生的因果顺序和共享 RNG 顺序。
- 所有 target producer 在调用 resolver 前必须已有确定顺序。若 producer 来自 dictionary/hash set，应在 producer 层先 canonical sort。
- 排空使用 `while (queue.Count > 0)`，而不是只处理进入函数时的初始 count。
- 排空期间由 contingency / equipment follow-up 产生的新反击 entry 追加到队尾，并在同一最外层 boundary 内处理。
- `_isDraining` 防止 nested logical action 完成时递归调用第二个 drain loop。
- 直接反击 origin 的 `CanTriggerReactions=false`，因此反击攻击本身不会入队；它触发的其他正式系统仍按各自规则运行。

drain 与异常清理的目标代码为：

```csharp
void IBattleReactionDrainOwner.Drain(BattleEventBatch batch) =>
    DrainCore(batch);

private void DrainCore(BattleEventBatch batch)
{
    RequireUsable();
    _attackActionCoordinator.RequireActiveRootBatch(batch);
    if (_isDraining)
        return;

    _isDraining = true;
    bool completed = false;
    try
    {
        while (_queue.Count > 0)
        {
            _attackActionCoordinator.ConsumeWorkItem(
                BattleReactionWorkItemKind.CounterattackDequeue
            );
            BattleCounterattackQueueEntry entry = _queue.Dequeue();
            TryExecute(entry, batch);
        }
        completed = true;
    }
    finally
    {
        _isDraining = false;
        if (completed)
        {
            if (_queue.Count != 0)
                throw new InvalidOperationException("reaction queue did not drain");
            _dedupe.Clear();
        }
    }
}

void IBattleReactionDrainOwner.AbortBoundary() =>
    AbortBoundaryCore();

private void AbortBoundaryCore()
{
    _queue.Clear();
    _dedupe.Clear();
    _isDraining = false;
}
```

最外层 `BattleReactionBoundaryScope.Complete()` 调用 `Drain(rootBatch)`；若 `TryExecute(...)` 抛错，`completed=false`，随后 scope 的未完成 dispose 必须调用 `AbortBoundary()`。不能在 `finally` 无条件清理后吞异常，因为 coordinator 还需要把该 root 标记为失败并恢复 action/origin depth。

damage hook 的现有同步递归顺序必须写入正式时序，而不是由实现者猜测：

```text
outer ResolveAttackEffects
  -> ApplyDamageToTargetResult
     -> BeforeDamageResolved
        -> ExecuteAutoCast(same root batch)
           -> nested logical attack
           -> nested resolver publishes nested fact
           -> nested fact enters counter queue
  -> outer damage commit / resolver tail
  -> outer resolver publishes outer fact
  -> outer fact enters counter queue
```

因此 nested AutoCast fact 先于 outer fact 入队是正确的“实际发布时间顺序”，不是需要按 outer-first 重排的异常。两者都只入队；直到最外层 command/timeline/battle-confirm producer 完成全部后处理后才开始 drain，所以 nested counterattack 也不会在 outer damage resolver 中途执行。

为避免错误接线或内容循环把同步调用变成无限递归，coordinator 同时拥有非玩法性的技术熔断：

```csharp
internal enum BattleReactionWorkItemKind
{
    LogicalAttack = 0,
    AttackFact,
    CounterattackDequeue
}

internal interface IBattleReactionDrainOwner
{
    void Drain(BattleEventBatch batch);
    void AbortBoundary();
}

internal readonly record struct BattleReactionBoundaryLimits(
    int MaxNestedBoundaryDepth,
    int MaxWorkItems
);

internal static class BattleReactionBoundarySafetyRules
{
    internal static BattleReactionBoundaryLimits Production =>
        new(MaxNestedBoundaryDepth: 64, MaxWorkItems: 4096);
}
```

- 每次进入 nested boundary 前检查 depth。
- 每次开始 logical attack、接收 attack fact、dequeue counterattack entry 各消费一个 work item。
- production coordinator 构造时固定使用 `BattleReactionBoundarySafetyRules.Production`，不从技能、装备、属性或 `.tres` 配置；internal regression constructor 可注入更小的 validated limits 以验证失败路径。
- 两个 limit 都必须大于 0；非法 override 在 coordinator setup 时拒绝，不能静默 clamp。
- 超限在执行下一个 work item 前抛出带 root boundary id、depth 与各计数的确定性异常；该 boundary 不 `Complete()`，按 §8.6 清 queue/dedupe/action stack，不把工作泄漏到下一入口。
- 该 guard 只是最后保险，不能代替 origin gate、charge 消耗、contingency source-event 去重与一次性 setup 等正式终止规则。

### 8.5 排空位置

排空必须发生在原始 producer 完成全部正式后处理之后，包括：

- 所有目标与 stage。
- 状态 turn timing 初始化。
- 特殊效果与位移。
- 装备 after-hit / hit-received 的同步链。
- durability projection。
- defeated handling / kill provenance。
- contingency HP/status hooks。
- contribution、rating 与 metrics。
- producer-specific outcome commit。

但排空仍发生在最外层 command/timeline entry 返回之前，继续使用同一个 `BattleEventBatch`。这保证原动作是完整事务序列，反击又仍属于同一次用户可见结算。

### 8.6 异常和生命周期

- scope 使用显式 `Complete()` 标记正常结束；`Dispose()` 只负责恢复 stack/depth。
- 最外层 boundary 未 `Complete()` 就退出时，清空本 boundary 的 queue 与 dedupe set，不执行反击。
- 已经提交的 gameplay mutation 不回滚；异常继续向上抛，battle fixture/运行时按现有失败策略处理。
- depth/work guard 超限走同一未完成 boundary 路径；不能吞掉异常、截断队列后仍把 entry point 当成功返回。
- 正常 boundary 完成后必须断言 queue、dedupe set、active action stack 全部为空；effect origin stack 必须恢复到 boundary 进入时的 depth，而不是假设全局为空。
- `ClearBattleState()`、`DisposeRuntime()` 与新 battle setup 都重置 allocator、origin stack、queue、dedupe 和 scope depth。
- damage resolver sink 在 dispose/rebind 时显式解绑，不能保留旧 runtime sidecar。

---

## 九、资格查询、纯规则与 attempt 提交

### 9.1 三层拆分

```text
BattleCounterattackQueryService.Build(entry)
  -> 调正式 state/range/barrier/immediate-attack query
  -> 只产出 facts

BattleCounterattackRules.Evaluate(context)
  -> 纯函数
  -> 产出 eligibility + typed block reason

BattleCounterattackSystem.TryExecute(entry)
  -> 原子尝试成本
  -> RNG
  -> immediate weapon attack
```

rules 不直接依赖 `BattleBarrierService`、`BattleRangeService` 或 runtime port。

### 9.2 query DTO

```csharp
internal readonly record struct BattleCounterattackEvaluationContext(
    BattleAttackResolutionFact Fact,
    BattleCounterattackCapability Capability,
    bool CapabilityStillPresent,
    bool DefenderPresent,
    bool DefenderAliveAndHpPositive,
    bool AttackerPresent,
    bool AttackerAliveAndHpPositive,
    bool HostilePair,
    bool CounterattackLocked,
    bool HardControlled,
    bool HasReactionCharge,
    BattleImmediateWeaponAttackAvailability AttackAvailability
);

internal sealed class BattleCounterattackQueryResult
{
    internal BattleCounterattackQueryResult(
        BattleCounterattackEvaluationContext evaluation,
        BattleImmediateWeaponAttackPlan plan
    )
    {
        Evaluation = evaluation;
        Plan = plan;
    }

    internal BattleCounterattackEvaluationContext Evaluation { get; }
    internal BattleImmediateWeaponAttackPlan Plan { get; }

    internal BattleImmediateWeaponAttackPlan RequireExecutablePlan()
    {
        if (Plan == null || !Plan.DefinitionAvailable)
        {
            throw new InvalidOperationException(
                "allowed counterattack must carry an executable plan"
            );
        }
        return Plan;
    }
}
```

`Plan` 只允许在 attacker/defender 已从当前 state 解析成功时存在；更早的 missing-unit failure 令它为 null，但 rules 会先在步骤 3/4 拒绝。`BattleCounterattackRules.Evaluate(...)` 只接收 `queryResult.Evaluation`，不接触 plan/live unit；allowed 后 `BattleCounterattackSystem` 只调用 `RequireExecutablePlan()` 取得同一个 plan，attempt cost 和 chance 成功后直接传给 `Execute(...)`。不得在 `Evaluate(...)` 后第二次调用 `PrepareCounterattack(...)`。

`BattleImmediateWeaponAttackAvailability` 不用一组会混淆“未检查”和“失败”的 bool；精确 DTO 为：

```csharp
// scripts/systems/battle/core/BattleCounterattackContracts.cs
internal enum BattleImmediateWeaponAttackBlockReason
{
    None = 0,
    AttackUnavailable,
    OutOfReach,
    BarrierBlocked,
    InsufficientStamina
}

internal enum BattleImmediateWeaponAttackCheckState
{
    NotEvaluated = 0,
    Passed,
    Failed
}

internal readonly record struct BattleImmediateWeaponAttackAvailability(
    bool IsAllowed,
    BattleImmediateWeaponAttackBlockReason Reason,
    BattleImmediateWeaponAttackCheckState DefinitionCheck,
    BattleImmediateWeaponAttackCheckState RangeCheck,
    BattleImmediateWeaponAttackCheckState BarrierCheck,
    BattleImmediateWeaponAttackCheckState StaminaCheck,
    int EffectiveRange,
    int CurrentDistance,
    int StaminaCost,
    int CurrentStamina
)
{
    internal static BattleImmediateWeaponAttackAvailability Blocked(
        BattleImmediateWeaponAttackBlockReason reason,
        int effectiveRange = -1,
        int currentDistance = -1,
        int staminaCost = -1,
        int currentStamina = -1
    ) => reason switch
    {
        BattleImmediateWeaponAttackBlockReason.AttackUnavailable =>
            new(
                false, reason,
                BattleImmediateWeaponAttackCheckState.Failed,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                effectiveRange, currentDistance, staminaCost, currentStamina
            ),
        BattleImmediateWeaponAttackBlockReason.OutOfReach =>
            new(
                false, reason,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Failed,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                effectiveRange, currentDistance, staminaCost, currentStamina
            ),
        BattleImmediateWeaponAttackBlockReason.BarrierBlocked =>
            new(
                false, reason,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Failed,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                effectiveRange, currentDistance, staminaCost, currentStamina
            ),
        BattleImmediateWeaponAttackBlockReason.InsufficientStamina =>
            new(
                false, reason,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Failed,
                effectiveRange, currentDistance, staminaCost, currentStamina
            ),
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    internal static BattleImmediateWeaponAttackAvailability Allowed(
        int effectiveRange,
        int currentDistance,
        int staminaCost,
        int currentStamina
    ) => new(
        true,
        BattleImmediateWeaponAttackBlockReason.None,
        BattleImmediateWeaponAttackCheckState.Passed,
        BattleImmediateWeaponAttackCheckState.Passed,
        BattleImmediateWeaponAttackCheckState.Passed,
        BattleImmediateWeaponAttackCheckState.Passed,
        effectiveRange,
        currentDistance,
        staminaCost,
        currentStamina
    );
}
```

上述 block reason、check state 与 availability 都属于 detached domain contract，落在
`BattleCounterattackContracts.cs`。`BattleImmediateWeaponAttackService` 和
`BattleCounterattackQueryService` 只生产/消费它们，不拥有这些类型。这样位于
`scripts/systems/battle/rules/BattleReportFormatter.cs` 的 formatter 只依赖 core
contract，不会反向引用 `scripts/systems/battle/runtime/` 中的 service-private 类型。

P1A availability 不携带 preview damage range；该字段只在 P1B 的 detached preview DTO 中加入，不能迫使运行时 query 为了 UI 去执行伤害 RNG。

`BattleCounterattackQueryService.Build(...)` 的单位角色不能写反：原 fact defender 是反击 source，原 fact attacker 是反击 target。目标方法体为：

```csharp
internal sealed class BattleCounterattackQueryService
    : BattleRuntimeModuleBorrower
{
    private readonly BattleImmediateWeaponAttackService
        _immediateWeaponAttackService;

    internal BattleCounterattackQueryService(
        BattleImmediateWeaponAttackService immediateWeaponAttackService
    )
    {
        _immediateWeaponAttackService =
            immediateWeaponAttackService
            ?? throw new ArgumentNullException(
                nameof(immediateWeaponAttackService)
            );
    }

internal BattleCounterattackQueryResult Build(
    in BattleCounterattackQueueEntry entry
)
{
    BattleState state = _runtime.GetState();
    BattleUnitState defender = null;
    BattleUnitState attacker = null;
    bool defenderPresent =
        state != null
        && state.TryGetUnitTyped(
            entry.Fact.DefenderUnitId,
            out defender
        )
        && defender != null;
    bool attackerPresent =
        state != null
        && state.TryGetUnitTyped(
            entry.Fact.AttackerUnitId,
            out attacker
        )
        && attacker != null;

    bool capabilityStillPresent =
        defenderPresent
        && defender.TryGetCounterattackCapabilityTyped(
            entry.Capability.InstanceId,
            out BattleCounterattackCapability currentCapability
        )
        && currentCapability.Equals(entry.Capability);
    BattleCounterattackActorPairFacts actorPair =
        BuildActorPairFacts(defender, attacker);

    BattleImmediateWeaponAttackPlan plan = null;
    BattleImmediateWeaponAttackAvailability availability =
        BattleImmediateWeaponAttackAvailability.Blocked(
            BattleImmediateWeaponAttackBlockReason.AttackUnavailable
        );
    if (defenderPresent && attackerPresent)
    {
        plan = _immediateWeaponAttackService.PrepareCounterattack(
            new BattleCounterattackImmediateWeaponAttackRequest(
                state,
                defender,
                attacker,
                entry.Capability
            )
        );
        availability = _immediateWeaponAttackService.Query(plan);
    }
    BattleCounterattackAttemptReadinessFacts readiness =
        BuildAttemptReadinessFacts(defender, availability);

    return new BattleCounterattackQueryResult(
        new BattleCounterattackEvaluationContext(
            entry.Fact,
            entry.Capability,
            capabilityStillPresent,
            actorPair.DefenderPresent,
            actorPair.DefenderAliveAndHpPositive,
            actorPair.AttackerPresent,
            actorPair.AttackerAliveAndHpPositive,
            actorPair.HostilePair,
            readiness.CounterattackLocked,
            readiness.HardControlled,
            readiness.HasReactionCharge,
            readiness.AttackAvailability
        ),
        plan
    );
}

internal BattleCounterattackActorPairFacts BuildActorPairFacts(
    BattleUnitState defender,
    BattleUnitState attacker
)
{
    bool defenderPresent = defender != null;
    bool attackerPresent = attacker != null;
    return new BattleCounterattackActorPairFacts(
        defenderPresent,
        defenderPresent
            && defender.IsAlive()
            && defender.GetCurrentHp() > 0,
        attackerPresent,
        attackerPresent
            && attacker.IsAlive()
            && attacker.GetCurrentHp() > 0,
        defenderPresent
            && attackerPresent
            && defender.unit_id != attacker.unit_id
            && defender.faction_id != new StringName("")
            && attacker.faction_id != new StringName("")
            && defender.faction_id != attacker.faction_id
    );
}

internal BattleCounterattackAttemptReadinessFacts
    BuildAttemptReadinessFacts(
        BattleUnitState defender,
        in BattleImmediateWeaponAttackAvailability availability
    )
{
    bool defenderPresent = defender != null;
    bool hardControlled =
        defenderPresent
        && (
            BattleStatusSemanticTable.IsHardControlled(defender)
            || (
                defender.GetStatusEffect(
                    BattleStatusSemanticTable.STATUS_TIME_STASIS
                ) is
                    BattleStatusEffectState stasis
                && stasis.stacks > 0
            )
        );
    return new BattleCounterattackAttemptReadinessFacts(
        defenderPresent
            && _runtime.IsUnitCounterattackLocked(defender),
        hardControlled,
        defenderPresent && defender.HasReactionChargeTyped(),
        availability
    );
}
}
```

query 只比较完整 record equality，因此同 instance id 但
chance/bonus/trigger/priority/weapon-action-definition 任一值变化都会得到
`CapabilityStillPresent=false`。

`BuildActorPairFacts(...)` 与 `BuildAttemptReadinessFacts(...)` 既由 execution
query 调用，也由 P1B preview 调用；不得在 preview service 中重新读取
`lock_counterattack`、硬控或 faction 并复制判断。query、preview 和执行必须通过同一个
`BattleImmediateWeaponAttackService`，不能各自复制射程、屏障、成本或武器可用性。

### 9.3 fail-closed 顺序

只有已经捕获 capability 的 entry 才进入以下链：

| # | 判定 | block reason |
|---|---|---|
| 1 | action/origin/fact 仍合法 | `invalid_fact` |
| 2 | capability instance 仍存在且值未替换 | `capability_gone` |
| 3 | defender 存在、alive 且 HP > 0 | `defender_down` |
| 4 | attacker 存在、alive 且 HP > 0 | `attacker_gone` |
| 5 | 双方不同且敌对 | `not_hostile` |
| 6 | trigger 与冻结 delivery/result 一致 | `trigger_mismatch` |
| 7 | 未被 `lock_counterattack` 封锁 | `counterattack_locked` |
| 8 | 无 stunned/paralyzed/time_stasis 等硬控 | `hard_controlled` |
| 9 | 共享 reaction charge > 0 | `no_reaction_charge` |
| 10 | immediate weapon attack definition 可用 | `attack_unavailable` |
| 11 | 当前射程合法 | `out_of_reach` |
| 12 | 当前屏障允许 | `barrier_blocked` |
| 13 | stamina 足够 | `insufficient_stamina` |

pure rules 的 typed result 与方法体固定为：

```csharp
internal enum BattleCounterattackBlockReason
{
    None = 0,
    InvalidFact,
    CapabilityGone,
    DefenderDown,
    AttackerGone,
    NotHostile,
    TriggerMismatch,
    CounterattackLocked,
    HardControlled,
    NoReactionCharge,
    AttackUnavailable,
    OutOfReach,
    BarrierBlocked,
    InsufficientStamina
}

internal readonly record struct BattleCounterattackEligibility(
    bool IsAllowed,
    BattleCounterattackBlockReason Reason
)
{
    internal static BattleCounterattackEligibility Allowed =>
        new(true, BattleCounterattackBlockReason.None);

    internal static BattleCounterattackEligibility Blocked(
        BattleCounterattackBlockReason reason
    ) => new(false, reason);
}

internal readonly record struct BattleCounterattackActorPairFacts(
    bool DefenderPresent,
    bool DefenderAliveAndHpPositive,
    bool AttackerPresent,
    bool AttackerAliveAndHpPositive,
    bool HostilePair
);

internal readonly record struct BattleCounterattackAttemptReadinessFacts(
    bool CounterattackLocked,
    bool HardControlled,
    bool HasReactionCharge,
    BattleImmediateWeaponAttackAvailability AttackAvailability
);

internal static class BattleCounterattackRules
{
    internal static bool TryMapTrigger(
        in BattleAttackResolutionFact fact,
        out BattleCounterattackTriggerKind triggerKind
    )
    {
        if (
            !fact.IncludesWeaponDamage
            || fact.DeliveryKind != BattleAttackDeliveryKind.MeleeWeapon
        )
        {
            triggerKind = default;
            return false;
        }
        triggerKind = fact.AttackSucceeded
            ? BattleCounterattackTriggerKind.MeleeHitReceived
            : BattleCounterattackTriggerKind.MeleeAttackEvaded;
        return true;
    }

    internal static BattleCounterattackEligibility Evaluate(
        in BattleCounterattackEvaluationContext context
    )
    {
        BattleAttackResolutionFact fact = context.Fact;
        if (
            !fact.ActionId.IsValid
            || fact.RootBoundaryId <= 0
            || fact.Origin == null
            || !fact.Origin.CanTriggerReactions
            || fact.AttackerUnitId == new StringName("")
            || fact.DefenderUnitId == new StringName("")
        )
        {
            return Blocked(BattleCounterattackBlockReason.InvalidFact);
        }
        if (!context.CapabilityStillPresent)
            return Blocked(BattleCounterattackBlockReason.CapabilityGone);
        BattleCounterattackEligibility actorPair =
            EvaluateActorPair(
                new BattleCounterattackActorPairFacts(
                    context.DefenderPresent,
                    context.DefenderAliveAndHpPositive,
                    context.AttackerPresent,
                    context.AttackerAliveAndHpPositive,
                    context.HostilePair
                )
            );
        if (!actorPair.IsAllowed)
            return actorPair;
        if (
            !TryMapTrigger(
                fact,
                out BattleCounterattackTriggerKind triggerKind
            )
            || triggerKind != context.Capability.TriggerKind
        )
        {
            return Blocked(BattleCounterattackBlockReason.TriggerMismatch);
        }
        return EvaluateAttemptReadiness(
            new BattleCounterattackAttemptReadinessFacts(
                context.CounterattackLocked,
                context.HardControlled,
                context.HasReactionCharge,
                context.AttackAvailability
            )
        );
    }

    internal static BattleCounterattackEligibility EvaluateActorPair(
        in BattleCounterattackActorPairFacts facts
    )
    {
        if (!facts.DefenderPresent || !facts.DefenderAliveAndHpPositive)
            return Blocked(BattleCounterattackBlockReason.DefenderDown);
        if (!facts.AttackerPresent || !facts.AttackerAliveAndHpPositive)
            return Blocked(BattleCounterattackBlockReason.AttackerGone);
        if (!facts.HostilePair)
            return Blocked(BattleCounterattackBlockReason.NotHostile);
        return BattleCounterattackEligibility.Allowed;
    }

    internal static BattleCounterattackEligibility
        EvaluateAttemptReadiness(
            in BattleCounterattackAttemptReadinessFacts facts
        )
    {
        if (facts.CounterattackLocked)
            return Blocked(BattleCounterattackBlockReason.CounterattackLocked);
        if (facts.HardControlled)
            return Blocked(BattleCounterattackBlockReason.HardControlled);
        if (!facts.HasReactionCharge)
            return Blocked(BattleCounterattackBlockReason.NoReactionCharge);

        return facts.AttackAvailability.Reason switch
        {
            BattleImmediateWeaponAttackBlockReason.None
                when facts.AttackAvailability.IsAllowed =>
                    BattleCounterattackEligibility.Allowed,
            BattleImmediateWeaponAttackBlockReason.AttackUnavailable =>
                Blocked(BattleCounterattackBlockReason.AttackUnavailable),
            BattleImmediateWeaponAttackBlockReason.OutOfReach =>
                Blocked(BattleCounterattackBlockReason.OutOfReach),
            BattleImmediateWeaponAttackBlockReason.BarrierBlocked =>
                Blocked(BattleCounterattackBlockReason.BarrierBlocked),
            BattleImmediateWeaponAttackBlockReason.InsufficientStamina =>
                Blocked(BattleCounterattackBlockReason.InsufficientStamina),
            _ => Blocked(BattleCounterattackBlockReason.AttackUnavailable),
        };
    }

    private static BattleCounterattackEligibility Blocked(
        BattleCounterattackBlockReason reason
    ) => BattleCounterattackEligibility.Blocked(reason);
}
```

上面 `BattleCounterattackBlockReason`、`BattleCounterattackEligibility`、
`BattleCounterattackActorPairFacts` 与
`BattleCounterattackAttemptReadinessFacts` 放在
`scripts/systems/battle/core/BattleCounterattackContracts.cs`；
`BattleCounterattackRules` class 才放在
`scripts/systems/battle/rules/BattleCounterattackRules.cs`。这样
`BattlePreview`/preview contracts 不反向依赖 runtime service 类型。

`EvaluateActorPair(...)` 与 `EvaluateAttemptReadiness(...)` 是 execution/preview
共享的唯二 actor-readiness 规则入口。execution 仍保持
fact → capability → actor pair → trigger → attempt readiness 的 fail-closed
顺序；P1B 只复用后两段，不构造伪造的 action id、root id 或 attack fact。

概率不属于 eligibility；它在 attempt 成本提交后由执行层处理。

日志规则：

- 没有匹配 capability 时完全不入队、不输出“被锁反击”等日志。
- 已捕获 capability 后被 lock、硬控、射程或资源阻止，可以产生结构化 report；UI 文案由 formatter 决定。
- preview 返回 block reason 供诊断，但不把敌方隐藏信息直接展示给玩家。

### 9.4 attempt 成本的原子性

`BattleCounterattackSystem` 通过 `BattleUnitState` 的窄 typed aggregate gateway 提交：

```csharp
internal bool TryCommitCounterattackAttemptCostTyped(
    int staminaCost,
    BattleEventBatch batch
);
```

这个 gateway 不判断资格或概率，只原子协调 `BattleUnitReactionState` 与 `BattleUnitCombatResourceState`。`batch` 非空且必须是 caller 当前 root batch；gateway 在同一个无 yield 的同步调用中：

1. 再检查 charge 与 stamina。
2. 两者都足够才通过两个 owner 的 non-failing commit primitive 同时写入。
3. 任一不足则两者都不写。
4. 两次写入都成功后只调用一次 `batch.AddChangedUnitId(unit_id)`；失败时不写 dirty fact。

两个 primitive 在 P1A 中明确新增为 `BattleUnitReactionState.CommitConsumeChargeKnownAvailable()` 与 `BattleUnitCombatResourceState.CommitSpendStaminaKnownAvailable(int)`：它们只接受 aggregate 已验证的值，内部只有 checked/plain value assignment，不调用外部 service、sink、signal、日志或 RNG，也不返回第二次业务失败。aggregate 先解析两个 next value 并完成全部 invariant 检查，再连续写入，最后才发布 changed facts；任何 invariant 错误必须在第一次写入前抛出。

不要让 caller 先 `TryConsumeCharge()`，再调用可能失败的 stamina API。

`BattleUnitState` gateway 的目标方法体为：

```csharp
internal bool TryCommitCounterattackAttemptCostTyped(
    int staminaCost,
    BattleEventBatch batch
)
{
    if (staminaCost < 0)
        throw new ArgumentOutOfRangeException(nameof(staminaCost));
    if (batch == null)
        throw new ArgumentNullException(nameof(batch));
    BattleUnitReactionState reactionState = _reactionState;
    BattleUnitCombatResourceState combatState = _combatResourceState;
    if (
        reactionState == null
        || combatState == null
        || !reactionState.HasCharge()
        || combatState.GetCurrentStamina() < staminaCost
    )
    {
        return false;
    }

    // 两个 Validate 方法只计算/验证 next value，不写状态。
    reactionState.ValidateConsumeChargeKnownAvailable();
    combatState.ValidateSpendStaminaKnownAvailable(staminaCost);
    // 两个 Commit 方法在 validation 后只有 non-throwing assignment。
    reactionState.CommitConsumeChargeKnownAvailable();
    combatState.CommitSpendStaminaKnownAvailable(staminaCost);
    batch.AddChangedUnitId(unit_id);
    return true;
}
```

### 9.5 RNG 顺序

正式执行顺序固定为：

```text
deterministic eligibility
  -> commit charge + stamina
  -> roll capability chance
  -> chance failed: log/report, end
  -> chance passed: build/execute counterattack action
```

- chance failure 是一次已经花费反应机会的尝试。
- chance failure 不构造攻击检定，因此不消费命中/伤害 RNG。
- preview 与 AI query 永远停在 deterministic eligibility。
- 全仓库继续使用正式共享 RNG；不新增反击专用随机流。

为使“恰好调用一次”可以被回归证明，P1A 增加一个只包静态共享流、不拥有 seed/state 的
narrow port：

```csharp
internal interface IBattleCounterattackChanceRoller
{
    int RollInclusive1To100();
}

internal sealed class BattleCounterattackChanceRoller
    : IBattleCounterattackChanceRoller
{
    internal static BattleCounterattackChanceRoller Instance { get; } =
        new();

    private BattleCounterattackChanceRoller() { }

    public int RollInclusive1To100() =>
        TrueRandomSeedService.RandiRange(1, 100);
}
```

production adapter 没有自己的 RNG；它仍消费现有全局正式随机流。internal runtime fixture
constructor 注入 counting roller，不能通过重置全局 seed 猜调用次数。

`BattleCounterattackSystem.TryExecute(...)` 的目标代码为：

```csharp
private void TryExecute(
    in BattleCounterattackQueueEntry entry,
    BattleEventBatch batch
)
{
    RequireUsable();
    using IDisposable originScope =
        _effectExecutionContext.Push(
            BattleEffectOrigin.Counterattack(
                entry.Fact.ActionId,
                entry.Capability.InstanceId
            )
        );
    BattleCounterattackQueryResult query =
        _queryService.Build(entry);
    BattleCounterattackEligibility eligibility =
        BattleCounterattackRules.Evaluate(query.Evaluation);
    if (!eligibility.IsAllowed)
    {
        _runtime._append_report_entry_to_batch(
            batch,
            _runtime._report_formatter.BuildCounterattackBlockedEntry(
                entry,
                eligibility,
                query.Evaluation.AttackAvailability
            )
        );
        return;
    }

    BattleImmediateWeaponAttackPlan plan =
        query.RequireExecutablePlan();
    if (
        !plan.SourceUnit.TryCommitCounterattackAttemptCostTyped(
            plan.StaminaCost,
            batch
        )
    )
    {
        throw new InvalidOperationException(
            "counterattack cost changed after read-only eligibility"
        );
    }

    int chancePercent = entry.Capability.ChancePercent;
    int chanceRoll = 0;
    bool chancePassed;
    if (chancePercent <= 0)
    {
        chancePassed = false;
    }
    else if (chancePercent >= 100)
    {
        chancePassed = true;
    }
    else
    {
        chanceRoll = _chanceRoller.RollInclusive1To100();
        if (chanceRoll < 1 || chanceRoll > 100)
        {
            throw new InvalidOperationException(
                "counterattack chance roller returned out of range"
            );
        }
        chancePassed = chanceRoll <= chancePercent;
    }
    if (!chancePassed)
    {
        _runtime._append_report_entry_to_batch(
            batch,
            _runtime._report_formatter.BuildCounterattackChanceFailedEntry(
                entry,
                chancePercent,
                chanceRoll,
                plan.StaminaCost
            )
        );
        return;
    }

    _immediateWeaponAttackService.Execute(plan, batch);
}
```

Counterattack origin 覆盖本次 attempt 的 query、blocked report、成本提交、chance roll、
chance-failed report 与实际攻击。这样两个失败 report 经
`_append_report_entry_to_batch(...)` attach 的 `effect_origin` 也固定为
`Counterattack`，其 `triggering_attack_action_id` 与 entry fact 的 action id 一致；它们
不得继承排空现场的 `player_command` / `timeline` / `autocast` 父 origin。

`ChancePercent` 的 owner 在安装 capability 时已验证 `[0,100]`；这里的 `<=0/>=100` 仍是防御性闭集端点，并明确不消费共享 RNG。只有 `1..99` 恰好调用一次 `TrueRandomSeedService.RandiRange(1,100)`。`BuildCounterattackBlockedEntry(...)` 与 `BuildCounterattackChanceFailedEntry(...)` 新增到现有 `BattleReportFormatter`；system 不手拼 dictionary key 或日志文本。完整目标代码为：

```csharp
internal System.Collections.Generic.Dictionary<string, object>
    BuildCounterattackBlockedEntry(
        in BattleCounterattackQueueEntry entry,
        in BattleCounterattackEligibility eligibility,
        in BattleImmediateWeaponAttackAvailability availability
    )
{
    if (
        eligibility.IsAllowed
        || eligibility.Reason == BattleCounterattackBlockReason.None
    )
    {
        throw new ArgumentException(
            "blocked counterattack entry requires a block reason",
            nameof(eligibility)
        );
    }
    return BuildCounterattackAttemptEntry(
        entry,
        outcome: "blocked",
        reason: CounterattackBlockReasonName(eligibility.Reason),
        chancePercent: entry.Capability.ChancePercent,
        chanceRoll: 0,
        staminaCost: availability.StaminaCost,
        text: CounterattackBlockText(eligibility.Reason)
    );
}

internal System.Collections.Generic.Dictionary<string, object>
    BuildCounterattackChanceFailedEntry(
        in BattleCounterattackQueueEntry entry,
        int chancePercent,
        int chanceRoll,
        int staminaCost
    )
{
    if (chancePercent < 0 || chancePercent > 100)
        throw new ArgumentOutOfRangeException(nameof(chancePercent));
    if (chanceRoll < 0 || chanceRoll > 100)
        throw new ArgumentOutOfRangeException(nameof(chanceRoll));
    if (staminaCost < 0)
        throw new ArgumentOutOfRangeException(nameof(staminaCost));
    return BuildCounterattackAttemptEntry(
        entry: entry,
        outcome: "chance_failed",
        reason: "chance_failed",
        chancePercent: chancePercent,
        chanceRoll: chanceRoll,
        staminaCost: staminaCost,
        text: "反击尝试未能触发。"
    );
}

private static System.Collections.Generic.Dictionary<string, object>
    BuildCounterattackAttemptEntry(
        in BattleCounterattackQueueEntry entry,
        string outcome,
        string reason,
        int chancePercent,
        int chanceRoll,
        int staminaCost,
        string text
    )
{
    if (!entry.Fact.ActionId.IsValid)
        throw new ArgumentException("counterattack fact action id is invalid");
    if (entry.Capability.InstanceId == new StringName(""))
        throw new ArgumentException("counterattack capability id is empty");
    return new System.Collections.Generic.Dictionary<string, object>(
        System.StringComparer.Ordinal
    )
    {
        ["entry_kind"] = "counterattack_attempt",
        ["outcome"] = outcome,
        ["reason"] = reason,
        ["triggering_attack_action_id"] = entry.Fact.ActionId.Value,
        // 原 fact defender 是反击 source，原 fact attacker 是反击 target。
        ["source_unit_id"] = entry.Fact.DefenderUnitId.ToString(),
        ["target_unit_id"] = entry.Fact.AttackerUnitId.ToString(),
        ["capability_instance_id"] =
            entry.Capability.InstanceId.ToString(),
        ["chance_percent"] = chancePercent,
        ["chance_roll"] = chanceRoll,
        ["stamina_cost"] = staminaCost,
        ["text"] = text,
    };
}

private static string CounterattackBlockReasonName(
    BattleCounterattackBlockReason reason
) => reason switch
{
    BattleCounterattackBlockReason.InvalidFact => "invalid_fact",
    BattleCounterattackBlockReason.CapabilityGone => "capability_gone",
    BattleCounterattackBlockReason.DefenderDown => "defender_down",
    BattleCounterattackBlockReason.AttackerGone => "attacker_gone",
    BattleCounterattackBlockReason.NotHostile => "not_hostile",
    BattleCounterattackBlockReason.TriggerMismatch => "trigger_mismatch",
    BattleCounterattackBlockReason.CounterattackLocked =>
        "counterattack_locked",
    BattleCounterattackBlockReason.HardControlled => "hard_controlled",
    BattleCounterattackBlockReason.NoReactionCharge =>
        "no_reaction_charge",
    BattleCounterattackBlockReason.AttackUnavailable =>
        "attack_unavailable",
    BattleCounterattackBlockReason.OutOfReach => "out_of_reach",
    BattleCounterattackBlockReason.BarrierBlocked => "barrier_blocked",
    BattleCounterattackBlockReason.InsufficientStamina =>
        "insufficient_stamina",
    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
};

private static string CounterattackBlockText(
    BattleCounterattackBlockReason reason
) => reason switch
{
    BattleCounterattackBlockReason.InvalidFact =>
        "反击请求已经失效。",
    BattleCounterattackBlockReason.CapabilityGone =>
        "反击能力已经失效。",
    BattleCounterattackBlockReason.DefenderDown =>
        "反击者已无法行动。",
    BattleCounterattackBlockReason.AttackerGone =>
        "反击目标已经失效。",
    BattleCounterattackBlockReason.NotHostile =>
        "当前目标不满足敌对关系。",
    BattleCounterattackBlockReason.TriggerMismatch =>
        "本次攻击不再满足反击触发条件。",
    BattleCounterattackBlockReason.CounterattackLocked =>
        "反击被封锁。",
    BattleCounterattackBlockReason.HardControlled =>
        "反击者受控制，无法反击。",
    BattleCounterattackBlockReason.NoReactionCharge =>
        "反应次数不足，无法反击。",
    BattleCounterattackBlockReason.AttackUnavailable =>
        "当前武器动作不可用。",
    BattleCounterattackBlockReason.OutOfReach =>
        "反击目标超出武器射程。",
    BattleCounterattackBlockReason.BarrierBlocked =>
        "反击路径被屏障阻挡。",
    BattleCounterattackBlockReason.InsufficientStamina =>
        "体力不足，无法反击。",
    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
};
```

这两个 public-to-module formatter 入口返回的 key 恰好是
`entry_kind/outcome/reason/triggering_attack_action_id/source_unit_id/target_unit_id/capability_instance_id/chance_percent/chance_roll/stamina_cost/text`。
blocked 的 `chance_roll` 固定为 `0`；definition 尚不可用时 `stamina_cost` 保留 availability
的 `-1`，表示未解析，而不是伪造为零成本。chance failure 的
`outcome/reason` 都固定为 `chance_failed`。返回值继续交给现有
`BattleMetricsReportService._append_report_entry_to_batch(...)`；该方法会附加当前
`effect_origin`，把非空 `text` 恰好复制成一条 batch log，因此 formatter 和
`TryExecute(...)` 都不得另行 `batch.AddLogLine(...)`。
`BattleReportFormatter.cs` 当前没有 `using System;`，实施这段代码时必须在文件头新增该
using；返回类型刻意写成
`System.Collections.Generic.Dictionary<string, object>`，不能误写成该文件已导入的
`Godot.Collections.Dictionary`，因为 `_append_report_entry_to_batch(...)` 接收的是
`IReadOnlyDictionary<string, object>`。

---

## 十、完整即时武器攻击执行

### 10.1 为什么必须抽正式 service

仅调用 `BattleDamageResolver.ResolveAttackEffects(...)` 不等于完成一次正式 runtime 攻击。外层仍可能负责：

- status turn timing。
- changed unit/coord 与日志/report。
- defeat handling 与 kill provenance。
- contingency HP/status hooks。
- contribution ledger、battle rating 与 metrics。
- mode-specific progression；Counterattack 只允许 §10.4.1 的 stateless
  weapon-training grant。
- producer-specific outcome。

现有装备即时武器攻击只能作为算法参考，不能直接复制后宣称具有上述完整语义。

### 10.2 新 owner

```csharp
internal sealed class BattleImmediateWeaponAttackService
    : BattleRuntimeModuleBorrower
{
    private readonly BattleWeaponAttackOutcomeCommitter
        _weaponAttackOutcomeCommitter;
    private readonly IBattleCounterattackWeaponAttackDefinitionProvider
        _counterattackDefinitionProvider;

    internal BattleImmediateWeaponAttackService(
        BattleWeaponAttackOutcomeCommitter weaponAttackOutcomeCommitter,
        IBattleCounterattackWeaponAttackDefinitionProvider
            counterattackDefinitionProvider
    )
    {
        _weaponAttackOutcomeCommitter =
            weaponAttackOutcomeCommitter
            ?? throw new ArgumentNullException(
                nameof(weaponAttackOutcomeCommitter)
            );
        _counterattackDefinitionProvider =
            counterattackDefinitionProvider
            ?? throw new ArgumentNullException(
                nameof(counterattackDefinitionProvider)
            );
    }

    // PrepareCounterattack / PrepareEquipmentReaction / Query / Execute
    // 的完整方法体见 §10.3；全部放在本 class 内。
}
```

两个 factory request 只携带构造 plan 所需的最小输入，目标声明如下：

```csharp
internal sealed class BattleCounterattackImmediateWeaponAttackRequest
{
    internal BattleCounterattackImmediateWeaponAttackRequest(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleCounterattackCapability capability
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        if (capability.InstanceId == new StringName(""))
            throw new ArgumentException("capability instance id is required");
        Capability = capability;
    }

    internal BattleState State { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal BattleCounterattackCapability Capability { get; }
}

internal sealed class BattleEquipmentImmediateWeaponAttackRequest
{
    internal BattleEquipmentImmediateWeaponAttackRequest(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        StringName traitId,
        StringName bindingId,
        StringName actionId,
        StringName sourceEquipmentInstanceId
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        SkillDefinition = skillDefinition
            ?? throw new ArgumentNullException(nameof(skillDefinition));
        if (bindingId == new StringName(""))
            throw new ArgumentException("binding id is required", nameof(bindingId));
        if (actionId == new StringName(""))
            throw new ArgumentException("action id is required", nameof(actionId));
        TraitId = traitId;
        BindingId = bindingId;
        ActionId = actionId;
        SourceEquipmentInstanceId = sourceEquipmentInstanceId;
    }

    internal BattleState State { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal SkillDefinition SkillDefinition { get; }
    internal StringName TraitId { get; }
    internal StringName BindingId { get; }
    internal StringName ActionId { get; }
    internal StringName SourceEquipmentInstanceId { get; }
}
```

内部使用：

```text
IBattleCounterattackWeaponAttackDefinitionProvider
  -> BattleAttackCheckPolicyService
  -> BattleDamageResolver
  -> BattleWeaponAttackOutcomeCommitter
```

counterattack system 不读取基础攻击的具体 skill id，也不允许 adapter 内部硬编码 `"basic_attack"`。P1A 使用显式 provider port：

```csharp
internal sealed class BattleImmediateWeaponAttackDefinition
{
    internal BattleImmediateWeaponAttackDefinition(
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int staminaCost
    )
    {
        SkillDefinition = skillDefinition
            ?? throw new ArgumentNullException(nameof(skillDefinition));
        if (effectDefinitions == null || effectDefinitions.Count == 0)
            throw new ArgumentException("weapon attack effects are required");
        if (!BattleAttackDeliveryRules.IncludesWeaponDamage(effectDefinitions))
            throw new ArgumentException("definition must include weapon damage");
        if (staminaCost < 0)
            throw new ArgumentOutOfRangeException(nameof(staminaCost));
        EffectDefinitions =
            new List<CombatEffectDefinition>(effectDefinitions).ToArray();
        StaminaCost = staminaCost;
    }

    internal SkillDefinition SkillDefinition { get; }
    internal IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    internal int StaminaCost { get; }
}

internal interface IBattleCounterattackWeaponAttackDefinitionProvider
{
    bool TryResolve(
        BattleState state,
        BattleUnitReadView sourceUnit,
        BattleCounterattackCapability capability,
        out BattleImmediateWeaponAttackDefinition definition
    );
}

internal sealed class
    BattleRuntimeCounterattackWeaponAttackDefinitionProvider
    : BattleRuntimeModuleBorrower,
        IBattleCounterattackWeaponAttackDefinitionProvider
{
    public bool TryResolve(
        BattleState state,
        BattleUnitReadView sourceUnit,
        BattleCounterattackCapability capability,
        out BattleImmediateWeaponAttackDefinition definition
    )
    {
        definition = null;
        BattleRuntimeModule runtime = _runtime;
        if (
            runtime == null
            || state == null
            || !ReferenceEquals(state, runtime.GetState())
            || !sourceUnit.IsValid
            || capability.WeaponActionDefinitionId == new StringName("")
        )
        {
            return false;
        }

        StringName definitionId =
            capability.WeaponActionDefinitionId;
        SkillDefinition skillDefinition =
            runtime.GetSkillDefinitionTyped(definitionId);
        if (skillDefinition?.CombatProfile == null)
            return false;

        int skillLevel;
        if (sourceUnit.HasKnownSkillLevel(definitionId))
        {
            skillLevel = Math.Max(
                sourceUnit.GetKnownSkillLevel(definitionId),
                0
            );
        }
        else if (sourceUnit.KnowsActiveSkill(definitionId))
        {
            skillLevel = 1;
        }
        else
        {
            return false;
        }

        SkillEffectiveCombatDefinition effective =
            SkillEffectiveCombatDefinition.BuildUncached(
                skillDefinition,
                skillLevel
            );
        IReadOnlyList<CombatEffectDefinition> effects =
            effective.CombatProfile?.EffectDefinitions
            ?? Array.Empty<CombatEffectDefinition>();
        int staminaCost = effective.ResourceCosts.StaminaCost;
        if (
            effects.Count == 0
            || !BattleAttackDeliveryRules.IncludesWeaponDamage(effects)
            || staminaCost < 0
        )
        {
            return false;
        }

        definition = new BattleImmediateWeaponAttackDefinition(
            skillDefinition,
            effects,
            staminaCost
        );
        return true;
    }
}
```

production 绑定上面的 runtime provider。它只解释 capability 已经携带的
`WeaponActionDefinitionId`，不会选择、猜测或比较任何特定 ID。source 必须已经拥有该
active definition；level 解析逐句复制 `BattleHitResolver.cs:344-356` 的
“explicit known level，否则 known active 取 1”规则，所以 effective stamina、range 与随后
attack check 使用同一级别。definition 缺失、source 不拥有、combat profile/effects 缺失、
不是武器攻击或成本非法时返回 false，query 投影为 `attack_unavailable`。

P1A 仍不安装任何 production capability，因此不会偷偷启用具体内容；focused fixture
通过 runtime 的测试 skill-definition index 与直接安装 capability 形成可执行 plan。
后续内容提案只负责产出 capability 的 generic definition reference，不需要替换
counterattack/query/execute/provider 代码。`EquipmentReaction` 不走这个 port，而是使用
payload 已显式给出的 `SkillDefinition`。

service 使用闭集 mode，而不是松散 bool：

```csharp
internal enum BattleImmediateWeaponAttackMode
{
    Counterattack = 0,
    EquipmentReaction
}
```

两种 mode 共用 attack definition、attack check、damage resolver、装备同步反应、耐久与基础 outcome DTO；mode-specific outcome policy 由 `BattleWeaponAttackOutcomeCommitter` 的 typed 分支拥有。

`BattleWeaponAttackOutcomeCommitter` 的迁移源不是整个 `_apply_unit_skill_result(...)`，而是其中可证明与具体技能无关的四个有序阶段：

```csharp
internal enum BattleWeaponAttackOutcomeKind
{
    Unknown = 0,
    StandardWeaponSkillAttack,
    Counterattack,
    EquipmentReaction
}

internal sealed class BattleWeaponAttackOutcomeCommitter
{
    internal void CommitResolverSurface(
        BattleWeaponAttackResolverSurfaceRequest request
    );

    internal void CommitPostProducerHooks(
        BattleWeaponAttackPostProducerHookRequest request
    );

    internal void CommitUnappliedResultSurface(
        BattleWeaponAttackUnappliedResultSurfaceRequest request
    );

    internal void CommitAppliedResultSurface(
        BattleWeaponAttackAppliedResultSurfaceRequest request
    );

    internal void CommitTerminalOutcome(
        BattleWeaponAttackTerminalOutcomeRequest request
    );
}
```

同一个 enum 同时进入 `BattleKillProvenance`，否则现有结构只能表达“这是武器攻击及其装备 attribution”，不能类型化区分标准技能、反击与装备追击。`scripts/systems/battle/core/BattleKillProvenance.cs:3-91` 的目标签名改为：

```csharp
internal readonly struct BattleKillProvenance
{
    private BattleKillProvenance(
        BattleWeaponAttackOutcomeKind weaponAttackOutcomeKind,
        bool isAttack,
        bool includesWeaponDamage,
        StringName sourceEquipmentInstanceId,
        StringName sourceBindingId,
        StringName sourceActionId
    )
    {
        WeaponAttackOutcomeKind = weaponAttackOutcomeKind;
        IsAttack = isAttack;
        IncludesWeaponDamage = includesWeaponDamage;
        SourceEquipmentInstanceId =
            ProgressionDataUtils.to_string_name(
                sourceEquipmentInstanceId
            );
        SourceBindingId =
            ProgressionDataUtils.to_string_name(sourceBindingId);
        SourceActionId =
            ProgressionDataUtils.to_string_name(sourceActionId);
    }

    internal BattleWeaponAttackOutcomeKind WeaponAttackOutcomeKind { get; }
    internal bool IsAttack { get; }
    internal bool IncludesWeaponDamage { get; }
    internal StringName SourceEquipmentInstanceId { get; }
    internal StringName SourceBindingId { get; }
    internal StringName SourceActionId { get; }

    internal static BattleKillProvenance None =>
        new(
            BattleWeaponAttackOutcomeKind.Unknown,
            false,
            false,
            "",
            "",
            ""
        );

    internal static BattleKillProvenance ForWeaponAttack(
        BattleWeaponAttackOutcomeKind outcomeKind,
        StringName sourceEquipmentInstanceId,
        StringName sourceBindingId,
        StringName sourceActionId
    )
    {
        RequireConcreteOutcomeKind(outcomeKind);
        if (sourceActionId == new StringName(""))
            throw new ArgumentException("source action id is required");
        return new BattleKillProvenance(
            outcomeKind,
            true,
            true,
            sourceEquipmentInstanceId,
            sourceBindingId,
            sourceActionId
        );
    }

    internal static BattleKillProvenance FromWeaponAttackResult(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        BattleWeaponAttackOutcomeKind outcomeKind,
        StringName sourceActionId
    )
    {
        RequireConcreteOutcomeKind(outcomeKind);
        if (sourceUnit == null || !IncludesWeaponDamageResult(result))
            return None;
        if (sourceActionId == new StringName(""))
        {
            throw new ArgumentException(
                "source action id is required for a weapon-damage result",
                nameof(sourceActionId)
            );
        }
        return FromWeaponAttackResult(
            sourceUnit,
            result,
            outcomeKind,
            ForWeaponAttack(outcomeKind, "", "", sourceActionId)
        );
    }

    internal static BattleKillProvenance FromWeaponAttackResult(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        BattleWeaponAttackOutcomeKind outcomeKind,
        BattleKillProvenance fallback
    )
    {
        RequireConcreteOutcomeKind(outcomeKind);
        if (
            fallback.WeaponAttackOutcomeKind
                != BattleWeaponAttackOutcomeKind.Unknown
            && fallback.WeaponAttackOutcomeKind != outcomeKind
        )
        {
            throw new ArgumentException(
                "fallback outcome kind does not match",
                nameof(fallback)
            );
        }
        if (sourceUnit == null || !IncludesWeaponDamageResult(result))
            return None;

        AttackCheckInput attackCheck = result.AttackCheck;
        bool forcedCriticalApplied =
            result.CriticalHit
            && attackCheck.ForceCriticalOnHit;
        StringName equipmentInstanceId =
            forcedCriticalApplied
                ? attackCheck
                    .ForcedCriticalSourceEquipmentInstanceId
                : fallback.SourceEquipmentInstanceId;
        if (equipmentInstanceId == new StringName(""))
        {
            equipmentInstanceId =
                sourceUnit
                    .GetEquipmentView()
                    ?.GetEquippedInstanceId("main_hand")
                ?? new StringName("");
        }
        StringName bindingId =
            forcedCriticalApplied
                ? attackCheck.ForcedCriticalSourceBindingId
                : fallback.SourceBindingId;
        StringName actionId =
            forcedCriticalApplied
            && attackCheck.ForcedCriticalSourceActionId
                != new StringName("")
                ? attackCheck.ForcedCriticalSourceActionId
                : fallback.SourceActionId;
        return ForWeaponAttack(
            outcomeKind,
            equipmentInstanceId,
            bindingId,
            actionId
        );
    }

    private static bool IncludesWeaponDamageResult(
        AttackEffectResolutionResult result
    )
    {
        foreach (
            DamageEventResult damageEvent
                in result.DamageEvents
                    ?? Array.Empty<DamageEventResult>()
        )
        {
            if (
                damageEvent.AddWeaponDice
                && damageEvent.WeaponDamageDice.Count > 0
                && damageEvent.WeaponDamageDice.Sides > 0
            )
            {
                return true;
            }
        }
        return false;
    }

    private static void RequireConcreteOutcomeKind(
        BattleWeaponAttackOutcomeKind outcomeKind
    )
    {
        if (
            outcomeKind == BattleWeaponAttackOutcomeKind.Unknown
            || !Enum.IsDefined(outcomeKind)
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcomeKind),
                outcomeKind,
                null
            );
        }
    }
}
```

`ForWeaponAttack(...)` 与两个 `FromWeaponAttackResult(...)` 都拒绝 `Unknown`；只有结果确认
包含真实 weapon-dice damage 时才要求非空 `sourceActionId`，无武器伤害仍返回 `None`，不因
构造一个不会使用的 fallback 提前抛错。forced-critical equipment attribution 仍按当前
`BattleKillProvenance.cs:62-81` 覆盖 equipment/binding/action 三个字段，但
`WeaponAttackOutcomeKind` 永远取显式 `outcomeKind`，不能从 fallback 或字符串 action id
猜测。删除旧 `ForEquipmentAttack(...)` 和两个缺少 outcome kind 的
`FromWeaponAttackResult(...)` overload，不加兼容别名。当前调用点一次性迁移为：

| 当前调用点 | 显式 kind | 非空 `sourceActionId` 证明 |
|---|---|---|
| `BattleSkillExecutionOrchestrator.cs:1099-1110`、`BattleRepeatAttackResolver.cs:295`、`BattleChargeResolver.cs:1062`、`BattleGroundEffectService.cs:813` | `StandardWeaponSkillAttack` | 使用已解析且通过内容校验的 `skillDefinition.SkillId` |
| `BattleChainDamageService.cs:228` | `StandardWeaponSkillAttack` | applied chain 入口先执行下文的 definition/skill-id guard，再传 `skillDefinition.SkillId` |
| `BattleEquipmentAbilityRuntimeService.cs:1547-1555` 迁入的新 equipment plan | `EquipmentReaction` | `BattleEquipmentImmediateWeaponAttackRequest` constructor 已要求非空 `ActionId` |
| 新 counterattack terminal request | `Counterattack` | capability 安装和 request constructor 已要求非空 `InstanceId` |
| `run_executioner_axe_weapon_ability_regression.cs`、`run_lumberjack_axe_weapon_ability_regression.cs`、`run_memoryeater_vine_weapon_ability_regression.cs` 的直接 factory 调用 | 按 fixture 模拟的实际来源显式选择，不用 `SourceActionId` 代替 kind | fixture 显式提供非空 action id |

`BattleChainDamageService._apply_chain_damage_effects(...)` 保留当前
`if (!primaryResolution.Applied) return;` 后，进入 effect loop 前精确新增：

```csharp
ArgumentNullException.ThrowIfNull(skillDefinition);
if (skillDefinition.SkillId == new StringName(""))
{
    throw new InvalidOperationException(
        "applied chain damage requires a non-empty source skill id"
    );
}
```

因此“applied weapon chain + null definition”不再靠 provenance factory 偶然兜底；它在
chain owner 的输入边界按稳定错误 fail fast。所有 production caller 继续传正式
`SkillDefinition`，不新增空 action-id provenance。

四个 phase 使用五个同步 typed request（result surface 按 applied/unapplied 二选一），不使用一个带大量 nullable 字段的万能 DTO：

| request | 必填字段 |
|---|---|
| `BattleWeaponAttackResolverSurfaceRequest` | kind、source/target、resolution、root batch |
| `BattleWeaponAttackPostProducerHookRequest` | kind、source/target、resolution、root batch、previous target HP、producer 合并后的非 null status-id read view、source event id |
| `BattleWeaponAttackUnappliedResultSurfaceRequest` | kind、source/target、resolution、root batch |
| `BattleWeaponAttackAppliedResultSurfaceRequest` | kind、source/target、resolution、root batch、非空 subject label、target display label |
| `BattleWeaponAttackTerminalOutcomeRequest` | kind、source/target、resolution、root batch、skill id、typed kill provenance |

目标 request 声明固定如下；实施时不得改成 object/dictionary，也不得把五个入口合并成 `bool applied` + nullable label/status/provenance：

```csharp
internal abstract class BattleWeaponAttackOutcomeRequest
{
    protected BattleWeaponAttackOutcomeRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch
    )
    {
        if (
            kind == BattleWeaponAttackOutcomeKind.Unknown
            || !Enum.IsDefined(kind)
        )
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        Kind = kind;
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        Resolution = resolution;
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
    }

    internal BattleWeaponAttackOutcomeKind Kind { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal AttackEffectResolutionResult Resolution { get; }
    internal BattleEventBatch Batch { get; }
}

internal sealed class BattleWeaponAttackResolverSurfaceRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackResolverSurfaceRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch
    ) : base(kind, sourceUnit, targetUnit, resolution, batch) { }
}

internal sealed class BattleWeaponAttackPostProducerHookRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackPostProducerHookRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch,
        int previousTargetHp,
        IReadOnlyList<StringName> appliedStatusIds,
        StringName sourceEventId
    ) : base(kind, sourceUnit, targetUnit, resolution, batch)
    {
        AppliedStatusIds = appliedStatusIds
            ?? throw new ArgumentNullException(nameof(appliedStatusIds));
        if (sourceEventId == new StringName(""))
            throw new ArgumentException("source event id is required", nameof(sourceEventId));
        PreviousTargetHp = previousTargetHp;
        SourceEventId = sourceEventId;
    }

    internal int PreviousTargetHp { get; }
    internal IReadOnlyList<StringName> AppliedStatusIds { get; }
    internal StringName SourceEventId { get; }
}

internal sealed class BattleWeaponAttackUnappliedResultSurfaceRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackUnappliedResultSurfaceRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch
    ) : base(kind, sourceUnit, targetUnit, resolution, batch) { }
}

internal sealed class BattleWeaponAttackAppliedResultSurfaceRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackAppliedResultSurfaceRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayLabel
    ) : base(kind, sourceUnit, targetUnit, resolution, batch)
    {
        if (string.IsNullOrWhiteSpace(subjectLabel))
            throw new ArgumentException("subject label is required", nameof(subjectLabel));
        SubjectLabel = subjectLabel;
        TargetDisplayLabel = targetDisplayLabel ?? "";
    }

    internal string SubjectLabel { get; }
    internal string TargetDisplayLabel { get; }
}

internal sealed class BattleWeaponAttackTerminalOutcomeRequest
    : BattleWeaponAttackOutcomeRequest
{
    internal BattleWeaponAttackTerminalOutcomeRequest(
        BattleWeaponAttackOutcomeKind kind,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult resolution,
        BattleEventBatch batch,
        StringName skillId,
        BattleKillProvenance killProvenance
    ) : base(kind, sourceUnit, targetUnit, resolution, batch)
    {
        if (skillId == new StringName(""))
            throw new ArgumentException("skill id is required", nameof(skillId));
        if (
            killProvenance.WeaponAttackOutcomeKind
                != BattleWeaponAttackOutcomeKind.Unknown
            && killProvenance.WeaponAttackOutcomeKind != kind
        )
        {
            throw new ArgumentException(
                "kill provenance kind does not match request kind",
                nameof(killProvenance)
            );
        }
        SkillId = skillId;
        KillProvenance = killProvenance;
    }

    internal StringName SkillId { get; }
    internal BattleKillProvenance KillProvenance { get; }
}
```

这些 request 只允许在当前同步调用栈借用 `BattleUnitState` 与 batch；committer 不缓存 request 或其中任何 live reference。每个 module-internal committer 入口第一条业务语句都调用 `_attackActionCoordinator.RequireActiveRootBatch(request.Batch)`（经 `BattleRuntimeModule.RequireActiveReactionBatch(...)` 转发）。unapplied/applied 使用不同入口，caller 不能用一个 policy bool 任意拼出不存在的 result surface；真正的启用项仍由 `BattleWeaponAttackOutcomeKind` 闭集决定。

四个阶段不能再次合并，因为当前代码在 hook 与 result surface 中间有 early return、Vajra mastery 和 forced-move log。边界固定为：

| 阶段 | 从现有 `_apply_unit_skill_result(...)` 迁出的通用步骤 | 当前源码锚点 |
|---|---|---|
| `CommitResolverSurface` | resolver status turn timing、changed unit/coord、result-source status facts | `BattleSkillExecutionOrchestrator.cs:1988-1994` |
| `CommitPostProducerHooks` | producer 合并实际 status ids 后发 contingency HP/status hooks | `BattleSkillExecutionOrchestrator.cs:2017-2033` 中的 status-id merge 与 `EmitContingencyHpAndStatusHooks` |
| result surface | `CommitUnappliedResultSurface` 只 append result report；`CommitAppliedResultSurface` 提交 damage log、durability log/projection 与 result report；每次攻击二选一 | unapplied `:2034-2055`；applied `:2058-2087` |
| `CommitTerminalOutcome` | defeated handling、achievement/kill provenance、effect metrics、contribution ledger 与 battle rating | `BattleSkillExecutionOrchestrator.cs:2195-2231`，但 `:2197-2203` 的 skill-specific on-kill resource 不迁入 |

每个 outcome kind 的启用项是闭集 policy，不靠 caller bool：

| committer step | `StandardWeaponSkillAttack` | `Counterattack` | `EquipmentReaction` P1A baseline |
|---|---:|---:|---:|
| resolver status timing / changed facts / source status facts | 保持现状 | 是 | 不调用；只保留 resolver 内已有提交 |
| post-producer HP/status hooks | 保持现状 | 是 | 不调用 |
| result log / durability projection / report surface | 保持现状 | 是 | 不调用；使用下述 equipment baseline surface |
| defeated handling / typed provenance | 保持现状 | 是 | 是 |
| effect metrics / contribution / rating | 保持现状 | 是 | 否 |

committer 的目标实现不是“按 kind 随便跳过几行”，而是下面这个闭集分派。前三个阶段和两个 result 入口只接受 standard/counter；equipment 若误调用立即抛错，从而防止以后重构时静默给旧装备路径增加 surface。`CommitTerminalOutcome` 是唯一接受 equipment 的入口：

```csharp
internal sealed class BattleWeaponAttackOutcomeCommitter
    : BattleRuntimeModuleBorrower
{
    private readonly BattleEquipmentDurabilityResultProjector
        _durabilityResultProjector;

    internal BattleWeaponAttackOutcomeCommitter(
        BattleEquipmentDurabilityResultProjector durabilityResultProjector
    )
    {
        _durabilityResultProjector = durabilityResultProjector
            ?? throw new ArgumentNullException(nameof(durabilityResultProjector));
    }

    internal void CommitResolverSurface(
        BattleWeaponAttackResolverSurfaceRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime.MarkAppliedStatusesForTurnTiming(
            request.TargetUnit,
            request.Resolution.StatusEffectIds
        );
        _runtime._append_changed_unit_id(request.Batch, request.TargetUnit.unit_id);
        _runtime._append_changed_unit_coords(request.Batch, request.TargetUnit);
        _runtime.AppendResultSourceStatusEffects(
            request.Batch,
            request.SourceUnit,
            request.Resolution
        );
    }

    internal void CommitPostProducerHooks(
        BattleWeaponAttackPostProducerHookRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime.EmitContingencyHpAndStatusHooks(
            request.SourceUnit,
            request.TargetUnit,
            request.PreviousTargetHp,
            request.AppliedStatusIds,
            request.SourceEventId
        );
    }

    internal void CommitUnappliedResultSurface(
        BattleWeaponAttackUnappliedResultSurfaceRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime.AppendResultReportEntry(request.Batch, request.Resolution);
    }

    internal void CommitAppliedResultSurface(
        BattleWeaponAttackAppliedResultSurfaceRequest request
    )
    {
        RequireStandardOrCounter(request);
        _runtime._report_formatter.AppendDamageResultLogLines(
            request.Batch,
            request.SubjectLabel,
            request.TargetDisplayLabel,
            request.Resolution
        );
        _durabilityResultProjector.Commit(
            request.TargetUnit,
            request.Resolution,
            request.Batch
        );
        _runtime.AppendResultReportEntry(request.Batch, request.Resolution);
    }

    internal void CommitTerminalOutcome(
        BattleWeaponAttackTerminalOutcomeRequest request
    )
    {
        RequireBoundAndActiveBatch(request);
        bool isEquipmentReaction =
            request.Kind
                == BattleWeaponAttackOutcomeKind.EquipmentReaction;
        bool recordsCombatContribution =
            request.Kind
                == BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack
            || request.Kind
                == BattleWeaponAttackOutcomeKind.Counterattack;
        if (!isEquipmentReaction && !recordsCombatContribution)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Kind),
                request.Kind,
                null
            );
        }

        if (request.TargetUnit.IsAlive() != true)
        {
            _runtime.HandleUnitDefeatedByRuntimeEffect(
                request.TargetUnit,
                request.SourceUnit,
                request.Batch,
                $"{request.TargetUnit.display_name} 被击倒。",
                new BattleDefeatHandlingOptions(
                    recordEnemyDefeatedAchievement: true,
                    killProvenance: request.KillProvenance
                )
            );
        }

        if (isEquipmentReaction)
            return;

        int damage = request.Resolution.Damage;
        int healing = request.Resolution.Healing;
        bool causedDefeat = !request.TargetUnit.IsAlive();
        _runtime._record_effect_metrics(
            request.SourceUnit,
            request.TargetUnit,
            damage,
            healing,
            causedDefeat ? 1 : 0
        );
        _runtime._battle_rating_system.RecordContributionFromUnits(
            request.SourceUnit,
            request.TargetUnit,
            damage,
            healing,
            causedDefeat,
            request.Kind == BattleWeaponAttackOutcomeKind.Counterattack
                ? new StringName("counterattack")
                : new StringName("skill"),
            request.SkillId
        );
    }

    private void RequireStandardOrCounter(
        BattleWeaponAttackOutcomeRequest request
    )
    {
        RequireBoundAndActiveBatch(request);
        if (
            request.Kind != BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack
            && request.Kind != BattleWeaponAttackOutcomeKind.Counterattack
        )
        {
            throw new InvalidOperationException(
                $"{request.Kind} has no standard weapon result surface"
            );
        }
    }

    private void RequireBoundAndActiveBatch(
        BattleWeaponAttackOutcomeRequest request
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_runtime == null)
            throw new InvalidOperationException("weapon outcome committer is not bound");
        _runtime.RequireActiveReactionBatch(request.Batch);
    }
}
```

`BattleEquipmentDurabilityResultProjector.Commit(...)` 的方法体逐句来自当前 `BattleSkillExecutionOrchestrator.cs:2237-2285`。唯一机械变化是 `_append_changed_unit_id/_coords` 改成 `_runtime` 调用；它的目标代码为：

```csharp
internal sealed class BattleEquipmentDurabilityResultProjector
    : BattleRuntimeModuleBorrower
{
    internal void Commit(
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    )
    {
        if (_runtime == null)
            throw new InvalidOperationException("durability projector is not bound");
        if (targetUnit == null || batch == null)
            return;

        bool destroyedAny = false;
        foreach (
            EquipmentDurabilityEventResult eventResult
                in result.EquipmentDurabilityEvents
                    ?? Array.Empty<EquipmentDurabilityEventResult>()
        )
        {
            string itemId = eventResult.ItemId ?? "";
            if (string.IsNullOrEmpty(itemId))
                itemId = "装备";
            if (eventResult.SaveResult.HasSave && eventResult.SaveResult.Success)
            {
                batch.AddLogLine($"{targetUnit.display_name} 的 {itemId} 抵抗了裂解术。");
                continue;
            }
            int durabilityLoss = eventResult.DurabilityLoss;
            if (durabilityLoss <= 0)
                continue;
            if (eventResult.Destroyed)
            {
                destroyedAny = true;
                batch.AddLogLine($"{targetUnit.display_name} 的 {itemId} 被裂解为尘埃。");
            }
            else
            {
                batch.AddLogLine(
                    $"{targetUnit.display_name} 的 {itemId} 被裂解，耐久 "
                    + $"{eventResult.DurabilityBefore} -> "
                    + $"{eventResult.DurabilityAfter}。"
                );
            }
        }
        if (!destroyedAny)
            return;
        _runtime._append_changed_unit_id(batch, targetUnit.unit_id);
        _runtime._append_changed_unit_coords(batch, targetUnit);
    }
}
```

`EquipmentReaction` 还保留一个不属于四阶段 committer 的 mode-specific baseline surface。它不是抽象描述，而是 `BattleEquipmentAbilityRuntimeService.cs:1521-1537` 的原样迁移：

1. `:1521-1530` 的 `BattleEquipmentAbilityImmediateWeaponAttackResult` 字段映射由 `BattleImmediateWeaponAttackService` 构造为返回 summary：`BindingId` ← request binding id、`ActionId` ← request action id、`TargetUnitId` ← target id、`Applied` ← resolver result、`Damage` ← `Math.Max(result.Damage, 0)`。
2. `:1531-1532` 的 source/target `AddChangedUnitId`、`:1533-1534` 的 target occupied coords、`:1535-1537` 的“借 trait 追击”日志，在 resolver 成功且满足当前 `:1517` 的计数条件后、`CommitTerminalOutcome` 前，由 `BattleImmediateWeaponAttackService` 恰好提交一次。
3. 这些语句不得进入 `CommitResolverSurface`、`CommitPostProducerHooks`、`CommitUnappliedResultSurface` 或 `CommitAppliedResultSurface`，否则 `EquipmentReaction` 会获得标准技能当前没有的外层 surface，或与 resolver 内部 `AttachAttackReportEntry` / dispatch 重复。
4. `BattleEquipmentAbilityRuntimeService` 只在 `Execute(...)` 返回后调用现有 `BattleEquipmentAbilityOnKillResult.AddImmediateWeaponAttackResult(...)`（定义 `BattleEquipmentAbilityRuntimeService.cs:241-247`）聚合 summary；该 list 当前只由 `Resolved`/getter（`:207-219`）公开，不在 nested attack 中作为控制流读取，因此返回后聚合不改变战斗时序。

`BattleEquipmentImmediateWeaponAttackRequest` 必须冻结 source/target、skill definition、
trait id、binding id、action id 与 source equipment instance id；available immutable plan
复制 skill/effects，projection unavailable plan 只保留 source/target 与 immutable equipment
attribution，不携带不可执行 definition。两者都不得传入 outer
`context.KillProvenance`、concrete equipment runtime、mutable
`ActiveEquipmentAbilityBinding`、mutable action/binding definition 或
`BattleEquipmentAbilityOnKillResult`。即时攻击自己的 provenance 必须在 resolver 返回后，
用本次 `attackResult` 与 equipment instance/binding/action 三个冻结 ID 构造，不能借用
触发它的上一次击杀 provenance。

以下逻辑明确留在 `BattleSkillExecutionOrchestrator` 或各自现有 owner，不能进入 committer：

- source-bound weapon bonus、last-stand、guard、Vajra、target-result 与最终 grant 等全部 mastery 调用。
- shield 应用、special resolution、forced move、chain damage、terrain/height 修改。
- doom-sentence 等具体 skill report tag。
- skill-specific on-kill resource gain。
- AP、cooldown、casting、skill usage/mastery 的 command 外壳。

`StandardWeaponSkillAttack` 只用于 `BattleAttackDeliveryRules.IncludesWeaponDamage(effectDefinitions) == true` 的标准技能路径，不按 skill id/tag 猜测。该 typed rule 接管当前 `BattleDamageResolver.EffectDefinitionsIncludeWeaponDamage(...)`（`BattleDamageResolver.cs:705-719`）的判断；orchestrator 与 resolver 必须调用同一个 owner。非武器 unit spell 不为复用本 committer 而改变 owner。

`_apply_unit_skill_result(...)` 的 production caller 都从 validated target collection、
random-chain non-null pool 或已通过 `targetUnit?.IsAlive() == true` 的 terminal resolver
进入；现有方法体里的 `target_unit?.` 是防御性残留，不构成“null target 是合法结算”的
API 契约。P1A 在方法第一行、任何 barrier/resolver/committer 调用前固定输入合同：

```csharp
ArgumentNullException.ThrowIfNull(active_unit);
ArgumentNullException.ThrowIfNull(target_unit);
ArgumentNullException.ThrowIfNull(batch);
effectDefinitions ??= Array.Empty<CombatEffectDefinition>();
```

因此 weapon 与 non-weapon 分支共享同一个非空 source/target/root-batch 前置条件；
`BattleWeaponAttackOutcomeRequest` 的 constructor fail-fast 与 orchestrator 入口一致。不得为
保留现有 `?.` 写法而把 committer request 改成 nullable，也不得让 weapon 分支比
non-weapon 分支更晚才发现非法 target。现有生产 caller 不需要行为迁移；新增回归直接对
internal 入口传 null source、null target、null batch，分别断言在任何 resolver、RNG、状态
写入或 batch append 前抛对应 `ArgumentNullException`。

当前 `_apply_unit_skill_result(...)`（`BattleSkillExecutionOrchestrator.cs:1918-2234`）的逐段迁移合同如下：

| 当前源码 | 当前语义 | weapon 分支 P1A 动作 | non-weapon 分支 |
|---|---|---|---|
| `:1953-1963` | previous HP、执行 resolver、取得 `damageResult` | 保留；用这些值构造同步 request | 原样 |
| `:1965-1979` | source-bound、last-stand mastery，构造 guard grant | 留在 orchestrator，原序不动 | 原样 |
| `:1980-1987` | shield apply | 留在 orchestrator | 原样 |
| `:1988-1994` | target status timing、changed unit/coords、source status surface | 替换为 `CommitResolverSurface(request)` | 保留现有语句 |
| `:1995-2016` | special result、special status timing、target-result mastery | 留在 orchestrator | 原样 |
| `:2017-2033` | 合并 actual status ids、HP/status contingency hooks | 列表仍由 orchestrator按现有顺序构造；hook 调用替换为 `CommitPostProducerHooks(request)` | 保留现有 hook |
| `:2034-2055` | 计算 producer-applied；false 时 append report/custom/special log 并返回 | false 分支先调用 `CommitUnappliedResultSurface(...)`，再保留 custom/special log 与 return；不调用 terminal，也不应用 guard grant | 原样 |
| `:2058-2079` | label、Vajra mastery、forced-move log | 留在 orchestrator，原序不动 | 原样 |
| `:2080-2087` | damage log、durability surface、result report | 替换为 `CommitAppliedResultSurface(...)` | 保留现有语句 |
| `:2088-2194` | doom tag、heal/shield/status/dispel log、chain、custom/special、terrain/height | 留在 orchestrator | 原样 |
| `:2195-2203` | dead check + skill-specific on-kill resource | dead check/资源调用留在 orchestrator并先执行 | 原样 |
| `:2204-2231` | defeated handling + metrics/contribution | 替换为 `CommitTerminalOutcome(request)`；provenance 由 caller 预先构造 | 保留当前 owner 与代码 |
| `:2233` | `ApplySkillMasteryGrantTyped(target_unit, guardMasteryGrant, batch)` | 必须在 terminal 返回后保留 | 原样 |
| `:2237-2285` | durability result log/projection helper | 删除 orchestrator 方法；`CommitAppliedResultSurface(...)` 调用唯一 `BattleEquipmentDurabilityResultProjector.Commit(...)` | 直接调用同一个 projector；不得保留旧 helper 或复制方法体 |

目标控制流必须保持下面的形状；四个 committer phase 不能被压成一次尾部调用。
`BattleSkillExecutionOrchestrator` 不新增或缓存这两个 module-owned borrower 字段；在
`_apply_unit_skill_result(...)` 进入下述分支前，通过当前已绑定 runtime 的
`BattleRuntimeModuleBorrowerSet` 解析本次同步调用使用的局部引用。runtime 缺失属于
orchestrator 生命周期契约破坏，必须在读取 borrower 前 fail fast：

```csharp
BattleRuntimeModule runtime = Runtime
    ?? throw new InvalidOperationException(
        "battle runtime is not bound"
    );
BattleWeaponAttackOutcomeCommitter weaponAttackOutcomeCommitter =
    runtime._moduleBorrowers.WeaponAttackOutcomeCommitter;
BattleEquipmentDurabilityResultProjector durabilityResultProjector =
    runtime._moduleBorrowers.EquipmentDurabilityResultProjector;

bool isWeaponAttack =
    BattleAttackDeliveryRules.IncludesWeaponDamage(effectDefinitions);

// :1965-1987 source-bound/last-stand/guard mastery + shield 原样保留。
if (isWeaponAttack)
{
    weaponAttackOutcomeCommitter.CommitResolverSurface(
        new BattleWeaponAttackResolverSurfaceRequest(
            BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack,
            active_unit,
            target_unit,
            damageResult,
            batch
        )
    );
}
else
{
    MarkAppliedStatusesForTurnTiming(
        target_unit,
        damageResult.StatusEffectIds
    );
    _append_changed_unit_id(
        batch,
        target_unit?.unit_id ?? new StringName("")
    );
    _append_changed_unit_coords(batch, target_unit);
    append_result_source_status_effects(batch, active_unit, damageResult);
}

// :1995-2026 specialResult、target mastery、appliedStatusIds 构造原样保留。
if (isWeaponAttack)
{
    weaponAttackOutcomeCommitter.CommitPostProducerHooks(
        new BattleWeaponAttackPostProducerHookRequest(
            BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack,
            active_unit,
            target_unit,
            damageResult,
            batch,
            previousTargetHp,
            appliedStatusIds,
            sourceEventId
        )
    );
}
else
{
    Runtime?.EmitContingencyHpAndStatusHooks(
        active_unit,
        target_unit,
        previousTargetHp,
        appliedStatusIds,
        sourceEventId
    );
}

bool applied =
    damageResult.Applied
    || shieldResult.Applied
    || specialResult.Applied;
if (!applied)
{
    if (isWeaponAttack)
    {
        weaponAttackOutcomeCommitter.CommitUnappliedResultSurface(
            new BattleWeaponAttackUnappliedResultSurfaceRequest(
                BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack,
                active_unit,
                target_unit,
                damageResult,
                batch
            )
        );
    }
    else
    {
        append_result_report_entry(batch, damageResult);
    }
    foreach (string customLine in effectResolution.CustomLogLines)
    {
        if (!string.IsNullOrEmpty(customLine))
            batch?.AddLogLine(customLine);
    }
    foreach (string specialLine in specialResult.LogLines)
    {
        if (!string.IsNullOrEmpty(specialLine))
            batch?.AddLogLine(specialLine);
    }
    return false;
}

// :2058-2079 skillLabel/skillSubject/damage/healing/movedSteps、
// Vajra mastery 与 forced-move log 原样保留。
if (isWeaponAttack)
{
    weaponAttackOutcomeCommitter.CommitAppliedResultSurface(
        new BattleWeaponAttackAppliedResultSurfaceRequest(
            BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack,
            active_unit,
            target_unit,
            damageResult,
            batch,
            skillSubject,
            target_unit?.display_name ?? ""
        )
    );
}
else
{
    AppendDamageResultLogLines(
        batch,
        skillSubject,
        target_unit?.display_name ?? "",
        damageResult
    );
    durabilityResultProjector.Commit(
        target_unit,
        damageResult,
        batch
    );
    append_result_report_entry(batch, damageResult);
}

StringName skillId =
    skillDefinition?.SkillId ?? new StringName("");
// :2089-2194 doom/heal/shield/status/dispel/chain/custom/special/
// terrain/height 的现有语句原样执行；这里的注释是源码保留区，不是新 API。
if (target_unit?.IsAlive() != true)
{
    _apply_on_kill_gain_resources_effects(
        active_unit,
        target_unit,
        skillDefinition,
        effectDefinitions,
        batch
    );
}

BattleKillProvenance killProvenance =
    isWeaponAttack
        ? BattleKillProvenance.FromWeaponAttackResult(
            active_unit,
            damageResult,
            BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack,
            skillId
        )
        : BattleKillProvenance.None;
if (isWeaponAttack)
{
    weaponAttackOutcomeCommitter.CommitTerminalOutcome(
        new BattleWeaponAttackTerminalOutcomeRequest(
            BattleWeaponAttackOutcomeKind.StandardWeaponSkillAttack,
            active_unit,
            target_unit,
            damageResult,
            batch,
            skillId,
            killProvenance
        )
    );
}
else
{
    if (target_unit?.IsAlive() != true)
    {
        Runtime?.HandleUnitDefeatedByRuntimeEffect(
            target_unit,
            active_unit,
            batch,
            $"{target_unit.display_name} 被击倒。",
            new BattleDefeatHandlingOptions(
                recordEnemyDefeatedAchievement: true,
                killProvenance: killProvenance
            )
        );
    }
    if (active_unit != null && target_unit != null)
    {
        bool causedDefeat = !target_unit.IsAlive();
        _record_effect_metrics(
            active_unit,
            target_unit,
            damage,
            healing,
            causedDefeat ? 1 : 0
        );
        Runtime?._battle_rating_system.RecordContributionFromUnits(
            active_unit,
            target_unit,
            damage,
            healing,
            causedDefeat,
            new StringName("skill"),
            skillId
        );
    }
}

ApplySkillMasteryGrantTyped(target_unit, guardMasteryGrant, batch);
```

`CommitTerminalOutcome` 只是最后一个 committer phase，不是 orchestrator 最后一条语句。禁止把 `BattleSkillExecutionOrchestrator.cs:2233` 移到 `:2195` 之前，也禁止把 `:2197-2203` 的具体技能资源收益移入 committer。`Counterattack` 没有技能专属夹层，按 resolver → hooks → result surface → terminal 调用四阶段；terminal 返回后只允许执行 §10.4.1 的 stateless 武器精通 grant，不调用 command skill mastery、source-bound bonus mastery、last-stand、guard、Vajra 或 target-result mastery。`EquipmentReaction` 不调用前三阶段/result surface，在 resolver 后执行上述 equipment baseline surface，再只调用 terminal defeat policy；误调用由 committer guard 拒绝。它不新增旧路径没有的 contribution/rating/metrics 或 mastery。

### 10.3 query/execute 一致性

- execution 排空时先由专用 factory 构建不可变 attack plan；非法 mode/input 组合不可构造。
- eligibility 使用该 plan 的 availability。
- attempt 成本成功后立即执行同一个 plan，中间不得 yield 或重新选择目标。
- `Execute(plan, batch)` 要求 active root boundary 与同一 batch，为这次独立 immediate action 打开 nested boundary 和新的 logical action；origin 从外层 effect scope 冻结，不在 service 内回落为默认 origin。
- preview 使用同一 service 的 read-only query 路径，但不创建会消费 RNG 的 attack check。

factory 产出的 plan 是 immutable class，公共字段集合固定为：

| plan 字段 | `Counterattack` | `EquipmentReaction` |
|---|---|---|
| `State/SourceUnit/TargetUnit` | 当前 runtime state、反击者、原攻击者 | caller 已校验的 state/source/target |
| `DefinitionAvailable` | definition 已解析且 delivery 为 concrete weapon kind 时 true | referenced skill 含 weapon damage 且当前 weapon projection 可形成 concrete delivery 时 true；projection 缺失/不可用时 false |
| `SkillDefinition/EffectDefinitions` | definition adapter 解析出的当前正式即时武器攻击；effects 在 factory 内复制成 array | caller 传入 definition；effects 在 factory 内复制成 array |
| `DeliveryKind` | factory 按当前冻结武器 projection 计算 | factory 按 definition + 当前冻结武器 projection 计算 |
| `StaminaCost` | provider 返回并由 query/attempt 使用 | `0`，保持现有免费追击 |
| `AttackRollBonus` | captured capability 的 `AttackRollBonus` | `0`，保持当前 `:1503` |
| `TraceSource` | capability instance id | equipment action id |
| `SourceActionId` | capability instance id | equipment action id |
| `WeaponTrainingSkillId` | factory 按创建 logical attack 前的 weapon projection 冻结；只有已映射的 `sword_training/bow_training/unarmed_training`，否则为空 | 不存在；equipment baseline 不新增 mastery |
| equipment attribution | 不存在，由 mode-specific subtype 表达 | `TraitId/BindingId/ActionId/SourceEquipmentInstanceId` 四个非 mutable 值 |

`PrepareCounterattack(...)` 允许返回“definition unavailable”的 typed plan，供
`Query(...)` 产生 `attack_unavailable`；这种 plan 的 `Execute(...)` 必须抛错。
`PrepareEquipmentReaction(...)` 只接受 caller 已按当前 `:1469-1475` 验证的
definition/effects：引用到 non-weapon effects 属于 authored
`immediate_weapon_attack` 契约破坏并 fail fast；effects 合法但当前 weapon projection
缺失/不可用时返回 `DefinitionAvailable=false` 的 typed plan，由 equipment caller 安静
跳过，不把武器损坏或 projection 暂不可用升级成 runtime constructor 异常。两种 plan
不得持有 `ActiveEquipmentAbilityBinding`、payload/action definition 或 capability owner。

mode-specific plan/result 类型固定为：

```csharp
internal readonly record struct
    BattleImmediateWeaponAttackEquipmentAttribution(
        StringName TraitId,
        StringName BindingId,
        StringName ActionId,
        StringName SourceEquipmentInstanceId
    );

internal abstract class BattleImmediateWeaponAttackPlan
{
    protected BattleImmediateWeaponAttackPlan(
        BattleImmediateWeaponAttackMode mode,
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool definitionAvailable,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleAttackDeliveryKind deliveryKind,
        int staminaCost,
        int attackRollBonus,
        StringName traceSource,
        StringName sourceActionId
    )
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        SourceUnit = sourceUnit
            ?? throw new ArgumentNullException(nameof(sourceUnit));
        TargetUnit = targetUnit
            ?? throw new ArgumentNullException(nameof(targetUnit));
        if (traceSource == new StringName(""))
            throw new ArgumentException("trace source is required", nameof(traceSource));
        if (sourceActionId == new StringName(""))
            throw new ArgumentException("source action id is required", nameof(sourceActionId));
        if (staminaCost < 0)
            throw new ArgumentOutOfRangeException(nameof(staminaCost));
        if (definitionAvailable)
        {
            if (
                skillDefinition == null
                || effectDefinitions == null
                || effectDefinitions.Count == 0
                || (
                    deliveryKind != BattleAttackDeliveryKind.MeleeWeapon
                    && deliveryKind != BattleAttackDeliveryKind.RangedWeapon
                )
            )
            {
                throw new ArgumentException(
                    "available plan requires skill/effects/delivery"
                );
            }
        }
        else if (
            skillDefinition != null
            || (effectDefinitions?.Count ?? 0) != 0
            || deliveryKind != BattleAttackDeliveryKind.Unknown
        )
        {
            throw new ArgumentException(
                "unavailable plan cannot carry attack definition"
            );
        }

        Mode = mode;
        DefinitionAvailable = definitionAvailable;
        SkillDefinition = skillDefinition;
        EffectDefinitions = effectDefinitions != null
            ? new List<CombatEffectDefinition>(effectDefinitions).ToArray()
            : Array.Empty<CombatEffectDefinition>();
        DeliveryKind = deliveryKind;
        StaminaCost = staminaCost;
        AttackRollBonus = attackRollBonus;
        TraceSource = traceSource;
        SourceActionId = sourceActionId;
    }

    internal BattleImmediateWeaponAttackMode Mode { get; }
    internal BattleState State { get; }
    internal BattleUnitState SourceUnit { get; }
    internal BattleUnitState TargetUnit { get; }
    internal bool DefinitionAvailable { get; }
    internal SkillDefinition SkillDefinition { get; }
    internal IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    internal BattleAttackDeliveryKind DeliveryKind { get; }
    internal int StaminaCost { get; }
    internal int AttackRollBonus { get; }
    internal StringName TraceSource { get; }
    internal StringName SourceActionId { get; }
}

internal sealed class BattleCounterattackWeaponAttackPlan
    : BattleImmediateWeaponAttackPlan
{
    internal BattleCounterattackWeaponAttackPlan(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool definitionAvailable,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleAttackDeliveryKind deliveryKind,
        int staminaCost,
        int attackRollBonus,
        StringName capabilityInstanceId,
        StringName weaponTrainingSkillId
    ) : base(
        BattleImmediateWeaponAttackMode.Counterattack,
        state,
        sourceUnit,
        targetUnit,
        definitionAvailable,
        skillDefinition,
        effectDefinitions,
        deliveryKind,
        staminaCost,
        attackRollBonus,
        capabilityInstanceId,
        capabilityInstanceId
    )
    {
        if (capabilityInstanceId == new StringName(""))
            throw new ArgumentException(
                "capability instance id is required",
                nameof(capabilityInstanceId)
            );
        CapabilityInstanceId = capabilityInstanceId;
        WeaponTrainingSkillId =
            ProgressionDataUtils.to_string_name(
                weaponTrainingSkillId
            );
        if (
            WeaponTrainingSkillId != new StringName("")
            && !BattleSkillMasteryService.IsWeaponTrainingSkillId(
                WeaponTrainingSkillId
            )
        )
        {
            throw new ArgumentException(
                "counterattack weapon training skill id is invalid",
                nameof(weaponTrainingSkillId)
            );
        }
    }

    internal StringName CapabilityInstanceId { get; }
    internal StringName WeaponTrainingSkillId { get; }
}

internal sealed class BattleEquipmentReactionWeaponAttackPlan
    : BattleImmediateWeaponAttackPlan
{
    internal BattleEquipmentReactionWeaponAttackPlan(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool definitionAvailable,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleAttackDeliveryKind deliveryKind,
        BattleImmediateWeaponAttackEquipmentAttribution equipmentAttribution
    ) : base(
        BattleImmediateWeaponAttackMode.EquipmentReaction,
        state,
        sourceUnit,
        targetUnit,
        definitionAvailable,
        definitionAvailable ? skillDefinition : null,
        definitionAvailable
            ? effectDefinitions
            : Array.Empty<CombatEffectDefinition>(),
        definitionAvailable
            ? deliveryKind
            : BattleAttackDeliveryKind.Unknown,
        0,
        0,
        equipmentAttribution.ActionId,
        equipmentAttribution.ActionId
    )
    {
        EquipmentAttribution = equipmentAttribution;
    }

    internal BattleImmediateWeaponAttackEquipmentAttribution
        EquipmentAttribution { get; }
}

internal sealed class BattleImmediateWeaponAttackResult
{
    private BattleImmediateWeaponAttackResult(
        AttackEffectResolutionResult resolution,
        bool countsTowardMaxAttacks,
        BattleEquipmentAbilityImmediateWeaponAttackResult equipmentSummary
    )
    {
        if (countsTowardMaxAttacks && equipmentSummary == null)
            throw new ArgumentNullException(nameof(equipmentSummary));
        if (!countsTowardMaxAttacks && equipmentSummary != null)
            throw new ArgumentException("uncounted result cannot carry summary");
        Resolution = resolution;
        CountsTowardMaxAttacks = countsTowardMaxAttacks;
        EquipmentSummary = equipmentSummary;
    }

    internal AttackEffectResolutionResult Resolution { get; }
    internal bool CountsTowardMaxAttacks { get; }
    internal BattleEquipmentAbilityImmediateWeaponAttackResult
        EquipmentSummary { get; }

    internal static BattleImmediateWeaponAttackResult ForCounterattack(
        AttackEffectResolutionResult resolution
    ) => new(resolution, false, null);

    internal static BattleImmediateWeaponAttackResult ForEquipmentMiss(
        AttackEffectResolutionResult resolution
    ) => new(resolution, false, null);

    internal static BattleImmediateWeaponAttackResult ForEquipment(
        AttackEffectResolutionResult resolution,
        BattleEquipmentAbilityImmediateWeaponAttackResult summary
    ) => new(resolution, true, summary);
}
```

constructor 已经保证 state/source/target 非 null，trace/source action id 非空；available plan
要求 skill/effects 非空且 delivery 只能是 `MeleeWeapon/RangedWeapon`；两种 unavailable plan
都强制 skill null、empty effects、delivery `Unknown`；counter plan 的
`WeaponTrainingSkillId` 只能为空或属于 `BattleSkillMasteryService` 的正式三值映射。
equipment plan 只有在 authored weapon-effect 契约成立且当前 projection 可形成 concrete
delivery 时 available。effects 在 base constructor 中复制到新 array，之后不再读取原
definition collection。

两个 factory 与 query 的目标代码固定为：

```csharp
internal BattleImmediateWeaponAttackPlan PrepareCounterattack(
    BattleCounterattackImmediateWeaponAttackRequest request
)
{
    if (request == null)
        throw new ArgumentNullException(nameof(request));
    bool resolved = _counterattackDefinitionProvider.TryResolve(
        request.State,
        request.SourceUnit,
        request.Capability,
        out BattleImmediateWeaponAttackDefinition definition
    );
    BattleAttackDeliveryKind deliveryKind =
        resolved && definition != null
            ? BattleAttackDeliveryRules.Resolve(
                definition.EffectDefinitions,
                request.SourceUnit.GetWeaponProjectionReadViewTyped()
            )
            : BattleAttackDeliveryKind.Unknown;
    bool available =
        resolved
        && definition != null
        && (
            deliveryKind == BattleAttackDeliveryKind.MeleeWeapon
            || deliveryKind == BattleAttackDeliveryKind.RangedWeapon
        );
    return new BattleCounterattackWeaponAttackPlan(
        request.State,
        request.SourceUnit,
        request.TargetUnit,
        available,
        available ? definition.SkillDefinition : null,
        available
            ? definition.EffectDefinitions
            : Array.Empty<CombatEffectDefinition>(),
        available ? deliveryKind : BattleAttackDeliveryKind.Unknown,
        available ? definition.StaminaCost : 0,
        request.Capability.AttackRollBonus,
        request.Capability.InstanceId,
        _runtime._skill_mastery_service
            ?.ResolveWeaponTrainingSkillId(request.SourceUnit)
            ?? new StringName("")
    );
}

internal BattleImmediateWeaponAttackPlan PrepareEquipmentReaction(
    BattleEquipmentImmediateWeaponAttackRequest request
)
{
    if (request == null)
        throw new ArgumentNullException(nameof(request));
    IReadOnlyList<CombatEffectDefinition> effectDefinitions =
        request.SkillDefinition.CombatProfile?.EffectDefinitions
        ?? Array.Empty<CombatEffectDefinition>();
    if (!BattleAttackDeliveryRules.IncludesWeaponDamage(effectDefinitions))
    {
        throw new InvalidOperationException(
            "immediate_weapon_attack resolved non-weapon effects"
        );
    }
    BattleAttackDeliveryKind deliveryKind =
        BattleAttackDeliveryRules.Resolve(
            effectDefinitions,
            request.SourceUnit.GetWeaponProjectionReadViewTyped()
        );
    bool available =
        deliveryKind == BattleAttackDeliveryKind.MeleeWeapon
        || deliveryKind == BattleAttackDeliveryKind.RangedWeapon;
    return new BattleEquipmentReactionWeaponAttackPlan(
        request.State,
        request.SourceUnit,
        request.TargetUnit,
        available,
        request.SkillDefinition,
        effectDefinitions,
        deliveryKind,
        new BattleImmediateWeaponAttackEquipmentAttribution(
            request.TraitId,
            request.BindingId,
            request.ActionId,
            request.SourceEquipmentInstanceId
        )
    );
}

internal BattleImmediateWeaponAttackAvailability Query(
    BattleImmediateWeaponAttackPlan plan
)
{
    if (plan == null)
        throw new ArgumentNullException(nameof(plan));
    if (!ReferenceEquals(plan.State, _runtime.GetState()))
    {
        throw new InvalidOperationException(
            "immediate attack plan belongs to another battle"
        );
    }
    if (!plan.DefinitionAvailable)
    {
        return BattleImmediateWeaponAttackAvailability.Blocked(
            BattleImmediateWeaponAttackBlockReason.AttackUnavailable
        );
    }

    int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
        plan.SourceUnit,
        plan.SkillDefinition
    );
    int currentDistance = _runtime.GetGridService().GetDistanceBetweenUnits(
        plan.SourceUnit,
        plan.TargetUnit
    );
    if (currentDistance > effectiveRange)
    {
        return BattleImmediateWeaponAttackAvailability.Blocked(
            BattleImmediateWeaponAttackBlockReason.OutOfReach,
            effectiveRange,
            currentDistance,
            plan.StaminaCost,
            plan.SourceUnit.GetCurrentStamina()
        );
    }

    BattleBarrierInteractionResult barrier =
        _runtime._layered_barrier_service.PreviewSkillBarrierInteractionResult(
            plan.SourceUnit,
            plan.TargetUnit,
            plan.SkillDefinition,
            plan.EffectDefinitions
        );
    if (barrier.Blocked)
    {
        return BattleImmediateWeaponAttackAvailability.Blocked(
            BattleImmediateWeaponAttackBlockReason.BarrierBlocked,
            effectiveRange,
            currentDistance,
            plan.StaminaCost,
            plan.SourceUnit.GetCurrentStamina()
        );
    }
    int currentStamina = plan.SourceUnit.GetCurrentStamina();
    if (currentStamina < plan.StaminaCost)
    {
        return BattleImmediateWeaponAttackAvailability.Blocked(
            BattleImmediateWeaponAttackBlockReason.InsufficientStamina,
            effectiveRange,
            currentDistance,
            plan.StaminaCost,
            currentStamina
        );
    }
    return BattleImmediateWeaponAttackAvailability.Allowed(
        effectiveRange,
        currentDistance,
        plan.StaminaCost,
        currentStamina
    );
}
```

上面的 runtime guard 是最后一道 invariant，不代替 authored content validation。为使
`NonWeapon` 在 production load 后不可达，P1A 同步做下面四个精确改动：

1. `BattleUnitSkillDefinitionExecutionRules` 新增共享 definition predicate；§6.3 的
   `BattleAttackDeliveryRules.IncludesWeaponDamage(...)` 改为一行转发，装备能力 validator
   与 battle runtime 不复制两套武器标志规则：

```csharp
internal static bool IncludesWeaponDamage(
    IEnumerable<CombatEffectDefinition> effectDefinitions
)
{
    foreach (
        CombatEffectDefinition effectDefinition in
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
    )
    {
        if (
            effectDefinition != null
            && (
                effectDefinition.AddWeaponDice
                || effectDefinition.RequiresWeapon
            )
        )
        {
            return true;
        }
    }
    return false;
}
```

2. `EquipmentAbilityContentValidationContext` 把只保存 id 的
   `KnownSkillIds` 一次性替换为 definition map，不保留兼容 alias：

```csharp
public IReadOnlyDictionary<StringName, SkillDefinition>
    KnownSkillDefinitions { get; init; }
```

`ProgressionContentRegistry.BuildEquipmentAbilityValidationContext()` 赋值
`KnownSkillDefinitions = CloneTypedDictionary(_skillDefinitionIndex)`；
`EquipmentAbilityStatusDeclarationCatalog.ExpandWithEquipmentDeclarations(...)` 原样转发该
map；`EquipmentAbilityBindingValidator.ValidateSkillReference(...)` 与
`EquipmentAbilityPayloadValidators` 中 summon known-skill 检查分别从
`KnownSkillIds.Contains(id)` 改成 `KnownSkillDefinitions.ContainsKey(id)`。
`EquipmentAbilityContentRegistry.HasCompleteValidationContext(...)` 同步要求 map 非 null，
并把 `EQA_VALIDATION_CONTEXT_INCOMPLETE` 文本中的 `KnownSkillIds` 改为
`KnownSkillDefinitions`。两个 regression fixture 中手工构造 context 的位置也必须提供
definition map，不并存可能漂移的 set/map 双 owner。

3. `EquipmentAbilityPayloadValidators.ValidateImmediateWeaponAttackPayload(...)` 在
`ValidateSkillReference(...)` 后新增：

```csharp
if (
    context.KnownSkillDefinitions.TryGetValue(
        payload.skill_id,
        out SkillDefinition skillDefinition
    )
    && !BattleUnitSkillDefinitionExecutionRules
        .IncludesWeaponDamage(
            skillDefinition.CombatProfile?.EffectDefinitions
        )
)
{
    EquipmentAbilityContentRegistry.AddError(
        errors,
        "EQA_IMMEDIATE_WEAPON_ATTACK_REQUIRES_WEAPON_DAMAGE",
        $"{path}.payload.skill_id",
        $"skill_id {payload.skill_id} does not resolve weapon damage"
    );
}
```

unknown skill id 仍只由现有 `EQA_REFERENCE_UNKNOWN_SKILL` 报告，不重复增加第二条错误。
正式 glory/memoryeater 两个 payload 都引用 `basic_attack`，其 base effect 带
`AddWeaponDice=true`，因此通过新 validator。

4. equipment caller 不调用通用 `Query(...)` 重新施加 counterattack 专用的
range/barrier/stamina 顺序；目标与 `require_weapon_range` 仍由现有
`CollectImmediateWeaponAttackTargets(...)` 唯一拥有。caller 在
`PrepareEquipmentReaction(...)` 后只检查 `plan.DefinitionAvailable`：false 时
`continue`，true 时才调用 `Execute(...)`。

`BattleImmediateWeaponAttackBlockReason` 在 P1A 只包含 `None/AttackUnavailable/OutOfReach/BarrierBlocked/InsufficientStamina`；`Allowed(...)` 固定四个 check 都为 `Passed`，`Blocked(...)` 固定 `IsAllowed=false` 并把失败点后的 check 留为 `NotEvaluated`。range check 在 barrier preview 之前，barrier preview 在 stamina check 之前，与 §9.3 的 10–13 顺序一致。`Query(...)` 不调用 resolver、attack-check builder、RNG、batch 或任何 commit API。

下面是 `Execute(...)` 的完整目标顺序。这里出现的每个 helper 都在紧随其后的两个 mode 方法中展开，没有隐藏的“补齐完整攻击”占位方法：

```csharp
internal BattleImmediateWeaponAttackResult Execute(
    BattleImmediateWeaponAttackPlan plan,
    BattleEventBatch batch
)
{
    if (plan == null)
        throw new ArgumentNullException(nameof(plan));
    if (batch == null)
        throw new ArgumentNullException(nameof(batch));
    if (!ReferenceEquals(plan.State, _runtime.GetState()))
        throw new InvalidOperationException("immediate attack plan belongs to another battle");
    if (!plan.DefinitionAvailable)
        throw new InvalidOperationException("unavailable immediate attack plan cannot execute");
    if (
        (
            plan is BattleCounterattackWeaponAttackPlan
            && plan.Mode
                != BattleImmediateWeaponAttackMode.Counterattack
        )
        || (
            plan is BattleEquipmentReactionWeaponAttackPlan
            && plan.Mode
                != BattleImmediateWeaponAttackMode.EquipmentReaction
        )
        || (
            plan is not BattleCounterattackWeaponAttackPlan
            and not BattleEquipmentReactionWeaponAttackPlan
        )
    )
    {
        throw new InvalidOperationException(
            "immediate attack plan type/mode mismatch"
        );
    }
    _runtime.RequireActiveReactionBatch(batch);

    using BattleReactionBoundaryScope boundary =
        _runtime.BeginReactionBoundary(batch);
    BattleImmediateWeaponAttackResult execution;
    using (
        BattleLogicalAttackScope logicalAttack =
            _runtime.BeginLogicalAttack(plan.DeliveryKind)
    )
    {

    int previousTargetHp = plan.TargetUnit.GetCurrentHp();
    StringName sourceEventId =
        plan.Mode == BattleImmediateWeaponAttackMode.Counterattack
            ? _runtime.AllocateContingencySourceEventId("counterattack")
            : new StringName("");
    BattleAttackCheckPolicyService attackPolicy =
        _runtime.GetAttackCheckPolicyService()
        ?? throw new InvalidOperationException("attack policy is not bound");
    BattleDamageResolver damageResolver =
        _runtime._damage_resolver
        ?? throw new InvalidOperationException("damage resolver is not bound");
    BattleAttackCheckPolicyContext policyContext =
        attackPolicy.BuildSkillDefinitionAttackContext(
            plan.State,
            plan.SourceUnit,
            plan.TargetUnit,
            plan.SkillDefinition,
            new StringName("skill_attack_check"),
            plan.TraceSource,
            force_hit_no_crit: false
        );
    AttackCheckInput attackCheck = attackPolicy.BuildAttackCheck(
        policyContext,
        plan.AttackRollBonus,
        0
    );
    AttackEffectResolutionResult resolution =
        damageResolver.ResolveAttackEffects(
            plan.SourceUnit,
            plan.TargetUnit,
            plan.EffectDefinitions,
            attackCheck,
            new AttackContext
            {
                BattleState = plan.State,
                SkillId = plan.SkillDefinition.SkillId,
                EventBatch = batch,
                Action = logicalAttack.Context,
            }
        );

    execution =
        plan.Mode switch
        {
            BattleImmediateWeaponAttackMode.Counterattack =>
                CommitCounterattackOutcome(
                    plan,
                    resolution,
                    previousTargetHp,
                    sourceEventId,
                    batch
                ),
            BattleImmediateWeaponAttackMode.EquipmentReaction =>
                CommitEquipmentReactionOutcome(plan, resolution, batch),
            _ => throw new ArgumentOutOfRangeException(
                nameof(plan.Mode),
                plan.Mode,
                null
            ),
        };

    logicalAttack.Complete();
    }
    boundary.Complete();
    return execution;
}
```

这里必须使用显式 `using (...) { ... }` 块，不能把 logical scope 写成与 boundary
同级的 using declaration；nested `boundary.Complete()` 校验 logical depth 已恢复到进入值，
因此顺序必须是 `logicalAttack.Complete()` → logical dispose →
`boundary.Complete()` → boundary dispose。

counterattack mode 没有 shield/special producer，因此 status-id merge 只复制 resolver ids；它仍必须依次经过四阶段：

```csharp
private BattleImmediateWeaponAttackResult CommitCounterattackOutcome(
    BattleImmediateWeaponAttackPlan plan,
    AttackEffectResolutionResult resolution,
    int previousTargetHp,
    StringName sourceEventId,
    BattleEventBatch batch
)
{
    if (
        plan is not BattleCounterattackWeaponAttackPlan
            counterattackPlan
    )
    {
        throw new InvalidOperationException(
            "counterattack outcome requires counterattack plan"
        );
    }
    const BattleWeaponAttackOutcomeKind kind =
        BattleWeaponAttackOutcomeKind.Counterattack;
    _weaponAttackOutcomeCommitter.CommitResolverSurface(
        new BattleWeaponAttackResolverSurfaceRequest(
            kind,
            plan.SourceUnit,
            plan.TargetUnit,
            resolution,
            batch
        )
    );

    var appliedStatusIds = new List<StringName>();
    foreach (StringName statusId in resolution.StatusEffectIds)
    {
        if (
            statusId != new StringName("")
            && !appliedStatusIds.Contains(statusId)
        )
        {
            appliedStatusIds.Add(statusId);
        }
    }
    _weaponAttackOutcomeCommitter.CommitPostProducerHooks(
        new BattleWeaponAttackPostProducerHookRequest(
            kind,
            plan.SourceUnit,
            plan.TargetUnit,
            resolution,
            batch,
            previousTargetHp,
            appliedStatusIds,
            sourceEventId
        )
    );

    if (!resolution.Applied)
    {
        _weaponAttackOutcomeCommitter.CommitUnappliedResultSurface(
            new BattleWeaponAttackUnappliedResultSurfaceRequest(
                kind,
                plan.SourceUnit,
                plan.TargetUnit,
                resolution,
                batch
            )
        );
        return BattleImmediateWeaponAttackResult.ForCounterattack(resolution);
    }

    string sourceLabel =
        string.IsNullOrEmpty(plan.SourceUnit.display_name)
            ? "未知单位"
            : plan.SourceUnit.display_name;
    _weaponAttackOutcomeCommitter.CommitAppliedResultSurface(
        new BattleWeaponAttackAppliedResultSurfaceRequest(
            kind,
            plan.SourceUnit,
            plan.TargetUnit,
            resolution,
            batch,
            $"{sourceLabel} 发起反击",
            plan.TargetUnit.display_name ?? ""
        )
    );
    BattleKillProvenance killProvenance =
        BattleKillProvenance.FromWeaponAttackResult(
            plan.SourceUnit,
            resolution,
            kind,
            plan.SourceActionId
        );
    _weaponAttackOutcomeCommitter.CommitTerminalOutcome(
        new BattleWeaponAttackTerminalOutcomeRequest(
            kind,
            plan.SourceUnit,
            plan.TargetUnit,
            resolution,
            batch,
            plan.SkillDefinition.SkillId,
            killProvenance
        )
    );
    BattleSkillMasteryGrant weaponTrainingGrant =
        _runtime._skill_mastery_service
            ?.BuildCounterattackWeaponTrainingMasteryGrant(
                plan.SourceUnit,
                plan.TargetUnit,
                counterattackPlan.WeaponTrainingSkillId,
                resolution,
                _runtime.GetSkillDefinitionIndexTyped()
            );
    _runtime.ApplySkillMasteryGrantTyped(
        plan.SourceUnit,
        weaponTrainingGrant,
        batch
    );
    return BattleImmediateWeaponAttackResult.ForCounterattack(resolution);
}
```

equipment mode 明确不调用前三阶段/result surface，只迁移旧 `:1517-1557`：

```csharp
private BattleImmediateWeaponAttackResult CommitEquipmentReactionOutcome(
    BattleImmediateWeaponAttackPlan plan,
    AttackEffectResolutionResult resolution,
    BattleEventBatch batch
)
{
    bool countsTowardMaxAttacks =
        resolution.Applied || resolution.AttackSuccess;
    if (!countsTowardMaxAttacks)
        return BattleImmediateWeaponAttackResult.ForEquipmentMiss(resolution);

    if (
        plan is not BattleEquipmentReactionWeaponAttackPlan equipmentPlan
    )
    {
        throw new InvalidOperationException(
            "equipment mode requires equipment plan"
        );
    }
    BattleImmediateWeaponAttackEquipmentAttribution attribution =
        equipmentPlan.EquipmentAttribution;
    var summary = new BattleEquipmentAbilityImmediateWeaponAttackResult
    {
        BindingId = attribution.BindingId,
        ActionId = attribution.ActionId,
        TargetUnitId = plan.TargetUnit.unit_id,
        Applied = resolution.Applied,
        Damage = Math.Max(resolution.Damage, 0),
    };
    batch.AddChangedUnitId(plan.SourceUnit.unit_id);
    batch.AddChangedUnitId(plan.TargetUnit.unit_id);
    foreach (Vector2I coord in plan.TargetUnit.GetOccupiedCoordsTyped())
        batch.AddChangedCoord(coord);
    batch.AddLogLine(
        $"{plan.SourceUnit.display_name} 借 {attribution.TraitId} "
        + $"追击 {plan.TargetUnit.display_name}。"
    );

    const BattleWeaponAttackOutcomeKind kind =
        BattleWeaponAttackOutcomeKind.EquipmentReaction;
    BattleKillProvenance fallback =
        BattleKillProvenance.ForWeaponAttack(
            kind,
            attribution.SourceEquipmentInstanceId,
            attribution.BindingId,
            attribution.ActionId
        );
    BattleKillProvenance killProvenance =
        BattleKillProvenance.FromWeaponAttackResult(
            plan.SourceUnit,
            resolution,
            kind,
            fallback
        );
    _weaponAttackOutcomeCommitter.CommitTerminalOutcome(
        new BattleWeaponAttackTerminalOutcomeRequest(
            kind,
            plan.SourceUnit,
            plan.TargetUnit,
            resolution,
            batch,
            plan.SkillDefinition.SkillId,
            killProvenance
        )
    );
    return BattleImmediateWeaponAttackResult.ForEquipment(
        resolution,
        summary
    );
}
```

`BattleImmediateWeaponAttackResult.ForCounterattack(...)` 固定 `CountsTowardMaxAttacks=false/EquipmentSummary=null`；`ForEquipmentMiss(...)` 固定 `false/null`；`ForEquipment(...)` 要求非 null summary 并固定 `true`。因此 equipment caller 不能观察到 `CountsTowardMaxAttacks=true` 但 summary 为 null 的中间状态。

### 10.4 counterattack mode 的固定语义

| 语义 | 结论 |
|---|---|
| AP | 不消费 |
| cooldown / casting | 不创建、不推进 |
| stamina | 消费 query 得到的正式即时武器攻击成本 |
| counterattack action / definition skill mastery | 不记录，不把反击伪装成一次主动技能施放 |
| weapon training mastery | 正式参与；`resolution.Applied` 且满足 `weapon_attack_quality` 时，给攻击前冻结的武器精通 skill 发放 |
| profession promotion | 武器精通不具备职业升级触发资格；本次 grant 不请求 promotion modal、不冻结 timeline |
| source-bound / last-stand / guard / Vajra / target-result mastery | P1A 不调用；本次只开放武器精通 |
| attack roll / crit / fate | 正式参与 |
| weapon physical/nonphysical damage | 正式参与 |
| equipment attack/damage reactions | 正式参与，保持同步 |
| durability | 正式参与 |
| combo stack | 正式参与 |
| contingency | 允许触发 |
| contribution / rating / metrics | 正式记录，source kind 为 typed `Counterattack` |
| defeat / achievements / kill provenance | 正式提交，provenance 为 typed `Counterattack` |
| 触发新的 counterattack | 禁止，由 origin gate 保证 |

禁止通过一组松散 bool 参数组合这些语义。service 提供专用 `PrepareCounterattack(...)` / `PrepareEquipmentReaction(...)` factory，使非法组合不可构造。

#### 10.4.1 反击的武器精通熟练度

当前主动技能熟练度通过 `BattleSkillMasteryService._resolutionEvents` 跨 target/stage
累计，再由 command 尾部 `_grant_skill_mastery_if_needed(...)` 一次结算。反击不能调用
这条入口：反击发生在 root drain 中，期间的 equipment/contingency/AutoCast 可能进入同一个
service；调用 `Clear()`、`RecordTargetResult(...)` 或
`ResolveActiveSkillMasteryAmount()` 都会清空或混入外层 command 数据。

反击只复用相同的纯规则：

- `basic_attack` 的正式 `mastery_trigger_mode` 必须仍为
  `WeaponAttackQuality`。
- `AttackSuccess == true`，并且 `CriticalHit == true`，或者至少一个真实 weapon
  damage event 满足
  `WeaponDamageDiceIsMax == true && WeaponDamageDiceIsMaxReason == WeaponDiceMax`。
- amount 继续使用 `basic_attack` definition 的正式
  `mastery_amount_mode`；当前是 `PerTargetRank`。
- reward skill 继续使用当前 weapon projection 的
  `sword_training / bow_training / unarmed_training` 映射。
- reward definition 必须带现有 `weapon_training` 标签；该标签由 progression owner
  解释为“可以增长技能等级，但禁止成为职业升级触发技能”。

weapon projection 可能在 resolver 的耐久/装备 reaction 中改变，所以
`PrepareCounterattack(...)` 必须在攻击前把映射结果冻结进
`BattleCounterattackWeaponAttackPlan.WeaponTrainingSkillId`。没有正式映射时冻结空值；
不能在 terminal 后根据已经损坏或替换的装备重新选择训练 skill。

`BattleSkillMasteryService.cs` 将当前
`ResolveMasteryRewardSkillId(...)` 的 weapon-family 分支抽成下面的唯一 owner，并新增
stateless grant builder：

```csharp
internal static bool IsWeaponTrainingSkillId(StringName skillId)
{
    StringName normalized =
        ProgressionDataUtils.to_string_name(skillId);
    return normalized == SwordTrainingSkillId
        || normalized == BowTrainingSkillId
        || normalized == UnarmedTrainingSkillId;
}

internal StringName ResolveWeaponTrainingSkillId(
    BattleUnitState sourceUnit
)
{
    if (sourceUnit == null)
        return new StringName("");
    BattleWeaponProjectionValues weaponProjection =
        sourceUnit.GetWeaponProjectionReadViewTyped().Values;
    StringName weaponFamily =
        ProgressionDataUtils.to_string_name(
            weaponProjection.Family
        );
    if (weaponFamily == new StringName("sword"))
        return SwordTrainingSkillId;
    if (weaponFamily == new StringName("bow"))
        return BowTrainingSkillId;
    if (weaponFamily == new StringName("unarmed"))
        return UnarmedTrainingSkillId;

    StringName weaponKind =
        ProgressionDataUtils.to_string_name(
            weaponProjection.ProfileKind
        );
    if (
        weaponKind
            == BattleUnitState.ToStringName(
                BattleWeaponProfileKind.Unarmed
            )
        || weaponKind
            == BattleUnitState.ToStringName(
                BattleWeaponProfileKind.Natural
            )
    )
    {
        return UnarmedTrainingSkillId;
    }
    return new StringName("");
}

public StringName ResolveMasteryRewardSkillId(
    BattleUnitState sourceUnit,
    StringName skillId
)
{
    StringName normalizedSkillId =
        ProgressionDataUtils.to_string_name(skillId);
    if (normalizedSkillId != BasicAttackSkillId)
        return normalizedSkillId;
    StringName weaponTrainingSkillId =
        ResolveWeaponTrainingSkillId(sourceUnit);
    return weaponTrainingSkillId != new StringName("")
        ? weaponTrainingSkillId
        : normalizedSkillId;
}

internal BattleSkillMasteryGrant
    BuildCounterattackWeaponTrainingMasteryGrant(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName frozenWeaponTrainingSkillId,
        AttackEffectResolutionResult result,
        IReadOnlyDictionary<StringName, SkillDefinition>
            skillDefinitions
    )
{
    StringName masterySkillId =
        ProgressionDataUtils.to_string_name(
            frozenWeaponTrainingSkillId
        );
    if (
        sourceUnit == null
        || targetUnit == null
        || !result.Applied
        || sourceUnit.source_member_id == new StringName("")
        || !IsWeaponTrainingSkillId(masterySkillId)
    )
    {
        return null;
    }
    if (
        !TryGetSkillDefinition(
            skillDefinitions,
            BasicAttackSkillId,
            out SkillDefinition basicAttackDefinition
        )
        || !TryGetSkillDefinition(
            skillDefinitions,
            masterySkillId,
            out _
        )
        || _GetSkillMasteryTriggerMode(basicAttackDefinition)
            != CombatSkillMasteryTriggerMode.WeaponAttackQuality
        || !_IsSkillMasteryQualifyingResult(
            result,
            basicAttackDefinition
        )
    )
    {
        return null;
    }

    int amount = _ResolveSkillMasteryTargetAmount(
        sourceUnit,
        targetUnit,
        basicAttackDefinition
    );
    if (amount <= 0)
        return null;
    return new BattleSkillMasteryGrant
    {
        MemberId = sourceUnit.source_member_id,
        SkillId = masterySkillId,
        Amount = amount,
        SourceType = "battle",
        SourceLabel = "战斗",
        ReasonText = "反击：武器高质量攻击",
        // 现有字段实际映射到 emit_achievement_event，不控制职业晋升。
        AllowUnlocks = true,
    };
}
```

##### 10.4.1.1 `Applied` 是标准最终发放门，不是反击特例

不能只读取
`BattleSkillMasteryService._IsSkillMasteryQualifyingResult(...)` 就得出“标准武器精通不要求
`Applied`”。该纯谓词确实只判断：

```csharp
return result.AttackSuccess
    && (
        result.CriticalHit
        || _ResultHasWeaponDiceMaxEvent(result)
    );
```

但标准单目标技能的正式发放还有两层外部控制。首先
`BattleSkillExecutionOrchestrator._apply_unit_skill_result(...)` 返回的是整个目标结果是否
实际应用：

```csharp
bool applied =
    damageResult.Applied
    || shieldResult.Applied
    || specialResult.Applied;
if (!applied)
{
    // 保留现有 unapplied report/log 提交。
    return false;
}
return true;
```

随后 `_handle_skill_command(...)` 只有在这个返回值为 true 时才提交 accumulator 中的
mastery，并无条件清空未提交事件：

```csharp
bool definitionApplied = _handle_unit_skill_command(
    active_unit,
    command,
    skillDefinition,
    policy?.UnitExecutionCastVariantDefinition,
    policy?.EffectDefinitions,
    batch
);
if (
    definitionApplied
    && ShouldGrantSkillMasteryForCommand(
        command,
        active_unit,
        skillDefinition
    )
)
{
    _grant_skill_mastery_if_needed(
        active_unit,
        skillDefinition,
        batch
    );
}
Runtime?._skill_mastery_service.Clear();
```

`basic_attack` 是单目标 damage definition；counterattack mode 又明确没有 shield/special
producer。因此对这两条路径，最终实际发放条件等价为：

```text
Applied
&& AttackSuccess
&& (CriticalHit || real WeaponDiceMax)
```

所以 `BuildCounterattackWeaponTrainingMasteryGrant(...)` 的 `!result.Applied` 前置条件必须
保留，grant 也必须继续位于 applied terminal outcome 之后。不得把 stateless builder
移动到 `CommitUnappliedResultSurface(...)` 前后并向 unapplied 分支发放。回归必须显式构造
`AttackSuccess=true && CriticalHit=true && Applied=false`，证明局部
`weapon_attack_quality` 谓词虽为 true，最终仍没有 weapon-training progression delta。

##### 10.4.1.2 执行动作、mastery policy 与奖励技能不可互换

反击支持 capability 引用任意合法武器动作，这不意味着该动作 definition 可以改写武器训练
经济。三个值的 owner 和用途固定如下：

| 值 | 唯一用途 | 禁止用途 |
|---|---|---|
| `plan.SkillDefinition` / `WeaponActionDefinitionId` | 执行本次反击的 effects、attack check、range、cost 与 kill provenance | 不决定武器精通 trigger、amount 或 reward skill |
| `BasicAttackSkillId` 对应 definition | 只提供 canonical `WeaponAttackQuality` trigger 与 `PerTargetRank` amount policy | 不替换本次实际执行的 action definition |
| `counterattackPlan.WeaponTrainingSkillId` | 作为 mastery grant 的接收技能，攻击前按 weapon projection 冻结 | 不参与攻击 definition 选择或攻击执行分流 |

因此，无论 `WeaponActionDefinitionId` 是否等于 `basic_attack`，
`BuildCounterattackWeaponTrainingMasteryGrant(...)` 都必须读取
`BasicAttackSkillId` 的 trigger/amount policy，并把结果发给冻结的
`WeaponTrainingSkillId`。该动作 definition 自身的 `mastery_trigger_mode`、
`mastery_amount_mode` 和 skill id 在这条 stateless weapon-training 路径中全部忽略；反击也
继续不获得 action/definition mastery。

这不是 counterattack system 对具体 skill id 的分流：capability selection、query、
attack plan 和 resolver 从不比较 `basic_attack`；只有现有
`BattleSkillMasteryService` 的 canonical weapon-training policy owner 读取该常量。若
`basic_attack` definition 缺失，或其正式 trigger 不再是 `WeaponAttackQuality`，builder
必须 fail closed，不得回退到 action definition 的 mastery profile。

##### 10.4.1.3 武器精通不得触发职业升级

这不是 counterattack-only 例外。普通攻击、反击、书籍、训练或其他来源只要最终增长的是
带 `weapon_training` 标签的技能，就必须共享同一条 progression 规则。禁止把
`AllowUnlocks=false` 当作修复：当前
`BattleRuntimeModule.ApplySkillMasteryGrantTyped(...)` 把该字段传给
`IBattleRatingCharacterGateway.GrantSkillMasteryFromSource(...)` 的最后一个参数，而现有
参数名和实际语义是 `emit_achievement_event`；它只控制
`skill_mastery_gained` achievement，不控制 pending profession choice。

本规则只禁止 weapon-training 充当 `active_level_trigger_core_skill_id` 并由此发起晋升；
不把它强制改成 non-core，也不修改现有 profession tag qualification 或 core assignment
规则。若未来要让武器精通连职业条件、核心技能槽都不能参与，那是另一项 progression
规则变更，不能在本条“不得触发”约束中顺带扩大。

新增 `scripts/systems/progression/SkillProfessionPromotionRules.cs`，以现有标签为
authoring boundary，但把业务判断收口到 typed rule owner：

```csharp
using Godot;

internal static class SkillProfessionPromotionRules
{
    private static readonly StringName WeaponTrainingTag =
        "weapon_training";

    internal static bool IsWeaponTraining(
        SkillDefinition skillDefinition
    ) =>
        skillDefinition?.HasTag(WeaponTrainingTag) == true;

    internal static bool CanTriggerProfessionPromotion(
        SkillDefinition skillDefinition
    ) =>
        skillDefinition != null
        && !IsWeaponTraining(skillDefinition);
}
```

`LevelGrowthEvaluationService.SetActiveTriggerCoreSkillTyped(...)` 在修改
`active_level_trigger_core_skill_id` 前必须加入 definition 与规则检查；检查顺序固定在
`skill_not_learned` 之后、`skill_not_core` 之前：

```csharp
SkillDefinition skillDefinition = GetSkillDefinition(skillId);
if (skillDefinition == null)
{
    return LevelGrowthTriggerResult.Fail(
        "skill_definition_not_found"
    );
}
if (
    !SkillProfessionPromotionRules
        .CanTriggerProfessionPromotion(skillDefinition)
)
{
    return LevelGrowthTriggerResult.Fail(
        "skill_cannot_trigger_profession_promotion"
    );
}
if (!skillProgress.is_core)
    return LevelGrowthTriggerResult.Fail("skill_not_core");
```

只拦设置入口还不够。手工 fixture、损坏状态或实施前已经存在的内存态可能把
weapon-training id 放入 active trigger。三个正式 readiness/preview 读面都要 fail closed，不做
兼容迁移或自动改写：

```csharp
// LevelGrowthEvaluationService.IsActiveTriggerReadyForLevelUp(...)
SkillDefinition skillDefinition = GetSkillDefinition(triggerSkillId);
if (
    skillProgress == null
    || !SkillProfessionPromotionRules
        .CanTriggerProfessionPromotion(skillDefinition)
)
{
    return false;
}
```

```csharp
// ProgressionService.GetReadyActiveLevelTriggerSkillId()
SkillDefinition skillDefinition = GetSkillDefinition(triggerSkillId);
if (
    skillProgress == null
    || !SkillProfessionPromotionRules
        .CanTriggerProfessionPromotion(skillDefinition)
)
{
    return "";
}
```

```csharp
// ProfessionRuleService.GetReadyActiveLevelTriggerSkillId()
SkillDefinition skillDefinition = GetSkillDefinition(triggerSkillId);
if (
    skillProgress == null
    || !SkillProfessionPromotionRules
        .CanTriggerProfessionPromotion(skillDefinition)
)
{
    return "";
}
```

`ProfessionRuleService.cs:533-560` 当前是与 `ProgressionService.cs:1393-1420` 逐句相同的
第二份 readiness 实现，`GetRankUpPreviewAssignedCoreSkillIds(...)` 从这里构造
`CanRankUpProfession(...)` 的 rank-gate 预览。它不是可排除的 UI-only read model：
`weapon_training` 的全局“不允许触发职业升级”规则要求这第三处也消费同一个 typed owner，
否则人工注入的非法 active trigger 仍可能让职业晋升预览返回可用。

这样 weapon-training 不能生成新的 pending profession choice。为保证“本次武器精通
grant 不请求晋升弹窗”在已有其他 pending choice 时也成立，
`CharacterManagementModule._grant_skill_mastery_internal(...)` 在调用
`_fill_delta_from_progression(...)` 后必须过滤**本次返回 delta**：

```csharp
SkillDefinition grantedSkillDefinition =
    GetSkillDefinition(skill_id);
bool canTriggerProfessionPromotion =
    SkillProfessionPromotionRules
        .CanTriggerProfessionPromotion(grantedSkillDefinition);

var progression_service = BuildProgressionService(progression);
var mastery_source_type =
    _resolve_mastery_source_type(source_type);
if (
    !progression_service.GrantSkillMastery(
        skill_id,
        amount,
        mastery_source_type
    )
)
{
    delta.character_level_after =
        delta.character_level_before;
    return delta;
}

// mastery change 构造保持现有代码。
_fill_delta_from_progression(
    delta,
    progression,
    before_skill_levels,
    before_granted_skill_ids,
    before_profession_ranks
);
if (!canTriggerProfessionPromotion)
{
    delta.SetPendingProfessionChoices(
        Array.Empty<PendingProfessionChoice>()
    );
    delta.needs_promotion_modal = false;
}
if (emit_achievement_event)
{
    delta.AppendUnlockedAchievementIds(
        RecordAchievementEvent(
            member_id,
            "skill_mastery_gained",
            amount,
            skill_id
        )
    );
}
return delta;
```

这里只过滤 transaction delta，不调用
`progression.SetPendingProfessionChoices(...)`，因此不会吞掉其他正式核心技能已经产生的
待选职业；它们仍留在 canonical `UnitProgress`，由原有成长/UI 入口处理。武器精通本身
仍可增加 `current_mastery`、`total_mastery_earned`、`mastery_from_battle`、提升
`skill_level`，并继续产生 `skill_mastery_gained` achievement。上面的 achievement block
是当前 `CharacterManagementModule.ContentDefs.cs:332-335` 的原样保留，不得因插入
promotion delta 过滤而删除、提前或重复执行。

`CommitCounterattackOutcome(...)` 的上文完整代码把 grant 放在
`CommitTerminalOutcome(...)` 之后、返回 result 之前。`resolution.Applied == false`
已经从 unapplied 分支提前返回，因此 miss、被完全判定为未应用的结果以及非高质量命中都
不会发放。提交继续走现有
`BattleRuntimeModule.ApplySkillMasteryGrantTyped(...)`，因此 progression delta、技能
刷新和 mastery achievement 仍进入同一个 root batch；但按照上述全局规则，本次
weapon-training delta 的 `needs_promotion_modal` 必须为 false，不得设置
`BattleModalStateKind.PromotionChoice`、`batch.modal_requested` 或
`timeline.frozen`。FIFO drain 仍排到 queue 为空再结束 root。

因此 P1A 不在 `IBattleReactionDrainOwner.Drain(...)` /
`BattleCounterattackSystem.DrainCore(...)` 或其 caller 的
`AdvanceTimeline(...)` 前新增 promotion-specific 分支。该 drain 在本提案中唯一新增的
progression 写入就是上述 weapon-training grant，而它的 progression contract 已保证不能
请求职业晋升 modal；若后续另一个功能向同一 drain 加入可能请求 modal 的奖励或成长写入，
必须把“停止推进 timeline”作为那个新功能的独立架构变更重新设计和回归，不能借
weapon-training 路径隐式获得该语义。

该路径不得调用 `_record_skill_success(...)`、
`BattleRatingSystem.RecordSkillSuccess(...)` 或 `"skill_used"` achievement，也不得把
`CapabilityInstanceId` / `WeaponActionDefinitionId` 当作 mastery skill id。它表达的是
“本次反击使用的武器获得训练熟练度”，不是“施放了一次反击技能”。
这里读取 `BasicAttackSkillId` 只发生在现有 `BattleSkillMasteryService` 的 canonical
weapon-training 规则内，不参与 capability 选择、反击触发、query 或攻击执行分流，因而
不构成 counterattack runtime 的具体 skill-id 特判。

### 10.5 现有即时攻击迁移

为证明 service 确实是 canonical owner，P1A 应把现有装备即时武器攻击改为调用 `EquipmentReaction` mode，而不是保留两份近似实现。

迁移顺序固定为：

1. 先从 `_apply_unit_skill_result(...)` 的 weapon-damage 分支抽出上述四个 committer phase，并让 `StandardWeaponSkillAttack` 在 §10.2 列出的四个原位置调用；非武器分支保持现 owner，回归证明两类标准技能行为与调用顺序都不变。
2. 再迁移 `BattleEquipmentAbilityRuntimeService.ResolveImmediateWeaponAttackAction(...)`：保留 `:1449-1488` 的 context/anchor/content 校验、`CollectImmediateWeaponAttackTargets(...)` 与 `attackCount/MaxAttacks` 循环 owner；只把每个 target 的 `:1489-1557` attack check/resolver/surface/defeat 迁到 `BattleImmediateWeaponAttackService` 的 `EquipmentReaction` mode。
3. 最后接入 `Counterattack` mode；不得先复制旧装备路径再补统计。

装备 caller 的目标代码固定为：

```csharp
foreach (BattleUnitState targetUnit in CollectImmediateWeaponAttackTargets(
    state,
    sourceUnit,
    defeatedUnit,
    anchorUnit,
    payload,
    skillDefinition
))
{
    if (attackCount >= payload.MaxAttacks)
        break;

    BattleImmediateWeaponAttackPlan plan =
        _immediateWeaponAttackService.PrepareEquipmentReaction(
            new BattleEquipmentImmediateWeaponAttackRequest(
                state,
                sourceUnit,
                targetUnit,
                skillDefinition,
                binding?.TraitId ?? "",
                binding?.BindingId ?? "",
                action?.ActionId ?? "",
                activeBinding.Source?.SourceEquipmentInstanceId ?? ""
            )
        );
    if (!plan.DefinitionAvailable)
        continue;
    BattleImmediateWeaponAttackResult execution =
        _immediateWeaponAttackService.Execute(plan, context.Batch);
    if (!execution.CountsTowardMaxAttacks)
        continue;

    attackCount++;
    result?.AddImmediateWeaponAttackResult(execution.EquipmentSummary);
}
```

`CountsTowardMaxAttacks` 必须精确等价于旧代码 `BattleEquipmentAbilityRuntimeService.cs:1517-1520`：`Resolution.Applied || Resolution.AttackSuccess`。miss 仍已经完成 resolver fact 发布，但不递增 `attackCount`，不创建 equipment summary，不写 changed facts/追击日志，也不执行 defeated handling。`EquipmentSummary` 在 `CountsTowardMaxAttacks == true` 时必须非 null；caller 不重新读取 `AttackEffectResolutionResult` 拼第二份 summary。

旧方法各语句的唯一去向如下：

| 当前源码 | P1A owner |
|---|---|
| `BattleEquipmentAbilityRuntimeService.cs:1449-1475` | 保留在 equipment caller：state/source/anchor/payload/content 校验 |
| `:1477-1488`、`CollectImmediateWeaponAttackTargets:1562-1619` | 保留在 equipment caller：目标顺序与 `MaxAttacks` |
| `:1489-1504` | `BattleImmediateWeaponAttackService.PrepareEquipmentReaction/Execute`：正式 attack policy/check |
| `:1505-1516` | immediate service：以当前 root batch、独立 logical action context 调用 resolver |
| `:1517-1520` | immediate result 的 `CountsTowardMaxAttacks` + caller 的 `attackCount++` |
| `:1521-1530` | immediate service 构造 `EquipmentSummary`，caller 只 Add |
| `:1531-1537` | immediate service 的 equipment baseline surface |
| `:1538-1557` | `CommitTerminalOutcome(EquipmentReaction)`；provenance 精确改为 `FromWeaponAttackResult(source, result, EquipmentReaction, ForWeaponAttack(EquipmentReaction, instance, binding, action))` |

这项迁移必须保持装备反应现有同步顺序和既有 outcome policy。旧路径当前缺少 contribution/rating/metrics，因此：

- `Counterattack` mode 仍按 §10.4 的正式语义提交。
- `EquipmentReaction` mode 先保持基线，不在架构重构中静默改变。
- 是否补齐旧装备路径的统计作为独立行为修复，另行确认并加回归。

---

## 十一、递归与级联

### 11.1 允许与禁止

```text
普通攻击
  -> 可触发 counterattack

counterattack attack
  -> 不可直接触发 counterattack
  -> 可触发 equipment reaction
  -> 可触发 contingency

由上述 contingency 产生的 AutoCast
  -> 可触发 counterattack
```

`EquipmentReaction` mode 不 push 新 effect origin，而是在创建自己的 logical action 时冻结
当前父 origin。因此：

- 普通 command 或 AutoCast 直接触发的装备即时攻击保留 `CanTriggerReactions=true`，可以形成
  反击事实。
- counterattack 直接触发的装备即时攻击继承 counterattack origin，
  `CanTriggerReactions=false`，不能绕过 direct-reaction gate 形成 A→B→A 环。
- 装备即时攻击仍使用 typed `EquipmentReaction` outcome/provenance；effect origin 表达因果父链，
  outcome kind 表达本次攻击种类，两者不互相替代。

`CanTriggerReactions=false` 只屏蔽这条直接反击及其同步 equipment attack 子链的反击事实，
不关闭 equipment/contingency 自身。contingency AutoCast 显式 push 新 origin 后重新允许
reaction，正是上图最后一条。

### 11.2 为什么排空必须直到 queue 为空

counterattack 触发 contingency 后，AutoCast 可能产生新的合法反击候选。若 drain 只处理初始 queue count，新 entry 会泄漏到下一 command 或 timeline step。

因此：

- 同一 root boundary 的 nested entry 必须在本次 drain 尾部继续处理。
- 正式终止性来自：direct counterattack 的 reaction gate 关闭、AutoCast 的 contingency gate 关闭、同一 boundary 内不会推进 TU 因而 reaction charge 不会补充、每次 attempt 先消费有限 charge、contingency source-event 去重与 setup consumption。
- equipment reaction 必须继续满足其现有 once-scope/per-battle charge/触发条件约束，但不能把“内容应该不会成环”作为反击系统唯一安全证明。
- `MaxNestedBoundaryDepth` 与 `MaxWorkItems` 只负责把接线/内容错误变成确定性失败，不替代上述正式终止规则，也不用于平衡。
- boundary 结束时 queue 非空属于实现错误。

---

## 十二、preview、HUD 与未来 AI seam

### 12.1 preview DTO、覆盖状态与多段概率

反击风险进入 `BattlePreview`，不是 `BattlePresentationDelta`。P1B 不再使用不存在的
`BattleDamagePreviewRange` 类型，也不把两个可能引用不同 weapon action definition 的
hit/miss capability 压成一个 damage range。目标 contract 固定为：

`BattleCounterattackPreviewContracts.cs` 必须有
`using System; using System.Collections.Generic; using System.Collections.ObjectModel; using System.Linq; using Godot;`；
`BattleCounterattackPreviewService.cs` 必须有
`using System; using System.Collections.Generic; using Godot;`；
`BattleSkillPreviewService.cs` 在现有 imports 上新增 `using System.Linq;`；
`BattlePreview.cs` 在现有 imports 上新增 `using System;`。这些 import 是目标代码的一部分，
不能依赖 implicit usings（当前 project 未把它作为文档 contract）。

```csharp
internal enum BattleCounterattackRiskCoverage
{
    NotEvaluated = 0,
    Complete,
    RandomTargetSelectionUnknown,
    ProducerSequenceUnsupported,
    OutcomeChanceUnsupported
}

internal static class BattleCounterattackRiskCoverageNames
{
    internal static StringName ToStringName(
        BattleCounterattackRiskCoverage value
    ) => value switch
    {
        BattleCounterattackRiskCoverage.NotEvaluated =>
            new StringName("not_evaluated"),
        BattleCounterattackRiskCoverage.Complete =>
            new StringName("complete"),
        BattleCounterattackRiskCoverage
            .RandomTargetSelectionUnknown =>
            new StringName("random_target_selection_unknown"),
        BattleCounterattackRiskCoverage
            .ProducerSequenceUnsupported =>
            new StringName("producer_sequence_unsupported"),
        BattleCounterattackRiskCoverage
            .OutcomeChanceUnsupported =>
            new StringName("outcome_chance_unsupported"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            null
        ),
    };
}

internal readonly record struct
    BattleCounterattackDefinitionDamageRange(
        bool HasDamage,
        int MinDamage,
        int MaxDamage
    )
{
    internal static BattleCounterattackDefinitionDamageRange Empty =>
        new(false, 0, 0);

    internal static BattleCounterattackDefinitionDamageRange From(
        in BattleDamagePreviewRangeService.SkillDamagePreview preview
    ) => preview.HasDamage
        ? new(true, preview.MinDamage, preview.MaxDamage)
        : Empty;
}

internal readonly record struct BattleCounterattackRiskBranch(
    BattleCounterattackTriggerKind TriggerKind,
    StringName CapabilityInstanceId,
    int CapabilityChancePercent,
    BattleCounterattackEligibility Eligibility,
    int StaminaCost,
    BattleCounterattackDefinitionDamageRange DefinitionDamageRange
);

internal readonly record struct BattleCounterattackRiskEntry(
    StringName DefenderUnitId,
    BattleCounterattackRiskBranch? OnHit,
    BattleCounterattackRiskBranch? OnMiss,
    int PotentialCounterattackChanceBasisPoints
)
{
    internal BattleCounterattackDefinitionDamageRange
        ExecutableDefinitionDamageEnvelope =>
            MergeExecutableDamage(OnHit, OnMiss);

    private static BattleCounterattackDefinitionDamageRange
        MergeExecutableDamage(
            BattleCounterattackRiskBranch? left,
            BattleCounterattackRiskBranch? right
        )
    {
        bool leftIncluded =
            left.HasValue
            && left.Value.Eligibility.IsAllowed
            && left.Value.DefinitionDamageRange.HasDamage;
        bool rightIncluded =
            right.HasValue
            && right.Value.Eligibility.IsAllowed
            && right.Value.DefinitionDamageRange.HasDamage;
        if (!leftIncluded && !rightIncluded)
            return BattleCounterattackDefinitionDamageRange.Empty;
        if (!leftIncluded)
            return right.Value.DefinitionDamageRange;
        if (!rightIncluded)
            return left.Value.DefinitionDamageRange;
        return new BattleCounterattackDefinitionDamageRange(
            true,
            Math.Min(
                left.Value.DefinitionDamageRange.MinDamage,
                right.Value.DefinitionDamageRange.MinDamage
            ),
            Math.Max(
                left.Value.DefinitionDamageRange.MaxDamage,
                right.Value.DefinitionDamageRange.MaxDamage
            )
        );
    }
}

internal sealed class BattleCounterattackPreviewTarget
{
    private readonly ReadOnlyCollection<int>
        _stageHitChanceBasisPoints;

    internal BattleCounterattackPreviewTarget(
        StringName defenderUnitId,
        bool producesAttackResolutionFact,
        bool includesWeaponDamage,
        BattleAttackDeliveryKind deliveryKind,
        IReadOnlyList<int> stageHitChanceBasisPoints
    )
    {
        if (defenderUnitId == new StringName(""))
            throw new ArgumentException(
                "counterattack preview defender is required",
                nameof(defenderUnitId)
            );
        int[] copied =
            (stageHitChanceBasisPoints ?? Array.Empty<int>())
                .ToArray();
        if (
            producesAttackResolutionFact
            && includesWeaponDamage
            && deliveryKind == BattleAttackDeliveryKind.MeleeWeapon
            && copied.Length == 0
        )
        {
            throw new ArgumentException(
                "melee attack preview requires an outcome chance profile",
                nameof(stageHitChanceBasisPoints)
            );
        }
        if (copied.Any(value => value < 0 || value > 10_000))
            throw new ArgumentOutOfRangeException(
                nameof(stageHitChanceBasisPoints)
            );
        DefenderUnitId = defenderUnitId;
        ProducesAttackResolutionFact = producesAttackResolutionFact;
        IncludesWeaponDamage = includesWeaponDamage;
        DeliveryKind = deliveryKind;
        _stageHitChanceBasisPoints =
            Array.AsReadOnly(copied);
    }

    internal StringName DefenderUnitId { get; }
    internal bool ProducesAttackResolutionFact { get; }
    internal bool IncludesWeaponDamage { get; }
    internal BattleAttackDeliveryKind DeliveryKind { get; }
    internal IReadOnlyList<int> StageHitChanceBasisPoints =>
        _stageHitChanceBasisPoints;
}

internal sealed class BattleCounterattackRiskProjection
{
    private readonly ReadOnlyCollection<BattleCounterattackRiskEntry>
        _entries;

    internal BattleCounterattackRiskProjection(
        BattleCounterattackRiskCoverage coverage,
        IReadOnlyList<BattleCounterattackRiskEntry> entries,
        long potentialExpectedCountBasisPoints
    )
    {
        if (!Enum.IsDefined(coverage))
            throw new ArgumentOutOfRangeException(nameof(coverage));
        if (potentialExpectedCountBasisPoints < 0)
            throw new ArgumentOutOfRangeException(
                nameof(potentialExpectedCountBasisPoints)
            );
        BattleCounterattackRiskEntry[] copied =
            (entries ?? Array.Empty<BattleCounterattackRiskEntry>())
                .ToArray();
        if (
            coverage != BattleCounterattackRiskCoverage.Complete
            && (
                copied.Length != 0
                || potentialExpectedCountBasisPoints != 0
            )
        )
        {
            throw new ArgumentException(
                "incomplete risk coverage cannot carry numeric entries"
            );
        }
        long computedExpectedCountBasisPoints = 0;
        foreach (BattleCounterattackRiskEntry entry in copied)
        {
            if (
                entry.DefenderUnitId == new StringName("")
                || (!entry.OnHit.HasValue && !entry.OnMiss.HasValue)
                || (
                    entry.OnHit.HasValue
                    && (
                        entry.OnHit.Value.TriggerKind
                            != BattleCounterattackTriggerKind
                                .MeleeHitReceived
                        || entry.OnHit.Value.CapabilityInstanceId
                            == new StringName("")
                        || entry.OnHit.Value.CapabilityChancePercent < 0
                        || entry.OnHit.Value.CapabilityChancePercent > 100
                    )
                )
                || (
                    entry.OnMiss.HasValue
                    && (
                        entry.OnMiss.Value.TriggerKind
                            != BattleCounterattackTriggerKind
                                .MeleeAttackEvaded
                        || entry.OnMiss.Value.CapabilityInstanceId
                            == new StringName("")
                        || entry.OnMiss.Value.CapabilityChancePercent < 0
                        || entry.OnMiss.Value.CapabilityChancePercent > 100
                    )
                )
                || entry.PotentialCounterattackChanceBasisPoints < 0
                || entry.PotentialCounterattackChanceBasisPoints > 10_000
            )
            {
                throw new ArgumentException(
                    "counterattack risk entry is invalid",
                    nameof(entries)
                );
            }
            computedExpectedCountBasisPoints = checked(
                computedExpectedCountBasisPoints
                + entry.PotentialCounterattackChanceBasisPoints
            );
        }
        if (
            computedExpectedCountBasisPoints
            != potentialExpectedCountBasisPoints
        )
        {
            throw new ArgumentException(
                "counterattack risk aggregate does not match entries",
                nameof(potentialExpectedCountBasisPoints)
            );
        }
        Coverage = coverage;
        _entries = Array.AsReadOnly(copied);
        PotentialExpectedCountBasisPoints =
            potentialExpectedCountBasisPoints;
    }

    internal BattleCounterattackRiskCoverage Coverage { get; }
    internal IReadOnlyList<BattleCounterattackRiskEntry> Entries =>
        _entries;
    internal long PotentialExpectedCountBasisPoints { get; }

    internal static BattleCounterattackRiskProjection Empty(
        BattleCounterattackRiskCoverage coverage
    ) => new(
        coverage,
        Array.Empty<BattleCounterattackRiskEntry>(),
        0
    );
}
```

`BattleCounterattackRiskCoverage.Complete` 只表示当前 producer 形态、目标集合和
hit/miss branch 已被这套 projection 完整覆盖；它不表示正式排空前的状态转移已知，也
不表示反击一定发生。`Complete + empty entries` 才表示在当前状态与当前 producer
输入下没有可建模的反击机会；任何尚未建模的 producer/outcome 必须返回对应 typed
unknown，不能降级成 `Complete`。

`DefinitionDamageRange` 只复用当前
`BattleDamagePreviewRangeService.BuildSkillDamagePreview(...)` 的 definition +
weapon-dice 范围；它明确不声称包含暴击、屏障吸收、条件装备 bonus dice 或后续 hook。
HUD 文案只能称“基础伤害范围”。在 full-fidelity damage preview owner 落地前，不得把它
改名为 total/expected damage。

`PotentialCounterattackChanceBasisPoints` 不是简单的
`hit × hit-capability + miss × miss-capability`。repeat/multi-stage 在某一 trigger
第一次找到 capability 时就 reserve dedupe key；即使该 branch 随后被资源、射程或概率
挡住，也不会在后续 stage 重试。因此唯一公式实现放在 preview service：

```csharp
private static int ComputePotentialCounterattackChanceBasisPoints(
    IReadOnlyList<int> stageHitChanceBasisPoints,
    BattleCounterattackRiskBranch? onHit,
    BattleCounterattackRiskBranch? onMiss
)
{
    decimal unresolvedProbability = 1m;
    decimal counterattackProbability = 0m;
    foreach (
        int hitChanceBasisPoints
            in stageHitChanceBasisPoints
                ?? Array.Empty<int>()
    )
    {
        decimal hitProbability =
            Math.Clamp(hitChanceBasisPoints, 0, 10_000)
            / 10_000m;
        decimal missProbability = 1m - hitProbability;

        if (onHit.HasValue && onHit.Value.Eligibility.IsAllowed)
        {
            counterattackProbability +=
                unresolvedProbability
                * hitProbability
                * onHit.Value.CapabilityChancePercent
                / 100m;
        }
        if (onMiss.HasValue && onMiss.Value.Eligibility.IsAllowed)
        {
            counterattackProbability +=
                unresolvedProbability
                * missProbability
                * onMiss.Value.CapabilityChancePercent
                / 100m;
        }

        decimal continuationProbability = 0m;
        if (!onHit.HasValue)
            continuationProbability += hitProbability;
        if (!onMiss.HasValue)
            continuationProbability += missProbability;
        unresolvedProbability *= continuationProbability;
        if (unresolvedProbability == 0m)
            break;
    }
    return Math.Clamp(
        decimal.ToInt32(
            decimal.Round(
                counterattackProbability * 10_000m,
                0,
                MidpointRounding.AwayFromZero
            )
        ),
        0,
        10_000
    );
}
```

该值是按 preview 当下的 position、stamina、reaction charge、硬控、lock 与 capability
计算的条件估计，并假设 defender 在原攻击后存活。正式执行要到 producer 全部后处理后
才复核；原攻击可能移动单位、改变资源/状态或增删 capability，所以该值不是数学意义的
严格上界或下界。P1B 不伪造这些转移概率，UI 必须写“按当前状态的潜在反击”，不能写
“必定承受”。

### 12.2 `BattleCounterattackPreviewService` 完整只读实现

服务是 runtime borrower，同时借用 P1A 的 query service 与 immediate attack
service；它不打开 boundary/action、构造 fact、入队、提交 cost 或读取 RNG：

```csharp
internal sealed class BattleCounterattackPreviewService
    : BattleRuntimeModuleBorrower
{
    private readonly BattleCounterattackQueryService _queryService;
    private readonly BattleImmediateWeaponAttackService
        _immediateWeaponAttackService;

    internal BattleCounterattackPreviewService(
        BattleCounterattackQueryService queryService,
        BattleImmediateWeaponAttackService immediateWeaponAttackService
    )
    {
        _queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        _immediateWeaponAttackService =
            immediateWeaponAttackService
            ?? throw new ArgumentNullException(
                nameof(immediateWeaponAttackService)
            );
    }

    internal BattleCounterattackRiskProjection Build(
        BattleState state,
        StringName originalAttackerUnitId,
        BattleCounterattackRiskCoverage coverage,
        IReadOnlyList<BattleCounterattackPreviewTarget> targets
    )
    {
        if (!ReferenceEquals(state, _runtime.GetState()))
        {
            throw new InvalidOperationException(
                "counterattack preview belongs to another battle"
            );
        }
        if (coverage == BattleCounterattackRiskCoverage.NotEvaluated)
            throw new ArgumentException(
                "preview service requires an evaluated coverage",
                nameof(coverage)
            );
        if (coverage != BattleCounterattackRiskCoverage.Complete)
            return BattleCounterattackRiskProjection.Empty(coverage);
        if (
            !state.TryGetUnitTyped(
                originalAttackerUnitId,
                out BattleUnitState originalAttacker
            )
            || originalAttacker == null
        )
        {
            throw new InvalidOperationException(
                "counterattack preview attacker is not present"
            );
        }

        var seenDefenderIds = new HashSet<StringName>();
        var entries = new List<BattleCounterattackRiskEntry>();
        long aggregateBasisPoints = 0;
        foreach (
            BattleCounterattackPreviewTarget target
                in targets
                    ?? Array.Empty<BattleCounterattackPreviewTarget>()
        )
        {
            if (
                target == null
                || !seenDefenderIds.Add(target.DefenderUnitId)
                || !target.ProducesAttackResolutionFact
                || !target.IncludesWeaponDamage
                || target.DeliveryKind
                    != BattleAttackDeliveryKind.MeleeWeapon
            )
            {
                continue;
            }
            if (
                !state.TryGetUnitTyped(
                    target.DefenderUnitId,
                    out BattleUnitState defender
                )
                || defender == null
            )
            {
                throw new InvalidOperationException(
                    "counterattack preview defender is not present"
                );
            }

            BattleCounterattackEligibility actorPair =
                BattleCounterattackRules.EvaluateActorPair(
                    _queryService.BuildActorPairFacts(
                        defender,
                        originalAttacker
                    )
                );
            BattleCounterattackRiskBranch? onHit = BuildBranch(
                state,
                defender,
                originalAttacker,
                BattleCounterattackTriggerKind.MeleeHitReceived,
                actorPair
            );
            BattleCounterattackRiskBranch? onMiss = BuildBranch(
                state,
                defender,
                originalAttacker,
                BattleCounterattackTriggerKind.MeleeAttackEvaded,
                actorPair
            );
            if (!onHit.HasValue && !onMiss.HasValue)
                continue;

            int chanceBasisPoints =
                ComputePotentialCounterattackChanceBasisPoints(
                    target.StageHitChanceBasisPoints,
                    onHit,
                    onMiss
                );
            entries.Add(
                new BattleCounterattackRiskEntry(
                    defender.unit_id,
                    onHit,
                    onMiss,
                    chanceBasisPoints
                )
            );
            aggregateBasisPoints = checked(
                aggregateBasisPoints + chanceBasisPoints
            );
        }
        return new BattleCounterattackRiskProjection(
            BattleCounterattackRiskCoverage.Complete,
            entries,
            aggregateBasisPoints
        );
    }

    private BattleCounterattackRiskBranch? BuildBranch(
        BattleState state,
        BattleUnitState defender,
        BattleUnitState originalAttacker,
        BattleCounterattackTriggerKind triggerKind,
        BattleCounterattackEligibility actorPair
    )
    {
        IReadOnlyList<BattleCounterattackCapability> candidates =
            defender.GetCounterattackCandidatesTyped(triggerKind);
        if (candidates.Count == 0)
            return null;
        BattleCounterattackCapability capability = candidates[0];
        BattleImmediateWeaponAttackPlan plan =
            _immediateWeaponAttackService.PrepareCounterattack(
                new BattleCounterattackImmediateWeaponAttackRequest(
                    state,
                    defender,
                    originalAttacker,
                    capability
                )
            );
        BattleImmediateWeaponAttackAvailability availability =
            _immediateWeaponAttackService.Query(plan);
        BattleCounterattackEligibility eligibility =
            actorPair.IsAllowed
                ? BattleCounterattackRules.EvaluateAttemptReadiness(
                    _queryService.BuildAttemptReadinessFacts(
                        defender,
                        availability
                    )
                )
                : actorPair;
        BattleCounterattackDefinitionDamageRange damageRange =
            plan.DefinitionAvailable
                ? BattleCounterattackDefinitionDamageRange.From(
                    BattleDamagePreviewRangeService
                        .BuildSkillDamagePreview(
                            defender,
                            plan.EffectDefinitions
                        )
                )
                : BattleCounterattackDefinitionDamageRange.Empty;
        return new BattleCounterattackRiskBranch(
            triggerKind,
            capability.InstanceId,
            capability.ChancePercent,
            eligibility,
            plan.StaminaCost,
            damageRange
        );
    }

    // 此方法体必须使用 §12.1 的唯一公式，不在其他文件复制。
    private static int ComputePotentialCounterattackChanceBasisPoints(
        IReadOnlyList<int> stageHitChanceBasisPoints,
        BattleCounterattackRiskBranch? onHit,
        BattleCounterattackRiskBranch? onMiss
    )
    {
        decimal unresolvedProbability = 1m;
        decimal counterattackProbability = 0m;
        foreach (
            int hitChanceBasisPoints
                in stageHitChanceBasisPoints
                    ?? Array.Empty<int>()
        )
        {
            decimal hitProbability =
                Math.Clamp(hitChanceBasisPoints, 0, 10_000)
                / 10_000m;
            decimal missProbability = 1m - hitProbability;
            if (
                onHit.HasValue
                && onHit.Value.Eligibility.IsAllowed
            )
            {
                counterattackProbability +=
                    unresolvedProbability
                    * hitProbability
                    * onHit.Value.CapabilityChancePercent
                    / 100m;
            }
            if (
                onMiss.HasValue
                && onMiss.Value.Eligibility.IsAllowed
            )
            {
                counterattackProbability +=
                    unresolvedProbability
                    * missProbability
                    * onMiss.Value.CapabilityChancePercent
                    / 100m;
            }
            decimal continuationProbability = 0m;
            if (!onHit.HasValue)
                continuationProbability += hitProbability;
            if (!onMiss.HasValue)
                continuationProbability += missProbability;
            unresolvedProbability *= continuationProbability;
            if (unresolvedProbability == 0m)
                break;
        }
        return Math.Clamp(
            decimal.ToInt32(
                decimal.Round(
                    counterattackProbability * 10_000m,
                    0,
                    MidpointRounding.AwayFromZero
                )
            ),
            0,
            10_000
        );
    }
}
```

文档在 §12.1 单独解释公式，在目标 class 中再次给出完整 method body；实际源码只保留
`BattleCounterattackPreviewService.cs` 内这一份实现。

### 12.3 producer 如何生成 outcome profile

只有 producer 知道“是否真正调用 `ResolveAttackEffects(...)`”以及 stage 顺序。preview
service 不得仅凭 `AddWeaponDice` 猜测会发布 fact。`BattleSkillPreviewService` 新增：

```csharp
private static IReadOnlyList<int>
    BuildStageHitChanceBasisPoints(
        AttackPreviewData attackPreview
    )
{
    if (attackPreview?.Stages?.Count > 0)
    {
        return Array.AsReadOnly(
            attackPreview.Stages
                .Select(stage =>
                    checked(
                        Math.Clamp(
                            stage.SuccessRatePercent,
                            0,
                            100
                        ) * 100
                    )
                )
                .ToArray()
        );
    }
    if (attackPreview == null)
        return Array.Empty<int>();
    return Array.AsReadOnly(
        new[]
        {
            checked(
                Math.Clamp(
                    attackPreview.SuccessRatePercent,
                    0,
                    100
                ) * 100
            ),
        }
    );
}

private void PopulateUnitSkillCounterattackRisk(
    BattleUnitReadView activeUnit,
    IReadOnlyList<BattleUnitReadView> previewTargetUnits,
    SkillDefinition skillDefinition,
    CombatCastVariantDefinition castVariantDefinition,
    bool isRandomChain,
    BattlePreview preview
)
{
    BattleRuntimeModule runtime = Runtime
        ?? throw new InvalidOperationException(
            "battle runtime is not bound"
        );
    BattleState state = _owner.RtState()
        ?? throw new InvalidOperationException(
            "battle state is not bound"
        );
    if (
        !state.TryGetUnitTyped(
            activeUnit.UnitId,
            out BattleUnitState activeUnitState
        )
        || activeUnitState == null
    )
    {
        throw new InvalidOperationException(
            "preview source unit is not present"
        );
    }
    IReadOnlyList<CombatEffectDefinition> effects =
        _owner.CollectUnitSkillEffectDefinitions(
            skillDefinition,
            castVariantDefinition,
            activeUnit
        );
    BattleAttackDeliveryKind deliveryKind =
        BattleAttackDeliveryRules.Resolve(
            effects,
            activeUnitState.GetWeaponProjectionReadViewTyped()
        );
    bool includesWeaponDamage =
        BattleAttackDeliveryRules.IncludesWeaponDamage(effects);
    CombatEffectDefinition repeatAttackEffect =
        runtime._repeat_attack_resolver
            ?.get_repeat_attack_effect_def(effects);
    if (isRandomChain)
    {
        bool producesAnyAttackFact =
            repeatAttackEffect != null
            || (
                previewTargetUnits
                    ?? Array.Empty<BattleUnitReadView>()
            ).Any(targetUnit =>
                runtime._skill_resolution_rules
                    ?.ShouldResolveUnitSkillAsFateAttack(
                        activeUnit,
                        targetUnit,
                        skillDefinition,
                        effects
                    )
                == true
            );
        preview.SetCounterattackRisk(
            BattleCounterattackRiskProjection.Empty(
                producesAnyAttackFact
                && includesWeaponDamage
                && deliveryKind
                    == BattleAttackDeliveryKind.MeleeWeapon
                    ? BattleCounterattackRiskCoverage
                        .RandomTargetSelectionUnknown
                    : BattleCounterattackRiskCoverage.Complete
            )
        );
        return;
    }
    var targets =
        new List<BattleCounterattackPreviewTarget>();
    foreach (
        BattleUnitReadView targetUnit
            in previewTargetUnits
                ?? Array.Empty<BattleUnitReadView>()
    )
    {
        bool producesAttackFact =
            repeatAttackEffect != null
            || (
                runtime._skill_resolution_rules
                    ?.ShouldResolveUnitSkillAsFateAttack(
                        activeUnit,
                        targetUnit,
                        skillDefinition,
                        effects
                    )
                == true
            );
        AttackPreviewData targetAttackPreview =
            producesAttackFact
                ? _owner._build_unit_skill_hit_preview(
                    activeUnit,
                    new[] { targetUnit },
                    skillDefinition,
                    castVariantDefinition
                )
                : null;
        IReadOnlyList<int> stageChances =
            BuildStageHitChanceBasisPoints(targetAttackPreview);
        if (
            producesAttackFact
            && includesWeaponDamage
            && deliveryKind == BattleAttackDeliveryKind.MeleeWeapon
            && stageChances.Count == 0
        )
        {
            preview.SetCounterattackRisk(
                BattleCounterattackRiskProjection.Empty(
                    BattleCounterattackRiskCoverage
                        .OutcomeChanceUnsupported
                )
            );
            return;
        }
        targets.Add(
            new BattleCounterattackPreviewTarget(
                targetUnit.UnitId,
                producesAttackFact,
                includesWeaponDamage,
                deliveryKind,
                stageChances
            )
        );
    }
    preview.SetCounterattackRisk(
        runtime._moduleBorrowers.CounterattackPreview.Build(
            state,
            activeUnit.UnitId,
            BattleCounterattackRiskCoverage.Complete,
            targets
        )
    );
}
```

精确调用点是
`BattleSkillPreviewService._preview_unit_skill_command_impl(...)` 当前
`:385-419` 的 `if (preview.allowed)` 内：完成 `hit_preview`、damage preview 与 save
branch 后，在进入 `preview:unit_skill.log_lines` 之前调用：

```csharp
PopulateUnitSkillCounterattackRisk(
    active_unit,
    previewTargetUnits,
    skillDefinition,
    castVariantDefinition,
    isRandomChain,
    preview
);
```

当前 random-chain preview 没有每个候选被选择的精确概率，因此必须返回
`RandomTargetSelectionUnknown`，不能把所有候选都按 100% 目标相加。当前 ground/charge
preview 又把普通 ground unit effects 与 path-step effects 合并成 target id 集合，丢失了
每个 target 的 resolver stage 顺序；在该结构重做前，精确接线为：

```csharp
// _preview_ground_skill_command_impl(...) 当前 :698-709 之后、
// preview:ground_skill.log_lines 之前。
BattleState riskState = _owner.RtState()
    ?? throw new InvalidOperationException(
        "battle state is not bound"
    );
if (
    !riskState.TryGetUnitTyped(
        active_unit.UnitId,
        out BattleUnitState riskSourceUnit
    )
    || riskSourceUnit == null
)
{
    throw new InvalidOperationException(
        "preview source unit is not present"
    );
}
BattleUnitWeaponProjectionReadView riskWeaponProjection =
    riskSourceUnit.GetWeaponProjectionReadViewTyped();
bool hasGroundMeleeAttackFact =
    BattleGroundEffectService.ShouldResolveGroundEffectsAsAttack(
        previewUnitEffectDefinitions
    )
    && BattleAttackDeliveryRules.Resolve(
        previewUnitEffectDefinitions,
        riskWeaponProjection
    ) == BattleAttackDeliveryKind.MeleeWeapon;
bool hasPathStepMeleeAttackFact =
    pathStepAoeEffect?.ResolveAsWeaponAttack == true
    && BattleAttackDeliveryRules.Resolve(
        new[] { pathStepAoeEffect },
        riskWeaponProjection
    ) == BattleAttackDeliveryKind.MeleeWeapon;
preview.SetCounterattackRisk(
    BattleCounterattackRiskProjection.Empty(
        hasGroundMeleeAttackFact
        || hasPathStepMeleeAttackFact
            ? BattleCounterattackRiskCoverage
                .ProducerSequenceUnsupported
            : BattleCounterattackRiskCoverage.Complete
    )
);
```

为使这段可直接编译且只解析一次 path-step effect，在
`previewUnitEffectDefinitions` / `previewUnitEffectCoords` 两个方法级 local 后新增：

```csharp
CombatEffectDefinition pathStepAoeEffect = null;
```

当前 `:599` 的 `CombatEffectDefinition pathStepAoeEffect = ...` 改成对该方法级 local 的
assignment；当前 `:660-665` 的第二次同名声明与重复
`GetChargePathStepAoeEffectDefinition(...)` 调用整段删除，后续 `if
(pathStepAoeEffect != null)` 直接复用第一次结果。不能只修改 `:660`，否则 `:599` 的
block-local 会与方法级 local 形成 CS0136。`previewUnitEffectDefinitions` 已是方法级
local。该 P1B 切片宁可显式显示“未计算”，也不发布错误的零风险。未来补 ground/charge
时，必须先让 preview owner 产出按实际 resolver 调用顺序排列的 per-target stage
profile，再把 coverage 改成 `Complete`。

### 12.4 `BattlePreview` storage 与 public projection

`BattlePreview.cs` 新增一个已经 defensive-copy 的 projection owner，不再并行维护 entry
list 与 aggregate：

```csharp
private BattleCounterattackRiskProjection _counterattackRisk =
    BattleCounterattackRiskProjection.Empty(
        BattleCounterattackRiskCoverage.NotEvaluated
    );

internal BattleCounterattackRiskProjection CounterattackRiskTyped =>
    _counterattackRisk;

internal void SetCounterattackRisk(
    BattleCounterattackRiskProjection value
)
{
    _counterattackRisk = value
        ?? throw new ArgumentNullException(nameof(value));
}

internal void ClearCounterattackRisk()
{
    _counterattackRisk =
        BattleCounterattackRiskProjection.Empty(
            BattleCounterattackRiskCoverage.NotEvaluated
        );
}
```

`BattleCommandPreviewService.PreviewCommand(...)` 创建的是全新 `BattlePreview`，因此无需
额外 clear；所有 allowed unit/ground skill 分支必须按 §12.3 设置一次，blocked preview
保持 `NotEvaluated`。`BattlePreviewProjection.WriteInto(...)` 在
`damage_preview` 后写入三个稳定字段：

```csharp
BattleCounterattackRiskProjection counterattackRisk =
    preview.CounterattackRiskTyped;
target["counterattack_risk_coverage"] =
    BattleCounterattackRiskCoverageNames.ToStringName(
        counterattackRisk.Coverage
    );
target["counterattack_potential_expected_count_basis_points"] =
    counterattackRisk.PotentialExpectedCountBasisPoints;
target["counterattack_risk_entries"] =
    WriteCounterattackRiskEntries(
        lease,
        counterattackRisk.Entries,
        "BattlePreviewProjection.counterattack_risk_entries"
    );
```

`WriteCounterattackRiskEntries(...)` 放在 `BattlePreviewProjection` class level，不能嵌套在
`WriteInto(...)` method body：

```csharp
private static GArray WriteCounterattackRiskEntries<TLeaseRoot>(
    GodotProjectionLease<TLeaseRoot> lease,
    IReadOnlyList<BattleCounterattackRiskEntry> entries,
    string reason
)
    where TLeaseRoot : class, IDisposable
{
    GArray result = lease.Own(new GArray(), reason);
    for (
        int index = 0;
        index < (entries?.Count ?? 0);
        index++
    )
    {
        BattleCounterattackRiskEntry entry = entries[index];
        BattleCounterattackDefinitionDamageRange damage =
            entry.ExecutableDefinitionDamageEnvelope;
        GDictionary item = lease.Own(
            new GDictionary(),
            $"{reason}[{index}]"
        );
        item["defender_unit_id"] = entry.DefenderUnitId;
        item["potential_counterattack_chance_basis_points"] =
            entry.PotentialCounterattackChanceBasisPoints;
        item["has_damage"] = damage.HasDamage;
        item["min_damage"] = damage.MinDamage;
        item["max_damage"] = damage.MaxDamage;
        result.Add(item);
    }
    return result;
}
```

Godot projection 的 entry 只暴露 `defender_unit_id`、
`potential_counterattack_chance_basis_points` 与 executable definition-damage
envelope 的 `has_damage/min_damage/max_damage`；capability instance id、block reason、
stamina 与敌方精确 budget 不跨 presentation boundary。对应
`WriteCounterattackRiskEntries(...)` 必须创建 lease-owned `GArray/GDictionary`，不能把
typed record 或 live unit 塞入 Godot collection。

### 12.5 HUD snapshot 的精确字段

展示链固定为：

```text
BattleCounterattackPreviewService
  -> immutable BattleCounterattackRiskProjection
  -> BattlePreview
  -> BattleHudAdapter
  -> detached BattleHudSnapshot
```

`BattleHudSnapshot.cs` 新增：

```csharp
internal sealed record BattleHudReactionBudgetSnapshot(
    bool Visible,
    int ChargesRemaining,
    int ChargeCapacity,
    int NextRechargeAtTu
) : IBattlePresentationSnapshotValue
{
    internal static BattleHudReactionBudgetSnapshot Hidden { get; } =
        new(false, 0, 0, 0);

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("visible", Visible),
            ("charges_remaining", ChargesRemaining),
            ("charge_capacity", ChargeCapacity),
            ("next_recharge_at_tu", NextRechargeAtTu)
        );
}

internal sealed record BattleHudCounterattackRiskEntrySnapshot(
    string DefenderUnitId,
    int PotentialChanceBasisPoints,
    bool HasDefinitionDamage,
    int DefinitionDamageMin,
    int DefinitionDamageMax
) : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("defender_unit_id", DefenderUnitId ?? ""),
            (
                "potential_chance_basis_points",
                PotentialChanceBasisPoints
            ),
            ("has_definition_damage", HasDefinitionDamage),
            ("definition_damage_min", DefinitionDamageMin),
            ("definition_damage_max", DefinitionDamageMax)
        );
}
```

`BattleHudFocusUnitSnapshot` 现有最后一个参数
`IReadOnlyList<BattleHudStatusEffectSnapshot> StatusEffects = null` 是 optional；
required 参数不能追加在它后面。精确改签是在 `StatusEffects` **之前**插入
`BattleHudReactionBudgetSnapshot ReactionBudget`，保留 `StatusEffects` 最后，
`CanonicalFacts` 新增 `("reaction_budget", ReactionBudget)`；不加
optional/default overload。adapter 的唯一 builder 为：

```csharp
private static BattleHudReactionBudgetSnapshot
    BuildReactionBudgetSnapshot(BattleUnitState unitState)
{
    // P1B 固定 policy：只展示 party-backed unit 的精确预算。
    if (
        unitState == null
        || unitState.source_member_id == new StringName("")
    )
    {
        return BattleHudReactionBudgetSnapshot.Hidden;
    }
    BattleUnitReactionSnapshot snapshot =
        unitState.CaptureReactionRawTyped();
    return snapshot.OwnerPresent
        ? new BattleHudReactionBudgetSnapshot(
            true,
            snapshot.ChargesRemaining,
            snapshot.ChargeCapacity,
            snapshot.NextRechargeAtTu
        )
        : BattleHudReactionBudgetSnapshot.Hidden;
}
```

`BuildFocusUnitSnapshot(...)` 的 null 分支在 `MoveMax` 后显式传
`BattleHudReactionBudgetSnapshot.Hidden`，并让最后的 optional `StatusEffects` 使用默认；
正常分支在 `MoveMax` 后依次传
`BuildReactionBudgetSnapshot(unitState), BuildStatusEffectSnapshots(unitState)`。两个
直接构造 focus snapshot 的 regression fixture 同步按这个参数顺序迁移。

`BattleHudSnapshot` 再新增 required
`IEnumerable<BattleHudCounterattackRiskEntrySnapshot> counterattackRisks` constructor
参数和 required `string counterattackRiskCoverage` 参数、只读 collection、
`CounterattackRisks`/`CounterattackRiskCoverage` properties 与
`("counterattack_risks", _counterattackRisks)` canonical fact。两个 required 参数精确
插在现有 required `BattleHudEquipmentPanelSnapshot equipmentPanel` 后、首个 optional
`IEnumerable<BattleHudBarrierSnapshot> barriers = null` 前；不能追加到 constructor
末尾。adapter 只投影
`PotentialCounterattackChanceBasisPoints > 0` 的 entry，并使用
`ExecutableDefinitionDamageEnvelope`；coverage 不是 `Complete` 时列表为空，HUD
根据单独的 `CounterattackRiskCoverage` string 显示“反击风险未计算”，而不是显示
“无反击风险”。测试侧有两处直接构造 `BattleHudSnapshot`，都必须显式传这两个新字段：

- `run_battle_hud_typed_projection_regression.cs:774-810` 的显式
  `new BattleHudSnapshot(...)` 在 `equipment` 后、`barriers` 前插入
  `Array.Empty<BattleHudCounterattackRiskEntrySnapshot>()` 与 `"not_evaluated"`。
- `run_battle_map_panel_schema_regression.cs:303-355` 的 `BuildSnapshot(...)` 使用
  target-typed `new(`，在 `BattleHudEquipmentPanelSnapshot` 后、具名
  `objectiveProgress:` 前插入同样两个位置实参。审计不能只搜索
  `new BattleHudSnapshot(`，否则会漏掉这个 required-parameter call site。

目标字段、constructor assignment 与 adapter builder 是：

```csharp
// BattleHudSnapshot fields
private readonly ReadOnlyCollection<
    BattleHudCounterattackRiskEntrySnapshot
> _counterattackRisks;

// 在 non-empty constructor 中：
_counterattackRisks =
    new List<BattleHudCounterattackRiskEntrySnapshot>(
        counterattackRisks
        ?? throw new ArgumentNullException(
            nameof(counterattackRisks)
        )
    ).AsReadOnly();
CounterattackRiskCoverage =
    counterattackRiskCoverage
    ?? throw new ArgumentNullException(
        nameof(counterattackRiskCoverage)
    );

internal IReadOnlyList<
    BattleHudCounterattackRiskEntrySnapshot
> CounterattackRisks => _counterattackRisks;
internal string CounterattackRiskCoverage { get; } = "";

// Empty constructor 中：
_counterattackRisks =
    new List<BattleHudCounterattackRiskEntrySnapshot>()
        .AsReadOnly();
CounterattackRiskCoverage = "not_evaluated";

// CanonicalFacts 中：
("counterattack_risk_coverage", CounterattackRiskCoverage),
("counterattack_risks", _counterattackRisks),

// BattleHudAdapter
private static IReadOnlyList<
    BattleHudCounterattackRiskEntrySnapshot
> BuildCounterattackRiskSnapshots(BattlePreview preview)
{
    BattleCounterattackRiskProjection risk =
        preview?.CounterattackRiskTyped
        ?? BattleCounterattackRiskProjection.Empty(
            BattleCounterattackRiskCoverage.NotEvaluated
        );
    if (risk.Coverage != BattleCounterattackRiskCoverage.Complete)
    {
        return Array.Empty<
            BattleHudCounterattackRiskEntrySnapshot
        >();
    }
    var result =
        new List<BattleHudCounterattackRiskEntrySnapshot>();
    foreach (BattleCounterattackRiskEntry entry in risk.Entries)
    {
        if (entry.PotentialCounterattackChanceBasisPoints <= 0)
            continue;
        BattleCounterattackDefinitionDamageRange damage =
            entry.ExecutableDefinitionDamageEnvelope;
        result.Add(
            new BattleHudCounterattackRiskEntrySnapshot(
                entry.DefenderUnitId.ToString(),
                entry.PotentialCounterattackChanceBasisPoints,
                damage.HasDamage,
                damage.MinDamage,
                damage.MaxDamage
            )
        );
    }
    return result.AsReadOnly();
}
```

`BattleHudAdapter.BuildSnapshot(...)` 的唯一 production constructor call 显式追加：

```csharp
counterattackRisks:
    BuildCounterattackRiskSnapshots(runtimePreview),
counterattackRiskCoverage:
    BattleCounterattackRiskCoverageNames.ToStringName(
        (
            runtimePreview?.CounterattackRiskTyped
            ?? BattleCounterattackRiskProjection.Empty(
                BattleCounterattackRiskCoverage.NotEvaluated
            )
        ).Coverage
    ).ToString(),
```

为了让 P1B 不停在“snapshot 有字段但 UI 不消费”，`BattleHudAdapter.cs` 还在现有 imports
上新增 `using System.Globalization;`，并把风险摘要追加到当前
`SelectedSkillPreviewTooltipText`。目标 helper 与 call-site 改签为：

```csharp
private static string BuildCounterattackRiskTooltip(
    BattlePreview preview
)
{
    BattleCounterattackRiskProjection risk =
        preview?.CounterattackRiskTyped
        ?? BattleCounterattackRiskProjection.Empty(
            BattleCounterattackRiskCoverage.NotEvaluated
        );
    if (
        risk.Coverage
            == BattleCounterattackRiskCoverage.Complete
        && risk.PotentialExpectedCountBasisPoints <= 0
    )
    {
        return "";
    }
    if (
        risk.Coverage
            == BattleCounterattackRiskCoverage.Complete
    )
    {
        decimal expectedCount =
            risk.PotentialExpectedCountBasisPoints / 10_000m;
        return "按当前状态的潜在反击：预计 "
            + expectedCount.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            )
            + " 次";
    }
    return risk.Coverage switch
    {
        BattleCounterattackRiskCoverage
            .RandomTargetSelectionUnknown =>
            "潜在反击：随机目标选择概率未计算。",
        BattleCounterattackRiskCoverage
            .ProducerSequenceUnsupported =>
            "潜在反击：复杂地面/冲锋阶段尚未计算。",
        BattleCounterattackRiskCoverage
            .OutcomeChanceUnsupported =>
            "潜在反击：当前攻击缺少可用命中概率。",
        _ => "",
    };
}

private static string BuildSelectedSkillPreviewTooltip(
    AttackPreviewData hitPreview,
    FatePreviewFacts fatePreview,
    DamagePreviewSummary damagePreview,
    BattlePresentationPayload saveBranchPreview,
    string counterattackRiskTooltip
)
{
    var sections = new List<string>();
    string saveBranchText = saveBranchPreview?.SummaryText ?? "";
    if (!string.IsNullOrEmpty(saveBranchText))
        sections.Add(saveBranchText);
    string hitText = hitPreview?.SummaryText ?? "";
    if (!string.IsNullOrEmpty(hitText))
        sections.Add(hitText);
    string damageText = damagePreview.SummaryText;
    if (!string.IsNullOrEmpty(damageText))
        sections.Add(damageText);
    string fateTooltip = fatePreview?.TooltipText ?? "";
    if (!string.IsNullOrEmpty(fateTooltip))
        sections.Add(fateTooltip);
    if (!string.IsNullOrEmpty(counterattackRiskTooltip))
        sections.Add(counterattackRiskTooltip);
    return string.Join("\n\n", sections);
}

string tooltipText = BuildSelectedSkillPreviewTooltip(
    hitPreview,
    fatePreview,
    damagePreview,
    saveBranchPreview,
    BuildCounterattackRiskTooltip(runtimePreview)
);
```

上面 method 保留当前 `BattleHudAdapter.cs:1886-1898` 的
save → hit → damage → fate 原序，只在 `:1899 return` 前插入 risk。最终
`BattleMapPanel.cs:1332` 已有
`skill_subtitle_label.TooltipText = snapshot.SelectedSkillPreviewTooltipText`，无需新增
panel node、signal 或第二份格式化逻辑。

`BattlePresentationDelta` 仍只表达刷新请求：

- 反应 charge 消耗/补充时，既有 batch 加入对应 `ChangedUnitIds`。
- HUD adapter 刷新时从 `CaptureReactionRawTyped()` 投影 detached budget。
- delta 不保存 counterattack risk、capability 或 budget payload。

### 12.6 未来 AI

P1A/P1B 只提供 detached query：

- AI 在当前 `BattleAiScoreInput` 构建期间可读取
  `preview.CounterattackRiskTyped`；P1B 不新增 score 权重。
- `BattleAiDecisionResult.CloneScoreInput(...)` 当前把 `preview=null`，因此未来若要让
  risk 跨 decision lifetime，必须新增 detached score-input fields 并显式 clone，不能
  保存 `BattlePreview` 或 live plan。
- evaluation 不打开 action scope、不入队、不消费预算。
- `BattleAiMutationSnapshot` 按 §13.4 覆盖 reaction/capability 两个 unit component，能
  证明 preview 调用无 mutation。
- 具体 score profile、权重与行为调整属于后续 CU-20 设计。

---

## 十三、snapshot、codec、存档与生命周期

### 13.1 unit component 覆盖

`BattleUnitReactionState` 与 `BattleUnitCounterattackCapabilityState` 必须进入：

- gameplay clone。
- strict codec。
- canonical snapshot。
- detached/read-only snapshot。
- `BattleUnitStatePlainSnapshot`。
- AI stable projection。
- AI mutation exact owner-presence/raw capture。

两个新组件统一使用 explicit `OwnerPresent`：

- missing owner 使用各自 `MissingOwner` snapshot。
- present 但 capability list 为空、capacity/charge 为 0，仍保持 `OwnerPresent=true`。
- capability snapshot 的 values 按 owner canonical 顺序 detached copy；不得用 null list 兼任 missing owner。
- reaction snapshot 的整数 raw value 在 mutation exact 中不 clamp；strict codec 负责拒绝超出合法域的值，normal clone 才可以规范化已经验证的值。

AI mutation snapshot 为两个组件分别保留 owner-presence key 与 raw payload key，不借用 cooldown 的 null-map sentinel。normal clone 可以规范化合法值；strict codec 与 mutation exact 必须保留上述 presence/raw 诊断纪律。

`BattleUnitState.BuildSnapshotPlain()`、`BuildPlainSnapshotDetached()` 与
`ToDictFields` 同时新增且只新增下面两个顶层字段；三处必须调用同一组 plain helper，不能各自
发明字段名：

```text
"reaction_state": {
  "owner_present": bool,
  "charges_remaining": int,
  "charge_capacity": int,
  "recharge_interval_tu": int,
  "next_recharge_at_tu": int
}

"counterattack_capability_state": {
  "owner_present": bool,
  "values": [
    {
      "instance_id": string,
      "trigger_kind": "melee_hit_received" | "melee_attack_evaded",
      "selection_priority": int,
      "chance_percent": int,
      "attack_roll_bonus": int,
      "weapon_action_definition_id": string
    }
  ]
}
```

`values` 固定使用 capability owner 的 canonical priority/id 顺序。missing owner 仍输出完整
nested object：`owner_present=false`、所有 reaction 整数为 `0`、`values=[]`；strict
reader 不接受缺字段、额外字段、未知 trigger、空 ID、重复 instance id、越界 chance、
非法 reaction interval/capacity/charge。由于本功能没有旧 payload，`FromDictionary(...)`
不为缺少这两个顶层字段提供默认值。

三个 projection 不能分别拼字典。新增
`scripts/systems/battle/core/BattleUnitState.Counterattack.cs`，由同一个 partial class
提供下面两个 encode helper；`BuildSnapshotPlain()` 与
`BuildPlainSnapshotDetached()` 的 initializer 都直接调用它们：

```csharp
private Dictionary<string, object> BuildReactionStatePlain()
{
    BattleUnitReactionSnapshot snapshot =
        CaptureReactionRawTyped();
    return new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["owner_present"] = snapshot.OwnerPresent,
        ["charges_remaining"] = snapshot.ChargesRemaining,
        ["charge_capacity"] = snapshot.ChargeCapacity,
        ["recharge_interval_tu"] = snapshot.RechargeIntervalTu,
        ["next_recharge_at_tu"] = snapshot.NextRechargeAtTu,
    };
}

private Dictionary<string, object>
    BuildCounterattackCapabilityStatePlain()
{
    BattleUnitCounterattackCapabilitySnapshot snapshot =
        CaptureCounterattackCapabilitiesRawTyped();
    var values = new List<object>(snapshot.Values.Count);
    foreach (BattleCounterattackCapability capability in snapshot.Values)
    {
        values.Add(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["instance_id"] = capability.InstanceId.ToString(),
                ["trigger_kind"] =
                    BattleCounterattackTriggerNames
                        .ToStringName(capability.TriggerKind)
                        .ToString(),
                ["selection_priority"] =
                    capability.SelectionPriority,
                ["chance_percent"] = capability.ChancePercent,
                ["attack_roll_bonus"] = capability.AttackRollBonus,
                ["weapon_action_definition_id"] =
                    capability.WeaponActionDefinitionId.ToString(),
            }
        );
    }
    return new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["owner_present"] = snapshot.OwnerPresent,
        ["values"] = values,
    };
}
```

`BattleUnitState.cs` 的 `ToDictFields` 在 `"status_effects"` 后精确追加：

```csharp
"reaction_state",
"counterattack_capability_state",
```

`BattleUnitState.BuildSnapshotPlain()` 的返回 dictionary 与
`BattleUnitStatePlainSnapshot.cs:BuildPlainSnapshotDetached()` 的 `Map(...)` 参数都精确追加：

```csharp
("reaction_state", BuildReactionStatePlain()),
(
    "counterattack_capability_state",
    BuildCounterattackCapabilityStatePlain()
),
```

其中 `BuildSnapshotPlain()` 使用 dictionary initializer 语法时，等价条目写成：

```csharp
["reaction_state"] = BuildReactionStatePlain(),
["counterattack_capability_state"] =
    BuildCounterattackCapabilityStatePlain(),
```

同一个新 partial 文件提供 strict decoder。它复用当前
`BattleUnitState.HasExactFields(...)` 与
`IsStringNamePayloadType(...)`，不引入宽松的 `GetInt(..., fallback)`：

```csharp
private static readonly string[] ReactionStateFields =
[
    "owner_present",
    "charges_remaining",
    "charge_capacity",
    "recharge_interval_tu",
    "next_recharge_at_tu",
];

private static readonly string[] CounterattackCapabilityStateFields =
[
    "owner_present",
    "values",
];

private static readonly string[] CounterattackCapabilityFields =
[
    "instance_id",
    "trigger_kind",
    "selection_priority",
    "chance_percent",
    "attack_roll_bonus",
    "weapon_action_definition_id",
];

private static bool TryReadCounterattackComponentSnapshots(
    GDictionary payload,
    out BattleUnitReactionSnapshot reactionSnapshot,
    out BattleUnitCounterattackCapabilitySnapshot capabilitySnapshot
)
{
    reactionSnapshot = BattleUnitReactionSnapshot.MissingOwner;
    capabilitySnapshot =
        BattleUnitCounterattackCapabilitySnapshot.MissingOwner;
    if (
        payload == null
        || payload["reaction_state"].VariantType
            != Variant.Type.Dictionary
        || payload["counterattack_capability_state"].VariantType
            != Variant.Type.Dictionary
    )
    {
        return false;
    }
    return TryReadReactionSnapshotStrict(
            payload["reaction_state"].AsGodotDictionary(),
            out reactionSnapshot
        )
        && TryReadCounterattackCapabilitySnapshotStrict(
            payload["counterattack_capability_state"]
                .AsGodotDictionary(),
            out capabilitySnapshot
        );
}

private static bool TryReadReactionSnapshotStrict(
    GDictionary payload,
    out BattleUnitReactionSnapshot snapshot
)
{
    snapshot = BattleUnitReactionSnapshot.MissingOwner;
    if (
        payload == null
        || !HasExactFields(payload, ReactionStateFields)
        || !TryReadStrictBool(
            payload,
            "owner_present",
            out bool ownerPresent
        )
        || !TryReadStrictInt32(
            payload,
            "charges_remaining",
            out int chargesRemaining
        )
        || !TryReadStrictInt32(
            payload,
            "charge_capacity",
            out int chargeCapacity
        )
        || !TryReadStrictInt32(
            payload,
            "recharge_interval_tu",
            out int rechargeIntervalTu
        )
        || !TryReadStrictInt32(
            payload,
            "next_recharge_at_tu",
            out int nextRechargeAtTu
        )
    )
    {
        return false;
    }
    if (!ownerPresent)
    {
        return chargesRemaining == 0
            && chargeCapacity == 0
            && rechargeIntervalTu == 0
            && nextRechargeAtTu == 0;
    }

    var parsed = new BattleUnitReactionSnapshot(
        true,
        chargesRemaining,
        chargeCapacity,
        rechargeIntervalTu,
        nextRechargeAtTu
    );
    try
    {
        BattleReactionBudgetRules.Validate(
            new BattleReactionBudgetConfig(
                chargeCapacity,
                rechargeIntervalTu
            )
        );
        var validator = new BattleUnitReactionState();
        validator.RestoreRaw(parsed);
    }
    catch (ArgumentException)
    {
        return false;
    }
    snapshot = parsed;
    return true;
}

private static bool
    TryReadCounterattackCapabilitySnapshotStrict(
        GDictionary payload,
        out BattleUnitCounterattackCapabilitySnapshot snapshot
    )
{
    snapshot =
        BattleUnitCounterattackCapabilitySnapshot.MissingOwner;
    if (
        payload == null
        || !HasExactFields(
            payload,
            CounterattackCapabilityStateFields
        )
        || !TryReadStrictBool(
            payload,
            "owner_present",
            out bool ownerPresent
        )
        || payload["values"].VariantType != Variant.Type.Array
    )
    {
        return false;
    }

    GArray rawValues = payload["values"].AsGodotArray();
    if (!ownerPresent)
        return rawValues.Count == 0;

    var parsed = new List<BattleCounterattackCapability>(
        rawValues.Count
    );
    foreach (Variant rawValue in rawValues)
    {
        if (
            rawValue.VariantType != Variant.Type.Dictionary
            || !TryReadCounterattackCapabilityStrict(
                rawValue.AsGodotDictionary(),
                out BattleCounterattackCapability capability
            )
        )
        {
            return false;
        }
        parsed.Add(capability);
    }

    BattleUnitCounterattackCapabilitySnapshot canonical;
    try
    {
        var validator =
            new BattleUnitCounterattackCapabilityState();
        validator.ReplaceAll(parsed);
        canonical = validator.CaptureRaw();
    }
    catch (ArgumentException)
    {
        return false;
    }
    if (canonical.Values.Count != parsed.Count)
        return false;
    for (int index = 0; index < parsed.Count; index++)
    {
        if (!canonical.Values[index].Equals(parsed[index]))
            return false;
    }
    snapshot = new(
        true,
        parsed.AsReadOnly()
    );
    return true;
}

private static bool TryReadCounterattackCapabilityStrict(
    GDictionary payload,
    out BattleCounterattackCapability capability
)
{
    capability = default;
    if (
        payload == null
        || !HasExactFields(
            payload,
            CounterattackCapabilityFields
        )
        || !TryReadNonEmptyStringNameStrict(
            payload,
            "instance_id",
            out StringName instanceId
        )
        || !TryReadNonEmptyStringNameStrict(
            payload,
            "trigger_kind",
            out StringName triggerName
        )
        || !BattleCounterattackTriggerNames.TryParse(
            triggerName,
            out BattleCounterattackTriggerKind triggerKind
        )
        || !TryReadStrictInt32(
            payload,
            "selection_priority",
            out int selectionPriority
        )
        || !TryReadStrictInt32(
            payload,
            "chance_percent",
            out int chancePercent
        )
        || chancePercent < 0
        || chancePercent > 100
        || !TryReadStrictInt32(
            payload,
            "attack_roll_bonus",
            out int attackRollBonus
        )
        || !TryReadNonEmptyStringNameStrict(
            payload,
            "weapon_action_definition_id",
            out StringName weaponActionDefinitionId
        )
    )
    {
        return false;
    }
    capability = new BattleCounterattackCapability(
        instanceId,
        triggerKind,
        selectionPriority,
        chancePercent,
        attackRollBonus,
        weaponActionDefinitionId
    );
    return true;
}

private static bool TryReadStrictBool(
    GDictionary payload,
    string key,
    out bool value
)
{
    value = false;
    Variant raw = payload[key];
    if (raw.VariantType != Variant.Type.Bool)
        return false;
    value = raw.AsBool();
    return true;
}

private static bool TryReadStrictInt32(
    GDictionary payload,
    string key,
    out int value
)
{
    value = 0;
    Variant raw = payload[key];
    if (raw.VariantType != Variant.Type.Int)
        return false;
    long parsed = raw.AsInt64();
    if (parsed < int.MinValue || parsed > int.MaxValue)
        return false;
    value = (int)parsed;
    return true;
}

private static bool TryReadNonEmptyStringNameStrict(
    GDictionary payload,
    string key,
    out StringName value
)
{
    value = "";
    Variant raw = payload[key];
    if (!IsStringNamePayloadType(raw.VariantType.ToString()))
        return false;
    value = ToStringName(raw);
    return value != new StringName("");
}
```

上段所在新 partial 文件头必须包含：

```csharp
using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
```

这些 using 之后必须声明 `public partial class BattleUnitState`；本节从
`BuildReactionStatePlain()` 到 `TryReadNonEmptyStringNameStrict(...)` 的全部 member 都放在
该 class 的同一对花括号内。不要声明成 extension/static helper class，否则 private
`HasExactFields(...)`、`IsStringNamePayloadType(...)` 和 private owner 字段都不可访问。

`BattleUnitState.FromDictionary(...)` 在顶层
`HasExactFields(payload, ToDictFields)` 成功后、读取任何其他字段前精确新增：

```csharp
if (
    !TryReadCounterattackComponentSnapshots(
        payload,
        out BattleUnitReactionSnapshot parsedReactionSnapshot,
        out BattleUnitCounterattackCapabilitySnapshot
            parsedCapabilitySnapshot
    )
)
{
    return null;
}
```

构造 `unitState` 完成后、`attribute_snapshot.SetValue(...)` 前只走前文两个 raw gateway，
精确插入代码为：

```csharp
unitState.RestoreReactionRawTyped(parsedReactionSnapshot);
unitState.RestoreCounterattackCapabilitiesRawTyped(
    parsedCapabilitySnapshot
);
```

decoder 不捕获 gateway 异常后继续构造；按上面的 validator-first 流程，走到 restore
时只能提交已经验证的 snapshot。missing owner 经 raw gateway 恢复为 `null`，不会被
projection getter 自动补成 present-empty。

`clone()` initializer 对应新增：

```csharp
_reactionState =
    _reactionState?.DuplicateState(),
_counterattackCapabilityState =
    _counterattackCapabilityState?.DuplicateState(),
```

不能使用 property getter 自动创建 owner，否则 missing owner 在 clone/snapshot 时会被读操作
改成 present-empty。

AI mutation guard 的实际 unit owner 是
`scripts/systems/battle/ai/BattleAiMutationSnapshots.cs` 内的
`BattleUnitFieldsSnapshot`，不是顶层 `BattleAiMutationSnapshot`。它只做 capture + stable
comparison，不负责 restore；不能在这里虚构 rollback API。字段精确新增：

```csharp
private BattleUnitReactionSnapshot _reactionSnapshot =
    BattleUnitReactionSnapshot.MissingOwner;
private BattleUnitCounterattackCapabilitySnapshot
    _counterattackCapabilitySnapshot =
        BattleUnitCounterattackCapabilitySnapshot.MissingOwner;
```

`BattleUnitFieldsSnapshot.Capture(...)` 在 cooldown capture 前新增：

```csharp
snapshot._reactionSnapshot =
    unit.CaptureReactionRawTyped();
snapshot._counterattackCapabilitySnapshot =
    unit.CaptureCounterattackCapabilitiesRawTyped();
```

`BattleUnitFieldsSnapshot.ToStableMap()` 在 `"cooldowns"` 前新增四个固定 key：

```csharp
result.Set(
    "reaction_state_owner_present",
    StableValue.FromBool(_reactionSnapshot.OwnerPresent)
);
result.Set(
    "reaction_state_raw",
    StableValue.FromMap(
        BattleAiMutationStableProjection.StableReactionStateRaw(
            _reactionSnapshot
        )
    )
);
result.Set(
    "counterattack_capability_state_owner_present",
    StableValue.FromBool(
        _counterattackCapabilitySnapshot.OwnerPresent
    )
);
result.Set(
    "counterattack_capabilities_raw",
    StableValue.FromArray(
        BattleAiMutationStableProjection
            .StableCounterattackCapabilitiesRaw(
                _counterattackCapabilitySnapshot.Values
            )
    )
);
```

`BattleAiMutationStableProjection.cs` 新增的 helper 完整代码为：

```csharp
internal static StableMap StableReactionStateRaw(
    BattleUnitReactionSnapshot snapshot
)
{
    StableMap result = new();
    result.Set(
        "charges_remaining",
        StableValue.FromInteger(snapshot.ChargesRemaining)
    );
    result.Set(
        "charge_capacity",
        StableValue.FromInteger(snapshot.ChargeCapacity)
    );
    result.Set(
        "recharge_interval_tu",
        StableValue.FromInteger(snapshot.RechargeIntervalTu)
    );
    result.Set(
        "next_recharge_at_tu",
        StableValue.FromInteger(snapshot.NextRechargeAtTu)
    );
    return result;
}

internal static List<StableValue>
    StableCounterattackCapabilitiesRaw(
        IEnumerable<BattleCounterattackCapability> values
    )
{
    var result = new List<StableValue>();
    foreach (
        BattleCounterattackCapability capability
            in values
                ?? Array.Empty<BattleCounterattackCapability>()
    )
    {
        StableMap entry = new();
        entry.Set(
            "instance_id",
            StableNullableStringName(capability.InstanceId)
        );
        entry.Set(
            "trigger_kind",
            StableNullableStringName(
                BattleCounterattackTriggerNames.ToStringName(
                    capability.TriggerKind
                )
            )
        );
        entry.Set(
            "selection_priority",
            StableValue.FromInteger(
                capability.SelectionPriority
            )
        );
        entry.Set(
            "chance_percent",
            StableValue.FromInteger(capability.ChancePercent)
        );
        entry.Set(
            "attack_roll_bonus",
            StableValue.FromInteger(capability.AttackRollBonus)
        );
        entry.Set(
            "weapon_action_definition_id",
            StableNullableStringName(
                capability.WeaponActionDefinitionId
            )
        );
        result.Add(StableValue.FromMap(entry));
    }
    return result;
}
```

mutation exact 不调用 capability owner 的 `ReplaceAll(...)`，也不 clamp reaction raw 值；它
按 capture 返回的 detached canonical list 原序投影。missing 与 present-empty 即使 raw
payload 都为零/空，也由两个独立 `*_owner_present` key 区分。真正需要恢复的 strict codec
与 gameplay clone 仍分别使用前文 raw gateway / `DuplicateState()`，不借 AI guard 实现。

### 13.2 不进入持久存档

反应 charge、anchor、capability runtime instances、action allocator 与 queue 都是当前战斗局部状态：

- 不进入 `SaveSerializer`。
- 不 bump save version。
- 不增加旧 payload fallback。
- 战斗期间继续遵守现有 save lock。

### 13.3 runtime sidecar lifecycle

setup：

1. 创建 effect execution context、coordinator、counterattack system、query service、immediate attack service。
2. 注入当前 battle state/catalog/query owners。
3. 绑定 damage resolver sink。
4. 初始化 unit reaction state。

unit owner 初始化不是一句抽象步骤，而且不能只覆盖 battle start：当前
`BattleEquipmentSummonResolver` 有两条战斗中 `state.SetUnit(summoned)` 路径。初始化 owner
必须由单单位 admission helper 负责，开局/fixture 的全量遍历只复用它。
`BattleRuntimeModule` 新增：

```csharp
internal void EnsureCounterattackUnitOwnersInitializedForAdmission(
    BattleState state,
    BattleUnitState unit
)
{
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(unit);
    if (!ReferenceEquals(state, _state))
    {
        throw new InvalidOperationException(
            "counterattack unit admission belongs to another battle"
        );
    }
    int currentTu = Math.Max(
        state.timeline?.current_tu ?? 0,
        0
    );
    BattleReactionBudgetConfig config =
        BattleReactionBudgetRules.EngineDefault;
    BattleReactionBudgetRules.Validate(config);
    if (!unit.CaptureReactionRawTyped().OwnerPresent)
    {
        unit.InitializeReactionBudgetTyped(
            currentTu,
            config,
            startFull: true
        );
    }
    if (
        !unit
            .CaptureCounterattackCapabilitiesRawTyped()
            .OwnerPresent
    )
    {
        unit.ReplaceCounterattackCapabilitiesTyped(
            Array.Empty<BattleCounterattackCapability>()
        );
    }
}

private void EnsureCounterattackUnitOwnersInitialized()
{
    BattleState state = _state;
    if (state == null)
        return;
    foreach (BattleUnitState unit in state.GetUnitsTyped())
    {
        if (unit != null)
        {
            EnsureCounterattackUnitOwnersInitializedForAdmission(
                state,
                unit
            );
        }
    }
}
```

正式 `StartBattleCore(...)` 在 ally/enemy/scenario actors 全部 placement 成功、objective 初始化
成功后，且在 `_initialize_unit_trait_hooks()` 前调用一次；这样 P1A 得到 present-empty
capability owner，未来 trait/content installer 可在其后 replace。`SetupStateForTests(...)`
在 `BindRuntimeBattleState(state)` 后也调用一次，但只补 missing owner，不覆盖 fixture 已安装
的 raw state。

运行中 unit admission 的三个当前写入点必须在 `SetUnit(...)` 前初始化；这样初始化失败时
不会把半初始化单位留在 state：

```csharp
// BattleSpawnPlacementService._place_spawn_unit_at_anchor(...)
_runtime.EnsureCounterattackUnitOwnersInitializedForAdmission(
    _runtime.GetState(),
    unit_state
);
_runtime._state.SetUnit(unit_state);

// BattleEquipmentSummonResolver.ResolveSummonUnitsAction(...) 两个 overload
_runtime.EnsureCounterattackUnitOwnersInitializedForAdmission(
    state,
    summoned
);
state.SetUnit(summoned);
```

精确源码锚点是 `BattleSpawnPlacementService.cs:103-104`，以及
`BattleEquipmentSummonResolver.cs:205-208`、`:289-292`。召唤路径仍先
`BuildSummonedUnit(...)`、判空，再初始化 owner、`SetUnit`、`PlaceUnit`；若 placement 失败，
现有 `RemoveUnit(...)` 回滚保持不变。`BattleChargeResolver.cs:869` 写入的是
`unitEntry.Unit.clone()`，clone 已携带两个 owner 的 raw copy，不重复执行 admission default。
以后新增战斗中 `BattleState.SetUnit(...)` production caller，必须先调用该 helper 并增加
owner-presence 回归；不能依赖下一次 timeline tick 补初始化。

`BattleRuntimeModuleBorrowerSet` 必须保持每个切片都可独立编译。P1A 尚未定义
`BattleCounterattackPreviewService`，因此 P1A 的 dependency-first 新成员与完整数组顺序
固定为：

```csharp
internal BattleEquipmentDurabilityResultProjector
    EquipmentDurabilityResultProjector { get; } = new();
internal BattleWeaponAttackOutcomeCommitter
    WeaponAttackOutcomeCommitter { get; }
internal BattleRuntimeCounterattackWeaponAttackDefinitionProvider
    CounterattackWeaponAttackDefinitionProvider { get; } = new();
internal BattleImmediateWeaponAttackService
    ImmediateWeaponAttack { get; }
internal BattleCounterattackQueryService CounterattackQuery { get; }

internal BattleRuntimeModuleBorrowerSet()
{
    WeaponAttackOutcomeCommitter =
        new BattleWeaponAttackOutcomeCommitter(
            EquipmentDurabilityResultProjector
        );
    ImmediateWeaponAttack =
        new BattleImmediateWeaponAttackService(
            WeaponAttackOutcomeCommitter,
            CounterattackWeaponAttackDefinitionProvider
    );
    CounterattackQuery =
        new BattleCounterattackQueryService(ImmediateWeaponAttack);

    _borrowers =
    [
        SpawnPlacement,
        SpecialSkillGate,
        MovementCommand,
        MetricsReport,
        ContingencyBridge,
        EquipmentDurabilityResultProjector,
        WeaponAttackOutcomeCommitter,
        CounterattackWeaponAttackDefinitionProvider,
        ImmediateWeaponAttack,
        CounterattackQuery,
        CommandPreview,
        AiDecisionBinding,
    ];

    var typeNames = new string[_borrowers.Length];
    for (int index = 0; index < _borrowers.Length; index++)
        typeNames[index] = _borrowers[index].GetType().Name;
    _topologySignature = string.Join(">", typeNames);
}
```

P1B 新增 preview type 后，才在 property 区新增：

```csharp
internal BattleCounterattackPreviewService CounterattackPreview { get; }
```

并在 P1B 的同一次 `BattleRuntimeModuleBorrowerSet.cs` 修改中，于
`CounterattackQuery` 构造之后新增：

```csharp
CounterattackPreview =
    new BattleCounterattackPreviewService(
        CounterattackQuery,
        ImmediateWeaponAttack
    );
```

同时把 P1A 的 `_borrowers` initializer **整体替换**为最终数组：

```csharp
_borrowers =
[
    SpawnPlacement,
    SpecialSkillGate,
    MovementCommand,
    MetricsReport,
    ContingencyBridge,
    EquipmentDurabilityResultProjector,
    WeaponAttackOutcomeCommitter,
    CounterattackWeaponAttackDefinitionProvider,
    ImmediateWeaponAttack,
    CounterattackQuery,
    CounterattackPreview,
    CommandPreview,
    AiDecisionBinding,
];
```

两个数组都必须是真正的 dependency-first。P1A 的逆序 teardown 是
AI → command preview → query → immediate attack → provider / committer / projector；
P1B 才增加 `CounterattackPreview` 对 `CounterattackQuery/ImmediateWeaponAttack` 的借用，
并形成 AI → command preview → counterattack preview → query → immediate attack →
provider / committer / projector。不得让 P1A 引用尚未存在的 P1B 类型，也不得把新增
borrower 机械追加到 AI 后面。

`BattleRuntimeModule.FinishSetup(...)` 在当前 `_moduleBorrowers.Setup(this)`（源码 `:473`）之后调用下面的 idempotent wiring；`_ensure_sidecars_ready()` 也在末尾调用同一方法：

```csharp
private readonly IBattleCounterattackChanceRoller
    _counterattackChanceRoller;
private BattleEffectExecutionContextService
    _effectExecutionContext;
private BattleAttackActionCoordinator
    _attackActionCoordinator;
private BattleCounterattackSystem _counterattackSystem;
private BattleDamageResolver _reactionSinkBoundDamageResolver;

public BattleRuntimeModule()
    : this(BattleCounterattackChanceRoller.Instance)
{
}

internal BattleRuntimeModule(
    IBattleCounterattackChanceRoller counterattackChanceRoller
)
{
    _counterattackChanceRoller = counterattackChanceRoller
        ?? throw new ArgumentNullException(
            nameof(counterattackChanceRoller)
        );
    _moduleBorrowers.Setup(this);
    SetTerrainGenerator(new BattleTerrainGenerator(), true);
    _ai_move_query_cost_callback =
        _aiDecisionBindingService._get_ai_move_query_cost;
    _ai_move_cost_callback =
        _movementCommandService._get_move_cost_for_unit_target;
    _ai_preview_command_callback =
        _commandPreviewService.PreviewCommand;
    _ai_skill_score_input_callback =
        _aiDecisionBindingService.BuildAiSkillScoreInput;
    _ai_action_score_input_callback =
        _aiDecisionBindingService.BuildAiActionScoreInput;
    _ai_query_action_score_input_callback =
        _aiDecisionBindingService.BuildAiQueryActionScoreInput;
    _ai_movement_blocked_callback =
        _aiDecisionBindingService.IsAiMovementBlocked;
    _ai_skill_cast_block_reason_callback =
        GetSkillCastBlockReason;
}

private void EnsureReactionRuntimeReady()
{
    _effectExecutionContext ??= new BattleEffectExecutionContextService();
    _attackActionCoordinator ??=
        new BattleAttackActionCoordinator(
            _effectExecutionContext,
            BattleReactionBoundarySafetyRules.Production
        );
    _counterattackSystem ??=
        new BattleCounterattackSystem(
            this,
            _attackActionCoordinator,
            _effectExecutionContext,
            _moduleBorrowers.CounterattackQuery,
            _moduleBorrowers.ImmediateWeaponAttack,
            _counterattackChanceRoller
        );
    _attackActionCoordinator.BindDrainOwner(_counterattackSystem);
    if (
        !ReferenceEquals(
            _reactionSinkBoundDamageResolver,
            _damage_resolver
        )
    )
    {
        if (_attackActionCoordinator.HasActiveBoundary)
        {
            throw new InvalidOperationException(
                "cannot rebind damage resolver in a reaction boundary"
            );
        }
        _reactionSinkBoundDamageResolver
            ?.SetAttackResolutionSink(null);
        _damage_resolver?.SetAttackResolutionSink(
            _counterattackSystem
        );
        _reactionSinkBoundDamageResolver = _damage_resolver;
    }
    if (_state == null)
        _attackActionCoordinator.StopAcceptingAndAbort();
}
```

`BindDrainOwner(...)` 的上文实现必须先判断同一实例并幂等返回，再判断 active
boundary；否则 `_ensure_sidecars_ready()` 在攻击链中重复 ensure 时会错误抛出。
不同 drain owner 与不同 damage resolver 都不得在 active boundary 中替换。resolver
本身没有 coordinator 依赖，因此 active-boundary 检查放在 runtime 的 rebind
分支；sink setter 只执行下面的单 owner 规则。runtime teardown 必须先
`StopAcceptingAndAbort()` 再解绑：

```csharp
internal void SetAttackResolutionSink(
    IBattleAttackResolutionSink sink
)
{
    if (ReferenceEquals(_attack_resolution_sink, sink))
        return;
    if (_attack_resolution_sink != null && sink != null)
    {
        throw new InvalidOperationException(
            "attack resolution sink is already bound"
        );
    }
    _attack_resolution_sink = sink;
}
```

definition provider 由 borrower
constructor 固定注入，production/test runtime 都不提供运行中 setter；fixture 通过
`SetupStateForTests(...)`、测试 skill-definition index 与 capability owner 构造输入。
上面保留了当前 constructor 末尾的 `_topologySignature` 生成代码；不能只扩展数组而漏掉
签名，因为 teardown regression 会读取 registered order。

`FinishSetup(...)` 在当前 `_moduleBorrowers.Setup(this)` 后精确新增：

```csharp
EnsureReactionRuntimeReady();
```

`_ensure_sidecars_ready()` 当前最后一条是 `_casting_time_service.Setup(this)`；其后精确新增：

```csharp
_moduleBorrowers.Setup(this);
EnsureReactionRuntimeReady();
```

顺序不能反转，因为 query/immediate/provider/committer/projector 都是 borrower；
counterattack system 构造时借用的必须已经是 bound borrower。

teardown：

1. 禁止新 boundary/action。
2. 解绑 damage resolver sink。
3. 清 queue/dedupe/origin/action stack。
4. 断开 runtime borrower 与 sibling service。
5. dispose immediate attack/query/counter/coordinator/effect execution context。

在当前 `DisposeManagedRuntime()` phase 2 中，reaction teardown 必须放在 `_moduleBorrowers.DisposeRuntime(...)` 之前，目标代码为：

```csharp
RunTeardownStep(
    ref firstFailure,
    () => _attackActionCoordinator?.StopAcceptingAndAbort()
);
RunTeardownStep(
    ref firstFailure,
    () => _reactionSinkBoundDamageResolver
        ?.SetAttackResolutionSink(null)
);
_reactionSinkBoundDamageResolver = null;
RunTeardownStep(
    ref firstFailure,
    () => _counterattackSystem?.DisposeRuntime()
);
RunTeardownStep(
    ref firstFailure,
    () => _attackActionCoordinator?.Dispose()
);
RunTeardownStep(
    ref firstFailure,
    () => _effectExecutionContext?.Clear()
);
_counterattackSystem = null;
_attackActionCoordinator = null;
_effectExecutionContext = null;

_moduleBorrowers.DisposeRuntime(ref firstFailure);
```

实际 battle-state transition owner 是
`BattleRuntimeModule.BindRuntimeBattleState(...)` 与
`ClearRuntimeBattleStateReference()`。它不能用一个“清完马上重新开放”的 helper，因为
clear 到 `null` 后 runtime 必须保持禁止接单，直到新 state 真正绑定。新增两个窄 helper：

```csharp
private void StopReactionRuntimeForBattleTransition()
{
    _attackActionCoordinator?.StopAcceptingAndAbort();
    _effectExecutionContext?.Clear();
}

private void ArmReactionRuntimeForBoundBattle()
{
    if (_state == null || _attackActionCoordinator == null)
        return;
    _attackActionCoordinator.ResetForBattle();
}
```

`StopAcceptingAndAbort()` 已经通过 bound `IBattleReactionDrainOwner.AbortBoundary()` 清
counter queue/dedupe；transition helper 不再直接调用 `_counterattackSystem.AbortBoundary()`
形成第二个 teardown owner。该调用保持幂等，但单 owner 能让调用次数回归精确断言。

`ClearRuntimeBattleStateReference()` 的第一条 teardown 操作，在
`Exception firstFailure = null;` 后新增：

```csharp
RunTeardownStep(
    ref firstFailure,
    StopReactionRuntimeForBattleTransition
);
```

它发生在 `_runtime_services.EndBattle` 与 `_state = null` 前，保证旧 state topology 被拆除前
queue/action/origin 已经失效；clear 结束后不调用 `ResetForBattle()`。

`BindRuntimeBattleState(state)` 在 `ReferenceEquals` early return 后、现有
`Exception firstFailure = null;` 前先调用：

```csharp
StopReactionRuntimeForBattleTransition();
```

现有 `_state = state` 与 null early return 保持；只有 non-null 分支在
`_battleCacheEpoch` 更新前新增：

```csharp
ArmReactionRuntimeForBoundBattle();
```

因此 failed placement 的 `ClearRuntimeBattleStateReference()` 会保持 stop；下一次 retry 的
`BindRuntimeBattleState(newState)` 才把 root/action/scope allocator 重置为 `1` 并重新允许
boundary。若 transition/teardown 发生时仍有 active scope，
`StopAcceptingAndAbort()` 会 abort 当前 root 并推进 generation；随后旧 handle 的
`Complete()` / `Dispose()` 只 no-op，不得覆盖触发 transition 的原始异常，也不得命中新
battle 复用的 scope id。

`StartBattleCore(...)` 的精确 owner 初始化插入点是
`InitializeBattleObjective(...)` 成功后的当前 `:885`
`_initialize_unit_trait_hooks()` 之前：

```csharp
EnsureCounterattackUnitOwnersInitialized();
_initialize_unit_trait_hooks();
```

`SetupStateForTests(...)` 的 non-null 分支精确改为：

```csharp
if (_state != null)
{
    _ensure_sidecars_ready();
    EnsureCounterattackUnitOwnersInitialized();
    _contingency_system.ResetForBattle(
        _characterGateway?.GetPartyState(),
        _state
    );
}
```

两个调用都只补 missing owner；strict fixture 在 `SetupStateForTests(...)` 前已经装入的 raw
reaction/capability snapshot 不会被默认值覆盖。

不得让 sink、scope handle 或 queue entry 跨 battle 复用。

---

## 十四、最小落地切片

### P1A：运行时闭环

1. typed weapon-range owner、敌人 natural/unarmed/equipped projection 修复，以及
   item/enemy 加载期闭集校验。
2. typed action id、origin flag、delivery kind、action context。
3. `BattleEffectExecutionContextService`、`BattleAttackActionCoordinator`、单一 root batch、depth/work guard、battle generation 与正常/异常 scope 生命周期。
4. `IBattleAttackResolutionSink` 与 resolver miss/hit 两出口。
5. 所有生产攻击 producer 显式传播 action context；charge 删除局部 batch，AutoCast 删除隐式 batch fallback。
6. capability/reaction 两个 unit component 及 snapshot/codec/clone/mutation。
7. reaction budget 初始化、TU 补充与 time-stasis anchor 平移。
8. query service + pure rules + attempt 成本 transaction。
9. counter queue、去重、FIFO drain、nested enqueue。
10. canonical immediate weapon attack service、四阶段 outcome committer 与标准/装备路径迁移。
11. equipment ability content validation 把 `immediate_weapon_attack` 引用的 skill 限定为
    weapon-damage definition；运行时 projection unavailable 返回不可执行 plan。
12. stateless counterattack weapon-training grant；不进入 command mastery accumulator。
13. progression owner 全局禁止 `weapon_training` 成为职业升级 trigger，并抑制该类
    mastery transaction 的 promotion-modal delta。
14. `lock_counterattack` 实际消费。
15. 测试通过 runtime fixture 直接安装 capability；不改任何内容资源。

### P1B：展示闭环

1. `BattleCounterattackPreviewService` 复用 execution query/readiness owner。
2. `BattlePreview` immutable risk projection、coverage 与多段 dedupe 概率聚合。
3. deterministic unit-target 完整计算；random-chain 与 ground/charge 丢失精确概率/阶段顺序
   时返回 typed unknown coverage，不伪装为零风险。
4. `BattlePreviewProjection` 只输出 presentation-safe envelope。
5. `BattleHudSnapshot` party-backed reaction budget 与 risk projection。
6. `BattlePresentationDelta` 只负责正确 dirty unit。
7. preview/execution parity、无 mutation 与 projection lease 回归。

### 后续独立提案

- 生产 capability 来源与 authoring 投影。
- 属性派生、动态重配置与平衡。
- 具体内容迁移。
- AI score/profile。
- ranged/any trigger。
- 借机攻击与威胁区。

---

## 十五、预计改动文件

### Core / state

- `scripts/systems/battle/core/AttackContext.cs`
- `scripts/systems/battle/core/BattleEffectOrigin.cs`
- `scripts/systems/battle/core/BattleKillProvenance.cs`
- 新增 `BattleAttackActionContext.cs`，声明
  `BattleAttackActionId` 与 `BattleAttackActionContext`
- 新增 `BattleAttackResolutionFact.cs`
- 新增 `BattleCounterattackContracts.cs`
- 新增 `BattleWeaponRangeType.cs`，声明
  `BattleWeaponRangeTypeKind`、名称映射与 `BattleAttackDeliveryKind`
- 新增 `BattleReactionBudgetRules.cs`
- 新增 `BattleWeaponAttackOutcomeContracts.cs`
- 新增 `BattleUnitReactionState.cs`
- 新增 `BattleUnitCounterattackCapabilityState.cs`
- `BattleUnitCombatResourceState.cs` 的 known-available stamina commit primitive
- `scripts/systems/battle/core/BattleUnitState.cs` 的字段列表、clone、canonical codec 接线
- 新增 `scripts/systems/battle/core/BattleUnitState.Counterattack.cs`，唯一放置 owner
  gateway、plain encoder 与 strict decoder
- `scripts/systems/battle/core/BattleUnitStatePlainSnapshot.cs` 的 detached projection

### Rules

- `scripts/systems/battle/rules/BattleDamageResolver.cs`
- `scripts/systems/battle/rules/BattleReportFormatter.cs`
- `scripts/systems/battle/rules/BattleStatusSemanticTable.cs` 的 TU granularity core-owner 迁移
- `scripts/systems/content/skills/BattleUnitSkillDefinitionExecutionRules.cs` 的共享
  weapon-damage definition predicate
- 新增 `BattleAttackDeliveryRules.cs`
- 新增 `BattleCounterattackRules.cs`

### Content projection / validation

- `scripts/enemies/EnemyTemplateDef.cs` 的 natural/unarmed/equipped 最终 weapon range
  projection 与最终投影校验
- `scripts/player/warehouse/ItemContentRegistry.cs` 的 melee/ranged 闭集校验

### Runtime

- 新增 `BattleEffectExecutionContextService.cs`
- 新增 `BattleAttackActionCoordinator.cs`
- 新增 `BattleReactionBoundarySafetyRules.cs`
- 新增 `BattleCounterattackSystem.cs`
- 新增 `IBattleCounterattackChanceRoller.cs`（含 production shared-RNG adapter）
- 新增 `BattleCounterattackQueryService.cs`
- 新增 `BattleImmediateWeaponAttackService.cs`
- 新增 `IBattleCounterattackWeaponAttackDefinitionProvider.cs`（含
  `BattleRuntimeCounterattackWeaponAttackDefinitionProvider`）
- 新增并从标准路径抽取 `BattleWeaponAttackOutcomeCommitter.cs`
- 新增并从 orchestrator 抽取 `BattleEquipmentDurabilityResultProjector.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs` 的
  setup/bind/clear/dispose、root wrapper 与 unit-admission gateway
- `scripts/systems/battle/runtime/BattleRuntimeModule.RuntimeEffects.cs`
- `scripts/systems/battle/runtime/BattleSkillMasteryService.cs` 的 weapon-training
  skill 映射抽取与 stateless counterattack grant builder
- `BattleRuntimeModuleBorrowerSet.cs` 的 borrower 注册、拓扑签名与逆序 teardown
- `BattleTimelineDriver.cs`
- `BattleRuntimeSkillTurnResolver.cs`
- `scripts/systems/battle/terrain/BattleTerrainEffectSystem.cs`
- `scripts/systems/battle/ai/BattleAiWaitActionEvaluator.cs`
- `BattleSkillExecutionOrchestrator.cs`
- `BattleSkillExecutionOrchestrator.AutoCast.cs`
- `BattleRandomChainSkillService.cs`
- `BattleNineEchoFinalHammerResolver.cs`
- `BattleRepeatAttackResolver.cs`
- `BattleChargeResolver.cs`
- `BattleGroundEffectService.cs`
- `BattleEquipmentAbilityRuntimeService.cs`
- `BattleEquipmentSummonResolver.cs`
- `BattleSpawnPlacementService.cs`
- `BattleContingencyBridgeService.cs`
- `BattleContingencySystem.cs`
- `BattleMetricsReportService.cs` 与当前 origin stack 的迁移调用点
- `scripts/systems/battle/ai/BattleAiMutationSnapshots.cs`
- `scripts/systems/battle/ai/BattleAiMutationStableProjection.cs`

### Progression

- `scripts/player/progression/ProgressionContentRegistry.cs` 的 equipment validation context
- `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityStatusDeclarationCatalog.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityBindingValidator.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityPayloadValidators.cs`
- 新增 `scripts/systems/progression/SkillProfessionPromotionRules.cs`
- `scripts/systems/progression/LevelGrowthEvaluationService.cs`
- `scripts/systems/progression/ProgressionService.cs`
- `scripts/systems/progression/ProfessionRuleService.cs`
- `scripts/systems/progression/CharacterManagementModule.ContentDefs.cs`

### Regression（P1A）

- 新增 `tests/shared/BattleReactionRootTestHelper.cs`，唯一承载
  `ExecuteInReactionRoot(...)` 两个 overload
- 新增
  `tests/static_analysis/run_battle_reaction_contract_static_regression.cs`，统一拥有
  direct-entry root manifest、resolver required-context 与 battle TU constant 三个静态门
- 新增 `tests/battle_runtime/runtime/run_battle_counterattack_action_contract_regression.cs`
- 新增 `tests/battle_runtime/runtime/run_battle_counterattack_queue_regression.cs`
- 新增 `tests/battle_runtime/runtime/run_battle_counterattack_execution_parity_regression.cs`
- 新增 `tests/battle_runtime/state_schema/run_battle_counterattack_state_regression.cs`
- 更新 `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`：枚举正式
  enemy template definition，natural/unarmed/equipped 最终 projection 的 range type
  必须被 `BattleWeaponRangeTypeNames.Parse(...)` 解析为非 `Unknown`
- 更新
  `tests/battle_runtime/runtime/run_encounter_roster_builder_typed_boundary_regression.cs`：
  真实 natural、unarmed 与 equipped enemy 进入 `BattleState` 后仍保留同一 concrete
  melee/ranged range type
- 更新 `tests/runtime/validation/run_item_recipe_registry_typed_regression.cs`：weapon
  `range_type` 为空或任意非 `melee/ranged` 值都在 registry seal 前失败
- 更新 `tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_battle_metrics_collector_regression.cs`：不再单参数
  调用 `_record_turn_started(...)`，传入 batch 并在调用前后打开/完成 reaction root
- 更新 `tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`：
  owner-turn fixture 在 `_record_turn_started(...)` 外打开/完成 reaction root
- 更新 `tests/text_runtime/commands/run_contingency_text_commands_regression.cs`：
  owner-turn fixture 在 `_record_turn_started(...)` 外打开/完成 reaction root，完成后再
  flush/report 断言
- 更新 `tests/battle_runtime/runtime/run_glory_weapon_ability_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_memoryeater_vine_weapon_ability_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_executioner_axe_weapon_ability_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs`
- 更新 `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`：
  validation context 的两个构造点用 `KnownSkillDefinitions` 替换 `KnownSkillIds`，并新增
  `immediate_weapon_attack` 引用 non-weapon skill 时产生
  `EQA_IMMEDIATE_WEAPON_ATTACK_REQUIRES_WEAPON_DAMAGE` 的 fixture
- 更新 `tests/battle_runtime/fate/run_fate_low_luck_tactical_skills_regression.cs`
- 更新 `tests/battle_runtime/skills/run_warrior_repeat_attack_mastery_bonus_regression.cs`
- 新增 `tests/progression/core/run_weapon_training_promotion_policy_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_battle_validation_result_projection_regression.cs`
- 更新 `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`
- 更新 `tests/battle_runtime/ai/run_battle_ai_unit_snapshot_regression.cs`
- 审计并运行
  `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`：
  当前 `Payload()`/round-trip case 必须由更新后的 `ToDictionaryLease()` 自动携带两个新
  顶层字段，missing/extra-field case 继续按 strict schema 拒绝
- 审计并运行
  `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`：三个
  `BattleUnitState.FromDictionary(...)` case 继续分别保持两条 malformed rejection 与一条
  canonical round-trip
- 更新 `tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_battle_ground_effect_typed_sets_regression.cs`：显式
  root + logical scope，并传 required action context
- 更新 `tests/battle_runtime/ai/run_battle_ai_charge_path_aoe_behavior_regression.cs`：两个
  direct charge caller 外显式 root
- 更新 `tests/battle_runtime/runtime/run_prismatic_sphere_special_entry_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs`
- 更新 `tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs`
- 更新 `tests/battle_runtime/skills/run_time_stasis_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_plague_tongue_weapon_ability_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_void_axe_weapon_ability_regression.cs`
- 更新 `tests/battle_runtime/runtime/run_sands_time_weapon_ability_regression.cs`
- 更新 `tests/battle_runtime/rules/run_weapon_hit_combo_stack_regression.cs`、
  `tests/battle_runtime/skills/run_hunter_mark_skill_regression.cs` 与
  `tests/battle_runtime/skills/run_warrior_perfect_rhythm_regression.cs`：删除 resolver 四参
  overload 后显式传 `new AttackContext()`
- 更新 `tests/shared/FixedHitOneDamageResolver.cs`、
  `tests/shared/FixedSuccessOneDamageResolver.cs`、
  `tests/shared/StageOutcomeDamageResolver.cs`、
  `tests/battle_runtime/skills/run_warrior_nine_echo_final_hammer_regression.cs`、
  `tests/battle_runtime/skills/run_warrior_overhead_chop_regression.cs` 与
  `tests/battle_runtime/runtime/run_plague_tongue_weapon_ability_regression.cs`：override 的
  `AttackContext` 参数删除 optional default

### Presentation（P1B）

- 新增
  `scripts/systems/battle/core/BattleCounterattackPreviewContracts.cs`
- `scripts/systems/battle/core/BattlePreview.cs`
- `scripts/systems/battle/core/BattlePreviewProjection.cs`
- 新增
  `scripts/systems/battle/runtime/BattleCounterattackPreviewService.cs`
- `scripts/systems/battle/runtime/BattleSkillPreviewService.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModuleBorrowerSet.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- `scripts/systems/battle/presentation/BattleHudSnapshot.cs`
- 新增
  `tests/battle_runtime/runtime/run_battle_counterattack_preview_regression.cs`
- 更新
  `tests/battle_runtime/presentation/run_battle_hud_typed_projection_regression.cs`
- 更新
  `tests/battle_runtime/runtime/run_battle_projection_lease_regression.cs`
- 更新
  `tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs`：除 focus snapshot
  参数顺序外，target-typed `new(` 构造的 `BattleHudSnapshot` 也传入 risk list/coverage 两个
  required 参数
- 再次更新
  `tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs`

不修改 `data/configs/skills/`、技能 definition/validator 或其他具体内容资源。

---

## 十六、回归要求

### 16.1 action contract

- `BattleAttackDeliveryRules.Resolve(...)` 对含武器伤害的 natural、unarmed、equipped
  projection 分别得到 concrete melee/ranged；同一 concrete range type 即使 active dice
  为 present-empty 也保持相同 delivery。missing owner、range `<= 0` 或未知 range type
  才返回 `Unknown`。
- 枚举正式 enemy template → definition → encounter unit 的最终 weapon projection，所有
  能执行 weapon-damage skill 的单位都不得得到 `Unknown`；item registry 拒绝空值和任意
  非 `melee/ranged` range type。
- 未绑定 `_state` 或 `command == null` 时，`IssueCommand(...)` 返回非空的空 batch，
  coordinator root begin count 为 0，origin depth、queue、work count 与 RNG 调用均不变。
- `_apply_unit_skill_result(...)` 的 null source、null target、null batch 分别在入口抛
  `ArgumentNullException`，且 resolver、RNG、状态 mutation、report/log append 调用数均为
  0；production caller 继续只传 validated non-null target。
- 普通单目标与多目标共享正确 action id。
- repeat/charge stage 复用父 action id。
- equipment immediate 的每个 target execution、每个 AutoCast、每次延迟 ground trigger 使用独立 ID。
- 对五个 production resolver call site 分别断言
  `Action/Action.DeliveryKind/EventBatch` 非空且 batch 同 root；
  `BattleNineEchoFinalHammerResolver` 与 random-chain 透传父 context。
- 两个直接调用 `ResolveUnitSkillEffectResult(...)` 的 fixture 必须显式 root/logical context；旧 null-batch 调用不再编译。
- `BattleDamageResolver` 的四参 `ResolveAttackEffects(...)` overload 不再存在，唯一五参
  overload 与全部 override 的 `AttackContext` 都没有 default；三个原四参 runner 显式传
  `new AttackContext()`，`run_trait_trigger_regression` 保留自己的确定性 context。
- `run_warrior_repeat_attack_mastery_bonus_regression.cs:72/:96` 两个 direct repeat fixture
  必须先把 source/target 放入 `BattleState`、调用 `SetupStateForTests(...)`，再用
  `ExecuteInReactionRoot(...)` 打开 root，并把该 root 内唯一 logical scope 的
  `Context` 传给 `ApplyRepeatAttackSkillResult(...)`；不增加 test-only nullable overload。
- sink 已绑定时缺失 action context fail fast。
- logical scope 已完成/释放后，在同一尚未结束的 root 内用旧 context 再发布 fact，必须在
  `RequireActiveLogicalAttack(...)` 处 fail root；queue、RNG 与 attempt cost 调用数都为 0。
- 前一 root 的 context/fact 带入后一 root 时，`RootBoundaryId` mismatch 必须在消费
  `AttackFact` work item 前 fail root；不允许按相同 unit/action 数据重新接纳。
- nested action 必须复用 root batch。
- charge 全路径只观察一个 batch instance，旧 `chargeBatch`/`MergeBatch` 不再存在。
- 每个 command 与 battle-confirm 各打开一个 root；每个 timeline tick、dead-active cleanup、ready-unit activation 各打开一个顺序 root，全部复用 `advance(...)` 的同一 batch 且不重叠。
- `advance(...)` 的 AI/失控 command 只由 `IssueCommand(...)` 打开 command root，本地 `advance:1010` batch 不形成空 outer root。
- `SubmitPromotionChoiceCore(...)` root begin count 保持 0；其 batch-only 路径不可到达 logical attack。
- `ExecuteAutoCast`、turn-start release、sequential release 缺 batch 或 active boundary 时 fail fast，不隐式 `_new_batch()`。
- §8.1 direct-entry 静态审计表中的 ground、charge、AutoCast、pending cast、command、
  timeline tick 与 ready-unit activation fixture 全部使用 `ExecuteInReactionRoot(...)`；
  timeline/AutoCast case 的 origin 与 production segment 相同，ground fixture 还显式传入
  当前 logical scope 的 `Context`。
- applied chain damage 传 null definition 或空 `SkillId` 时在
  `BattleChainDamageService` 输入边界 fail fast；unapplied result 仍保持现有 early return。
- `FromWeaponAttackResult(...)` 对不含真实 weapon dice 的 result 即使
  `sourceActionId` 为空也返回 `None`；真实 weapon-damage result 的空 id 必须稳定抛错。
- 静态验收
  `rg -n "const int\s+(TuGranularity|TU_GRANULARITY)\s*=" scripts/systems/battle`
  只返回 `BattleTimelineState.cs:9`；timeline、skill-turn、terrain、AI wait evaluator 与
  status semantic table 都引用 `BattleTimelineState.TuGranularity`。

### 16.2 resolver fact

- miss 出口在 combo clear 与 one-shot consume 后发布一次 fact。
- hit 出口在 resolver 全部同步 reaction/durability/report 后发布一次 fact。
- null source/target guard（`BattleDamageResolver.cs:505-512`）不发布 fact；同一调用不得同时走 guard 与正式出口。
- miss/hit 都通过同一个 `FinalizeAndPublishAttackResolution(...)` 返回 finalized result，sink callback 计数各为 1；helper 前后 result 的 report/damage aggregate 不漂移。
- delivery kind、origin、unit id、结果不丢失。
- `RootBoundaryId <= 0` 在 pure rules 中固定得到 `InvalidFact`；合法 fact 的 root id 只用于
  同步 contract，不进入 report 或 dedupe key。
- fact 不保留 live unit/state 引用。
- sink callback 的 batch 必须与 active root `ReferenceEquals`；传另一个非空 batch 也必须 fail fast，且 queue count 保持 0。

### 16.3 queue 与顺序

- AoE 所有目标原结算完成后才开始反击。
- 同一 action/pair 多 stage 只入队一次。
- 独立 follow-up action 可产生新的反击机会。
- nested enqueue 在同一 drain 尾部执行且最终 queue 为空。
- damage hook AutoCast 的 nested fact 先于 outer fact 入队，并按该实际发布时间 FIFO 执行。
- 顺序不依赖 dictionary/hash iteration。
- 异常 boundary 清 queue，不把 entry 泄漏到下一动作。
- 用 injectable 小阈值分别覆盖 depth guard 与 work guard；超限不继续 drain、不成功返回且下一 root boundary 从空状态开始。
- drain/work guard 抛出的原始异常必须原样穿过 logical/outer boundary dispose。fixture
  分别覆盖 logical scope 存活期内的 `AttackFact` 超限、父 logical scope 内 nested
  `LogicalAttack` 超限、父 logical scope 内 nested boundary depth 超限，以及不在 logical
  scope 内的 `CounterattackDequeue` 超限。三个 work-guard case 断言最终异常仍包含
  root boundary id、`depth=` 与 `consumed=`；depth-guard case 断言仍包含
  `"reaction boundary depth"`、实际 next depth 与配置上限。所有 case 都断言最终异常既不是
  `"logical attack disposed without Complete()"`，也不是
  `"reaction boundary disposed without Complete()"`。清理后下一 root 从空状态开始。
- 分别持有旧 generation 的 boundary handle 与 logical handle，调用
  `StopAcceptingAndAbort()` 后再 `ResetForBattle()` 并创建 scope id 重复的新 root；旧
  handle 的 `Complete()`/`Dispose()` 均不得抛错、不得完成或弹出新 frame、不得改变新
  root 的 batch/work/depth。新 generation scope 仍严格执行 Complete/LIFO 合同。

### 16.4 eligibility 与成本

- capability 捕获后移除会阻止执行。
- 事件后新增 capability 不追溯触发。
- alive 但 HP=0 的单位不能反击。
- 非敌对、自身、lock、硬控、无 charge、越界、屏障、stamina 分别 fail closed。
- capability 引用缺失 definition、source 不拥有 definition、非武器 effects 分别固定得到
  `attack_unavailable`；provider 全程不比较 `"basic_attack"`，且同一个 prepared plan 被
  query/execute。
- 把前一 battle 的 prepared plan 传给 `Query(...)` 或 `Execute(...)` 都在读取 range/barrier、
  扣 cost、RNG 和 resolver 之前抛 `immediate attack plan belongs to another battle`。
- availability 对 definition/range/barrier/stamina 的 `Failed/NotEvaluated` 状态与步骤顺序完全匹配。
- charge + stamina 要么同时扣，要么都不扣。
- chance failure 扣 attempt 成本但不消费 attack RNG。
- chance 0/100 不消费共享 chance RNG；1..99 恰好消费一次，失败后 resolver/attack RNG 调用数保持 0。
- blocked 与 chance-failed 两条 `counterattack_attempt` report 的
  `effect_origin.origin_kind` 都是 `Counterattack`，且 origin 与 entry 字段中的
  `triggering_attack_action_id` 都等于原攻击 action id；不得继承父 command/timeline origin。
- equipment immediate 的 referenced skill 为 non-weapon effects 时内容校验失败；绕过
  authored validator 的 synthetic request 在 factory 以稳定 invariant 异常失败。
- equipment immediate 的 weapon effects 合法但 source weapon projection 为 missing/
  unusable 时，factory 返回 `DefinitionAvailable=false`，caller 不调用 resolver、不增加
  `attackCount`、不产生 summary，也不抛 constructor 异常。

### 16.5 execution parity

- 反击保留暴击、非物理武器附伤、装备 bonus dice、装备 reaction、耐久与 combo。
- 反击伤害可触发 contingency。
- 反击直接攻击不产生反击。
- contribution、rating、metrics 与 kill provenance 正确。
- 不消费 AP、不设置 cooldown/cast；不发放 counterattack action/definition mastery。
- sword/bow/unarmed 三类已映射武器分别在反击 `Applied + critical` 与
  `Applied + real weapon-dice max` 时恰好发放一次对应 training mastery；普通命中、
  miss、unapplied、未映射 weapon family 与非 party/source-member 单位均为 0。
- `run_battle_counterattack_execution_parity_regression.cs` 必须单独构造
  `AttackSuccess=true && CriticalHit=true && Applied=false`，同时保证没有 shield/special
  producer。断言 `BuildCounterattackWeaponTrainingMasteryGrant(...) == null`，且完整执行后的
  root batch 中该 member/training skill 的 mastery change 数为 0；不能用普通 miss 代替这条
  case。
- 同一 runner 新增一个合法的非 `basic_attack` 武器 action fixture：它自己的
  `mastery_trigger_mode="status_applied"`、
  `mastery_amount_mode="per_cast_hp_ratio"`，本次结果不施加 status，但满足
  `basic_attack` 的 `Applied + critical`。反击必须实际执行该 action 的
  effects，却仍按 `basic_attack` 的 `WeaponAttackQuality + PerTargetRank` 生成
  weapon-training grant；action skill id 的 mastery change 数必须为 0。
- weapon training amount 对 normal/elite/boss 目标继续是当前
  `PerTargetRank` 的 `1/2/3`。每个正向 case 在执行前后读取 canonical
  `UnitSkillProgress.total_mastery_earned`，增量必须等于 expected amount；同时必须用下面的
  typed helper 直接检查 root batch 中的 progression delta，changed unit 只能作为 HUD dirty
  fact，不能作为 mastery 已发放的证据。

`run_battle_counterattack_execution_parity_regression.cs` 必须包含等价于下面的 helper；
不得只检查 projection dictionary、log text 或 `ChangedUnitIdsTyped`：

```csharp
private CharacterMasteryChangeFact
    AssertSingleWeaponTrainingMasteryChange(
        BattleEventBatch batch,
        StringName memberId,
        StringName expectedSkillId,
        int expectedAmount
    )
{
    if (batch == null)
    {
        _test.Fail("反击 root batch 不得为空。");
        return null;
    }

    CharacterMasteryChangeFact matched = null;
    int matchCount = 0;
    foreach (
        CharacterProgressionDelta delta
            in batch.ProgressionDeltasTyped
    )
    {
        if (delta == null || delta.member_id != memberId)
            continue;
        foreach (
            CharacterMasteryChangeFact change
                in delta.MasteryChangesTyped
        )
        {
            if (
                change == null
                || change.SkillId != expectedSkillId
            )
            {
                continue;
            }
            matchCount++;
            matched = change;
        }
    }

    _test.Eq(
        matchCount,
        1,
        "反击应恰好写入一条目标武器精通 mastery change。"
    );
    if (matched == null)
        return null;

    _test.Eq(
        matched.MasteryAmount,
        expectedAmount,
        "武器精通数量应匹配 basic_attack amount policy。"
    );
    _test.Eq(
        matched.SourceType,
        new StringName("battle"),
        "武器精通来源应为 battle。"
    );
    _test.Eq(
        matched.SourceLabel,
        "战斗",
        "武器精通来源标签应保持正式值。"
    );
    _test.Eq(
        matched.ReasonText,
        "反击：武器高质量攻击",
        "武器精通 reason 应保持 typed grant 的正式文本。"
    );
    return matched;
}
```

负向 case 使用同一层 typed traversal 统计对应 member/skill 的 matching change，必须恰好为
0。非 `basic_attack` action case 还要对 `plan.SkillDefinition.SkillId` 做第二次统计并断言
为 0，防止实现同时发放 action mastery 与 weapon-training mastery。
- weapon-training grant 即使把 skill 推到有效最大等级，也固定
  `needs_promotion_modal=false`、pending profession choices 为空、
  `batch.modal_requested=false`，且 battle state 的 modal 仍为 `None`、timeline 不冻结；
  同一 root 中其后的反击 queue entry 仍全部排空。
- 正向 weapon-training case 必须以 `emit_achievement_event=true` 走完整
  `ApplySkillMasteryGrantTyped(...)`，断言同一 progression delta 恰好追加一条
  `skill_mastery_gained` achievement，同时仍不产生 pending profession choice 或 promotion
  modal；control case 以 `emit_achievement_event=false` 调用同一 progression API，断言 mastery
  与 skill level 照常增长但该 achievement 为 0。不能用 changed-unit、日志或 modal 状态反推
  achievement 是否保留。
- 已有其他核心技能 pending profession choice 时，weapon-training grant 返回的 delta
  仍不携带该 choice、不请求 modal，但 canonical `UnitProgress` 中的既有 choice 保持不变。
- `run_weapon_training_promotion_policy_regression.cs` 对带 `weapon_training` 标签的已学会
  技能调用 `ProgressionService.SetSkillCore(..., true)` 仍成功；随后
  `SetActiveTriggerCoreSkillTyped(...)` 必须返回
  `skill_cannot_trigger_profession_promotion` 且不修改 active trigger；再人工注入非法 active
  trigger，`IsActiveTriggerReadyForLevelUp(...)` 返回 false、`ApplyLevelUpTyped(...)` 返回
  `trigger_skill_not_ready`、canonical
  `UnitProgress.PendingProfessionChoicesTyped` 为空、
  `ProfessionRuleService.CanRankUpProfession(...)` 返回 false，直接
  `ProgressionService.PromoteProfession(...)` 也返回 false 且职业 rank、promotion record、
  HP 与 active-trigger lock 状态全部不变。这里不能用含糊的“profession candidate 列表”
  代替两个 owner 的分别断言。`CanRankUpProfession(...)` fixture 要让其余 rank、属性、声望
  与已学技能 gate 全部通过，并令 required tag 只有“把非法 active trigger 预览分配给该
  profession”时才可满足，避免用另一个失败 gate 伪造 false。
- 同一 progression runner 以普通非 weapon-training 核心技能作为 control，证明达到有效
  最大等级时仍会生成 pending profession choice 和 promotion modal 请求。
- weapon 在 resolver/耐久 reaction 中损坏或被替换时，grant 仍使用 plan 创建前冻结的
  `WeaponTrainingSkillId`，不能转记到 terminal 后的新 weapon projection。
- counterattack weapon-training 路径不调用 `_skill_mastery_service.Clear()`、
  `RecordTargetResult(...)` 或 `ResolveActiveSkillMasteryAmount()`；预置一条 command
  accumulator sentinel 后执行反击，sentinel 的数量与内容必须不变。
- counterattack 不产生 `_record_skill_success`、battle-rating cast success 或
  `"skill_used"` achievement，也不把 capability/action definition id 记为 mastery
  skill id。
- 标准武器技能路径抽取 committer 前后 snapshot/report/metrics/mastery 行为等价；非武器技能路径保持原 owner 与行为。
- 标准武器技能仍保持 defeated handling → metrics/contribution → guard mastery grant 的尾部顺序；guard mastery 明确发生在 `CommitTerminalOutcome` 返回后。
- 标准武器 applied=false 路径只调用 resolver surface → post-producer hooks → result surface，不调用 terminal，也不应用 guard mastery；applied=true 才继续 terminal。
- Counterattack outcome 的 resolver surface、post-producer hooks、result surface 与
  terminal outcome 各提交一次；只有 applied 路径在 terminal 后额外尝试一次 stateless
  weapon-training grant，source-bound/last-stand/guard/Vajra/target-result mastery
  调用数保持 0。
- 标准、反击、装备追击的击杀分别断言 `WeaponAttackOutcomeKind` 为 `StandardWeaponSkillAttack/Counterattack/EquipmentReaction`；forced-critical equipment attribution 覆盖 equipment/binding/action 时不得改写 kind。
- 装备即时攻击迁移到 shared service 后，service 内保持 resolver → typed summary/changed facts/追击日志 → defeated handling 的既有同步顺序；changed facts 与追击日志各恰好提交一次，返回 summary 由装备 runtime 恰好聚合一次。
- EquipmentReaction 保持既有 contribution/rating/metrics 基线，不在迁移中静默补齐。
- `run_glory_weapon_ability_regression.cs:235-276` 继续断言免费追击、目标受伤、far target 不受伤、changed unit；fixture 在 `ResolveOnKill(...)` 外显式打开 root。
- `run_memoryeater_vine_weapon_ability_regression.cs:328-383` 继续断言即时追击击杀会同步触发生命簿 on-kill 链；该 fixture 同样显式 root，证明 service 迁移没有截断 nested equipment reaction。

### 16.6 timeline/state

- 开局初始化、容量 0、跨多间隔回满、anchor 不右漂。
- stasis step 平移 anchor 且不补 charge。
- stasis 在本 step 末到期时仍不追赶补充。
- 不连续两段 stasis（中间夹普通推进）后，`NextRechargeAtTu` 精确增加两段冻结时长之和；未来阈值不 clamp 到当下 TU。
- `AdvanceTimeStasisFrozenTimers(null, positiveElapsed, batch)`、零 elapsed 与负 elapsed 都
  返回 false，不调用 cooldown/reaction anchor API，也不抛异常。
- clone/codec/canonical/detached/mutation exact 覆盖两个新 owner。
- missing owner、present-empty capability、present-zero budget 在 strict/mutation snapshot 中保持三种不同状态。
- 开局 ally/enemy/scenario actor、`PlaceUnitsForTestsTyped(...)` 后置加入单位，以及
  `BattleEquipmentSummonResolver` 两个 summon overload 创建的单位，在首次进入 state 前都
  已有 present reaction/capability owner；召唤时 anchor 从当前 timeline TU 起算。
- admission helper 收到非当前 runtime state 时 fail fast，且 `state.SetUnit(...)` 尚未发生。
- clear/rebind/dispose 后 owner/sink/scope/queue 全部归零。
- P1A 的 borrower topology regression 必须断言 type signature 中没有
  `BattleCounterattackPreviewService`，`CounterattackQuery` 后直接是 `CommandPreview`，且
  setup/bound borrower 数与 teardown 后归零数匹配 P1A 数组；该切片必须在完全不存在 P1B
  preview type 的源码状态下编译通过。
- P1B 的同一 regression 再断言 `BattleCounterattackPreviewService` 恰好出现一次，顺序固定为
  `CounterattackQuery → CounterattackPreview → CommandPreview`，setup/bound borrower 数只比
  P1A 增加一，逆序 teardown 后仍全部归零。

### 16.7 preview（P1B）

- preview 不消费 RNG/charge/stamina，不写 queue。
- 单段只有 hit capability、只有 miss capability、两者都有三种组合分别匹配 execution。
- preview/execution 数值 parity fixture 必须固定“原攻击到 drain 之间不改变 defender
  position/stamina/reaction/lock/hard-control/capability 且 defender 存活”；另设一例在
  producer 后处理改变 readiness，断言 execution 以 drain 时复核为准而 preview 仍保留
  `Complete` 的当前状态估计，不能把二者差异当作 queue bug。
- 多段只有一种 trigger capability 时，计算“第一次出现受支持 trigger”的概率；hit/miss
  capability 都存在时只由第一 stage 决定。capability branch 即使 readiness blocked
  也终止 continuation，不能在后续 stage 假设重试。
- capability chance 0/100 与 stage chance 0/100 不溢出、不产生负数，最终值固定在
  `[0, 10_000]`。
- deterministic AoE 按 defender id 保留第一次出现并聚合，expected-count basis points
  可以大于 `10_000`；单个 entry 不得大于 `10_000`。
- random-chain 固定得到 `RandomTargetSelectionUnknown`、ground/charge attack 固定得到
  `ProducerSequenceUnsupported`，两者 entries/aggregate 为空但不得显示成“无风险”。
- `PrepareCounterattack(...)` 与 `Query(plan)` 各 branch 各调用一次；plan 的 state
  mismatch 在 range/barrier/resource 读取前 fail fast。
- definition damage envelope 只断言当前 range service 的 base definition/weapon dice，
  不把装备条件附伤或暴击冒充 total damage。
- risk 数据经 `BattlePreview -> BattleHudAdapter -> BattleHudSnapshot` 传递。
- `SelectedSkillPreviewTooltipText` 在原 save/hit/damage/fate 顺序后追加当前状态风险；
  `Complete + 0` 不追加，三种 unsupported coverage 显示各自“未计算”文案，现有
  `BattleMapPanel.cs:1332` 无需新绑定。
- public Godot projection 与 HUD snapshot 不暴露 capability instance id、block reason、
  stamina 或敌方 reaction budget。
- party-backed focus unit 显示 detached reaction budget；非 party-backed focus unit
  固定 `Visible=false`。
- 用 `BattleAiMutationSnapshot` 包住 preview 调用，两个新 owner 与既有 state 全部 exact
  match。
- projection lease dispose 后新增 `GArray/GDictionary` 没有泄漏，两个直接 focus
  constructor fixture与两处直接 HUD constructor fixture 已迁移 required 参数；其中
  `run_battle_map_panel_schema_regression.cs` 的 target-typed `new(` 也必须覆盖。
- `BattlePresentationDelta` 只携带 dirty facts。

---

## 十七、已定架构决策与不变量

### 17.1 已定

1. 采用 central fact sink + explicit action/boundary coordinator，不采用 orchestrator-only 或 inline execution。
2. logical attack ID 是去重唯一身份；drain 周期不是攻击身份。
3. 一个 active root boundary 只有一个 `BattleEventBatch` 实例；不设计 child batch 谱系，charge 删除局部 batch/merge。`advance(...)` 可按 tick/activation 顺序打开多个不重叠 root，但都复用同一个用户可见 batch。
4. root boundary 只由 command、timeline mutation segment、battle-confirm 或显式 fixture entry 打开；promotion 不打开，AutoCast 必须复用 active boundary/batch，不隐式创建。
5. effect origin 有独立 scope owner；origin 与 action id 在攻击创建时冻结并显式进入 `AttackContext`。
6. delivery kind 由 typed rule 根据正式攻击与 weapon projection 冻结，不从 skill id/tag/文本推导。
7. resolver sink 只发布 immutable fact。
8. capability 在事实发生时捕获，在排空时复核实例仍存在。
9. 同一 `(ActionId, attacker, defender)` 只产生一次机会。
10. queue 按事实实际发布顺序 FIFO；damage hook nested fact 可以先于 outer fact，nested enqueue 同 boundary 排到队尾并 drain 到空。
11. 排空发生在 producer 全部正式后处理之后、entry point 返回之前。
12. depth/work guard 是 coordinator 级技术熔断；正式终止仍由 origin、charge 与 contingency 去重/消费保证。
13. counterattack 使用 dedicated immediate weapon attack mode，并通过四阶段 committer 共享完整真实攻击提交。
14. direct counterattack origin 关闭 reaction，保留 contingency/equipment。
15. reaction budget 是单位级 TU owner；stasis 平移未来 recharge threshold，不按 current TU clamp。
16. 两个新 unit component 统一使用 explicit `OwnerPresent`，不以 null collection 兼任 missing owner。
17. P1A config battle-start 后不可动态修改。
18. battle-local state 不进存档，但必须进 codec/clone/snapshot/mutation。
19. P1A 不接生产内容来源，不按具体 skill id 特判。
20. P1B risk 是 drain 前当前状态条件估计；不把 producer 后处理造成的 readiness 变化伪装
    成精确预测。
21. 缺失 random/ground/outcome 概率时必须发布 typed unknown coverage，不用 empty entries
    表示“零风险”。
22. P1B 精确 reaction budget 只对 party-backed focus unit 可见；battle rules 与 typed
    internal snapshot 不按阵营删事实。
23. counterattack 不获得动作 definition 熟练度，但 applied 的高质量真实武器攻击必须按
    攻击前冻结的 weapon projection 获得对应 weapon-training 熟练度。
24. `weapon_training` 是 progression 类别而不是职业升级来源：无论 mastery 来自普通攻击、
    反击、训练或其他入口，都不能成为 active level trigger；该类 mastery transaction
    不得请求 promotion modal。
25. counterattack weapon-training 的最终资格包含 `Applied`；这是标准单目标
    `basic_attack` 的有效发放门，不是反击专属收紧。
26. counterattack 的 action definition、canonical `basic_attack` mastery policy 与冻结的
    weapon-training reward skill 是三个独立角色，任何实现不得互相替换。
27. weapon range type 是 `{melee, ranged}` 闭集；enemy natural/unarmed/equipped 与 item
    projection producer 必须产出合法 typed value，内容加载负责拒绝缺失/未知值，生产
    delivery rule 对 `Unknown` fail fast，不能把坏投影兼容成 `NonWeapon`。
28. boundary 与 logical-attack scope handle 都携带 coordinator generation；旧 generation
    handle 的 `Complete/Dispose` 是 no-op，同 generation 的栈与完成顺序仍严格校验。
29. `BattleAttackActionId` 与 `BattleAttackDeliveryKind` 都是 core contract；rules/runtime
    只能消费，不能拥有它们。
30. `BattleDamageResolver.ResolveAttackEffects(...)` 只有一个 required-context overload；
    production 与 isolated fixture 都必须显式选择 `AttackContext`，subclass override 也不得
    重新提供 optional default。
31. production root wrapper 之外的 direct fixture 必须显式建立等价 root/origin；是否“当前
    case 恰好未触发 AutoCast/pending attack”不能作为省略 boundary 的理由。

### 17.2 实施时必须保持

- production sink 已绑定时，无 action id/origin/delivery 的攻击不得静默继续。
- query、preview、AI 不修改状态。
- RNG 只在 attempt 成本提交后消费。
- immediate attack query 与 execute 不复制两套射程/屏障/成本规则。
- equipment reaction 当前同步递归顺序不变。
- charge、AutoCast、equipment、ground 与 repeat 的 nested attack 全部复用 root batch；任何 fallback/new/merge 都是合同错误。
- standard/counterattack/equipment outcome 只通过 typed policy 分流；Counterattack
  只允许 terminal 后的 stateless weapon-training grant，EquipmentReaction 不静默新增
  mastery 或统计。
- battle runtime 不用 `AllowUnlocks` 推断职业升级资格；该字段当前只控制 mastery
  achievement。职业升级资格唯一由 `SkillProfessionPromotionRules` 判定。
- weapon-training 回归必须直接检查 `ProgressionDeltasTyped` 中的
  `MasteryChangesTyped` 和 canonical mastery 增量；`ChangedUnitIdsTyped`、日志或 projection
  文本不能作为发放证据。
- outcome committer 固定四阶段；HP/status hook 与 result log/durability/report 不得重新合并为一次调用。
- standard weapon 路径的 terminal outcome 之后仍按现有顺序提交 guard mastery grant；不得把“最后一个 committer phase”实现成“orchestrator 最后一条语句”。
- EquipmentReaction 的 changed-unit/coord/追击日志由 immediate attack service 的 baseline surface 在 resolver 与 terminal 之间恰好提交一次；装备 runtime 只聚合返回 summary。
- depth/work guard 超限必须使 boundary 失败并清空 transient state，不能作为成功的部分 drain 返回。
- boundary 正常结束时 queue/dedupe/action stack 全空，effect origin stack 恢复到入口 depth。
- 未完成 boundary 不执行反击，也不把队列带入下一入口。

### 17.3 延后，不阻塞 P1A

- production capability grant/authoring provider（不是 §10 的 weapon-action
  definition provider）。
- 默认容量/恢复间隔及属性派生。
- capability chance/bonus 的内容 authoring。
- 是否把 P1B 固定隐藏的非 party-backed 精确 budget 改为可见，以及对应侦察/难度策略。
- AI 风险权重。

这些项目进入各自后续提案；在明确设计前不得通过兼容逻辑或特殊分支预埋。

---

## 十八、Project Context Units 影响

当前只修订 proposal，尚未改变已落地 ownership，因此 `docs/design/project_context_units.md` 继续保持现状。

实际实现 P1A 后必须更新：

- CU-12：登记 weapon-training mastery delta 的 promotion-modal 抑制边界。
- CU-14：登记 `SkillProfessionPromotionRules` 及 level-trigger 的 fail-closed 规则。
- CU-15：登记 `BattleAttackActionCoordinator`、`BattleCounterattackSystem`、`BattleImmediateWeaponAttackService` 的主运行链与排空边界。
- CU-16：登记 attack fact、delivery rules、reaction/capability unit owner、纯资格规则与 snapshot/mutation 边界。
- CU-19：登记反击 action contract、queue、stasis、execution parity 与 weapon-training
  promotion-policy 的 focused runners。

实现 P1B 后再更新 CU-18，说明 counterattack risk 属于 `BattlePreview`/HUD snapshot，`BattlePresentationDelta` 只承担 dirty projection。

CU-13 参与 P1A 的 equipment ability validation context 与
`immediate_weapon_attack` referenced-skill 契约修订，但它当前已经把装备能力 schema、
validator 与 skill definition catalog 列为同一推荐读集，ownership/read-set 未变化，因此
`project_context_units.md` 无需为该字段级加强另行修改。CU-20 不属于本文实施切片；P1A
仍只消费现有 `weapon_training` 标签，不修改技能资源。只有未来接入内容 provider、新增
标签 schema 或 AI score 时才扩展对应 context unit。
