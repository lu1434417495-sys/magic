# Godot / C# 边界清理与 Typed Window DTO 迁移方案

> 状态：`Implemented`（Phase A-G 全部落地）
>
> 当前代码核对日期：`2026-08-15`
>
> 目标：清理内部 C# 调用链中不必要的 `Variant`、`Godot.Collections`、`dynamic` 和 `[GlobalClass]` 传播；不改变玩法、窗口行为、存档 schema 或 headless snapshot 契约。

## 1. 结论

本方案不是“删除项目里的全部 Godot 类型”，也不是“因为 UI 属于 Godot，所以 UI 输入必须是 `GDictionary`”。正确边界是：

- `SettlementWindow : Control`、`ShopWindow : Control` 等 UI 节点由 Godot 管理生命周期、输入和绘制，因此它们属于 Godot 引擎层。
- C# 代码直接调用 `window.ShowXxx(typedDto)` 时，参数只经过 CLR 调用，不会自动封送为 `Variant`。
- 只有 `[Signal]`、`[Export]`、`GodotObject.Call`、`Callable`、GDScript、scene/resource/save 序列化和接收 `Variant` 的 Godot API 才是实际 Godot ABI 边界。
- 因此，正式运行时和窗口输入应优先使用 detached typed DTO；只有实际 ABI consumer 需要 `Godot.Collections` 时才做一次短期投影。

这次迁移的主要收益来自消除下列往返：

```text
typed runtime state
  -> GDictionary window payload
  -> UI 私有 parser
  -> UI 私有 typed mirror
```

目标链路是：

```text
typed runtime state
  -> detached immutable window DTO
  -> 普通 C# ShowXxx(dto)
  -> UI render
  -> typed C# intent 或真实 Godot signal 边界
```

## 2. 当前实现事实

以下结论只描述 `2026-08-15` 当前工作区，不代表本 proposal 已实施。

### 2.1 已经完成

- `AttackPreviewData`、`BattlePreview`、`BattleAiScoreProfile` 已不再声明 `[GlobalClass]`。
- `NpcQuestOfferWindowData` / `NpcQuestOfferEntryData` 已作为 typed DTO 直接传给 `NpcQuestOfferDialog`。
- `BountyBoardWindowData` / `BountyBoardEntryData` 已作为 typed DTO 直接传给 `BountyBoardWindow`。
- `SettlementOverviewWindowData` 及其成员/设施/驻留/服务 typed 条目已直接传给 `SettlementWindow`（Phase C，2026-08-15 落地）。
- `ForgeActionRequest` 已通过普通 C# event 从 `ShopWindow` 传出。
- `WarehouseWindowData`（包裹 `WarehouseWindowSnapshot`）已作为 typed DTO 直接传给 `PartyWarehouseWindow`（Phase B，2026-08-15 落地）：handler 只构建一次 typed 数据，plain snapshot 由它单向投影。
- `GameRuntimeCharacterInfoContext` 已作为 typed DTO 直接传给 `CharacterInfoWindow`（Phase A，2026-08-15 落地）：`GetCharacterInfoContextLease()` 已删除，facade/proxy 改为 `GetCharacterInfoContextTyped()`，UI 私有 parser/mirror 已删除；`BuildSnapshotPlain()` 与 headless snapshot key 未变。
- `SettlementServiceWindowData` 已作为商店/任务板/锻造/驿站四个面板的唯一窗口输入与 active modal context（Phase D，2026-08-15 落地）；`ShopWindow` 私有 parser 与四个 property-bag context wrapper 已删除。
- `ShopWindow` 已通过 4 个 panel-specific C# event 提交 typed intent（Phase E，2026-08-15 落地）；`action_requested(..., GDictionary)` 与 `_build_confirm_payload()` 已删除。
- 低幸运据点奖励 port 已改为 typed `LowLuckSettlementActionInput` -> `LowLuckEventResult`（Phase F，2026-08-15 落地）；两次字典往返已删除。

### 2.2 尚未完成

无。四条窗口输入链、shop 提交链与低幸运奖励 port 均已 typed；helper 与 `dynamic` / `[GlobalClass]` 收尾见 2.3 与 Phase G 落地记录。

### 2.3 Helper 当前状态

`GodotVariantReadExtensions` 位于 `scripts/systems/platform/GodotVariantReadExtensions.cs`，不是旧 proposal 中的 `scripts/utils/`。

Phase G 收敛后的事实：

- 文件只剩 `TryAsDictionary` 一个方法，服务 3 处真实 Variant 边界：`WorldMapFogSystem` 两处 save/schema 解码、`GameRuntimeSettlementCommandHandler` 一处世界记录 reward entry 解码。
- `TryAsObject`、`TryAsGodotArray`、`TryAsVector2I`、`TryAsBool` 零调用，已删除。
- 唯一的 `TryAsInt` 调用已内联为其 owning private decoder 的显式 `VariantType == Int` 检查，扩展本体删除。
- `GetValueOrDefault` 的 production 调用为零（现存同名调用全部是 .NET `Dictionary.GetValueOrDefault`），已连同 `TryRead` / `ToVariant` 私有实现移到测试作用域 `tests/shared/GodotDictionaryTestReadExtensions.cs`。
- 因此整文件不删除：`TryAsDictionary` 仍有真实边界消费者。

## 3. 强制边界与不变量

所有新增 window DTO 必须满足：

- 不继承 `GodotObject`、`Node`、`Resource` 或 `RefCounted`。
- 不持有 `PartyState`、`PartyMemberState`、service owner、session、runtime facade 或 UI node。
- 不含 `Variant`、`Godot.Collections.Dictionary`、`Godot.Collections.Array` 或 `Dictionary<string, object>` 业务 payload。
- 构造时复制集合，并只暴露 `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey, TValue>`。
- UI 只读取 DTO；UI selection 保存在窗口自身，不反写 DTO。
- runtime modal context 采用 replace-whole，不原地修改 DTO；rollback snapshot 可以安全借用 immutable DTO。
- `StringName`、`Vector2I` 等无独立 native 生命周期的 Godot value type 可以继续作为内部 typed 值使用；本方案针对的是 Variant/property-bag 传播和 GodotObject 生命周期。
- headless `IReadOnlyDictionary<string, object>` snapshot 继续稳定存在，但由 typed DTO 显式 `BuildSnapshotPlain()`，不作为 runtime 真相源。
- 不保留旧 `GDictionary` window API 作为兼容入口；全部仓内调用和测试迁移后直接删除。若将来确有 GDScript/外部 consumer，再单独增加边界 adapter。

## 4. 需要新增、复用和删除的具体类型

### 4.1 据点总览窗口 DTO

新文件建议：`scripts/systems/settlement/SettlementOverviewWindowData.cs`。

#### 新增 `SettlementOverviewWindowData`

字段：

```csharp
internal sealed class SettlementOverviewWindowData
{
    internal StringName SettlementId { get; }
    internal string DisplayName { get; }
    internal string TierName { get; }
    internal StringName FactionId { get; }
    internal StringName CountryId { get; }
    internal string FeedbackText { get; }
    internal string StateSummaryText { get; }
    internal Vector2I FootprintSize { get; }
    internal StringName DefaultMemberId { get; }
    internal IReadOnlyList<SettlementMemberOptionData> MemberOptions { get; }
    internal IReadOnlyList<SettlementFacilityEntryData> Facilities { get; }
    internal IReadOnlyList<SettlementResidentEntryData> Residents { get; }
    internal IReadOnlyList<SettlementServiceEntryData> Services { get; }
    internal bool IsValid { get; }
    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain();
}
```

`DefaultMemberId` 必须由 `GameRuntimeSettlementWindowDataBuilder` 按当前 leader/active/reserve 规则提前解析。DTO 不再携带 `ExplicitDefaultMemberId`、`SelectedMemberId` 和 `PartyState` 让 UI 重新推导。

#### 新增 `SettlementMemberOptionData`

```csharp
internal sealed record SettlementMemberOptionData(
    StringName MemberId,
    string DisplayName,
    string RosterRole,
    bool IsLeader,
    int CurrentHp,
    int CurrentMp
);
```

它同时替代 `SettlementWindow.MemberOption` 和 `ShopWindow.MemberOption`。label 格式化可以留在 UI 的纯展示 helper 中，不能从 `PartyState` 现场补数据。

#### 新增 `SettlementFacilityEntryData`

```csharp
internal sealed record SettlementFacilityEntryData(
    StringName FacilityId,
    string DisplayName,
    string SlotTag,
    string InteractionType
);
```

替代 `SettlementWindow.FacilityEntry`。

#### 新增 `SettlementResidentEntryData`

```csharp
internal sealed record SettlementResidentEntryData(
    StringName NpcId,
    string DisplayName,
    string ServiceType,
    string FacilityName
);
```

替代 `SettlementWindow.ResidentEntry`。

#### 新增 `SettlementMemberAvailabilityData`

```csharp
internal readonly record struct SettlementMemberAvailabilityData(
    bool IsEnabled,
    string DisabledReason
);
```

替代 `SettlementWindow.MemberAvailability`。正式索引改为 `IReadOnlyDictionary<StringName, SettlementMemberAvailabilityData>`，不再使用 string member key。

#### 新增 `SettlementServiceEntryData`

```csharp
internal sealed class SettlementServiceEntryData
{
    internal StringName ActionId { get; }
    internal StringName FacilityId { get; }
    internal string FacilityName { get; }
    internal StringName NpcId { get; }
    internal string NpcName { get; }
    internal string ServiceType { get; }
    internal StringName InteractionScriptId { get; }
    internal string CostLabel { get; }
    internal string StateLabel { get; }
    internal string SummaryText { get; }
    internal bool IsEnabled { get; }
    internal string DisabledReason { get; }
    internal SettlementPanelKind PanelKind { get; }
    internal IReadOnlyDictionary<StringName, SettlementMemberAvailabilityData>
        MemberAvailability { get; }
}
```

它替代 `SettlementWindow.ServiceEntry`。删除 `ServiceEntry.Payload`、`ResolvedService.Payload` 和 `ResolvedService.ApplyToPayload()`；当前据点窗口发出的 signal 只使用 action/member/quantity/source，根本不消费该 property bag。

#### 删除 UI 私有类型

- `SettlementWindow.SettlementWindowData`
- `SettlementWindow.FacilityEntry`
- `SettlementWindow.ResidentEntry`
- `SettlementWindow.MemberAvailability`
- `SettlementWindow.ServiceEntry`
- `SettlementWindow.MemberOption`
- `SettlementWindow.ResolvedService` 可以保留为不含 payload 的 UI 临时值，也可以改为一个局部 `record struct`；它不能成为 runtime owner。

### 4.2 商店、任务板、锻造、驿站共用窗口 DTO

新文件建议：`scripts/systems/settlement/SettlementServiceWindowData.cs`。

#### 新增 `SettlementServiceWindowData`

```csharp
internal sealed class SettlementServiceWindowData
{
    internal StringName SettlementId { get; }
    internal StringName ActionId { get; }
    internal SettlementPanelKind PanelKind { get; }
    internal string Title { get; }
    internal string Meta { get; }
    internal string SummaryText { get; }
    internal string StateSummaryText { get; }
    internal SettlementServiceWindowLabelsData Labels { get; }
    internal bool ShowMemberSelector { get; }
    internal StringName InteractionScriptId { get; }
    internal StringName FacilityId { get; }
    internal string FacilityName { get; }
    internal StringName NpcId { get; }
    internal string NpcName { get; }
    internal string ServiceType { get; }
    internal IReadOnlyList<SettlementServiceWindowEntryData> Entries { get; }
    internal IReadOnlyList<SettlementMemberOptionData> MemberOptions { get; }
    internal StringName DefaultMemberId { get; }
    internal StringName SelectedMemberId { get; }
    internal SettlementServiceConfirmationData Confirmation { get; }
    internal bool IsValid { get; }
    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain();
}
```

它替代 `ShopWindow.ShopWindowData`，并直接成为 shop/contract/forge/stagecoach active modal context。不要再创建四份只包一层 property bag 的 context wrapper。

#### 新增 `SettlementServiceWindowLabelsData`

```csharp
internal sealed record SettlementServiceWindowLabelsData(
    string ConfirmLabel,
    string CancelLabel,
    string EntryTitle,
    string SummaryTitle,
    string StateTitle,
    string CostTitle,
    string DetailsTitle,
    string MemberTitle,
    string EmptyStateLabel,
    string EmptyCostLabel,
    string EmptyDetailsText
);
```

#### 新增 `SettlementServiceConfirmationData`

```csharp
internal sealed record SettlementServiceConfirmationData(
    StringName QuestId,
    string Text,
    SettlementSubmissionSource Source
);
```

无确认状态使用 `null`，更新确认状态时构造新的 `SettlementServiceWindowData`，不原地修改 active context。

#### 新增 `SettlementServiceWindowEntryData`

```csharp
internal sealed record SettlementServiceWindowEntryData(
    StringName EntryId,
    string DisplayName,
    string SummaryText,
    string DetailsText,
    string StateLabel,
    string CostLabel,
    bool IsEnabled,
    string DisabledReason,
    SettlementServiceSelectionData Selection
);
```

`Selection` 是只包含提交所需稳定 id 的 tagged union；UI 不再复制整条 entry 到 `Dictionary<string, object>`。

#### 新增 selection tagged union

```csharp
internal abstract record SettlementServiceSelectionData;

internal enum SettlementShopActionKind
{
    Buy,
    Sell,
}

internal sealed record SettlementShopSelectionData(
    SettlementShopActionKind ActionKind,
    StringName ItemId,
    StringName InstanceId,
    int MaxQuantity
) : SettlementServiceSelectionData;

internal sealed record SettlementContractSelectionData(
    StringName QuestId
) : SettlementServiceSelectionData;

internal sealed record SettlementForgeSelectionData(
    StringName RecipeId
) : SettlementServiceSelectionData;

internal sealed record SettlementStagecoachSelectionData(
    StringName TargetSettlementId
) : SettlementServiceSelectionData;
```

disabled placeholder entry 可以使用 `null` selection；任何 enabled entry 必须携带与 `PanelKind` 匹配的 selection subtype。价格、库存、travel cost 和 quest state 仍由 runtime 在提交时按稳定 id 重新校验，不能信任 UI 展示值。

#### 删除或改造现有类型

- 删除 `ShopWindow.ShopWindowData`、`ShopWindow.ShopEntry`、`ShopWindow.MemberOption`。
- 删除 `SettlementModalContext`、`SettlementContractBoardContext`、`SettlementShopContext`、`SettlementForgeContext`、`SettlementStagecoachContext`。
- `SettlementShopWindowBuildResult.WindowDataPlain` 改为 `SettlementServiceWindowData WindowData`；`ProjectWindowDataLease()` 删除。
- `GameRuntimeSettlementCommandHandler` rollback snapshot 直接持有 immutable `SettlementServiceWindowData` 引用。

### 4.3 ShopWindow typed 提交请求

`SettlementActionRequest` 和 `ForgeActionRequest` 已存在，应复用，不要另建同义通用 payload。

新增文件建议：`scripts/systems/settlement/SettlementServiceActionRequests.cs`。

```csharp
internal readonly record struct SettlementShopActionRequest(
    SettlementActionRequest Action,
    SettlementShopActionKind ActionKind,
    StringName ItemId,
    StringName InstanceId,
    int Quantity
);

internal readonly record struct SettlementContractBoardActionRequest(
    SettlementActionRequest Action,
    StringName QuestId,
    bool ConfirmAccept
);

internal readonly record struct SettlementStagecoachActionRequest(
    SettlementActionRequest Action,
    StringName TargetSettlementId
);
```

`ShopWindow` 改为按实际实例用途暴露普通 C# event：

```csharp
internal event Action<SettlementShopActionRequest> ShopActionRequested;
internal event Action<SettlementContractBoardActionRequest> ContractActionRequested;
internal event Action<ForgeActionRequest> ForgeActionRequested;
internal event Action<SettlementStagecoachActionRequest> StagecoachActionRequested;
```

随后删除 `ShopWindow.action_requested(..., GDictionary payload)` 和 `_build_confirm_payload()`。`WorldMapSystem` 只负责把 typed intent 转交 proxy/facade；业务校验仍属于 runtime handler/service。

`SettlementWindow.action_requested` 当前只传递 Godot 支持的 primitive/String 参数，没有 property bag，可保留为真实 signal 边界。本方案不要求为了“零 signal”而做无收益改造。

### 4.4 仓库窗口类型

已有类型全部复用：

- `WarehouseWindowSnapshot`
- `WarehouseInventoryEntrySnapshot`
- `WarehouseTargetMemberSnapshot`

新增 `scripts/systems/game_runtime/WarehouseWindowData.cs`：

```csharp
internal sealed record WarehouseWindowData(
    string Title,
    string Meta,
    string SummaryText,
    string StatusText,
    WarehouseWindowSnapshot Snapshot
);
```

要做的修改：

- `GameRuntimeWarehouseHandler` 只构建一次 `WarehouseWindowData`。
- `PartyWarehouseWindow.ShowWarehouse(WarehouseWindowData data)` 直接读取 snapshot。
- `GetWarehouseWindowDataSnapshotPlain()` 从 `WarehouseWindowData` / `WarehouseWindowSnapshot` 投影 plain snapshot。
- 删除 `PartyWarehouseWindow.WarehouseWindowData`、`WarehouseEntry`、`TargetMember` 以及所有 GDictionary schema parser。
- 不扩展 `WarehouseWindowSnapshot` 去拥有 UI 标题和状态文案，避免把 presentation text 塞进仓库领域 snapshot。

### 4.5 人物信息窗口类型

不新增第二套 DTO，直接复用：

- `GameRuntimeCharacterInfoSource`
- `GameRuntimeCharacterInfoEntryKind`
- `GameRuntimeCharacterInfoEntry`
- `GameRuntimeCharacterInfoSection`
- `GameRuntimeCharacterInfoFate`
- `GameRuntimeCharacterInfoContext`

要做的修改：

- `CharacterInfoWindow.ShowCharacter(GameRuntimeCharacterInfoContext context)` 直接渲染 typed sections/fate。
- `GameRuntimeFacade` 和 `WorldMapRuntimeProxy` 提供 typed context getter；窗口渲染链删除 `GetCharacterInfoContextLease()`。
- `BuildSnapshotPlain()` 保留给 headless snapshot，不再反向作为 UI 输入。
- 删除 UI 私有 `EntryKind`、`CharacterInfoPayload`、`CharacterInfoSection`、`CharacterInfoEntry` 和对应 GDictionary parser。

### 4.6 低幸运据点奖励 port

已有类型足够，不新增 reward DTO：

- `LowLuckSettlementActionInput`
- `LowLuckEventResult`
- `PendingCharacterReward`
- `PendingCharacterRewardEntry`

接口从：

```csharp
GDictionary ResolveLowLuckSettlementEventRewards(GDictionary context);
```

改为：

```csharp
LowLuckEventResult ResolveLowLuckSettlementEventRewards(
    LowLuckSettlementActionInput input
);
```

同步修改 `FateRuntimeModule`、`GameRuntimeFacade.BattleResolution`、`GameRuntimeFacade.SettlementCommandPort` 和 `GameRuntimeSettlementCommandHandler`，删除 typed input -> dictionary -> typed input 以及 typed result -> dictionary -> typed reward 的两次往返。

### 4.7 明确不改造的类型

- `NpcQuestOfferWindowData` / `NpcQuestOfferEntryData`：窗口输入已经 typed。
- `BountyBoardWindowData` / `BountyBoardEntryData`：窗口输入已经 typed。
- `PendingCharacterReward` / `PendingCharacterRewardEntry`：已经是正式 reward owner。
- `SettlementActionRequest` / `ForgeActionRequest`：已经 typed，直接复用。
- `PartyState`：继续作为 runtime owner，但不得被 window DTO 引用。
- `GodotProjectionLease<T>` / `RuntimePlainPayload`：仍服务 save/schema、trace、headless 和真实 Godot API 边界。
- `WorldMapFogSystem` 的 save/schema decoder：属于真实 Variant 边界，不因窗口 DTO 迁移而删除。
- headless `IReadOnlyDictionary<string, object>` snapshot：属于稳定自动化表面，保留为 typed owner 的单向投影。

## 5. API 迁移表

| 当前 API | 目标 API | 处理 |
|---|---|---|
| ~~`GameRuntimeSettlementWindowDataBuilder.GetSettlementWindowData()`~~ | `BuildSettlementOverviewWindowData()` | Phase C 已完成。|
| ~~`GameRuntimeSettlementCommandHandler.GetSettlementWindowData()`~~ | `GetSettlementOverviewWindowData()` | Phase C 已完成。|
| ~~`GameRuntimeFacade/WorldMapRuntimeProxy.GetSettlementWindowData()`~~ | `GetSettlementOverviewWindowData()` | Phase C 已完成，GDictionary 入口已删除。|
| ~~`SettlementWindow.ShowSettlement(GDictionary)`~~ | `ShowSettlement(SettlementOverviewWindowData)` | Phase C 已完成，UI parser 已删除。|
| ~~modal port 的 `SetActive*Context(GDictionary/plain)`~~ | `SetActive*Context(SettlementServiceWindowData)` | Phase D 已完成，replace-whole immutable context。|
| ~~modal port 的 `GetActive*ContextLease()` / `GetActive*ContextPlain()`~~ | `GetActive*Context()` | Phase D 已完成，UI 不再创建 projection lease。|
| ~~`ShopWindow.ShowShop(GDictionary)`~~ | `ShowShop(SettlementServiceWindowData)` | Phase D 已完成，UI parser 已删除。|
| ~~`SettlementShopWindowBuildResult.WindowDataPlain`~~ | `WindowData` | Phase D 已完成。|
| ~~`GameRuntimeFacade/Proxy.Get*WindowDataLease()`~~ | `Get*WindowDataTyped()` | Phase D 已完成。|
| ~~`GameRuntimeFacade.GetWarehouseWindowData()`~~ | `GetWarehouseWindowDataTyped()` | Phase B 已完成。|
| ~~`PartyWarehouseWindow.ShowWarehouse(GDictionary)`~~ | `ShowWarehouse(WarehouseWindowData)` | Phase B 已完成，直接读取 snapshot。|
| ~~`GetCharacterInfoContextLease()`~~ | `GetCharacterInfoContextTyped()` | Phase A 已完成。|
| ~~`CharacterInfoWindow.ShowCharacter(GDictionary)`~~ | `ShowCharacter(GameRuntimeCharacterInfoContext)` | Phase A 已完成，UI parser 已删除。|
| ~~`ResolveLowLuckSettlementEventRewards(GDictionary)`~~ | `ResolveLowLuckSettlementEventRewards(LowLuckSettlementActionInput)` | Phase F 已完成，返回 `LowLuckEventResult`，字典 adapter 已删除。|
| ~~`ShopWindow.action_requested(..., GDictionary)`~~ | panel-specific C# events | Phase E 已完成，UI confirm property bag 已删除。|

所有 `Get*WindowDataSnapshotPlain()` 可继续保留，但实现必须调用 typed DTO 的单向 plain projection。不得通过 snapshot 再重建 runtime/modal/window owner。

## 6. 实施顺序

### Phase A：人物信息 typed 直达 UI（已完成，2026-08-15）

1. 增加 facade/proxy typed getter。
2. 修改 `CharacterInfoWindow.ShowCharacter`。
3. 删除 UI 私有 parser/mirror。
4. 保持 `BuildSnapshotPlain()` 和现有 headless snapshot key 不变。

这是最小风险切片，因为正式 typed context 已完整存在。

落地记录：

- `GameRuntimeFacade.GetCharacterInfoContextLease()` → `GetCharacterInfoContextTyped()`；`WorldMapRuntimeProxy` 同步改 typed，删除空 lease 兜底（无 runtime 时返回 `null`）。
- `WorldMapSystem` 直接 `character_info_window.ShowCharacter(proxy.GetCharacterInfoContextTyped())`，不再开 projection lease。
- `CharacterInfoWindow` 删除 `CharacterInfoPayload`、`CharacterInfoSection`、`CharacterInfoEntry`、`EntryKind` 及全部 schema key 常量；命运段落改为从 `GameRuntimeCharacterInfoFate` 直接格式化的展示层逻辑，`EffectiveLuck` / `HasMisfortune` 由 typed owner 派生，不再由 UI 校验一致性。
- 空上下文语义保持不变：`null` context、空 `Sections`、空白 `DisplayName` 均关闭窗口。
- 回归：`run_character_info_window_fate_regression`（改写为 typed invariant + 渲染断言）、`run_character_info_payload_schema_regression`（改写为 typed context 渲染）、`run_character_info_identity_regression`、`run_modal_window_shell_regression`、`run_game_runtime_reward_flow_regression`、`run_game_runtime_reward_flow_handler_regression`、`run_game_runtime_snapshot_builder_regression`、`run_text_command_party_battle_surface_regression` 全绿；`python tools/magic_dev.py verify` 470 passed / 0 failed。

### Phase B：仓库 typed snapshot 直达 UI（已完成，2026-08-15）

1. 新增 `WarehouseWindowData`。
2. `GameRuntimeWarehouseHandler` 从同一 typed 数据同时服务 UI 和 headless snapshot。
3. 修改 facade/proxy/window 签名。
4. 删除仓库 UI parser/mirror。

落地记录：

- 新增 `scripts/systems/game_runtime/WarehouseWindowData.cs`：`record (Title, Meta, SummaryText, StatusText, Snapshot)` + `BuildSnapshotPlain()`；标题/摘要/状态文案留在 window DTO，没有塞进 `WarehouseWindowSnapshot` 领域快照。
- `GameRuntimeWarehouseHandler` 的 `BuildWarehouseWindowData()` / `BuildWarehouseWindowDataSnapshotPlain()` 两份近乎重复的构建合并为一份 typed 构建；`GetWarehouseWindowDataSnapshotPlain()` 改为 `GetWarehouseWindowDataTyped()?.BuildSnapshotPlain()`。
- `GameRuntimeFacade.GetWarehouseWindowData()` / `WorldMapRuntimeProxy.GetWarehouseWindowData()` → `GetWarehouseWindowDataTyped()`；`WorldMapSystem` 直接传 typed DTO。
- `PartyWarehouseWindow` 删除私有 `WarehouseWindowData`、`WarehouseEntry`、`TargetMember` 和全部 Variant 读取 helper，直接消费 `WarehouseInventoryEntrySnapshot` / `WarehouseTargetMemberSnapshot`；同时删除无调用方的 `SetWindowData(GDictionary)`。
- 装备实例语义保持不变：只有 `HasEquipmentInstance` 的条目才带 instance id 并展示实例/品质/耐久行，堆叠条目提交空 instance id。
- 回归：`tests/warehouse/` 全部 8 个、`run_text_save_load_regression`、`run_game_runtime_snapshot_builder_regression` 全绿；`python tools/magic_dev.py verify` 470 passed / 0 failed。

### Phase C：据点总览 DTO（已完成，2026-08-15）

1. 新增 6 个 settlement overview/member/service typed 类型。
2. `GameRuntimeSettlementWindowDataBuilder` 直接构建 DTO。
3. 修改 handler/facade/proxy/WorldMapSystem/SettlementWindow 调用链。
4. 删除 `PartyState`、payload 和 UI schema parser 依赖。

落地记录：

- 新增 `scripts/systems/settlement/SettlementOverviewWindowData.cs`，含 `SettlementOverviewWindowData`、`SettlementMemberOptionData`、`SettlementFacilityEntryData`、`SettlementResidentEntryData`、`SettlementMemberAvailabilityData`、`SettlementServiceEntryData`。
- `GetSettlementWindowData()` 链（builder/handler/facade/proxy）全部改名为 `BuildSettlementOverviewWindowData()` / `GetSettlementOverviewWindowData()` 并返回 typed DTO；无据点记录时返回 `null`。
- 服务条目不再把 metadata 写回 leased 世界数据字典：typed 条目直接由 `SettlementServiceMetadata` + 记录字段构造，`_build_service_entries(GDictArray)` 已删除（命令侧 `SettlementServiceEntryResolution` 路径保持不变，属于 Phase D/后续范围）。
- `SettlementWindow` 删除 `SettlementWindowData`、`FacilityEntry`、`ResidentEntry`、`MemberAvailability`、`ServiceEntry`、`MemberOption` 及全部 Variant helper；`ResolvedService` 降级为无 payload 的 UI 局部 `record struct`，`ServiceEntry.Payload` / `ResolvedService.ApplyToPayload()` 一并删除。
- DTO 不再携带 `party_state` / `selected_member_id` / `explicit_default_member_id`：默认成员由 `ResolveDefaultSettlementMemberId()` 在 runtime 侧解析，窗口只做「默认不可用则取第一个可渲染成员」的兜底。
- `member_availability` 索引从 string key 改为 `IReadOnlyDictionary<StringName, SettlementMemberAvailabilityData>`；随之丢弃只有测试在读的 `has_available_research` 字段（UI 从来只消费 `is_enabled` / `disabled_reason`）。
- 与提案的差异：没有给 `SettlementOverviewWindowData` 加 `BuildSnapshotPlain()`。据点 headless snapshot 走的是独立的 `GetSettlementHeadlessFactsPlain()`，窗口 DTO 没有 plain 消费者，加了就是死代码。
- 回归：`run_settlement_shop_window_schema_regression`（据点半部改写为 typed 渲染/成员可用性/稳定 id 提交断言）、`run_game_runtime_settlement_command_handler_regression`、`run_settlement_forge_service_regression` 全绿；`python tools/magic_dev.py verify` 470 passed / 0 failed。

### Phase D：共用服务窗口 typed context（已完成，2026-08-15）

1. 新增 `SettlementServiceWindowData`、labels、confirmation、entry 和 selection tagged union。
2. shop/contract/forge/stagecoach builder 全部直接创建该 DTO。
3. active modal 和 rollback snapshot 改为 immutable typed context。
4. 修改 facade/proxy/WorldMapSystem/ShopWindow typed 入口。
5. 删除四个 property-bag context wrapper 和所有 window projection lease。

落地记录：

- 新增 `scripts/systems/settlement/SettlementServiceWindowData.cs`：`SettlementServiceWindowData`、`SettlementServiceWindowLabelsData`、`SettlementServiceConfirmationData`、`SettlementServiceWindowEntryData`、`SettlementServiceSelectionData` 四个子类型与 `SettlementShopActionKind`。DTO 只提供 `WithMemberOptions` / `WithConfirmation` / `WithIdentity` 三个 replace-whole 派生方法，没有原地修改入口。
- `SettlementShopService.BuildWindowDataTyped` / `SettlementForgeService.BuildWindowDataTyped` / 契约板 / 驿站 builder 全部直接产出 typed DTO；`SettlementShopWindowBuildResult.WindowDataPlain` 与 `ProjectWindowDataLease()` 删除，改为 `WindowData` + `HasWindowData`。
- `SettlementModalContexts.cs`（4 个 property-bag wrapper）删除；`IGameRuntimeSettlementModalPort` 的 `SetActive*ContextPlain` / `GetActive*ContextLease` / `GetActive*ContextPlain` 一并删除，只保留 typed `SetActive*Context` / `GetActive*Context`。
- `SettlementCommandRollbackSnapshot` 直接借用 immutable DTO 引用，不再深拷贝四份 plain 字典；`GameRuntimeSettlementCommandHandler.ReplacePlainPayload` 与 `ProjectWindowDataLease` 随之删除。
- `Get*WindowDataLease()`（handler/facade/proxy 三层）→ `Get*WindowDataTyped()`；`WorldMapSystem.RenderWindows` 直接传 typed DTO，不再开 projection lease。`Get*WindowDataSnapshotPlain()` 保留，实现改为 typed owner 的单向 `BuildSnapshotPlain()`。
- `ShopWindow` 删除 `ShopWindowData`、`ShopEntry`、`MemberOption` 和全部 Variant helper；成员标签格式化留在窗口的纯展示 helper 中，默认成员由 runtime 解析、窗口只做"默认不可用则取第一个可渲染成员"的兜底。
- 与提案的差异：
  - 新增 `SettlementContractEntryFactsData`（提案条目字段表里没有）。契约板条目的 `state_id` / `provider_kind` / `listing_channels` / `accept_dialogue_text` / `lock_reason_id` / `is_repeatable` 只服务 headless 文本快照与状态汇总，放进 selection 会污染提交契约，因此单列一个 typed facts 记录。
  - 每条契约条目不再复制 `accept_feedback_success` / `accept_confirmation_text`：没有任何消费者，运行时提交时直接读 `QuestDefinition`。
  - 商店快照的 `buy_entries` / `sell_entries` / `gold` / `shop_id` / `feedback_text` 与驿站的 `destinations` / `origin_name` 合并进统一的 `entries` + `state_summary_text`；forge context 不再存 `service_payload`，刷新时由 typed context 的稳定 id 重建一次短期投影给仍读世界记录字典的 forge service。
  - forge 面板的 `state_summary_text` 改为承载运行时反馈（原来取自 service payload，实际恒为空）。
  - 驿站出发消息改为重新读取出发据点记录的 `display_name`，不再信任 context 里存的 `origin_name`。
- 回归：`tests/world_map/`（41 个）、`tests/runtime/`（44 个）、`tests/text_runtime/`（12 个）全绿，文本场景 golden 未变；`python tools/magic_dev.py verify` 470 passed / 0 failed。

### Phase E：ShopWindow typed intent（已完成，2026-08-15）

1. 新增 shop/contract/stagecoach request。
2. 复用现有 `ForgeActionRequest`。
3. 替换 `ShopWindow` GDictionary signal 和 confirm payload builder。
4. 为 proxy/facade/handler 增加对应 typed command gateway。
5. runtime 通过稳定 id 重查库存、价格、quest state、recipe 和 destination，不信任 UI display snapshot。

落地记录：

- 新增 `scripts/systems/settlement/SettlementServiceActionRequests.cs`：`SettlementShopActionRequest`、`SettlementContractBoardActionRequest`、`SettlementStagecoachActionRequest`；forge 继续复用 `ForgeActionRequest`。
- `ShopWindow` 暴露 `ShopActionRequested` / `ContractActionRequested` / `ForgeActionRequested` / `StagecoachActionRequested` 四个 C# event，按选中条目的 selection 子类型分派；`action_requested` signal 与 `_build_confirm_payload()` 删除，`closed` 仍是真实 signal 边界。
- 新增 gateway：proxy `CommandExecuteShopAction` / `CommandExecuteContractBoardAction` / `CommandExecuteStagecoachAction`，facade `CommandExecuteShopActionTyped` / `CommandExecuteContractBoardActionTyped`，handler `CommandExecuteShopActionRuntimeTyped` / `CommandExecuteContractBoardActionRuntimeTyped`。
- 契约提交的 `provider_interaction_id` 由 runtime 从 active contract board context 取，不再由 UI 携带；确认态同样由 runtime 侧的 `Confirmation` 判定，UI 只回传 `ConfirmAccept`。
- 商店提交仍是单件（`Quantity = 1`），与迁移前 `request_quantity` 缺省行为一致；`MaxQuantity` 只用于展示与快照。
- 回归：`run_settlement_shop_window_schema_regression`（shop 半部改写为 typed 渲染 + 稳定 id 提交 + 确认流断言）、`run_world_map_system_surface_regression`、`run_game_runtime_settlement_command_handler_regression`、`run_settlement_forge_service_regression`、`run_settlement_persist_failure_rollback_regression` 全绿。

### Phase F：低幸运奖励 typed port（已完成，2026-08-15）

1. 修改 `IGameRuntimeSettlementStatePort` 签名。
2. 修改两个 facade adapter 和 `FateRuntimeModule`。
3. 删除 `BuildPendingCharacterRewardEntriesTyped(GArray)` 等只为 round trip 存在的 parser。

落地记录：

- `IGameRuntimeSettlementStatePort` / `GameRuntimeFacade.SettlementCommandPort` / `GameRuntimeFacade.BattleResolution` / `FateRuntimeModule` 四层签名统一为 `LowLuckEventResult ResolveLowLuckSettlementEventRewards(LowLuckSettlementActionInput)`；无 fate runtime 或无 low-luck service 时返回 `null`（原来返回空 `GDictionary`），调用方按 `null` 跳过。
- `FateRuntimeModule.BuildLowLuckSettlementActionInput(GDictionary)` 与随之失去调用方的 `ReadString(GDictionary, string)` 删除；`LowLuckEventResultToDictionary` 保留——战斗结算路径 `HandleBattleResolution` 仍需要它投影 `low_luck_event_result`，它不是 round-trip-only 的 parser。
- `GameRuntimeSettlementCommandHandler.ExtractPendingCharacterRewards` 改为直接遍历 `LowLuckEventResult.PendingCharacterRewards`，新增 `NormalizePendingCharacterReward(PendingCharacterReward, ...)` 保留原有的 source type/label 兜底与 `Port.BuildPendingCharacterReward` 的 party-state 复核（entry 归一化、不支持 entry type 拒绝），只是不再经过 `PendingCharacterRewardPayload.Project` -> `ReadStringName/ReadArray` 的字典往返。低幸运奖励的 `member_id` / `source_type` / `source_id` / `source_label` 由服务侧全部填满，兜底分支对它们是 no-op，行为不变。
- 与提案的差异：`BuildPendingCharacterRewardTyped(GDictionary, ...)` 与 `BuildPendingCharacterRewardEntriesTyped(GArray)` 未删除。它们不只服务 round trip——服务 payload 里 authored 的 `pending_character_rewards` 数组仍走这条真实世界记录解码路径。
- 回归：`tests/progression/fate`、`tests/battle_runtime/fate`（含 `run_fate_low_luck_tactical_skills_regression`）、`tests/world_map/runtime`（含 settlement command handler）全绿。

### Phase G：helper 与剩余 GD cleanup（已完成，2026-08-15）

1. 删除无调用的 `TryAsObject`、`TryAsGodotArray`、`TryAsVector2I`、`TryAsBool`。
2. UI parser 删除后复查 `TryAsDictionary`；预计仅保留真实 fog/save decoder 等边界调用。
3. 将单次 `TryAsInt` 使用改为其 owning private decoder 的显式 `VariantType` 检查。
4. 若 production 不再需要 `GetValueOrDefault`，将真实 Godot dictionary 测试 helper 移到 test scope；不能误删同名 .NET 调用。
5. 只有所有 extension 调用都归零时才删除整个 `GodotVariantReadExtensions.cs`。
6. `dynamic` 清理单独按 decoder owner 审核；当前 `BattleCellState`、`BattleLootEntryPayload`、`BattleSpecialProfileManifestValidator`、`MeteorSwarmNumericSummary`、`PartyWarehouseService` 均需逐处确认输入边界，禁止机械替换。
7. `[GlobalClass]` 继续逐类检查 scene、resource 和 GDScript 引用；不得使用“所有 `*Def`/`*State` 都保留”或“只有 C# 调用就一定删除”之类的批量规则。

落地记录（helper）：

- 1-5 按 2.3 的事实收尾：四个零调用 extension 删除，`TryAsInt` 内联进 `GameRuntimeSettlementCommandHandler` 的私有 decoder，`GetValueOrDefault` 连同私有 `TryRead` / `ToVariant` 迁到 `tests/shared/GodotDictionaryTestReadExtensions.cs`，`GodotVariantReadExtensions.cs` 因 `TryAsDictionary` 仍有 3 处真实边界而保留。

落地记录（`dynamic`）：

- 逐处确认后，五个 owner 的输入边界都是「从 Godot dictionary 读出的装箱 `Variant`」，因此把运行时 duck typing 换成显式 `rawValue is Variant` 分支 + 同一个转换方法，保留原来的 try/catch 语义（`AsInt32` / `AsGodotDictionary` 对不可转换 variant 仍然抛出并落到原有 fallback），不改成更严格的 `VariantType` 相等判断——那会让 float/bool variant 的既有宽松转换失效。
- `BattleCellState.TryGetExactValue(GDictionary, object key, out object)` 直接删除：文件内所有调用点都是字符串字面量 key，重载解析永远命中 `(GDictionary, string, out object)` 版本，`object` 版是死代码。
- `MeteorSwarmNumericSummary` 的两个 `TryRead(..., out dynamic value)` 改为 `out Variant`：`source[key]` 的静态类型本来就是 `Variant`，这里的 `dynamic` 只是把静态可解析的调用推迟到运行时，改后所有 `AsInt32` / `AsBool` / `AsGodotArray` / `to_string_name` 绑定到同一目标。
- `scripts/` 下 `dynamic` 关键字归零（唯一残留是 `ContingencyTemplateContentRegistry` 注释里的“dynamic fields”措辞）。

落地记录（`[GlobalClass]`）：

- 逐类扫描 193 个 `[GlobalClass]` 类，对每个类名和其源文件名在全部 `.tscn` / `.tres` / `.res` / `.gd` / `project.godot` 中检索（3415 个文件），得到 18 个既无 `script_class` 名引用也无脚本路径引用的类。
- 其中 14 个保留并记录理由：`GameSession` 是 `project.godot` 的 autoload；`AttributeRequirement`、`ReputationRequirement`、`ProfessionActiveCondition`、`TraitRollGroupDef`、`TraitRollGroupEntryDef`、`BattleObjectiveDef`、`EnemyAiAction`、`UseChargePathAoeAction` 以及 `EquipmentAbilityAuthoringDefs.cs` 里的 5 个 payload/overlay def 都是其它 authored `Resource` 的 `[Export]` 字段或 typed array 元素类型，inspector 的“新建子资源”选择器依赖全局类名，即使当前还没有 `.tres` 实例也属于 authoring surface。
- 只删除 3 个纯 C# 实例化的 UI 类：`BattleSkillSlotButton`（只在 `BattleMapPanel.SkillGrid` 里 `new`）、`ModalWindowShell` 与 `SelectableListWindow`（只作为 C# 窗口基类被继承，`.tscn` 挂的是它们的子类脚本）。这三者不出现在任何场景、资源或 GDScript 中，`[GlobalClass]` 不提供任何注册价值。

## 7. 文件变更清单

### 新增

- `scripts/systems/settlement/SettlementOverviewWindowData.cs`
- `scripts/systems/settlement/SettlementServiceWindowData.cs`
- `scripts/systems/settlement/SettlementServiceActionRequests.cs`
- `scripts/systems/game_runtime/WarehouseWindowData.cs`
- `tests/shared/GodotDictionaryTestReadExtensions.cs`（Phase G：`GetValueOrDefault` 的 test-scope 落点）

### 重点修改

- `scripts/systems/game_runtime/GameRuntimeSettlementWindowDataBuilder.cs`
- `scripts/systems/game_runtime/GameRuntimeServiceWindowCommandHandler.cs`
- `scripts/systems/game_runtime/GameRuntimeContractBoardCommandHandler.cs`
- `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- `scripts/systems/game_runtime/IGameRuntimeSettlementCommandPort.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.SettlementCommandPort.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.BattleResolution.cs`
- `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`
- `scripts/systems/game_runtime/WorldMapSystem.cs`
- `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`
- `scripts/systems/battle/fate/FateRuntimeModule.cs`
- `scripts/systems/settlement/SettlementShopService.cs`
- `scripts/systems/settlement/SettlementForgeService.cs`
- `scripts/systems/settlement/SettlementShopTradeResult.cs`
- `scripts/ui/SettlementWindow.cs`
- `scripts/ui/ShopWindow.cs`
- `scripts/ui/PartyWarehouseWindow.cs`
- `scripts/ui/CharacterInfoWindow.cs`

Phase G 另外修改（`dynamic` / helper / `[GlobalClass]`）：

- `scripts/systems/platform/GodotVariantReadExtensions.cs`
- `scripts/systems/battle/core/BattleCellState.cs`
- `scripts/systems/battle/core/BattleLootEntryPayload.cs`
- `scripts/systems/battle/core/meteor_swarm/MeteorSwarmNumericSummary.cs`
- `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifestValidator.cs`
- `scripts/systems/inventory/PartyWarehouseService.cs`
- `scripts/ui/BattleSkillSlotButton.cs`
- `scripts/ui/components/ModalWindowShell.cs`
- `scripts/ui/components/SelectableListWindow.cs`

### 删除

- `scripts/systems/settlement/SettlementModalContexts.cs`，前提是四类 active context 全部迁移完成。（Phase D 已删除）
- UI 文件内本方案明确列出的私有 parser/mirror 类型。（Phase A-E 已删除）
- `GodotVariantReadExtensions.cs` 仅在最终调用归零时删除；不是 Phase A-F 的前置条件。（Phase G 结论：`TryAsDictionary` 仍有 3 处真实边界，整文件保留，其余 5 个方法删除或迁出）

### landing 后更新当前设计文档

- `docs/design/world/settlement_module.md`：把 window/modal context 从 plain property bag 更新为 immutable typed DTO。
- `docs/design/project_context_units.md`：CU-06/CU-08/CU-10 增加 typed DTO 文件和 direct C# window chain；只更新 ownership/read set，不写迁移进度。

两份设计文档已于 2026-08-15 Phase F-G 收尾时同步为落地后事实，具体范围见第 9 节末尾。

## 8. 测试改造与验证

### UI 行为回归

- `tests/world_map/ui/run_settlement_shop_window_schema_regression.cs`（Phase D/E 已改写）
  - 四个“构造非法 GDictionary 并期待 UI parser 拒绝”用例已删除，替换为 typed 渲染、非法 window data 关窗、shop/stagecoach 稳定 id 提交三组断言。
  - 保留据点 country id、服务 member availability、contract confirmation、forge recipe 展示和按钮行为断言。
- `tests/warehouse/run_party_warehouse_window_schema_regression.cs`
  - 改为直接传 `WarehouseWindowData`。
  - 保留堆叠物、装备实例 discard、纯文本详情、无效 icon path 等行为断言。
- `tests/world_map/ui/run_character_info_window_fate_regression.cs`
- `tests/world_map/runtime/run_character_info_payload_schema_regression.cs`
  - 改为 typed context 构建与窗口渲染；plain snapshot schema 断言继续放在 runtime/headless 层。

### Runtime 与 snapshot 回归

- `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs`
- `tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs`
- `tests/world_map/runtime/run_settlement_forge_service_regression.cs`
- `tests/world_map/runtime/run_npc_quest_offer_regression.cs`
- `tests/warehouse/`
- `tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs`
- `tests/battle_runtime/fate/run_fate_low_luck_tactical_skills_regression.cs`
- `tests/equipment/run_phoenix_rebirth_unique_acquisition_regression.cs`

必须分别验证：

- UI 接收 typed DTO 后显示和交互不变。
- active modal 刷新、确认、关闭、事务失败 rollback 不变。
- shop/forge/stagecoach/contract 提交只信任稳定 id，并重新校验当前状态。
- headless snapshot key、排序和 plain scalar 类型不变。
- 关闭窗口和 application shutdown 不新增 `GodotObject` lease/finalizer 问题。

每个 Phase 先运行对应 focused runner；跨完据点、仓库、人物信息和 fate 后再运行：

```bash
python tools/magic_dev.py verify
```

最终合并前运行：

```bash
python tools/magic_dev.py full
```

常规 full 不包含 BattleSim、benchmark 或 E2E；本迁移无需数值模拟。

### 实际验证记录（2026-08-15，Phase F-G 收尾）

- `dotnet build`：0 警告 0 错误（含 architecture analyzer）。
- Phase F focused：`tests/progression/fate`、`tests/battle_runtime/fate`（9 个）、`tests/world_map/runtime`（含 settlement command handler）全绿。
- `python tools/magic_dev.py verify`：468 passed / 3 failed。
- `python tools/magic_dev.py full`：470 passed / 3 failed。
- 三个失败与本迁移无关，属于同一工作区并行进行的「行动节奏由敏捷派生」改动（`BattleUnitState.DefaultActionThreshold` 从常量 120 改为派生值）：
  - `run_battle_ai_trace_projection_lease_regression`：unit snapshot JSON 长度/SHA golden 差 1 字节，键表含 `action_threshold`。
  - `run_battle_projection_lease_regression`：同一 unit projection golden，5617 -> 5615。
  - `run_temporal_status_semantics_regression`：`全速单位 10 个 tick 应得 50 进度`，实得 10。
  - 三者都不触碰本方案改动的窗口 DTO、低幸运 port、helper、`dynamic` decoder 或 `[GlobalClass]` 类；该并行改动落地后需由其 owner 重刷 golden。

## 9. 完成定义

只有同时满足以下条件，本 proposal 才能标记完成：

- 四条窗口输入链不再以 `GDictionary` 作为 C# 内部正式契约。
- settlement/shop window DTO 不持有 `PartyState` 或 `Dictionary<string, object>` payload。
- shop/contract/forge/stagecoach active modal context 为 immutable typed DTO。
- `ShopWindow` 不再拼装供 C# runtime 重新解析的 confirm property bag。
- 低幸运据点奖励 port 使用 typed input/result。
- headless plain snapshot 由 typed owner 单向生成且回归保持稳定。
- helper 只保留真实 Variant/Godot collection 边界所需的方法；是否整文件删除由最终零调用事实决定。
- `[GlobalClass]` audit 有逐类 scene/resource/GDScript 证据，不以文件名或继承层级批量猜测。
- focused regressions、`magic_dev.py verify` 和 `magic_dev.py full` 均有实际结果记录。
- `docs/design/world/settlement_module.md` 与 `docs/design/project_context_units.md` 已更新为落地后的当前事实。

以上条件在 2026-08-15 Phase F-G 收尾后全部满足（验证记录见第 8 节，唯一未绿的 3 个用例属并行的行动节奏改动）。设计文档同步范围：

- `docs/design/world/settlement_module.md`：正文的 window/modal context 段落已是 typed DTO 表述；末尾「源码级重建清单」按当前源码重新生成，此前 43 条已失效签名（`GetSettlementWindowData`、`SetActive*Context(GDictionary)`、`GDictionary ToDictionary()`、`: RefCounted` 基类等）全部修正。
- `docs/design/project_context_units.md`：CU-06 的 modal context owner 从已删除的四个 property-bag wrapper 改为 `SettlementServiceWindowData` 并补低幸运 typed port；CU-08 增加三个 settlement DTO 文件与窗口输入/提交边界；CU-10 增加 `WarehouseWindowData` 文件与仓库窗口边界。
