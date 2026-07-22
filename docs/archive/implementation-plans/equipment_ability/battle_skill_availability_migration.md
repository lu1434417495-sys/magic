# Battle Skill Availability Migration

本文档是装备能力系统中 `BattleSkillAvailabilityService` 和 `SkillEntryId` 一等化的详细落地设计。它拆自 `docs/proposals/battle/equipment_ability/system_expansion.md`，原因是这条迁移会穿过大量存量战斗代码，不能只在总文档中写成“选择态和命令态新增 SkillEntryId”。

## 目标

V1 要支持装备授予主动技能，但不能把这些技能写入 `BattleUnitState.known_active_skill_ids`。因此需要一个 battle-only 的“当前可用技能入口”读口，把长期已知技能和装备授予技能合成给 HUD、手动选择、文本命令、preview、execution 和 AI。

`SkillEntryId` 是这个设计的事实键：

- `SkillId` 标识 catalog skill definition。
- `SkillEntryId` 标识当前战斗中某个单位可用的一个具体技能入口和来源。
- 同一个 `SkillId` 可能同时来自 known skill、装备 grant，或多个装备 grant 的 winner/suppressed sources。
- selection、command、preview、execution、HUD selected/highlight、AI plan 都必须以 `SkillEntryId` 区分来源。

## 当前代码事实

当前代码的真实影响面比一个字段大。以下是本设计基于当前代码读到的 owner。

| owner | 当前事实 | 迁移结论 |
| --- | --- | --- |
| `scripts/systems/battle/core/BattleCommand.cs` | `BattleCommand` 只有 `skill_id`，没有 entry/source 字段。 | `Skill` command 必须新增 `skill_entry_id`；`skill_id` 保留为 catalog lookup 和日志字段。 |
| `scripts/systems/game_runtime/GameRuntimeBattleSelectionState.cs` | 选择态只有 `selected_skill_id` / `selected_skill_variant_id`。 | 必须新增 `selected_skill_entry_id`；`selected_skill_id` 只做展示和 catalog 辅助。 |
| `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs` | slot 选择、失效同步、自动 unit skill、已选技能 command、preview command 都直接读 `known_active_skill_ids` 或只写 `skill_id`。 | 这是手动路径的主迁移 owner，所有 skill command 构造点都必须拿到 exact entry。 |
| `scripts/systems/battle/runtime/BattleRuntimeModule.cs` | `PreviewCommand` / `IssueCommand` 只按 `BattleCommand.skill_id` 进入 resolver，issue 前用 preview 阻挡。 | preview/issue 必须先解析 `BattleCommand.skill_entry_id` 并得到 command context；resolver 不应自己重新按 `skill_id` 猜来源。 |
| `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator*.cs` | preview/execution 内部多处重新 `GetSkillDefinitionTyped(command.skill_id)`，等级 fallback 读 known map/list。 | 命令路径要改成消费 `BattleSkillCommandContext`，避免 entry 等级在 range、hit、variant、cost、effect unlock 中退化。 |
| `scripts/systems/battle/rules/BattleRangeService.cs` | 静态 range 计算只有 unit + skill definition，等级从 known map/list 推导。 | 新增带 `skillLevel` 或 entry context 的 overload；旧 overload 只保留给 known-only 或兼容估算。 |
| `scripts/systems/battle/rules/BattleHitResolver.cs` | 攻击命中 preview 从 known map/list 推导 skill level 和 lock bonus。 | command preview path 必须传入 resolved skill level；known-only hit lock 仍可读 known map。 |
| `scripts/systems/battle/presentation/BattleHudAdapter.cs` | `BuildSkillSlots` 遍历 `known_active_skill_ids`，`is_selected` 比较 `skillId == selectedSkillId`。 | HUD slot payload 要来自 availability view，selected/highlight 比较 `skill_entry_id`。 |
| `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs` | battle snapshot 暴露 `selected_skill_id` 和 `hud.skill_slots`。 | 新增 `selected_skill_entry_id`；旧 `selected_skill_id` 保留给文本显示和旧断言过渡。 |
| `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs` | `battle skill <slot>` 走 slot index。 | slot 命令继续可用，但 slot 解析必须走 availability view。 |
| `scripts/utils/GameTextSnapshotRenderer.cs` | 文本快照打印 `selected_skill_id`。 | 新增 `selected_skill_entry_id` 输出，避免同 skill 不同 source 看不出来。 |
| `scripts/systems/battle/ai/*` | 多个 evaluator、helper、score/projection/threat path 读 `known_active_skill_ids` 或生成只含 `skill_id` 的 `BattleCommand`。 | AI 需要分两层迁移：候选/plan 按 entry，日志和 skill category 仍可按 `skill_id`。 |
| `scripts/systems/battle/core/BattleStateReadView.cs` | `KnowsActiveSkill`、`FirstKnownActiveSkillId` 明确 known-only。 | 不迁移语义，不让装备技能污染这些 API。 |
| `tests/**` | 大量测试直接 `new BattleCommand { skill_id = ... }` 或手写 known skill fixtures。 | 不能靠 runtime 长期 fallback；应加 test/helper factory 批量构造 known-skill entry command，再逐步更新直接构造点。 |

粗略搜索显示，`new BattleCommand` / `command.skill_id` / `skill_id =` 在 `scripts/systems` 和 `tests` 中约 268 处命中，`known_active_skill_ids` / `GetKnownSkillLevelTyped` / `KnowsActiveSkill` / `FirstKnownActiveSkillId` 约 124 处命中。不是每一处都要改，但足以说明这不是局部字段补丁。

## 不迁移边界

以下字段继续表达 catalog `SkillId`，不改成 `SkillEntryId`：

- `SkillDefinition.SkillId`、静态 skill catalog key。
- `BattleUnitState.known_active_skill_ids`、`known_skill_level_map`、`BattleStateReadView.KnowsActiveSkill(...)`、`FirstKnownActiveSkillId`。这些保持人物/单位长期已知主动技能语义。
- `BattlePendingCastState.SkillId`。V1 装备授予 pending-cast skill 被 validator 拒绝，因此 pending cast 不需要保存 equipment entry source。
- `source_skill_id`，包括 status、terrain effect、shield source、damage context、report/log、contribution event 等来源记录。
- battle special profile manifest 的 `owning_skill_ids`、meteor swarm plan/report 的 `skill_id`。这些是静态 skill/profile 绑定，不是可用入口。
- mastery、metrics、battle report 中的 `skill_id`。它们可新增 `skill_entry_id` trace 字段，但原 `skill_id` 仍是统计维度。
- auto-cast 的来源资格。`AutoCastRequest.StoredSkillId` 只能来自使用者自己已经真实学会的技能；装备、职业授予、种族、血脉、升华、状态、临时 battle-only entry 或 availability winner 提供的技能都不能成为 auto-cast source。`ScopedAutoCast` 只是一次执行 scope，不是临时授予技能的入口。

## 核心数据结构

### Skill entry id

V1 entry id 生成规则：

```text
known_skill:{skill_id}
equipment_skill:{binding_id}:{source_equipment_instance_id}:{effective_instance_key}:{granted_action_id}:{skill_id}
scoped_auto:{scope_id}:{skill_id}
```

`known_skill:{skill_id}` 是 unit-scoped id。跨单位 payload 由 `BattleCommand.unit_id`、selection active unit 或 snapshot owner 区分，不把 unit id 放进 entry id。

`equipment_skill` 必须把 `source_equipment_instance_id` 放进 `SkillEntryId`。`effective_instance_key` 不能单独代表装备实例，因为 `CharacterTraitService` 会根据 trait stack policy 把多个来源折叠成 trait 级 key，例如 `UniqueByTrait`、`HighestRoll` 和 `Additive` 都可能丢掉原始装备实例 identity。换下一件装备 A，再装备同 binding、同 trait、同 granted action、同 skill 的装备 B 时，旧 selection / old command 必须 stale；不得按同一个 `SkillId` 或折叠后的 `effective_instance_key` 静默切到 B。

`source_equipment_instance_id` 是 battle equipment view 中当前装备实例的 `EquipmentEntryState.instance_id` / `EquipmentInstanceState.instance_id`。projection 构建时如果无法从 `BattleUnitState.equipment_view` 反查该实例，不得生成 equipment granted skill entry。

`scoped_auto:{scope_id}:{skill_id}` 的 `scope_id` 固定来自 `AutoCastRequest` 的 `OwnerMemberId`、`SetupId` 和 `InstanceId`，推荐格式为 `{owner_member_id}:{setup_id}:{instance_id}`。`skill_id` 必须等于 `AutoCastRequest.StoredSkillId`，entry level 使用 `AutoCastRequest.CastLevel`。构造该 entry 前必须先通过 true-learned source gate；不能从 `BattleSkillAvailabilityService` 的当前 winner、equipment granted entry 或 race/identity projection 反推 auto-cast 来源。

V1 的 true-learned source gate 定义为：技能必须存在于使用者自身的长期已学技能状态，并在 battle setup 时投影为 known active skill。它的来源不能只是装备、职业授予、种族、血脉、升华、变身、状态、临时战斗效果或其他 battle-only entry。即使这些来源让技能在当前战斗中“可用”，也不能满足 auto-cast 的来源资格。

### Entry reference

```csharp
public enum BattleSkillEntrySourceKind
{
    KnownActiveSkill,
    EquipmentGrantedSkill,
    ScopedAutoCast,
}

public readonly record struct BattleSkillEntryRef(
    BattleSkillEntrySourceKind SourceKind,
    StringName SkillEntryId,
    StringName SkillId,
    StringName BindingId,
    StringName GrantedActionId,
    StringName EffectiveInstanceKey,
    StringName EquipmentInstanceId,
    StringName ScopeId,
    StringName OwnerMemberId,
    StringName AutoCastSetupId,
    StringName AutoCastInstanceId
);
```

`BindingId`、`GrantedActionId`、`EffectiveInstanceKey` 和 `EquipmentInstanceId` 只由 `EquipmentGrantedSkill` 填充。`ScopeId`、`OwnerMemberId`、`AutoCastSetupId` 和 `AutoCastInstanceId` 只由 `ScopedAutoCast` 填充。`KnownActiveSkill` 两组 source ref 字段均为空，避免把 known skill 错解释成某个装备或 auto-cast 实例。

### Available entry

```csharp
public sealed class BattleAvailableSkillEntry
{
    public int SlotIndex { get; init; }
    public StringName SkillEntryId { get; init; }
    public BattleSkillEntrySourceKind SourceKind { get; init; }
    public BattleSkillEntryRef SourceRef { get; init; }
    public StringName SkillId { get; init; }
    public int SkillLevel { get; init; }
    public bool IsBattleOnly { get; init; }
    public bool CountsAsKnownSkill { get; init; }
    public StringName DisplayCategory { get; init; }
    public StringName SourceLabelKey { get; init; }
    public bool IsEnabled { get; init; }
    public StringName DisabledReasonCode { get; init; }
    public string DisabledReasonText { get; init; }
    public IReadOnlyList<StringName> SuppressedSourceKeys { get; init; }
}
```

### Command and selection

```csharp
public class BattleCommand
{
    public StringName command_type = "";
    public StringName unit_id = "";
    public StringName skill_entry_id = "";
    public StringName skill_id = "";
    public StringName skill_variant_id = "";
    // existing targets/equipment fields stay unchanged
}

public sealed class GameRuntimeBattleSelectionState
{
    public StringName selected_skill_entry_id { get; set; } = "";
    public StringName selected_skill_id { get; set; } = "";
    public StringName selected_skill_variant_id { get; set; } = "";
}
```

`selected_skill_id` 和 `BattleCommand.skill_id` 不删除，原因是大量 UI、日志、特殊 profile 和 catalog lookup 仍需要 `SkillId`。但是它们不再是 access authority。

### Command context

为避免让每个 resolver 自己按 `skill_entry_id` 重新解析，preview/issue 边界应生成一次 command context：

```csharp
public sealed class BattleSkillCommandContext
{
    public BattleCommand Command { get; init; }
    public BattleAvailableSkillEntry Entry { get; init; }
    public SkillDefinition SkillDefinition { get; init; }
    public int SkillLevel { get; init; }
    public BattleSkillAvailabilityConsumerKind Consumer { get; init; }
}
```

`BattleRuntimeModule.PreviewCommand(...)` 和 `IssueCommand(...)` 对 `BattleCommandKind.Skill` 的处理顺序必须是：

```text
ResolvePreviewActiveUnit / active unit validation
  -> BattleSkillAvailabilityService.ValidateSkillEntryAccess(unit, command.skill_entry_id, command.skill_id, consumer)
  -> build BattleSkillCommandContext
  -> pass context to BattleSkillExecutionOrchestrator preview/execute
```

不得在 orchestrator 内部再用 `command.skill_id` 重新决定 entry 或 level。

### Scoped auto-cast source gate

`ScopedAutoCast` 不进入普通 HUD slot view，也不能从当前 availability winner 反推。它只服务 `AutoCastRequest` 执行期，目的是让严格 `skill_entry_id` access gate 仍能识别一次合法的 auto-cast skill entry。

V1 需要新增一个明确的 source gate，建议命名为 `BattleScopedAutoCastSourceGate` 或等价 helper：

```csharp
public readonly record struct ScopedAutoCastSourceRef(
    StringName OwnerMemberId,
    StringName OwnerUnitId,
    StringName CasterUnitId,
    StringName SetupId,
    StringName InstanceId,
    StringName StoredSkillId,
    int CastLevel
);

public sealed class ScopedAutoCastSourceGateResult
{
    public bool Ok { get; init; }
    public StringName RejectionCode { get; init; }
    public BattleAvailableSkillEntry Entry { get; init; }
    public int SkillLevel { get; init; }
}
```

Source gate 判定规则：

- `StoredSkillId` 必须在 `OwnerMemberId` 对应的 `PartyMemberState.progression.SkillsTyped` 中存在，且 `UnitSkillProgress.is_learned == true`。
- `UnitSkillProgress.granted_source_type` 必须进入 true-learned allow list。V1 默认只允许 `UnitSkillGrantSourceType.Player`；`Race`、`Subrace`、`Bloodline`、`Ascension`、装备授予、状态授予、临时 battle-only entry 都不允许。`Profession` 是否算真实学习不能隐式放行；除非 progression 设计明确把某类职业技能改标为 player-learned，否则 V1 不把 `Profession` 当 auto-cast source。
- battle setup/release 时还要确认 `OwnerUnitId` 当前 battle unit 的 known active projection 中仍包含该 `StoredSkillId`，用于防止已失去/被禁用的长期技能继续释放；但这只是二次一致性检查，不是来源资格本身。
- 如果 auto-cast 规则有“由某个来源技能触发/充能/释放”的条件，该来源技能也必须走同一个 true-learned source gate；不能因为触发事件的 `source_skill_id` 来自装备、职业授予、种族、血脉、升华、状态或临时 entry 就满足“使用者自己的技能”条件。
- gate 不得调用 `TryResolveWinningSkillEntryBySkillId(...)` 或按 `SkillId` 查询 availability view winner。availability view 只能说明“当前可用”，不能证明“自己真实学会”。

当前代码中的 `GameRuntimeFacade.Contingency.TryGetLearnedSkillLevel(...)` 只检查 `UnitSkillProgress.is_learned`，不足以表达这个约束。实现 R1 时应改成 `TryGetTrueLearnedSkillLevel(...)` 或等价 typed helper，并显式检查 `UnitSkillProgress.GrantedSourceTypeKind` / `granted_source_type`。同理，`BattleSkillExecutionOrchestrator.ExecuteAutoCast(...)` 当前通过临时 `AddKnownActiveSkill(...)` / `SetKnownSkillLevelTyped(...)` 让 skill 通过 known-only 入口；迁移后必须删除这段临时 known mutation，改为构造 `ScopedAutoCast` entry + `BattleSkillCommandContext`。

`ExecuteAutoCast(...)` 推荐顺序：

```text
validate AutoCastRequest structural fields
  -> validate true-learned source gate against OwnerMemberId / StoredSkillId
  -> build scoped_auto:{owner_member_id}:{setup_id}:{instance_id}:{stored_skill_id}
  -> build BattleSkillCommandContext with SourceKind=ScopedAutoCast, IsBattleOnly=true, CountsAsKnownSkill=false, SkillLevel=CastLevel
  -> execute skill with the same resolver path used by normal skill command context
```

如果 `CasterUnitId != OwnerUnitId`，`CasterUnitId` 只表示执行者/施法实体，不能替代 `OwnerMemberId` 的来源资格检查。trace 必须同时记录 owner、caster、setup、instance、stored skill、cast level 和 rejection code。

## BattleSkillAvailabilityService API

```csharp
public sealed class BattleSkillAvailabilityQuery
{
    public BattleSkillAvailabilityConsumerKind Consumer { get; init; }
    public StringName RequestedSkillEntryId { get; init; }
    public bool IncludeKnownActiveSkills { get; init; } = true;
    public bool IncludeEquipmentGrantedSkills { get; init; } = true;
    public bool IncludeDisabled { get; init; } = true;
    public int MaxVisibleSlots { get; init; } = 0;
    public BattleCommand Command { get; init; }
}

public sealed class BattleSkillAvailabilityService
{
    public BattleSkillAvailabilityView BuildView(
        BattleUnitState unit,
        BattleSkillAvailabilityQuery query
    );

    public bool TryGetSkillEntryBySlot(
        BattleUnitState unit,
        int slotIndex,
        BattleSkillAvailabilityConsumerKind consumer,
        out BattleAvailableSkillEntry entry
    );

    public bool TryResolveSkillEntryById(
        BattleUnitState unit,
        StringName skillEntryId,
        BattleSkillAvailabilityConsumerKind consumer,
        out BattleAvailableSkillEntry entry
    );

    public bool TryResolveWinningSkillEntryBySkillId(
        BattleUnitState unit,
        StringName skillId,
        BattleSkillAvailabilityConsumerKind consumer,
        out BattleAvailableSkillEntry entry
    );

    public int ResolveSkillEntryLevel(
        BattleUnitState unit,
        StringName skillEntryId,
        BattleSkillAvailabilityConsumerKind consumer,
        int fallback = 0
    );

    public BattleSkillAccessResult ValidateSkillEntryAccess(
        BattleUnitState unit,
        StringName skillEntryId,
        StringName expectedSkillId,
        BattleSkillAvailabilityConsumerKind consumer,
        BattleCommand command = null
    );
}
```

`TryResolveWinningSkillEntryBySkillId(...)` 只用于 preferred skill、文本诊断或“按 skill id 找当前 winner”的场景。它不能用于验证旧选择或旧 command，因为旧 entry stale 时必须被发现。

## 排序、去重和失效

- known active skills 保持 `BattleUnitState.known_active_skill_ids` 原始顺序。
- equipment granted skills 追加在 known skills 后，按 projection 排序规则稳定排序。
- known skill 与 equipment grant 同 `SkillId` 时，known entry 胜出，装备 source 进入 `SuppressedSourceKeys`。
- 多个 equipment grant 同 `SkillId` 时，只暴露一个 winner entry，规则为 enabled 胜 disabled、`SkillLevel` 高者胜、再按 projection order。
- source lifecycle 移除一个 source 后，availability cache invalidated；如果 `selected_skill_entry_id` 指向 removed/suppressed old entry，selection 清空。
- 如果 winner 从装备 A 变成装备 B，即使 `SkillId`、`binding_id`、`effective_instance_key`、`granted_action_id` 都相同，旧 command/selection 也 stale，不能静默切源；`source_equipment_instance_id` 是这条 stale 检测的必备字段。

## 代码迁移矩阵

### Manual selection

| 文件/方法 | 当前行为 | V1 迁移 |
| --- | --- | --- |
| `GameRuntimeBattleSelection.SelectBattleSkillSlotTyped(...)` | `index` 直接读 `activeUnit.known_active_skill_ids[index]`。 | `TryGetSkillEntryBySlot(..., ManualSelection)`；保存 `selected_skill_entry_id` 和 `selected_skill_id`。 |
| `SyncSelectedBattleSkillState(...)` | `known_active_skill_ids.Contains(selected_skill_id)`。 | `TryResolveSkillEntryById(selected_skill_entry_id, ManualSelection)`；entry 不存在或 disabled 则清空 skill、variant、targets。 |
| `BuildSelectedSkillPreviewCommand(...)` | command 只写 `skill_id`。 | command 写 `skill_entry_id` + `skill_id`；preview 不允许缺 entry。 |
| `BuildSelectedSkillCommand(...)` / `IssueSelectedMultiUnitSkill(...)` / ground click | command 只写 `skill_id`。 | 从 selected entry 构造 command；target selection 不参与 entry 重选。 |
| `BuildSkillCommand(...)` 自动点目标时找第一个 unit skill | 遍历 known skills。 | 遍历 `BuildView(..., ManualSelection)` 中 unit-target entries，构造 exact entry command。 |
| random-chain 立即施放 | slot skill 被选择后只带 `skill_id` 预览/执行。 | 用 slot entry 构造 command，立即施放也带 `skill_entry_id`。 |

### Runtime preview and execution

| owner | V1 迁移 |
| --- | --- |
| `BattleRuntimeModule.PreviewCommand(...)` | 对 skill command 构建 `BattleSkillCommandContext`；失败写 preview log，不进入 skill orchestrator。 |
| `BattleRuntimeModule.IssueCommand(...)` | issue 前仍可用 preview gate，但 preview gate 必须检查 exact entry；`IssueCommand` 不接受 skill-id-only command 作为长期兼容。 |
| `BattleSkillExecutionOrchestrator._preview_skill_command(...)` | 签名改为接收 context 或从参数获得 resolved definition/level；不再 `GetSkillDefinitionTyped(command.skill_id)` 后自行推 level。 |
| `BattleSkillExecutionOrchestrator._handle_skill_command(...)` | 使用 context.SkillDefinition / context.SkillLevel；pending cast start 对 equipment entry 默认被 validator 阻断。 |
| `BattleCastingTimeService` / pending cast | V1 不支持 equipment-granted pending cast；known skill pending cast 可继续保存 `SkillId`。 |
| `BattleRuntimeSkillTurnResolver` | main skill lock、known-only 状态规则继续读 known-only；madness fallback 若语义是“当前可用技能 fallback”，需要 availability view。 |

### Range, hit, cost and effect unlock

| owner | 当前风险 | V1 迁移 |
| --- | --- | --- |
| `BattleRangeService` | 装备 entry 的 range/threat range 会因 known level fallback 变成 0/1。 | 新增 `GetEffectiveSkillRange(..., int skillLevel, ...)` / query overload；availability consumers 必须传 entry level。 |
| `BattleHitResolver` | attack roll bonus 读 known level；装备 entry level 不生效。 | command preview path 传 resolved level；known-only lock hit bonus 仍读 known map。 |
| `BattleSkillResolutionRules` | cast variant unlock 和 effect unlock 读 known level。 | command path 使用 context.SkillLevel；只读 known-only helper 保留。 |
| `BattleChargeResolver` / charge skill helpers | 多处 `GetUnitSkillLevel(activeUnit, skillDefinition.SkillId)`。 | command-driven path 用 context level；非 command passive/charge estimation 保持 known-only，除非明确接入 availability。 |
| `BattleDamageResolver` / `BattleExecutionRules` | 部分伤害 scaling 用 context.SkillId 查 known level。 | 装备 skill command 的 damage context 应携带 resolved skill level 或由 command context 提供。 |

### HUD, snapshot and text

| owner | V1 迁移 |
| --- | --- |
| `BattleHudAdapter.BuildSnapshot(...)` | 参数增加 selected entry id，或直接接收 availability view。`BuildSkillSlots` 不再遍历 known list。 |
| HUD slot payload | 每个非空 slot 增加 `skill_entry_id`、`source_kind`、`source_label_key`、`skill_level`、`is_battle_only`、`suppressed_source_keys`。 |
| `is_selected` | 比较 `slot.skill_entry_id == selected_skill_entry_id`。 |
| `GameRuntimeSnapshotBuilder` | battle root 增加 `selected_skill_entry_id`；`selected_skill_id` 保留。 |
| `GameTextSnapshotRenderer` | 输出 `selected_skill_entry_id`，并在 slot 行显示 source/entry。 |
| `GameTextCommandRunner battle skill <slot>` | slot 继续是 1-based，但由 availability slot index 解析。按 skill name/id 直接施放若未来加入，必须在多 entry 时拒绝并提示使用 slot/source。 |

### AI

AI 不能只在最后生成 command 时补 entry，因为候选、variant、range、score 和 threat 都会依赖 entry level/source。

| owner | V1 迁移 |
| --- | --- |
| `BattleAiTypedActionHelper.ResolveKnownSkillIds(...)` | 改为 `ResolveSkillEntries(...)`，返回 `BattleAvailableSkillEntry` list。preferred skill ids 先按 `SkillId` 找当前 winner，再返回 exact entry。 |
| `BattleAiUnitSkillCandidateEvaluator` | foreach skillId 改为 foreach entry；command builder 接收 entry。 |
| `BattleAiGroundSkillActionEvaluator` / `BattleAiMultiUnitSkillEvaluator` / random-chain / charge evaluators | 生成 command 的地方都写 `skill_entry_id`；variant/range/effect unlock 使用 entry level。 |
| `BattleAiRuntimeActionPlan` | runtime skill specs 增加 `skill_entry_id`，保留 `skill_id` 用于 trace 和 catalog。`BuildSkillSignature` 应包含 entry id 和 level，避免装备 source 变化后复用旧 plan。 |
| `BattleAiContext` / affordance records | 当前按 `skill_id` 存 record，需改成按 `skill_entry_id` 存主索引，`skill_id` 作为 trace 字段。 |
| AI scoring/threat helpers | 对“当前可用威胁”遍历 availability view；对“目标 known-only 语义”仍可读 known list。 |
| `BattleAiMutationGuard` | 如果缓存 availability view 或 projection cache，restore 后必须 clear/lazy rebuild；不要把 view 当正式 battle state 保存。 |

## 严格性和迁移期策略

V1 不应给 runtime 长期保留“`skill_id`-only command 自动补 entry”的兼容 fallback。原因：

- 它会掩盖同 `SkillId` 多 source 的 stale selection bug。
- 它会让 direct tests 通过，但真实 HUD/AI/text 仍可能选错 source。
- 它违反装备 grant 不写入 known list 的边界。

允许的迁移辅助：

- 新增 `BattleCommandFactory` 或 test fixture helper，例如 `BuildKnownSkillCommand(unit, skillId, ...)`，在测试构造阶段显式填 `known_skill:{skill_id}`。
- 在过渡期保留旧 `selected_skill_id`、snapshot `selected_skill_id`、AI trace `skill_id` 字段作为 display/diagnostic 字段。
- 文本命令 `battle skill <slot>` 不需要用户输入 entry id，因为 slot facade 已经解析 exact entry。

不允许的迁移辅助：

- `BattleRuntimeModule.PreviewCommand` 在 `skill_entry_id == ""` 时按 `skill_id` 自动选 winner。
- `BattleSkillExecutionOrchestrator` 在 access gate 之后重新按 `skill_id` 查当前 winner。
- HUD selected 继续按 `skill_id` 高亮。
- AI plan 只保存 `skill_id`，执行前再解析 winner。

## 推荐落地顺序

### Phase 1: known-only availability service

- 新增 service、entry/view/query/access result。
- 只从 `known_active_skill_ids` 和 `known_skill_level_map` 生成 `known_skill:{skill_id}` entries。
- 不接入装备 grant。
- 加 known-only service tests，证明 slot order、level、disabled reason、hidden count 与现有行为一致。

### Phase 2: command and selection identity

- `BattleCommand` 加 `skill_entry_id`。
- `GameRuntimeBattleSelectionState` 加 `selected_skill_entry_id`。
- `GameRuntimeBattleSelection` 所有 skill command 构造点写 exact entry。
- `GameRuntimeFacade` / `IGameRuntimeSnapshotSource` / `WorldMapRuntimeProxy` 增加 selected entry getter/setter，旧 skill getter 继续存在。
- 更新 direct command tests 或加 helper factory。

### Phase 3: runtime access gate

- `PreviewCommand` 和 `IssueCommand` 建立 `BattleSkillCommandContext`。
- `BattleSkillExecutionOrchestrator` preview/execute 改吃 context。
- 缺失或 stale `skill_entry_id` 的 skill command 被拒绝。
- 增加 regression：同 skill id 的旧 entry stale 后不切到新 source。

### Phase 4: scoped auto-cast migration

- `AutoCastRequest.StoredSkillId` 进入 true-learned source gate，不能只检查 `is_learned` 或当前 availability winner。
- `BuildAutoCastCommand(...)` 或替代 context factory 生成 `scoped_auto:{owner_member_id}:{setup_id}:{instance_id}:{stored_skill_id}`。
- `ExecuteAutoCast(...)` 删除临时写 `known_active_skill_ids` / `known_skill_level_map` 的 mutation，改走 `ScopedAutoCast` command context。
- `BattleContingencySystem.BuildAutoCastRequestsForRelease(...)` 和 execute 前都要 revalidate source gate；失败时跳过请求并输出 trace/rejection code。
- 增加 regression：true-learned stored skill 能释放，装备/职业授予/种族/血脉/升华/状态/临时 entry 不能作为 stored skill 或来源技能触发 auto-cast。

### Phase 5: level consumers

- `BattleRangeService`、`BattleHitResolver`、`BattleSkillResolutionRules`、cost/effect unlock path 增加 entry level path。
- 保留 known-only helper 给 `KnowsActiveSkill`、main skill lock、静态估算等明确 known-only 语义。
- 增加 regression：装备 entry level 影响 range、variant、hit/cost scaling。

### Phase 6: presentation and headless

- HUD slots 用 availability view。
- Snapshot/text renderer 输出 selected entry 和 slot entry source。
- `battle skill <slot>` 走 availability slot。
- 增加 regression：同 `SkillId` 不同 source 不互相高亮。

### Phase 7: AI

- AI candidates、plans、commands 改用 entry。
- Plan signature 包含 `SkillEntryId`。
- AI scoring/threat 对当前可用技能改遍历 availability view。
- Mutation guard restore 后 clear/rebuild availability cache。

### Phase 8: equipment granted skills plug-in

- `EquipmentAbilityProjectionService.GrantedSkills` 接入 availability service。
- Source lifecycle invalidates skill availability。
- 装备 grant 与 known duplicate、multi-equipment duplicate、source removed/stale command regression 全部打开。

## 必测回归

- known-only availability view 与当前 known skill slot 顺序一致。
- `GameRuntimeBattleSelectionState.selected_skill_entry_id` 随 slot selection 写入，clear selection 时清空。
- skill command 缺 `skill_entry_id` 被 preview/issue 拒绝。
- known skill command 带 `known_skill:{skill_id}` 可以 preview/issue。
- 同 `SkillId` 的 known entry 与 equipment entry 同时存在时，known entry 胜出，equipment source 进入 suppressed list。
- 两件装备授予同一 `SkillId` 时，winner 变更后旧 `skill_entry_id` command stale，不切到新 winner。
- 卸下装备 A 后换上同 binding、同 trait、同 granted action、同 `SkillId` 的装备 B 时，A 生成的 `equipment_skill:{binding_id}:{source_equipment_instance_id}:...` command stale，不按 `SkillId` 或折叠后的 `effective_instance_key` 切到 B。
- HUD `is_selected` 按 `skill_entry_id` 比较。
- text snapshot 输出 selected entry id。
- `battle skill <slot>` 选择装备授予 entry 后 command 带 entry id。
- AI 生成的 skill command 带 entry id，plan signature/source 变化后不复用旧 plan。
- 装备 entry skill level 影响 range、variant unlock、hit bonus、cost/effect scaling。
- `BattleStateReadView.KnowsActiveSkill` 和 `FirstKnownActiveSkillId` 不把装备 grant 当 known skill。
- `AutoCastRequest.StoredSkillId` 来源于 `UnitSkillProgress.granted_source_type = player` 且 `is_learned = true` 时，auto-cast 生成 `ScopedAutoCast` entry 并按 `CastLevel` 执行。
- `AutoCastRequest.StoredSkillId` 来源于装备 granted skill、race/subrace/bloodline/ascension/profession grant、状态或临时 battle-only entry 时，source gate 拒绝并输出 rejection code。
- auto-cast 规则中用于触发/充能/释放资格的来源技能来自装备、种族或临时 entry 时，不满足“使用者自己的技能”条件。
- auto-cast 执行前后 `BattleUnitState.known_active_skill_ids` 和 `known_skill_level_map` 不发生临时污染或残留。

## 对 project_context_units.md 的影响

这次是设计文档拆分和装备能力子设计细化，不改变当前 repo 的架构 loading index。真正实现 `BattleSkillAvailabilityService` 时，会跨 CU-15、CU-16、CU-18、CU-21，并可能需要在 `docs/design/project_context_units.md` 的战斗相关单元中加入 equipment ability / availability service 的推荐读集。
