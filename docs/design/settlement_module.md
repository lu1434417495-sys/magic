# 据点服务模块可重建规格说明

更新日期：`2026-06-17`

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
    -> WorldMapDataContext(settlement record/state)
    -> PartyWarehouseService / PartyEquipmentService / PartyItemUseService
    -> CharacterManagementModule / QuestProgressService
    -> WorldMapFogSystem / WorldTimeSystem
    -> GameSession.SetWorldData / SetPartyState / CommitRuntimeState
```

`WorldMapSystem` 只负责打开窗口和把 UI action 转给 proxy；正式服务规则必须在 command handler 或 settlement service 类中。服务结果使用 typed `SettlementServiceResult` / typed payload，避免把 `ToDictionary()` 再回读成业务态。

## 据点数据契约

据点实例来自 `world_data.settlements[]`，核心字段：`settlement_id`、`display_name`、`tier`、`tier_name`、`faction_id`、`origin`、`footprint_size`、`facilities`、`available_services`、`service_npcs`、`settlement_state`。

`available_services[]` 每项至少包含：

| 字段 | 说明 |
|---|---|
| `settlement_id` | 所属据点 id。|
| `facility_id` / `facility_template_id` / `facility_name` | 服务来源设施。|
| `npc_id` / `npc_template_id` / `npc_name` | 服务 NPC。|
| `service_type` | 面向 UI 的服务类型。|
| `action_id` | 正式命令 id，如 `service:warehouse`。|
| `interaction_script_id` | 原始交互脚本 id，用于生成 action id。|

`settlement_state` 至少需要 `visited`、`reputation`、`shop_inventory_seed`、`shop_last_refresh_step`，研究/驿站/商店等服务可扩展字段。写回时必须替换 active world data 中对应 settlement 的 `settlement_state` 并刷新 context lookup。

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
- 商店、铁匠、驿站、任务板共用 `ShopWindow` 风格 modal 时，handler 构建不同 window data context。
- active modal kind 由 runtime 持有；打开服务 modal 时应关闭 settlement action feedback 的冲突状态。
- 窗口关闭只清空 active modal/context，不回滚已提交服务。

## 服务执行事务

服务命令应遵循：

1. 校验 runtime、active settlement、action entry、party/world/session 可用。
2. 构建 typed request，包含 settlement id、service entry、payload、world step、party state。
3. 调用领域 service，得到 typed result。
4. 如果 result 失败：只更新 feedback/status，不写 world/party。
5. 如果 result 成功：写 party、warehouse、equipment、quest、settlement_state、fog/world_data 等受影响 owner。
6. 先更新内存 owner，再调用 `GameSession.SetWorldData` / `SetPartyState` / `FlushGameState` 或 `CommitRuntimeStateInternal`。
7. 持久化失败时返回 failure，并把“服务已执行但持久化失败”明确写入状态。

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

`GameRuntimeSettlementCommandHandler` 重建时至少需要持有或能从 runtime 取到以下 owner：

- `GameRuntimeFacade Runtime`：唯一 runtime 根，用于读取 active map、world step、party state、session、content catalog、modal state。
- `WorldMapDataContext`：读取 settlement record、写 settlement state、取 active world data。
- `PartyWarehouseService` / `PartyEquipmentService` / `PartyItemUseService`：仓库、铁匠、奖励入仓等服务依赖。
- `CharacterManagementModule`：恢复、任务进度、奖励归并、角色摘要。
- `WorldMapFogSystem`：传闻/情报类 reveal 服务。
- active context：当前 settlement id、active feedback text、shop/forge/stagecoach/contract board modal context。

handler 不应缓存可变 `GDictionary` 作为长期真相；可以缓存 UI context，但每次执行服务前必须从 runtime/context 重新解析 active settlement 和服务 entry。

## 实现级补充：window data 构建

据点窗口数据应由 runtime/handler 构建，字段建议固定为：

| 字段 | 说明 |
|---|---|
| `settlement_id` | 当前据点 id。|
| `display_name` | 据点显示名，缺失 fallback “据点”。|
| `tier` / `tier_name` | tier 数值和中文 tier 名。|
| `faction_id` | 阵营。|
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

商店 action 打开 shop modal，window data 至少包含 settlement id、service action、buy entries、sell candidates、price/currency、库存刷新信息。买/卖命令走独立 `CommandShopBuyTyped` / `CommandShopSellTyped`，而不是再次走 generic settlement action。

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
- party/warehouse/world 保存失败：服务已执行时必须明确提示“已执行但持久化失败”；未执行时保持无副作用。

## 实现级补充：回归映射

| 行为 | 回归 |
|---|---|
| action 分发 / modal context | `run_game_runtime_settlement_command_handler_regression.cs` |
| 从世界地图进入据点 / visited state | `run_world_map_settlement_entry_regression.cs` |
| forge batch / 装备实例 | `run_settlement_forge_service_regression.cs` |
| research schema | `run_settlement_research_service_schema_regression.cs` |
| shop window payload | `run_settlement_shop_window_schema_regression.cs` |
