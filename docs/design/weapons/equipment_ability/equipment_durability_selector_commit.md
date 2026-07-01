# Equipment Durability Selector / Commit Split

本文档细化装备能力 V1 的第二个真实阻塞点：当前装备耐久效果入口把“选择哪件装备”和“对该装备提交耐久损失”耦合在同一个方法里。装备能力系统必须先把这条链路拆成 selector 与 selected-target commit，否则 `random_target_equipment`、AI 评估、trace 和实际扣耐久会不一致。

总入口见 [equipment_ability_system.md](../equipment_ability_system.md)。技能入口迁移见 [battle_skill_availability_migration.md](battle_skill_availability_migration.md)。

## 结论

V1 必须把当前 `BattleDamageResolver.ApplyEquipmentDurabilityDamageEffect(...)` 重构为：

```text
ApplyEquipmentDurabilityDamageEffect(...)
  -> BuildEquipmentDurabilitySelectionQueryFromEffect(...)
  -> SelectEquipmentForDurabilityDamage(...)
  -> ApplyEquipmentDurabilityDamageToSelection(...)
```

装备能力路径不能再调用“随机选装备并扣耐久”的旧入口，而是：

```text
EquipmentAbilityTargetSelectorResolver
  -> EquipmentAbilityEquipmentTargetRef
  -> EquipmentAbilityEquipmentMutationAdapter
  -> ApplyEquipmentDurabilityDamageToSelection(...)
  -> AttackEffectResolutionResult.EquipmentDurabilityEvents
  -> BattleSkillExecutionOrchestrator._apply_equipment_durability_result(...)
```

`ApplyEquipmentDurabilityDamageToSelection(...)` 是唯一 selected-target commit。它接受已经选中的装备引用，只 revalidate，不重新随机，不 fallback 到同槽位其它装备。

## 当前代码事实

| 当前 owner | 现有职责 | V1 处理方式 |
| --- | --- | --- |
| `scripts/systems/battle/rules/BattleDamageResolver.DtoHelpers.cs` | `ApplyEquipmentDurabilityDamageEffect(...)` 内部完成 target 选择、save、rarity bonus、耐久扣减、清槽和 event 构造 | 保留旧入口，但拆成 query -> select -> commit 编排 |
| `scripts/systems/battle/rules/BattleDamageResolver.DtoHelpers.cs` | `SelectEquipmentForDurabilityDamage(...)` 读取 `target_slots`、typed `equipment_durability_slot_weights`、`equipment_slot_override` 并使用 `TrueRandomSeedService.RandiRange(...)` | 改成不依赖 `CombatEffectDefinition` 的 query helper；spell effect 路径负责把 typed effect 字段转 query |
| `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs` | `BuildEquipmentDurabilitySelection(...)` 从 `EquipmentState` / `EquipmentEntryState` 构造 private selection，过滤空装备和 `current_durability <= 0` | 扩展为 shared candidate builder 的内部 revalidation 基础 |
| `scripts/player/equipment/EquipmentState.cs` | `GetEntry(...)`、`GetEntrySlotForSlot(...)`、`GetEntrySlotIdsTyped(...)`、`ClearEntrySlot(...)` 是装备槽位事实 API | selected-target commit 必须使用这些 API 精确验证和清槽 |
| `scripts/player/equipment/EquipmentEntryState.cs` | `item_id`、`instance_id`、`occupied_slot_ids`、`GetEquipmentInstance()` 是 entry 事实 | selector 只复制稳定 identity，不把 entry live object 暴露给 handler |
| `scripts/player/warehouse/EquipmentInstanceState.cs` | `current_durability` 是正式耐久事实 owner | 只能由 selected-target commit 经 battle equipment view 修改 |
| `scripts/systems/battle/core/AttackEffectResolutionResult.cs` | `EquipmentDurabilityEventResult` 和 `EquipmentDurabilityEvents` 是 battle result payload | 装备能力耐久 action 继续使用同一 payload，不新增 sidecar result |
| `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs` | `_apply_equipment_durability_result(...)` 负责 log、destroyed 刷新和 changed unit report | 装备能力路径必须产出同一 result，再复用该函数 |
| `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs` | battle-local `equipment_view` 写回 party state | V1 不新增装备能力专属写回 |

现有回归 `tests/battle_runtime/skills/run_spell_disjunction_equipment_durability_regression.cs` 已覆盖旧语义：耐久失败扣减、归零摧毁、`require_damage_applied`、save 成功仍产出 event、稀有度 bonus 影响 save。拆分后这些行为必须保持。

## 问题边界

当前旧入口的真实语义是“随机选一件装备并扣耐久”。这对 spell disjunction 这样的旧效果是合理的，因为效果本身没有预先选中装备。

装备能力不同。`random_target_equipment` selector 已经需要：

- 基于 `target_slots`、item tag、equipment type 和权重构造候选；
- 在 execution 中消费正式 battle RNG 选中一个 `EquipmentAbilityEquipmentTargetRef`；
- 把候选、总权重、roll 和选中 instance 写入 trace；
- 让 AI / preview 在不消费 RNG 的情况下读取候选和期望结果。

如果 action handler 选中装备后再调用旧 `ApplyEquipmentDurabilityDamageEffect(...)`，旧入口会再次随机。结果会出现：

- trace 显示选中了 A，实际扣的是 B；
- AI 按 A 的价值评分，战斗中扣到 B；
- 后续 chained action 读取 A，但装备耐久事件记录 B；
- stale target 本应 no-op，却 fallback 到同槽位或其它随机装备。

所以 V1 不是“给旧接口加一个 slot override”就足够。slot override 只能定位槽位，不能表达 exact `EquipmentInstanceId`、`ItemId`、entry identity 和 occupied slots 的一致性。

## 设计目标

- 旧 skill effect 外部行为保持不变：没有 explicit target 时仍按原规则从装备候选中随机选择。
- 装备能力 action 可以对已选中的 exact equipment ref 提交耐久损失。
- selected-target commit 复用现有 save、rarity bonus、自然 1/20、event result、log、projection refresh 和 writeback 语义。
- selector 不修改装备；commit 不随机。
- preview / AI / snapshot 可以读取候选和权重，但不消费正式 RNG，不提交 mutation。
- stale explicit ref 返回 no-op trace，不重新随机、不 fallback。

## 非目标

- V1 不新增装备能力专属耐久写回 schema。
- V1 不新增 active battle save schema；战斗中不能存档。
- V1 不实现临时禁用盾牌、缴械、维修、永久装备状态。
- V1 不支持一次 action 损坏多件装备；`max_damaged_items` 固定只允许 `1`。
- V1 不允许 handler 直接写 `EquipmentInstanceState.current_durability` 或直接调用 party warehouse service。
- V1 不给旧 payload 增加兼容 alias 或 fallback migration，除非另行确认。

## 数据结构

### Public Target Ref

`EquipmentAbilityEquipmentTargetRef` 是 selector、trace、action handler、AI 和 preview 共享的稳定引用。它只包含值，不包含 live `EquipmentEntryState` 或 `EquipmentInstanceState`。

```csharp
public sealed class EquipmentAbilityEquipmentTargetRef
{
    public StringName UnitId { get; init; }
    public StringName EntrySlotId { get; init; }
    public StringName SlotId { get; init; }
    public StringName ItemId { get; init; }
    public StringName EquipmentInstanceId { get; init; }
    public StringName EquipmentTypeId { get; init; }
    public IReadOnlyList<StringName> OccupiedSlotIds { get; init; }
    public IReadOnlyList<StringName> ItemTags { get; init; }
    public int CurrentDurability { get; init; }
}
```

字段契约：

- `UnitId` 必须等于 commit 时 `TargetUnit.unit_id`。
- `EntrySlotId` 是 `EquipmentState.GetEntry(EntrySlotId)` 的 key；多槽位装备只用 entry slot 做 identity。
- `SlotId` 是 selector 语义命中的槽位，可以是 entry slot，也可以是 occupied slot。
- `OccupiedSlotIds` 是 selector 时刻的快照，用于 trace 和 revalidation。
- `CurrentDurability` 是 selector 时刻的展示/评分值，不作为 commit 事实；commit 必须重新读 live battle equipment view。

### Selection Query

旧 spell effect 和装备能力 payload 都必须先转换成同一个 query。query 不持有 `CombatEffectDefinition`，避免 selector 被 spell schema 绑死。

```csharp
internal sealed class EquipmentDurabilitySelectionQuery
{
    public BattleUnitState TargetUnit { get; init; }
    public EquipmentAbilityConsumerKind Consumer { get; init; }
    public StringName SelectorId { get; init; }
    public IReadOnlyList<StringName> TargetSlots { get; init; }
    public IReadOnlyList<EquipmentSlotWeightDefinition> SlotWeights { get; init; }
    public IReadOnlySet<StringName> RequiredItemTags { get; init; }
    public IReadOnlySet<StringName> RequiredEquipmentTypeIds { get; init; }
    public StringName ExplicitSlotOverride { get; init; }
    public bool ConsumeRandom { get; init; }
    public bool IncludeCandidatesInTrace { get; init; }
    public StringName TraceReason { get; init; }
}
```

构造来源：

- `BuildEquipmentDurabilitySelectionQueryFromEffect(...)` 从 `CombatEffectDefinition` 读取 `target_slots`、typed `EquipmentDurabilitySlotWeights` 和 `DamageResolutionContext.EquipmentSlotOverride` / `"equipment_slot_override"`；`params.slot_weight_map` 不再是 spell effect schema，内容校验必须拒绝。
- `BuildEquipmentDurabilitySelectionQueryFromAbilityPayload(...)` 从 `EquipmentDurabilityDamageActionPayloadDef` 读取 `target_selector`、`target_slots`、typed `slot_weights`、`required_item_tags`、`required_equipment_type_ids`。
- `target_weapon`、`target_shield`、`target_armor`、`target_slot` 可以用同一 query，只是 `ConsumeRandom = false` 且通过 fixed selector 规则限制候选。
- `random_target_equipment` 在 execution 中 `ConsumeRandom = true`；preview / AI / snapshot 中 `ConsumeRandom = false`。

### Candidate

candidate 是 selector 的值对象，用来给 execution 随机和 preview/AI 评分。

```csharp
internal sealed class EquipmentDurabilitySelectionCandidate
{
    public EquipmentAbilityEquipmentTargetRef Target { get; init; }
    public int Weight { get; init; }
}
```

候选构建规则：

- 遍历 `EquipmentState.GetEntrySlotIdsTyped()`。
- 通过 `EquipmentState.GetEntry(entrySlotId)` 读取 entry。
- 排除 `entry == null`、`entry.IsEmpty()`、`entry.GetEquipmentInstance() == null`。
- 排除 `current_durability <= 0`。
- `TargetSlots` 非空时，entry slot 或任一 occupied slot 必须命中允许槽位。
- `RequiredItemTags` 非空时，item tags 必须全部满足；item tag 只来自 content catalog / `ItemDef` 只读事实。
- `RequiredEquipmentTypeIds` 非空时，item equipment type 必须命中。
- 多槽位装备只生成一个 candidate，identity 为 `EntrySlotId`。
- `SlotWeights` 权重按 entry slot 与 occupied slots 取最高正权重；未配置时默认 `1`；selector 内部允许为计算临时构建 typed key/value 索引，但 query / ABI 不暴露 dictionary。
- 内容校验阶段必须拒绝未知 slot、重复 slot、非正权重和静态上必然为空的过滤组合。

### Selection Result

```csharp
internal sealed class EquipmentDurabilitySelectionResult
{
    public bool HasSelection { get; init; }
    public EquipmentAbilityEquipmentTargetRef SelectedTarget { get; init; }
    public IReadOnlyList<EquipmentDurabilitySelectionCandidate> Candidates { get; init; }
    public int TotalWeight { get; init; }
    public int Roll { get; init; }
    public StringName NoTargetReason { get; init; }
}
```

结果规则：

- `ExplicitSlotOverride != ""` 时不随机，直接通过 `EquipmentState.GetEntrySlotForSlot(ExplicitSlotOverride)` 定位 entry；失败则 no target。
- fixed selector 只允许返回一个确定目标；若业务上有多个候选，必须在 selector spec 中定义 order，不允许 handler 自己取第一个。
- weighted selector 在 `ConsumeRandom = true` 时使用正式 battle RNG 在 `[1, TotalWeight]` 内选择；`Roll` 必须写入 trace。
- `ConsumeRandom = false` 时不设置 selected target；只返回 candidates 和 `TotalWeight`。

### Internal Commit Selection

commit 内部需要 live instance，但这个对象只能在 selected-target commit 内部存在，不进入 public trace。

```csharp
internal readonly record struct EquipmentDurabilitySelection(
    StringName TargetUnitId,
    StringName EntrySlotId,
    StringName SlotId,
    IReadOnlyList<StringName> OccupiedSlotIds,
    StringName ItemId,
    StringName EquipmentInstanceId,
    EquipmentInstanceState EquipmentInstance
);
```

`EquipmentDurabilitySelection` 可以由两种方式获得：

- 旧 effect 路径：`SelectEquipmentForDurabilityDamage(...)` 选出 `EquipmentAbilityEquipmentTargetRef` 后，commit 重新 revalidate 成 internal selection。
- 装备能力路径：`EquipmentAbilityEquipmentTargetRef` 直接进入 `TryBuildDurabilityCommitSelection(...)` revalidate。

不建议从 selector 直接把 live `EquipmentInstanceState` 传给 action handler，因为 handler 一旦持有 live object，就可以绕过 save/event/log/writeback 直接改耐久。

### Commit Request

```csharp
internal sealed class EquipmentDurabilityCommitRequest
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public EquipmentAbilityEquipmentTargetRef TargetEquipment { get; init; }
    public CombatEffectDefinition EffectDefinition { get; init; }
    public DamageResolutionContext DamageContext { get; init; }
    public int TotalDamage { get; init; }
    public int TotalShieldAbsorbed { get; init; }
    public StringName SourceKey { get; init; }
    public StringName ActionId { get; init; }
}
```

`EffectDefinition` 在 V1 仍保留，因为当前 save、DC、rarity bonus、`require_damage_applied` 等语义已经绑定在 `BattleDamageResolver` 的 effect 解析链路上。装备能力 adapter 负责把 `EquipmentDurabilityDamageActionPayloadDef` 组装成最小 `equipment_durability_damage` effect，但不得重写 save 逻辑。

长期可在 V2 把 save 字段再抽成 `EquipmentDurabilityCommitProfile`，但 V1 不为这个抽象复制 resolver。

### Mutation Adapter Request

外部装备能力 action 不直接构造 internal commit request，而是走 adapter 的 public request。

```csharp
public sealed class EquipmentDurabilityMutationRequest
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public EquipmentAbilityEquipmentTargetRef TargetEquipment { get; init; }
    public int DurabilityLoss { get; init; }
    public EquipmentDurabilitySaveProfile SaveProfile { get; init; }
    public bool RequireAttackSuccess { get; init; }
    public DamageResolutionContext DamageContext { get; init; }
    public int TotalDamage { get; init; }
    public int TotalShieldAbsorbed { get; init; }
    public BattleEquipmentAbilitySource Source { get; init; }
    public StringName ActionId { get; init; }
}

public sealed class EquipmentDurabilitySaveProfile
{
    public StringName SaveTag { get; init; }
    public int SaveDc { get; init; }
    public bool InheritExistingRarityBonus { get; init; } = true;
}
```

adapter 只做三件事：

- 把 payload/request 变成旧 resolver 可理解的 `CombatEffectDefinition`。
- 把 `TargetEquipment` 传入 selected-target commit。
- 把 commit result 包装成 action executor 可记录的 mutation result。

adapter 不做候选选择，不扣耐久，不清槽，不写 party state。

### Mutation Result

```csharp
public sealed class EquipmentDurabilityMutationResult
{
    public bool Resolved { get; init; }
    public StringName NoOpReason { get; init; }
    public AttackEffectResolutionResult AttackEffectResult { get; init; }
    public EquipmentDurabilityEventResult Event { get; init; }
    public bool Destroyed { get; init; }
    public bool RequiresProjectionRefresh { get; init; }
    public bool RequiresChangedUnitReport { get; init; }
    public EquipmentDurabilityMutationTrace Trace { get; init; }
}

public sealed class EquipmentDurabilityMutationTrace
{
    public StringName ActionId { get; init; }
    public StringName SourceKey { get; init; }
    public EquipmentAbilityEquipmentTargetRef RequestedTarget { get; init; }
    public EquipmentAbilityEquipmentTargetRef RevalidatedTarget { get; init; }
    public StringName NoOpReason { get; init; }
}
```

`Resolved = true` 表示 selected-target commit 已经进入 resolver 语义，包括 save 成功但没有耐久损失的情况。`Resolved = false` 表示 action 没有产生 durability event，例如 target stale、目标不存在、攻击未命中且要求命中。

## Revalidation

`ApplyEquipmentDurabilityDamageToSelection(...)` 必须在 commit 前重新验证 explicit ref：

1. `request`、`TargetUnit`、`TargetEquipment`、`EffectDefinition` 不为空。
2. `TargetUnit.unit_id == TargetEquipment.UnitId`。
3. `TargetUnit.GetEquipmentView()` 可用。
4. `EquipmentState.GetEntry(TargetEquipment.EntrySlotId)` 存在且非空。
5. `entry.item_id == TargetEquipment.ItemId`。
6. `entry.instance_id == TargetEquipment.EquipmentInstanceId`。
7. `entry.occupied_slot_ids` 仍包含 `TargetEquipment.SlotId`。
8. `TargetEquipment.OccupiedSlotIds` 中的槽位仍由同一个 entry 覆盖；使用 `EquipmentState.GetEntrySlotForSlot(slotId)` 验证。
9. `entry.GetEquipmentInstance()` 存在。
10. `equipmentInstance.item_id` 和 `equipmentInstance.instance_id` 与 ref 一致。
11. `equipmentInstance.current_durability > 0`。

任一条件失败时返回 no-op trace，不产出 `EquipmentDurabilityEventResult`，不重新随机，不 fallback 到同槽位其它装备。

固定 no-op reason：

| code | 含义 |
| --- | --- |
| `invalid_request` | request / effect / ref 结构缺失或 payload 非法 |
| `target_unit_missing` | target unit 不存在或 unit id 不匹配 |
| `target_equipment_missing` | entry slot 不存在、entry 为空或 live instance 不存在 |
| `target_equipment_changed` | item id、instance id、occupied slot 覆盖关系和 ref 不一致 |
| `already_destroyed` | live `current_durability <= 0` |
| `attack_not_successful` | effect 要求命中或造成伤害，但 context 不满足 |
| `resolver_rejected` | 旧 resolver 语义拒绝结算，例如 save profile 或 effect 参数非法 |

## Commit 语义

selected-target commit 成功进入 resolver 后，必须保持旧 `ApplyEquipmentDurabilityDamageEffect(...)` 的行为：

- 读取 live `equipmentInstance.current_durability` 作为 `DurabilityBefore`。
- 调用现有 save resolver，保留 `BattleSaveResolver.ResolveSaveResult(...)`、自然 1/20、rarity bonus 和 `SaveResolutionResult` 字段。
- save 成功时产出 `EquipmentDurabilityEventResult`，`DurabilityLoss = 0`、`Destroyed = false`。
- save 失败时 `DurabilityLoss = Math.Min(effect.Power, before)`。
- `after <= 0` 时调用 `EquipmentState.ClearEntrySlot(EntrySlotId)`，event 标记 `Destroyed = true`。
- `after > 0` 时只写 battle-local `equipmentInstance.current_durability = after`。
- 返回 `AttackEffectResolutionResult.EquipmentDurabilityEvents`，交给 `_apply_equipment_durability_result(...)` 统一产生日志、projection refresh 和 changed unit report。

注意：selection RNG 和 save RNG 是两件事。

- `random_target_equipment` execution 会消费一次 selection RNG。
- selected-target commit 不再消费 selection RNG。
- commit 内的 save 仍按现有 save resolver 消费/使用 save roll，包括测试中的 `save_roll_override`。

## Preview / AI / Snapshot

preview、AI 和 snapshot consumer 只能调用 selector query 的候选构建路径：

```text
BuildEquipmentDurabilitySelectionQuery(...)
  -> BuildEquipmentDurabilityCandidates(...)
  -> EquipmentDurabilitySelectionResult(Candidates, TotalWeight, Roll = 0, HasSelection = false)
```

它们不得调用 `ApplyEquipmentDurabilityDamageToSelection(...)`。

preview 建议输出：

- 候选装备列表；
- 总权重；
- 每个候选的当前耐久；
- 预期耐久损失；
- save 成功概率或 save profile 文本；
- 若候选为空，输出 `NoTargetReason`。

AI 建议评分：

- weighted candidate 期望损失：按 `Weight / TotalWeight` 加权；
- 归零摧毁额外价值：比较 `CurrentDurability <= DurabilityLoss`；
- shield/armor/weapon 价值来自 item/slot/type 只读事实；
- 不写 battle state，不污染 `BattleAiMutationGuard` snapshot。

## 实现顺序

1. 在 `BattleDamageResolver` 附近抽出 query/candidate/result 类型，先不接装备能力。
2. 把旧 `ApplyEquipmentDurabilityDamageEffect(...)` 改成 query -> select -> selected-target commit，保持现有测试通过。
3. 为 selected-target commit 增加 focused regression：同一目标装备上有两件候选时，explicit ref 指向 A，commit 只能扣 A，不可随机到 B。
4. 增加 stale ref regression：selector 后替换同槽位装备，commit 返回 `target_equipment_changed`，不扣新装备。
5. 增加 save success regression：explicit ref save 成功仍产出 durability event，且不刷新 projection。
6. 增加 destruction regression：explicit ref 归零时 `ClearEntrySlot(EntrySlotId)`，`_apply_equipment_durability_result(...)` 仍刷新 target projection。
7. 接入 `EquipmentAbilityEquipmentTargetSelector`，让 equipment target selector 使用同一 candidate builder。
8. 接入 `EquipmentAbilityEquipmentMutationAdapter`，只把 payload 转 effect + explicit ref commit。
9. 接入 preview/AI，只读候选与权重，不调用 commit。

## 回归矩阵

| 测试 | 目标 |
| --- | --- |
| `run_spell_disjunction_equipment_durability_regression.cs` | 旧 spell effect 行为不变 |
| 新增 selected-target commit regression | explicit ref 只扣选中 instance，禁止二次随机 |
| 新增 stale ref regression | item/instance/slot 变化后 no-op，不 fallback |
| 新增 weighted selector regression | typed `slot_weights`、occupied slot 最高权重、默认权重一致 |
| 新增 preview/AI candidate regression | 不消费 RNG，不产生 durability event，不改 `current_durability` |
| 新增 adapter regression | action payload 走 existing event/log/refresh/writeback 语义 |

## 验收条件

- 旧 disjunction 回归全过。
- 装备能力 action handler 没有任何直接写 `EquipmentInstanceState.current_durability` 的路径。
- `EquipmentMutationAdapter` 不调用旧随机入口，只调用 selected-target commit。
- `random_target_equipment` trace 的 selected instance 与 event 的 `EquipmentInstanceId` 一致。
- stale explicit ref 只产生 no-op trace。
- save 成功、耐久扣减、归零摧毁、log、changed unit report 和 battle-local writeback 全部复用现有语义。
