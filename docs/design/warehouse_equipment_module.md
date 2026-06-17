# 仓库与装备模块可重建规格说明

更新日期：`2026-06-17`

## 目标与边界

本文描述队伍共享仓库、物品定义、装备实例、装备/卸装、物品使用、商店/战利品接入的可重建规格。覆盖 `PartyWarehouseService`、`PartyEquipmentService`、`PartyItemUseService`、`GameRuntimeWarehouseHandler`、`GameRuntimePartyCommandHandler` 与仓库 UI 的正式契约。

## 模块拓扑

```text
GameContentCatalog.GetItemDefsTyped / GetSkillDefsTyped
  -> PartyWarehouseService(WarehouseState + ItemDef + equipment id allocator)
  -> PartyEquipmentService(PartyState + EquipmentState + Warehouse)
  -> PartyItemUseService(PartyState + ItemDef + SkillDef + CharacterManagement)
GameRuntimeWarehouseHandler / GameRuntimePartyCommandHandler
  -> PartyWarehouseWindow / PartyManagementWindow
  -> GameSession.SetPartyState / CommitRuntimeState
```

仓库真实状态在 `PartyState.warehouse` 与成员 `EquipmentState`；窗口只展示 handler 构建的数据。正式内容读取必须用 typed item/skill catalog，不恢复 string-key fallback。

## 静态内容契约

`ItemDef` 至少描述 item id、显示名、类别、堆叠上限、是否装备、装备类型、使用效果、价格、武器 profile、耐久/稀有度相关字段。装备类物品必须能创建 `EquipmentInstanceState`，非装备类只以 stack 数量存在。

`RecipeDef` 只消费 typed item catalog 校验输入/输出；forge 服务调用 warehouse batch 预览/提交时 entry 必须是 `{ item_id = StringName, quantity = int }` 等正式字典，不用裸 Variant item id。

`WeaponDamageDiceDef.ValidateDice` 是程序集内部校验入口，不作为 Godot-facing public helper。

## 运行时状态

- `WarehouseState`：存 stack entries 与 equipment instances；容量/堆叠规则由 service 解释。
- `WarehouseStackState`：`item_id` + `quantity`。
- `EquipmentInstanceState`：实例 id、item id、rarity、durability、rolled affixes/weapon data 等；装备实例必须有唯一 id。
- `EquipmentState`：成员各装备槽位到 equipment instance 的引用。

装备实例 id 由 `GameSession.AllocateEquipmentInstanceId` 或等价 allocator 分配；战利品和商店产生装备实例也必须走同一 allocator，避免 id 碰撞。

## 仓库服务行为

`PartyWarehouseService.Setup(partyState, itemDefs, equipmentInstanceIdAllocator)` 后才能操作。核心行为：

- `AddItemTyped(itemId, quantity)`：非装备按 stack 叠加；装备 item 应创建 equipment instance 并加入实例表；数量 <= 0 失败。
- `RemoveItemTyped(itemId, quantity)`：只对可堆叠/普通物品扣数量，数量不足失败。
- `RemoveEquipmentInstanceTyped(itemId, instanceId)`：装备必须按 instance id 删除，避免删错同名装备。
- discard one/all：handler 根据 item 是否装备选择 stack remove 或 instance remove。
- batch preview/commit：先完整校验容量、数量、item id，再一次性提交；失败不应部分写入。

所有结果应是 typed result，handler 只负责转 status/window refresh。

## 装备服务行为

`PartyEquipmentService` 负责装备/卸装：

1. 校验 member id、item id、instance id、item def、装备类型和目标槽位。
2. 校验职业/属性/等级/标签需求。
3. 装备时从 warehouse 移除实例并写入成员 `EquipmentState`；如果目标槽已有装备，先卸回仓库或按服务规则交换。
4. 卸装时从成员槽位移除并加入 warehouse。
5. 成功后重算角色属性、武器投影和 UI summary；失败不改状态。

`EquipmentRules` 拥有装备槽位和 equipment type 映射；不要在 UI 或 handler 复制合法槽位 HashSet。

## 物品使用

`PartyItemUseService` 通过 typed `PartyItemUseOptions` / `PartyItemUseResult` 接入：

- 消耗品可恢复资源、添加状态、推进任务或触发角色成长。
- 技能书使用需要 item def 映射 skill id，并通过 `CharacterManagementModule.LearnSkillOptionsData` 学习技能。
- 使用成功后扣除 warehouse 物品并刷新正式 `_render_from_runtime()`。
- 使用失败不得扣物品。

## UI / runtime handler

`GameRuntimeWarehouseHandler`：打开仓库、discard、use item、构建 window data、按 entry label 区分“据点服务/队伍管理”。`PartyWarehouseWindow` 不直接修改 party state。

`GameRuntimePartyCommandHandler`：队伍编成、选择成员、装备、卸装、打开仓库。装备错误消息由 typed `PartyEquipmentService.EquipmentActionResult` 解释，不从 UI payload 推断。

## 事务与持久化

仓库、装备、物品使用成功后必须：

1. 更新 `PartyState` 内存对象。
2. 通知 character management 或重算成员摘要。
3. 调 `GameSession.SetPartyState` / runtime commit。
4. 刷新仓库/队伍窗口。

持久化失败要返回 failure，并提示状态可能已改变但保存失败。

## 回归入口

```bash
godot --headless -s res://tests/world_map/schema/run_world_map_low_level_defensive_regression.cs
godot --headless -s res://tests/world_map/runtime/run_game_runtime_party_command_handler_regression.cs
godot --headless -s res://tests/world_map/ui/run_party_management_window_regression.cs
```

## 安全约束

- 不恢复 GDictionary setup string-key fallback。
- 不用裸 item id Variant 作为 batch entry。
- 不在 UI 里改 warehouse/equipment state。
- 不绕过 equipment instance id allocator。

## 实现级补充：ItemDef 判定

重建 `ItemDef` 时必须提供 typed helper，而不是让调用方猜字段：

- `IsEquipment()`：由 item category / equipment type 判定。
- `GetStackLimit()`：非装备可堆叠，装备通常 stack limit = 1 且进入 equipment instance 表。
- `GetEquipmentType()`：返回 enum/typed equipment type。
- `GetUseKind()`：消耗品、技能书、材料、不可使用。
- `GetWeaponProfile()`：武器类装备投影为 battle weapon projection。

item id 一律用 `StringName` 正式 key。display name 只用于 UI，不用于业务匹配。

## 实现级补充：WarehouseState 不变量

- stack quantity 永远 > 0；扣到 0 时移除 stack entry。
- equipment instance id 全局唯一；同一个 instance 不能同时在 warehouse 和成员装备槽。
- warehouse 中装备 instance 的 `item_id` 必须存在且 item def 是 equipment。
- stack item 不允许出现在 equipment instance 表。
- capacity 若启用，应按 stack entry 和 equipment instance 分别计数；preview 必须先检查容量。

## 实现级补充：Add / Remove 事务

### AddItemTyped

1. 校验 item id 非空、item def 存在、quantity > 0。
2. 若 item 是装备：循环 quantity 次分配 equipment instance id，创建 `EquipmentInstanceState`，加入 warehouse instance 表。
3. 若 item 非装备：按 stack limit 拆分/合并数量。
4. 任一装备 instance 分配失败时整个 add 失败，不应部分添加。

### RemoveItemTyped

1. 仅用于非装备 stack。
2. 校验数量充足。
3. 从最合适 stack 扣减；扣到 0 删除。
4. 返回 removed summary，供 UI/log 使用。

### RemoveEquipmentInstanceTyped

1. 校验 instance id 非空。
2. 查 instance；校验 item id 匹配（如果调用方传了 item id）。
3. 删除 instance 并返回 removed equipment snapshot。
4. 找不到 instance 时 failure，不删除其他同名装备。

## 实现级补充：batch swap

Batch swap 用于 forge、商店、任务提交等“扣多个输入、给多个输出”的事务：

- Preview 阶段构建 normalized entries：`item_id: StringName`、`quantity:int`、可选 `instance_id:StringName`。
- Preview 必须验证所有输入数量、所有输出 item def、装备实例容量和 allocator 可用性。
- Commit 必须基于 preview 结果执行；不要在 commit 重新解释 UI payload。
- 任一输入/输出失败则整体失败，不能先扣材料后失败。

## 实现级补充：装备槽位

`EquipmentRules` 拥有：

- equipment type -> allowed slots。
- main hand/off hand/two hand 冲突。
- armor/accessory 多槽规则。
- occupied slot 列表，用于 battle-local change equipment preview。

装备时如果装备占多个槽，必须一次性占用；卸装也必须释放所有占用槽。双手武器应阻止 offhand 同时装备，或按规则自动卸下冲突装备并回仓库。

## 实现级补充：属性/战斗投影

装备改变后必须让 CharacterManagement/AttributeService 重新计算：

- attribute snapshot。
- weapon projection / weapon dice。
- armor / resistance / tags。
- battle unit factory 使用的 equipment view。

不要让 battle runtime 直接读取 warehouse state 推断装备；battle 只接收角色管理层给出的装备/武器投影。

## 实现级补充：物品使用结果

`PartyItemUseResult` 应表达：成功/失败、消耗数量、目标 member、learned skill、resource delta、quest/achievement events、message。使用技能书时：

1. item def 解析 skill id。
2. member 存在且可学习。
3. 如需替换 practice/skill slot，必须由 typed options 明确确认。
4. 成功后扣物品、写 UnitProgress、刷新窗口。

## 实现级补充：UI payload

仓库窗口 entry 建议字段：`item_id`、`display_name`、`quantity`、`is_equipment`、`instance_id`、`rarity`、`durability`、`equipped_by`、`can_use`、`can_discard`。按钮回传必须带 item id 和 instance id（装备必带），不能只回传列表 index。

## 实现级补充：回归映射

| 行为 | 回归 |
|---|---|
| strict typed warehouse/equipment boundary | `run_world_map_low_level_defensive_regression.cs` |
| party command equip/unequip | `run_game_runtime_party_command_handler_regression.cs` |
| party UI projection | `run_party_management_window_regression.cs` |
| forge batch service | `run_settlement_forge_service_regression.cs` |

## 源码级重建清单：仓库/装备文件与 surface

以下清单用于弥补纯设计文档遗漏：重建时必须逐项恢复这些 owner 文件、公开/内部 typed surface 与职责边界；若实现拆文件，仍要保留等价 API 与行为。

### `scripts/systems/inventory/PartyWarehouseService.cs`

- `public partial class PartyWarehouseService : RefCounted`
- `internal sealed class WarehouseBatchItemEntry`
- `internal sealed class WarehouseBatchSwapResult`
- `internal Godot.Collections.Dictionary ToDictionary() =>`
- `public static WarehouseBatchSwapResult Success() => new(true, "", "", "");`
- `internal sealed class WarehouseAddItemResult`
- `internal Godot.Collections.Dictionary ToDictionary()`
- `internal sealed class WarehouseRemoveItemResult`
- `internal Godot.Collections.Dictionary ToDictionary()`
- `public WarehouseRemoveItemResult WithError(string errorCode) =>`
- `public void Setup(PartyState partyState) =>`
- `public int GetTotalCapacity()`
- `public int GetUsedSlots()`
- `public int GetFreeSlots() => Mathf.Max(GetTotalCapacity() - GetUsedSlots(), 0);`
- `public bool IsOverCapacity() => GetUsedSlots() > GetTotalCapacity();`
- `public int CountItem(StringName itemId)`
- `internal IReadOnlyList<WarehouseInventoryEntry> GetInventoryEntriesTyped()`
- `public ItemDef GetItemDef(StringName itemId)`
- `internal WarehouseAddItemResult PreviewAddItemTyped(StringName itemId, int quantity) =>`
- `internal WarehouseAddItemResult AddItemTyped(StringName itemId, int quantity) =>`
- `internal WarehouseRemoveItemResult RemoveItemTyped(StringName itemId, int quantity)`
- `public bool HasEquipmentInstance(StringName instanceId, StringName expectedItemId = default) =>`
- `public EquipmentInstanceState TakeEquipmentInstanceByItem(StringName itemId)`
- `public bool DepositEquipmentInstance(EquipmentInstanceState instance)`

### `scripts/systems/inventory/PartyEquipmentService.cs`

- `public class PartyEquipmentService`
- `internal sealed class EquipmentDisplacedEntry`
- `internal sealed class EquipmentEquipPreviewResult`
- `internal sealed class EquipmentActionResult`
- `internal sealed class EquipmentViewEntry`
- `public PartyEquipmentService()`
- `public void Dispose()`
- `public ItemDef GetItemDef(StringName itemId)`
- `public EquipmentState GetEquipmentState(StringName memberId)`
- `internal List<EquipmentViewEntry> GetEquippedEntriesTyped(StringName memberId)`
- `internal EquipmentActionResult EquipItemTyped(StringName memberId, StringName itemId) =>`
- `internal EquipmentActionResult UnequipItemTyped(StringName memberId, StringName slotId)`

### `scripts/systems/inventory/PartyItemUseService.cs`

- `public class PartyItemUseService`
- `internal sealed class PartyItemUseOptions`
- `public PartyItemUseOptions(bool confirmPracticeReplacement = false)`
- `public CharacterManagementModule.LearnSkillOptionsData ToLearnSkillOptions() =>`
- `internal sealed class PartyItemUseResult`
- `public static PartyItemUseResult Create(StringName itemId, StringName memberId) =>`
- `public PartyItemUseResult WithReason(string reason)`
- `public PartyItemUseResult WithSkill(StringName skillId)`
- `public PartyItemUseResult WithConfirmationRequired(PracticeSkillLearnStatus status)`
- `public PartyItemUseResult WithSuccess(int consumedQuantity)`
- `internal GDictionary ToDictionary() =>`
- `public void Dispose()`

### `scripts/systems/inventory/EquipmentDropService.cs`

- `public class EquipmentDropService`
- `public EquipmentDropService()`
- `public EquipmentDropService(RandomNumberGenerator rng)`
- `public void SetRngForTesting(RandomNumberGenerator rng)`
- `internal void SetRollRangeForTesting(Func<int, int, int> rollRange)`
- `public List<object> RollDrops(StringName dropTableId, int dropLuck)`
- `public int RollDropRarity(int dropLuck)`

### `scripts/player/warehouse/WarehouseState.cs`

- `public partial class WarehouseState : RefCounted`
- `public IReadOnlyList<WarehouseStackState> GetStacksTyped()`
- `public IReadOnlyList<WarehouseStackState> GetNonEmptyStacksTyped()`
- `public WarehouseStackState GetStackAt(int index)`
- `public void AddStack(WarehouseStackState stack)`
- `public bool RemoveStackAt(int index)`
- `public void ReplaceStacks(IEnumerable<WarehouseStackState> values)`
- `public IReadOnlyList<EquipmentInstanceState> GetEquipmentInstancesTyped()`
- `public IReadOnlyList<EquipmentInstanceState> GetNonEmptyEquipmentInstancesTyped()`
- `public EquipmentInstanceState GetEquipmentInstanceAt(int index)`
- `public void AddEquipmentInstance(EquipmentInstanceState instance)`
- `public EquipmentInstanceState RemoveEquipmentInstanceAt(int index)`
- `public void ReplaceEquipmentInstances(IEnumerable<EquipmentInstanceState> values)`
- `public WarehouseState DuplicateState()`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public static WarehouseState FromDictionary(Godot.Collections.Dictionary payload)`

### `scripts/player/warehouse/EquipmentInstanceState.cs`

- `public partial class EquipmentInstanceState : RefCounted`
- `public static EquipmentInstanceState CreateInstance(StringName pItemId, StringName pInstanceId)`
- `public static EquipmentInstanceState CreateTransientInstance(StringName pItemId)`
- `public static StringName FormatInstanceId(int serial) =>`
- `public static StringName FormatPreviewInstanceId(int serial) =>`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public EquipmentInstanceState DuplicateState()`
- `public static EquipmentInstanceState FromDictionary(Godot.Collections.Dictionary data) =>`
- `public static EquipmentInstanceState FromTransientLootDictionary(Godot.Collections.Dictionary data) =>`
- `public static bool IsValidRarity(int value) =>`

### `scripts/player/warehouse/ItemDef.cs`

- `public partial class ItemDef : Resource`
- `public int GetEffectiveMaxStack()`
- `public int GetBasePrice()`
- `public int GetBuyPrice()`
- `public int GetBuyPrice(int price_basis_points)`
- `public int GetSellPrice()`
- `public int GetSellPrice(int price_basis_points)`
- `public List<StringName> GetTagsTyped() => NormalizeStringNameList(tags);`
- `public List<StringName> GetCraftingGroupsTyped() =>`
- `public List<StringName> GetQuestGroupsTyped() =>`
- `public StringName GetItemCategoryNormalized()`
- `public bool HasEquipmentCategory()`
- `public List<StringName> GetEquipmentSlotIdsTyped()`
- `public bool IsEquipment()`
- `public StringName GetEquipmentTypeIdNormalized()`
- `public bool HasValidEquipmentType()`
- `public bool IsWeapon()`
- `public int GetWeaponAttackRange()`
- `public StringName GetWeaponPhysicalDamageTag()`
- `internal WeaponPhysicalDamageTagKind GetWeaponPhysicalDamageTagKind()`
- `public bool IsArmor()`
- `public int GetMaxDexBonus()`
- `public bool IsAccessory()`
- `public bool IsSkillBook()`
- `public List<AttributeModifier> GetAttributeModifiersTyped()`
- `public List<StringName> GetFinalOccupiedSlotIdsTyped(StringName entry_slot_id)`
- `internal static ItemCategoryKind ToItemCategoryKind(StringName value)`
- `internal static ItemEquipmentTypeKind ToEquipmentTypeKind(StringName value)`
- `internal static WeaponPhysicalDamageTagKind ToWeaponPhysicalDamageTagKind(StringName value)`
- `internal static StringName ToStringName(ItemCategoryKind kind)`
- `internal static StringName ToStringName(ItemEquipmentTypeKind kind)`
- `internal static StringName ToStringName(WeaponPhysicalDamageTagKind kind)`

### `scripts/player/equipment/EquipmentRules.cs`

- `public static class EquipmentRules`
- `public static IReadOnlyList<StringName> GetAllSlotIdsTyped()`
- `public static bool IsValidSlot(StringName slot_id)`
- `public static string GetSlotLabel(StringName slot_id)`
- `internal static EquipmentSlotKind ToSlotKind(StringName value)`
- `internal static StringName ToStringName(EquipmentSlotKind kind)`

### `scripts/player/equipment/EquipmentState.cs`

- `public partial class EquipmentState : RefCounted`
- `public StringName GetEquippedItemId(StringName slot_id)`
- `public StringName GetEquippedInstanceId(StringName slot_id)`
- `public EquipmentInstanceState GetEquippedInstance(StringName slot_id)`
- `public EquipmentEntryState GetEntry(StringName entry_slot_id)`
- `public EquipmentEntryState GetEntryForSlot(StringName slot_id)`
- `public IReadOnlyList<StringName> GetOccupiedSlotIdsForEntryTyped(StringName entry_slot_id)`
- `public void ClearSlot(StringName slot_id)`
- `public void ClearEntrySlot(StringName entry_slot_id)`
- `public EquipmentInstanceState PopEquippedInstance(StringName entry_slot_id)`
- `public StringName GetEntrySlotForSlot(StringName slot_id) =>`
- `public IReadOnlyList<StringName> GetEntrySlotIdsTyped()`
- `public IReadOnlyList<StringName> GetFilledSlotIdsTyped()`
- `public int GetEquippedCount() => GetEntrySlotIdsTyped().Count;`
- `public EquipmentState DuplicateState()`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public static EquipmentState FromDictionary(Godot.Collections.Dictionary data)`

### `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`

- `public sealed class GameRuntimeWarehouseHandler`
- `private sealed class WarehouseTransactionSnapshot`
- `public void Setup(GameRuntimeFacade runtime)`
- `public void Dispose()`
- `internal Dictionary GetWarehouseWindowData()`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandOpenPartyWarehouseTyped()`
- `public void OpenPartyWarehouseWindow(string entryLabel)`
- `public void OnPartyWarehouseWindowClosed()`

### `scripts/systems/game_runtime/GameRuntimePartyCommandHandler.cs`

- `public sealed class GameRuntimePartyCommandHandler`
- `public void Setup(GameRuntimeFacade runtime)`
- `public void Dispose()`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandOpenPartyTyped()`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandSelectPartyMemberTyped(StringName memberId)`
- `internal GameRuntimeFacade.RuntimeCommandResult CommandSetPartyLeaderTyped(StringName memberId)`
- `public void OpenPartyManagementWindow()`
- `public void OnPartyLeaderChangeRequested(StringName memberId)`
- `public void OnPartyManagementWindowClosed()`
- `public void OnPartyManagementWarehouseRequested()`
- `public void ApplyPartyStateToRuntime(string successMessage)`

