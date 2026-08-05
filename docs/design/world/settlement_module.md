# 据点服务模块可重建规格说明

> 状态：`Current / Implemented`
> 核对日期：`2026-07-29`

更新日期：`2026-07-29`

## 目标与边界

本文描述据点服务模块的运行时所有权、数据契约、命令分发、服务窗口、持久化写回与回归入口。目标是在 `scripts/systems/settlement/`、`GameRuntimeSettlementCommandHandler`、据点窗口脚本丢失时，可以按本文重建功能一致的据点服务链路。

模块覆盖：

- 据点窗口数据构建、服务列表、服务 NPC、反馈文本。
- settlement action id 到具体服务实现的分发。
- 仓库、休息、商店、铁匠、驿站、任务板、研究、传闻、世界门等世界侧服务入口。
- 服务消耗、奖励、任务进度、据点状态、fog reveal、world data 持久化回写。

不覆盖：世界生成如何创建据点实例；物品、装备、任务、角色成长内部规则；战斗内部逻辑。

## 模块拓扑

```text
WorldMapSystem / SettlementWindow / ShopWindow
  -> WorldMapRuntimeProxy.CommandExecuteSettlementAction(...)
  -> GameRuntimeFacade.CommandExecuteSettlementActionTyped(...)
  -> GameRuntimeSettlementCommandHandler
    -> IGameRuntimeSettlementCommandPort
      -> state/content/transaction/modal/world capability adapters
      -> concrete runtime/session/service owners
```

`WorldMapSystem` 只负责打开窗口和把 UI action 转给 proxy；正式服务规则必须在 command handler 或 settlement service 类中。handler 弱借按 state/content/transaction/modal/world 分组的 `IGameRuntimeSettlementCommandPort`，不持有或向子处理器透出 `GameRuntimeFacade`、`GameSession`、角色管理或迷雾 owner。锻造确认由 `ShopWindow.ForgeActionRequested` 普通 C# 事件携带 `ForgeActionRequest`，经 proxy/facade 的 typed 入口提交，不再把确认请求投影为 Godot Dictionary signal。服务结果使用 typed `SettlementServiceResult` / typed payload，避免把 `ToDictionary()` 再回读成业务态。

## 据点数据契约

据点实例来自 `world_data.settlements[]`，核心字段：`settlement_id`、`display_name`、`tier`、`tier_name`、`faction_id`、`country_id`、`origin`、`footprint_size`、`facilities`、`available_services`、`service_npcs`、`settlement_state`。

`country_id` 是据点记录级的国家归属键；空字符串精确表示“无国家归属”。它与 `faction_id` 相互独立，不进入下方 5 字段 `settlement_state`。服务层与窗口层必须原样投影该字段，不得从 `faction_id`、`display_name`、据点名称池或名称前缀推导国家。

`available_services[]` 每项至少包含：

| 字段 | 说明 |
|---|---|
| `settlement_id` | 所属据点 id。|
| `facility_id` / `facility_template_id` / `facility_name` | 服务来源设施。|
| `npc_id` / `npc_template_id` / `npc_name` | 服务 NPC。|
| `service_type` | 面向 UI 的服务类型。|
| `action_id` | 正式命令 id，如 `service:warehouse`。|
| `interaction_script_id` | 原始交互脚本 id，用于生成 action id。|

`settlement_state` 的当前持久化 schema 必须精确包含 5 个字段：

| 字段 | 类型与约束 |
|---|---|
| `visited` | `bool`。|
| `reputation` | `int`。|
| `active_conditions` | 由非空字符串组成的数组；数组本身可为空。|
| `cooldowns` | 非空字符串键到非负 `int` 的字典。|
| `shop_states` | shop id 到 typed 商店状态的字典。|

每个 `shop_states[shop_id]` 精确包含 `shop_id`、`current_inventory`、`seed`、`last_refresh_step`；每个库存条目精确包含 `item_id`、正数 `quantity`、正数 `unit_price` 与 `sold_out=false`。商店子状态中的 `seed` / `last_refresh_step` 是该商店随机结果和刷新进度的唯一事实来源；据点层不再保存共享镜像。目标商店到期刷新时独立调用 `TrueRandomSeedService.GenerateSeed()`，只更新该商店，其他商店的 seed、刷新步数和库存必须保持不变。

`WorldMapSettlementStateData` 是完整不可变 owner，`SettlementShopStateData` / `SettlementShopStockEntryData` 是它的 typed 子状态。`WorldMapSettlementRecordData` 持有该 owner，`WorldRuntimeData.TrySetSettlementState(...)` 只接受完整聚合；`MarkSettlementVisited(...)` 必须使用 `WithVisited(true)`，不能由局部字段重建整个状态。

服务新增持久字段时必须同步扩展 typed aggregate、spawn default、严格校验、save version 与回归，不允许通过额外字段 property bag 隐式扩展。`world_step` 由 `WorldRuntimeData` 持有并作为参数传给商店服务；`shop_feedback_text` 属于 active shop context / active settlement feedback，二者都不是持久化据点状态。当前顶层存档版本为 18，旧版本及不完整/额外字段 payload 直接拒绝，不提供迁移或 fallback。

## Action 分发

正式 action id 形如 `service:<name>`。生成器已保证 `party_warehouse` fallback 服务存在；handler 仍必须防御 action id 为空、据点不存在、服务 entry 不存在、runtime 未初始化、modal 状态不匹配。

核心 action 类别：

- `service:warehouse`：打开队伍仓库窗口，不直接修改物品。
- `service:rest_basic` / `service:rest_full`：消耗货币或服务条件，恢复队伍资源/状态，推进任务或日志。
- 商店类：`service:basic_supply`、`service:local_trade`、`service:city_market`、`service:military_supply`、`service:grand_auction` 打开 shop modal，买卖由 shop 命令处理。
- `service:repair_gear` / `service:master_reforge`：打开 forge modal 或执行装备耐久/重铸服务。
- `service:stagecoach` / `service:world_gate_travel`：打开目的地列表或执行旅行，必须更新玩家坐标和 world data。
- `service:contract_board`：打开任务板，任务列表来自 typed `QuestDef` 和 party quest state。
- `service:village_rumor` / `service:intel_network`：调用 `WorldMapFogSystem` reveal，写回 active fog state。
- `service:research` / `service:unlock_archive`：读写据点研究状态并可能解锁内容或任务进度。

未知 action 返回 command failure，不应静默成功。

## 窗口与 modal 所有权

- `SettlementWindow` 展示据点名、tier、服务 NPC 和 action buttons；不计算服务规则。
- 商店、铁匠、驿站、任务板共用 `ShopWindow` 风格 modal 时，handler 构建不同 window data context；铁匠确认使用专用 `ForgeActionRequest` C# 事件，其他窗口继续使用各自现有提交协议。
- active modal kind 由 runtime 持有；打开服务 modal 时应关闭 settlement action feedback 的冲突状态。
- 窗口关闭只清空 active modal/context，不回滚已提交服务。

## 服务执行事务

服务命令应遵循：

1. 校验 runtime、active settlement、action entry、party/world/session 可用。
2. 构建 typed request，包含 settlement id、service entry、payload、world step、party state。
3. 调用领域 service，得到 typed result。
4. 如果 result 失败：把原因写入 active service context 与 feedback/status，不写 world/party。
5. 如果 result 成功：写 party、warehouse、equipment、quest、settlement_state、fog/world_data 等受影响 owner。
6. 先更新内存 owner，再由 transaction port 调用正式 runtime transaction 提交；handler 不直接访问 `GameSession`。
7. 持久化失败时恢复命令前的 party/world/modal context 与 session staging，再返回统一的“操作已回滚” failure。

不要做旧 payload 兼容；服务 payload 类型错误应 fail fast。

## 与任务、奖励和进度的桥接

- 合同板显示基于 typed `QuestDef` 和 party quest state；不要把 quest def 转 dictionary 后再回读。
- 任务提交、领取、物品提交通过 `GameRuntimeQuestCommandHandler` / `CharacterManagementModule` typed payload。
- settlement service 可发 quest progress event，但必须包含 world step、action id、settlement id/source id 等 typed 上下文。
- 服务奖励应进入统一 reward flow 或 warehouse service，不在 UI 里直接改 party。

## 回归入口

```bash
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_settlement_entry_regression.cs
godot --headless -s res://tests/world_map/runtime/run_settlement_forge_service_regression.cs
godot --headless -s res://tests/world_map/runtime/run_settlement_research_service_schema_regression.cs
godot --headless -s res://tests/world_map/ui/run_settlement_shop_window_schema_regression.cs
```

## 安全约束

- 不把 settlement domain rule 放进 `WorldMapSystem` 或窗口脚本。
- 不恢复 `SettlementServiceResult.FromDictionary()` 作为正式业务链。
- 不新增旧 schema fallback，除非用户明确要求兼容。
- 所有消耗/奖励/旅行/fog reveal 都必须有持久化失败路径。

## 实现级补充：handler 内部状态

`GameRuntimeSettlementCommandHandler` 重建时只持有一个弱引用的
`IGameRuntimeSettlementCommandPort`。该 composite port 按职责拆成：

- state：party、settlement record/state、warehouse service 与成员/奖励语义操作。
- content：item、trait、recipe、quest、enemy definition 查询和 quest command。
- transaction：rollback snapshot、commit/rollback、单 owner persist 与 command result。
- modal：active modal 与 shop/forge/stagecoach/contract/NPC/bounty context。
- world：world step、坐标、settlement entry、visited 与 fog 可见/reveal 语义操作。

facade 显式适配器可以访问 concrete runtime/session/service owner，但这些 owner 不跨 port。主 handler 继续决定事务捕获/提交/回滚时机、action dispatch 与反馈；四个子处理器只调用主 handler 的语义方法，不读取 port。

handler 不应缓存可变 `GDictionary` 作为长期真相；active UI context 使用 detached plain CLR graph，只有同步 Godot/UI 边界才能创建 Request-domain projection lease。每次执行服务前仍须从 runtime/context 重新解析 active settlement 和服务 entry。

## 实现级补充：window data 构建

据点窗口数据应由 runtime/handler 构建，字段建议固定为：

| 字段 | 说明 |
|---|---|
| `settlement_id` | 当前据点 id。|
| `display_name` | 据点显示名，缺失 fallback “据点”。|
| `tier` / `tier_name` | tier 数值和中文 tier 名。|
| `faction_id` | 阵营。|
| `country_id` | 国家归属 id；空字符串表示无国家归属，必须原样投影且不得由 faction 或名称推导。|
| `visited` | 来自 settlement_state。|
| `services` | 可展示服务条目数组。|
| `service_npcs` | NPC 展示数组。|
| `feedback_text` | 最近服务反馈。|
| `world_step` | 当前 active world step。|

服务条目投影时要带 `enabled`、`disabled_reason`、`action_id`、`label`、`description`、`cost_preview`。不可执行原因只影响按钮，不应在 UI 中隐藏正式 action id。

## 实现级补充：action resolution

执行 `CommandExecuteSettlementActionTyped(actionId, payload)` 的等价步骤：

1. `actionId` 去空白，空则 failure。
2. 解析 command settlement id：优先 active settlement id，其次 payload 中 settlement id，最后 selected coord 上的 settlement。
3. 通过 `WorldMapDataContext.GetSettlementRecord(settlementId)` 取原始 record；缺失 failure。
4. 在 record.available_services 中找 `action_id == actionId` 的 service entry；找不到但 action 是内置 fallback 时可构建 synthetic entry。
5. 构建 `SettlementServiceEntryResolution`，包含 settlement record、state、entry、source、payload。
6. 分发到具体 `Handle<Service>`。
7. 结果进入 `FinalizeSettlementServiceResult`：更新 feedback、modal context、party/world/session，并记录 command log。

不要通过按钮 label、NPC name 或 UI index 来定位服务；正式定位只认 action id + settlement id。

## 实现级补充：服务结果模型

`SettlementServiceResult` 应表达：

- `Ok` / `Message` / `FeedbackText`。
- 是否需要打开 modal：none、shop、forge、stagecoach、contract board、warehouse。
- settlement state patch。
- party/warehouse/equipment 变更是否已提交。
- world data/fog 是否需要保存。
- quest/achievement progress event。
- 可选 reward summary。

服务可以“打开窗口但不提交消耗”，例如 shop/forge/stagecoach 打开 modal；也可以“立即执行并提交”，例如 rest、rumor、research。两类结果必须区分，避免打开 shop 时错误保存无关 state。

## 实现级补充：典型服务契约

### 仓库服务

`service:warehouse` 只打开 PartyWarehouseWindow，entry label 通常为“据点服务”。它不改变 warehouse state。关闭窗口只清 modal。

### 休息服务

休息服务应校验费用/条件，成功后恢复 active party 成员资源或清理可恢复状态。成功必须写 PartyState；若费用来自 warehouse/currency，也必须同事务扣除。失败不恢复、不扣费。

### 商店服务

商店 action 打开 shop modal，window data 至少包含 settlement id、service action、buy entries、sell candidates、price/currency、库存刷新信息。初次打开不继承据点级旧反馈；买卖失败原因写入 active shop context。买/卖命令走独立 `CommandShopBuyTyped` / `CommandShopSellTyped`，而不是再次走 generic settlement action。

### 铁匠服务

forge modal 负责展示可修理/重铸/制作项。commit 时通过 `PartyWarehouseService.Preview/CommitBatchSwapEntriesTyped` 校验材料与产物；装备修理/重铸必须按 equipment instance id 操作。

### 驿站 / 世界门旅行

旅行必须校验目的地 settlement id、是否可达、费用。成功后更新 player coord / selected coord、active modal、world data，并保存 session。跨 active submap 的旅行必须走 WorldMapDataContext 的 active world 同步，不可只改坐标。

### 任务板

contract board 只展示 typed QuestDef 中 provider/settlement 条件匹配的任务。accept/submit/claim 走 quest command handler。任务板自己不改 QuestState。

### 传闻 / 情报

传闻类服务应调用 fog reveal（通常 diamond range），将 reveal 结果写入 active fog state，再 `SetWorldData(root_world_data)`。若 reveal 没有新增格，也应返回成功反馈而非错误。

## 实现级补充：失败路径

- runtime/session 缺失：failure “运行时尚未初始化”或“游戏会话不可用”。
- active settlement 缺失：关闭 settlement modal 或返回 failure，不保留悬空 id。
- service entry 缺失：failure，feedback 指明服务不可用。
- payload 类型错误：failure，不尝试兼容旧字段。
- party/warehouse/world 保存失败：恢复受事务覆盖的 runtime owner、GameSession staging 与 active modal context，返回“存档提交失败，操作已回滚”。

## 实现级补充：回归映射

| 行为 | 回归 |
|---|---|
| action 分发 / modal context | `run_game_runtime_settlement_command_handler_regression.cs` |
| 从世界地图进入据点 / visited state | `run_world_map_settlement_entry_regression.cs` |
| forge batch / 装备实例 | `run_settlement_forge_service_regression.cs` |
| research schema | `run_settlement_research_service_schema_regression.cs` |
| shop window payload | `run_settlement_shop_window_schema_regression.cs` |

## 源码级重建清单：据点服务文件与 surface

以下清单用于弥补纯设计文档遗漏：重建时必须逐项恢复这些 owner 文件、公开/内部 typed surface 与职责边界；若实现拆文件，仍要保留等价 API 与行为。

### `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`

- `public partial class GameRuntimeSettlementCommandHandler : RefCounted`
- `private sealed class SettlementActionValidationResult`
- `internal static SettlementActionValidationResult Success(GDictionary serviceEntry = null) =>`
- `internal static SettlementActionValidationResult Failure(string message) =>`
- `internal GDictionary ToDictionary()`
- `private sealed class ContractBoardQuestData`
- `private sealed class SettlementServiceEntryResolution`
- `internal static SettlementServiceEntryResolution Missing() => new(null, false, "");`
- `private sealed class StagecoachDestinationData`
- `internal GDictionary ToDictionary() =>`
- `internal SettlementPersistResult(int partyError, int worldError, int playerError)`
- `internal GDictionary ToDictionary() =>`
- `internal void SetupRuntime(GameRuntimeFacade runtime)`
- `public new void Dispose()`
- `internal void DisposeRuntime()`
- `internal GDictionary GetSettlementWindowData(string settlement_id = "")`
- `internal GDictionary GetShopWindowData()`
- `internal GDictionary GetContractBoardWindowData()`
- `internal GDictionary GetForgeWindowData()`
- `internal GDictionary GetStagecoachWindowData()`
- `internal void OnSettlementWindowClosed()`
- `internal void OnShopWindowClosed()`
- `internal void OnContractBoardWindowClosed()`
- `internal void OnForgeWindowClosed()`
- `internal void OnStagecoachWindowClosed()`
- `internal string ResolveCommandSettlementId()`
- `internal GDictionary RestorePartyResources(float restore_ratio, bool restore_full)`
- `internal GDictionary CommandOk(string message = "")`
- `internal GDictionary CommandError(string message)`
- `internal bool IsBattleActive()`
- `internal void UpdateStatus(string message)`
- `internal string GetActiveSettlementId()`
- `internal void SetActiveSettlementId(string settlement_id)`
- `internal void SetSettlementFeedbackText(string feedback_text)`
- `internal string GetSettlementFeedbackText()`
- `internal GDictionary GetSelectedSettlement()`
- `internal PartyState GetPartyState()`
- `internal int GetPartyGold()`
- `internal GDictionary GetSettlementRecord(string settlement_id)`
- `internal GArray GetAllSettlementRecords()`
- `internal GDictionary GetSettlementState(string settlement_id)`
- `internal WorldMapSettlementStateData GetSettlementStateData(string settlement_id)`
- `internal bool SetActiveSettlementState(string settlement_id, WorldMapSettlementStateData settlement_state)`
- `internal PartyWarehouseService GetPartyWarehouseService()`
- `internal string GetItemDisplayName(StringName item_id)`
- `internal IReadOnlyDictionary<StringName, RecipeDef> GetRecipeDefsTyped()`
- `internal IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped()`
- `internal AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id)`
- `internal string GetMemberDisplayName(StringName member_id)`
- `internal void OpenPartyWarehouseWindow(string entry_label)`
- `internal void SyncPartyStateFromCharacterManagement()`
- `internal int PersistPartyState()`
- `internal int PersistWorldData()`
- `internal int PersistPlayerCoord()`
- `internal WorldMapFogSystem GetFogSystem()`
- `internal bool IsSettlementVisibleToPlayer(GDictionary settlement)`
- `internal string GetPlayerFactionId()`
- `internal void AdvanceWorldTimeBySteps(int delta_steps)`
- `internal void RefreshWorldVisibility()`
- `internal int GetWorldStep()`
- `internal void SetPlayerCoord(Vector2I coord)`
- `internal void SetSelectedCoord(Vector2I coord)`
- `internal void ClearSettlementEntryContext(bool reset_selected = true)`
- `internal RuntimeModalKind GetActiveModalKind()`
- `internal void SetActiveModalKind(RuntimeModalKind modalKind)`
- `internal bool PresentPendingRewardIfReady()`
- `internal void SetActiveShopContext(GDictionary context)`
- `internal void SetActiveContractBoardContext(GDictionary context)`
- `internal void SetActiveForgeContext(GDictionary context)`
- `internal void ClearActiveShopContext()`
- `internal void ClearActiveContractBoardContext()`
- `internal void ClearActiveForgeContext()`
- `internal GDictionary GetActiveShopContext()`
- `internal GDictionary GetActiveContractBoardContext()`
- `internal GDictionary GetActiveForgeContext()`
- `internal void SetActiveStagecoachContext(GDictionary context)`
- `internal void ClearActiveStagecoachContext()`
- `internal GDictionary GetActiveStagecoachContext()`

### `scripts/systems/settlement/SettlementServiceResult.cs`

- `public sealed class SettlementServiceResult`
- `public SettlementServiceResult SetInventoryDelta(GDictionary value)`
- `public SettlementServiceResult SetServiceSideEffects(GDictionary effects)`
- `public GDictionary ToDictionary()`

### `scripts/systems/settlement/SettlementShopService.cs`

- `public partial class SettlementShopService : RefCounted`
- `public new void Dispose()`

### `scripts/systems/settlement/SettlementForgeService.cs`

- `public partial class SettlementForgeService : RefCounted`
- `public new void Dispose()`
- `private sealed class RecipeItemValidationResult`
- `public static RecipeItemValidationResult Success() => new(true, "");`
- `public static RecipeItemValidationResult Failed(string message) => new(false, message);`
- `public bool IsSupportedInteraction(string interaction_script_id)`

### `scripts/systems/settlement/SettlementResearchService.cs`

- `public partial class SettlementResearchService : RefCounted`
- `private sealed class ResearchMemberAvailability`
- `internal GDictionary ToDictionary() =>`
- `public bool IsSupportedInteraction(string interaction_script_id)`

### `scripts/systems/settlement/SettlementServiceMetadata.cs`

- `internal sealed class SettlementServiceMetadata`
- `internal GDictionary ToDictionary()`
