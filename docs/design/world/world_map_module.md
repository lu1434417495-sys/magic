# 世界地图模块可重建规格说明

> 状态：`Current / Implemented`
> 核对日期：`2026-07-29`

更新日期：`2026-07-29`

## 目标与边界

本文记录当前世界地图模块的功能、数据契约、运行时所有权、关键算法与回归入口。目标是在相关代码丢失时，开发者仅凭本文和静态资源即可重新实现出行为一致的世界地图模块。

世界地图模块覆盖：

- 世界预设与 `WorldMapGenerationConfig` 静态资源读取。
- 根世界与挂载子地图的生成、激活、返回与持久化。
- 世界格网、据点 footprint 占用、4 邻接移动与坐标换算。
- 玩家坐标、选中坐标、世界时间步、迷雾可见性与探索状态。
- 据点、世界事件、野外遭遇锚点、世界 NPC 的索引与点击交互。
- 世界场景到 `GameRuntimeFacade` 的命令桥接，以及世界/战斗/窗口模式切换。
- `WorldMapView` 的大地图绘制、迷雾遮罩、图标与点击信号。

不属于本文重建范围但会被调用的模块：

- `GameSession` / save serializer / content catalog。
- 战斗内部规则、AI、HUD 和战斗棋盘渲染。
- 据点服务、仓库、角色管理、任务、奖励结算的领域规则。

## 模块拓扑

```text
LoginScreen / Save
  -> GameSession(active world, generation config, party, content catalog)
  -> scenes/main/world_map.tscn
    -> WorldMapSystem(scene adapter)
      -> GameRuntimeFacade(runtime owner)
        -> WorldMapDataContext(active root/submap world data + lookup indexes)
        -> WorldMapGridSystem(world size + occupancy)
        -> WorldMapFogSystem(faction fog state)
        -> WorldMapSpawnSystem(world/submap generation)
        -> settlement / warehouse / quest / battle sidecar handlers
      -> WorldMapRuntimeProxy(narrow UI command/read bridge + auto render callback)
      -> WorldMapView(pure world render + cell click signals)
      -> settlement/shop/party/submap/battle windows
```

所有正式运行期状态由 `GameRuntimeFacade` 及其领域 sidecar 拥有；`WorldMapSystem` 是 Godot 场景适配器，只负责节点绑定、信号接线、输入转命令、窗口显示和根据 runtime snapshot 重绘。`WorldMapView` 只读取已经注入的 grid/fog/world data，不拥有世界规则。

## 静态资源契约

### WorldMapGenerationConfig

每个世界预设或子地图配置必须是 `WorldMapGenerationConfig` 资源。字段语义：

| 字段 | 类型 | 语义 |
|---|---|---|
| `seed` | `int` | 生成用随机种子。|
| `world_size_in_chunks` | `Vector2I` | 世界 chunk 尺寸，两个分量必须大于 0。|
| `chunk_size` | `Vector2I` | 每个 chunk 的 cell 尺寸，两个分量必须大于 0。|
| `player_start_coord` | `Vector2I` | 默认玩家出生坐标；若生成了玩家起始据点，运行时以据点入口坐标为准。|
| `player_vision_range` | `int` | 玩家视野菱形半径。|
| `procedural_generation_enabled` | `bool` | 是否按数量和间距程序化放置据点。|
| `procedural_*_count` | `int` | 各 tier 程序化目标据点数量。村庄数量最少为 1，其余 tier 可为 0。|
| `*_spacing_cells` | `int` | 各 tier 程序化据点最小曼哈顿间距参考值。|
| `guarantee_starting_wild_encounter` | `bool` | 是否保证起点附近有野外遭遇。|
| `starting_wild_spawn_min_distance` / `max_distance` | `int` | 起始野怪距离范围，min 不得大于 max。|
| `settlement_library` | `Array<Resource>` | 可引用的 `SettlementConfig` 模板。|
| `facility_library` | `Array<Resource>` | 可引用的 `FacilityConfig` 模板。|
| `settlement_distribution` | `Array<Resource>` | 固定据点分布规则。|
| `wild_monster_distribution` | `Array<Resource>` | 野外遭遇生成规则。|
| `mounted_submaps` | `Array<Resource>` | 根世界可挂载的子地图定义。|
| `world_events` | `Array<Resource>` | 世界事件定义，如进入子地图入口。|

派生值：`world_size_cells = world_size_in_chunks * chunk_size`。所有世界坐标均为 cell 坐标，左上角为 `(0, 0)`，合法范围为 `0 <= x < width` 且 `0 <= y < height`。

### 据点与设施资源

- `SettlementConfig`：`settlement_id` 是模板 id；`tier` 取 `VILLAGE=0`、`TOWN=1`、`CITY=2`、`CAPITAL=3`、`WORLD_STRONGHOLD=4`、`METROPOLIS=5`。
- `SettlementDistributionRule.country_id` 是固定据点的国家归属 id；空字符串表示该据点没有国家归属。它与 `faction_id` 是两个独立维度。
- 据点 footprint 由 tier 决定：村 `1x1`，镇/城 `2x2`，主城 `3x3`，世界据点 `4x4`，都会 `5x5`。
- `facility_slots` 定义设施在据点 footprint 内的本地坐标和 slot tag；`guaranteed_facility_ids` 必定尝试放置；`optional_facility_pool` 最多放置 `max_optional_facilities` 个。
- `FacilityConfig.facility_id` 是设施模板 id；`interaction_type` 通过固定表映射为服务 action id，例如 `party_warehouse -> service:warehouse`、`service_contract_board -> service:contract_board`、`service_stagecoach -> service:stagecoach`。
- `FacilityNpcConfig` 可绑定服务 NPC；服务窗口展示和 action dispatch 依赖 `service_type`、`interaction_script_id`、`local_slot_id`。

### 野外遭遇、事件与子地图资源

- `WildSpawnRule` 生成 `EncounterAnchorData`：`region_tag`、`monster_name`、正式 `encounter_profile_id`、`density_per_chunk`、`min_distance_to_settlement`、`vision_range`、可选 `chunk_coords`；据点型生成可另声明 `settlement_encounter_profile_id/display_name`。敌方 roster 由 `BattleEncounterDefinition` 解析，不存入 anchor。
- `WorldEventConfig` 当前正式事件类型只有 `enter_submap`；必须提供 `event_id`、`display_name`、`world_coord`、`target_submap_id`、提示标题和正文。
- `MountedSubmapConfig` 由根世界保存：`submap_id`、`display_name`、`generation_config_path`、`return_hint_text`。子地图第一次进入时延迟生成，并把生成后的 `world_data` 写回根世界的 `mounted_submaps[submap_id]`。

## 持久化 world_data schema

世界生成输出和存档中的世界数据是 `Dictionary`。根世界和每个子地图的 `world_data` 使用同一主体 schema：

根字段名称与 required/optional/array/string 分类由 `WorldRuntimeSaveSchema` 唯一声明，`WorldRuntimeData` 的 canonical 读写和 `SaveSerializer` 的磁盘边界校验共同消费。nested settlement、event、resource node、mounted submap 与 return-stack entry 的字段集合归各自 typed record；serializer 只负责递归顺序与可定位错误，不再复制 record 字段表。当前顶层存档版本为 SaveVersion 18。

| key | 类型 | 说明 |
|---|---|---|
| `map_seed` | `int` / `long` | 实际使用的生成种子。|
| `settlements` | `Array<Dictionary>` | 据点实例列表。|
| `world_npcs` | `Array<Dictionary>` | 世界地图移动/标记 NPC 列表。|
| `encounter_anchors` | `Array<EncounterAnchorData>` | 野外或据点遭遇锚点。|
| `world_events` | `Array<Dictionary>` | 世界事件实例。|
| `mounted_submaps` | `Dictionary` | 仅根世界有意义；子地图定义与生成态。|
| `active_submap_id` | `String` | 仅根世界持有，空字符串表示当前在根世界。|
| `submap_return_stack` | `Array<Dictionary>` | 子地图返回栈。|
| `world_step` | `int` | 世界时间步，移动或命令推进时递增。|
| `next_equipment_instance_serial` | `int` | 装备实例 id 分配器状态。|
| `player_start_coord` | `Vector2I` | 该地图默认出生点。|
| `player_start_settlement_id` | `String` | 起始据点 id，可能为空。|
| `player_start_settlement_name` | `String` | 起始据点显示名，可能为空。|
| `fog_states` | `Dictionary` | `WorldMapFogSystem` 导出的迷雾持久态，可缺省。|

### settlement 实例

每个据点字典必须精确包含：`entity_id`、`template_id`、`settlement_id`、`display_name`、`tier`、`tier_name`、`faction_id`、`country_id`、`origin`、`footprint_size`、`facilities`、`is_player_start`、`settlement_state`、`available_services`、`service_npcs`。

`country_id` 是据点记录级的国家归属键；空字符串精确表示“无国家归属”。它不属于 `settlement_state`，也不等价于决定迷雾/敌对关系的 `faction_id`。运行时和内容生成不得从 `faction_id`、`display_name`、据点名称池或名称前缀推导 `country_id`。国家声望由 `PartyState.country_reputations` 按相同 `country_id` 独立持有。

`settlement_state` 由 `WorldMapSettlementStateData` 完整持有，当前 schema 精确为 `visited`、`reputation`、`active_conditions`、`cooldowns`、`shop_states`。每个 `shop_states[shop_id]` 独立持有自身的 `seed`、`last_refresh_step` 和库存；据点层没有共享的商店随机状态，刷新一个商店不得改动其他商店。服务不得通过未知字段临时扩展该 payload；新增持久字段必须同步 typed owner、生成默认值、严格校验与 save version。单字段修改通过完整聚合的 `With*` API 写回 `WorldRuntimeData`，不能用局部投影替换整个状态。`world_step` 与服务反馈分别属于 world owner 和 modal/context，不进入 `settlement_state`。

### EncounterAnchorData

遭遇锚点是 `RefCounted` typed object，序列化字段必须精确等于：

```text
entity_id, display_name, world_coord, faction_id,
region_tag, vision_range, is_cleared, encounter_kind,
encounter_profile_id, growth_stage, suppressed_until_step
```

合法 `encounter_kind`：`single`、`settlement`。`entity_id`、`faction_id`、`encounter_kind` 非空；`vision_range`、`growth_stage`、`suppressed_until_step` 必须为非负整数；`display_name` 必须为非空字符串。

### fog_states

`WorldMapFogSystem.BuildPersistentStatePlain()` 生成：

```text
{
  version: 2,
  factions: {
    faction_id: {
      explored: Array<Vector2I>,
      revealed: Array<Vector2I>
    }
  }
}
```

`explored`、`revealed` 只允许 Godot 原生 `Vector2I`，不接受 `"x,y"` 字符串或 `{x,y}` Dictionary。读取时严格校验 version、精确字段、factions 类型、faction id 与数组元素 Variant 类型；失败返回 false 并保持系统为空状态，不提供旧 schema 迁移或 fallback。该破坏性变更对应顶层 save version 16。

## 世界生成算法

`WorldMapSpawnSystem.BuildWorldTyped(generation_config, grid_system)` 的行为应等价于：

1. 读取并规范化配置，调用 `TrueRandomSeedService.GenerateSeed()` 生成实际 `map_seed` 并设置 RNG；当前实现不直接使用配置里的 `seed` 作为最终 `map_seed`。若 config 开启 `inject_default_main_world_content`，把默认 settlement bundle、wild spawn bundle、名称池合并进当前配置。
2. `gridSystem.Setup(config.world_size_in_chunks, config.chunk_size)`；清空所有占用。
3. 生成据点：
   - 先应用 `settlement_distribution` 中的固定规则。按 `preferred_origin` 放置对应模板，失败则跳过或尝试后续规则。
   - 若 `procedural_generation_enabled`，按 tier 目标数量继续放置；候选坐标必须在世界内、footprint 未占用，并满足对应 tier spacing。
   - 每个成功据点都调用 `gridSystem.RegisterFootprint(entity_id, origin, footprint_size)`。
   - 据点 id 和 entity id 必须稳定、可存档；固定分布由资源顺序决定，程序化分布由本次 `map_seed` 与配置共同决定。
   - 起始据点优先取包含/邻近 `player_start_coord` 的据点；若没有则取第一个玩家起始据点或配置坐标。
4. 为据点填充设施：
   - guaranteed facility 按 id 从 facility library 解析。
   - optional facility 按 slot tag、tier、数量上限筛选。
   - 设施世界坐标 = settlement origin + facility local coord。
   - 从设施和 NPC 构建 `available_services`，action id 使用固定映射表。
5. 生成世界 NPC：根据服务 NPC 或专门规则投影成 `world_npcs`，其坐标必须合法且不破坏据点 footprint。
6. 生成野外遭遇：
   - 对每条 `WildSpawnRule`，在指定 chunk 或全世界 chunk 中按 `density_per_chunk` 生成候选。
   - 候选必须在世界内、不是据点占用格、距离所有据点至少 `min_distance_to_settlement` 对应的 blocked-cell 半径；当前实现按欧氏距离平方过滤。
   - 若 `guarantee_starting_wild_encounter`，在起点 min/max 距离环内补一个合法遭遇。
   - 输出 `EncounterAnchorData`，`encounter_kind=single`、`is_cleared=false`、`growth_stage=0`、`suppressed_until_step=0`。
7. 生成世界事件：复制 `WorldEventConfig` 为实例字典；`enter_submap` 事件必须引用存在的 mounted submap。
8. 生成 mounted submap entries：每个 entry 初始 `is_generated=false`，保留 config path、display name、return hint；不立即生成子地图 world_data。
9. 输出 `WorldBuildData.ToDictionary()`，并包含 `world_step=0`、空 `active_submap_id`、空 `submap_return_stack`。

## Grid 系统

`WorldMapGridSystem` 是纯 C# 服务，不继承 Godot Node。

必需 API：

- `Setup(world_size_in_chunks, chunk_size)`：chunk 分量最小归一为 1，world chunk 分量最小归一为 0，最终 `world_size_cells = normalizedWorldSize * normalizedChunkSize`；清空占用和 footprint。
- `GetWorldSizeCells()`、`GetChunkSize()`。
- `IsCellInsideWorld(coord)`：边界闭开区间判断。
- `IsCellWalkable(coord)`：当前只等价于 `IsCellInsideWorld`，据点 footprint 不阻挡移动。
- `GetChunkCoord(coord)`：整数除法；chunk size 非法时返回 `Vector2I.Zero`。
- `GetCell(coord)`：越界返回 null；合法时返回 `WorldMapCellData(coord, chunkCoord)`，若该格有 footprint，填 `occupant_id` 和 `footprint_root_id`。
- `CanPlaceFootprint(origin, size)`：size 任一分量 <= 0 返回 false；footprint 内所有格必须在世界内且未占用。
- `RegisterFootprint(entity_id, origin, size)`：entity id 为空返回 false；若该 entity 已有 footprint，先清除；新 footprint 放置失败时必须恢复旧 footprint。
- `ClearFootprint(entity_id)`：只清除 root id 匹配的占用格。
- `GetOccupantRoot(coord)`：越界或空格返回空字符串。
- `GetNeighbors4(coord)`：按左、右、上、下返回世界内 4 邻接格。

## Fog 系统

迷雾有三个显示态：`Unexplored=0`、`Explored=1`、`Visible=2`。每个 faction 拥有独立状态：

- `visible` 是每次视野重建得到的临时集合。
- `explored` 是曾经可见过的持久集合。
- `revealed` 是服务或事件付费揭示得到的持久集合。

核心行为：

- `Setup(world_size_cells, persistent_state?)` 设置世界尺寸，清空状态；若传入 persistent state，则严格读取。
- `RebuildVisibilityForFaction(factionId, sources)`：先清空该 faction visible；对每个同 faction source，以曼哈顿距离 `<= range` 的菱形标 visible。实现必须同时把 visible 格标记为 explored，否则玩家离开后这些格应保持 explored 显示。
- `RevealDiamond(center, range, factionId)`：把合法菱形格加入 explored 与 revealed，返回本次遍历到的坐标列表。
- `MarkExplored(coord, factionId)`：合法格加入 explored。
- `IsVisible`、`IsExplored`、`GetFogState`：visible 优先于 explored/revealed；越界坐标不应变成 visible。
- `ExportPersistentState`：按 faction id 排序导出，确保稳定存档 diff。

## Runtime 初始化与同步

`WorldMapSystem._Ready()` 必须按以下顺序初始化：

1. 从 autoload 取得 `GameSession`，要求有 active world 和 generation config；缺失则记录错误并停止。
2. 绑定 `world_map.tscn` 中所有 UI 节点。
3. 创建 `GameRuntimeFacade` 并 `Setup(gameSession)`。
4. 创建 `WorldMapRuntimeProxy`，绑定 runtime 与当前 `WorldMapSystem`，所有 UI 命令都经 proxy。
5. 将 proxy/runtime/session 注入 battle panel；将 content catalog 的 typed skill/profession/item/achievement 等注入 party window。
6. 连接世界 view、窗口、battle panel、log dock 等信号。
7. `WorldMapView.Configure(grid, fog, worldData, playerCoord, selectedCoord, playerVisible, factionId)`。
8. `RenderFromRuntime(true, {})` 完成初始绘制。

`GameRuntimeFacade.Setup(gameSession)` 必须：

1. 保存 session/root/content catalog；从 content catalog 读取 typed 内容，不从 UI 字典回读业务态。
2. 绑定 root `world_data` 到 `WorldMapDataContext`。
3. 初始化野外遭遇 roster builder、角色管理、仓库、物品使用、装备、战斗 runtime、snapshot builder、命令 logger、settlement/warehouse/party/reward/quest/battle sidecars。
4. 调用 active world 同步：
   - `WorldMapDataContext.SyncActiveWorldContext(rootConfig, grid, playerCoord, selectedCoord)` 解析根世界或 active submap，重建事件/据点/NPC/遭遇 lookup，注册据点 footprint。
   - `fog.Setup(activeWorldSize, activeWorldFogState)`。
   - 用玩家视野 source 重建玩家 faction visibility。
   - 若玩家或选中坐标越界，归一到 active map 的 player coord 或起点。
   - 验证 grid/fog/world size 一致。

## 世界命令行为

所有场景交互经 `WorldMapRuntimeProxy.Command*` 转发到 `GameRuntimeFacade`，命令返回统一字典：

```text
{ ok: bool, message: string, code: int, battle_refresh_mode?: string }
```

proxy 在命令后负责调用 render target 的 `RenderFromRuntime(refreshWorld, result)`。runtime 不应直接操作 UI 节点。`RunRuntimeCommand` 在 runtime 缺失时返回 code=`RuntimeUnavailable`、message=`运行时尚未初始化。`；普通 runtime command 统一用 `RenderFromRuntime(true, result.ToDictionary())` 刷新，battle direct command 会把 `BattleRefreshMode.None` 提升为 `Full` 后刷新。

### 移动与选择

- 左键世界 cell：若非战斗/非 modal，更新 selected coord；若点击可交互对象，按对象类型打开据点窗口、子地图提示、战斗确认等 modal。
- 右键世界 cell：通常尝试移动或取消/返回，具体命令仍由 runtime 判断状态。
- 键盘移动：世界模式下方向键/WASD 形成 held key 队列，按 `0.5s` repeat interval 重复触发移动。
- 移动合法性：目标必须在 grid 内且 `IsCellWalkable`；当前实现据点 footprint 不阻挡移动。
- 成功移动后：更新 player coord 与 selected coord；递增 `world_step`；刷新玩家视野；持久化 player coord、world data 和 fog state；检查当前位置的据点、事件、遭遇锚点并触发相应 prompt 或战斗生成请求。

### 据点

- `WorldMapDataContext` 通过 settlement footprint 建立 `coord -> settlement` lookup，因此点击据点任一 footprint 格都应得到同一 settlement。
- 进入据点后 active modal 为 settlement window；首次访问要把 settlement state `visited=true` 写回 active world data。
- 据点 action 只分发 action id 与 typed 上下文给 settlement command handler；休息、商店、铁匠、驿站、任务板、仓库等规则不写在 `WorldMapSystem`。

### 野外遭遇与战斗切换

- 世界坐标命中未清除且未 suppressed 的 `EncounterAnchorData` 时，runtime 构造 pending battle start prompt。
- 确认后根据 `encounter_profile_id` 解析正式 `BattleEncounterDefinition`，再取得 roster、objective、world resolution，并结合当前 active map 信息和 player party 构造战斗请求；缺少正式 encounter/objective 时立即失败。
- 战斗 active 时，`RenderFromRuntime` 隐藏 world map/background/bottom action bar，显示 `BattleMapPanel`；世界输入暂停。
- 战斗结束写回时，清除或更新对应 encounter anchor，提交 loot/reward/progression，恢复世界地图并刷新 world view。

### 子地图

- 世界事件 `enter_submap` 被点击/触发后显示 `SubmapEntryWindow`。
- 确认进入：若目标 submap 未生成，`WorldMapDataContext.EnsureSubmapGenerated` 加载其 generation config 并用 `WorldMapSpawnSystem` 生成 `world_data`，写入根世界 `mounted_submaps[submapId]`，设置 `is_generated=true` 和子地图 `player_coord`。
- 进入时把当前 `{ map_id, player_coord, selected_coord }` 推入 root `submap_return_stack`，设置 root `active_submap_id=submapId`，然后重新同步 active world context。
- 子地图内返回：保存当前子地图 player coord 到 mounted entry，弹出 return stack，恢复 root `active_submap_id`，返回保存的坐标并重建 grid/fog/lookups。
- root/submap 切换应作为一次事务保存，避免只写坐标或只写 active_submap_id 的半状态。

## WorldMapView 绘制规格

`WorldMapView` 是 `Control`，导出项包括 cell size、padding、背景纹理、玩家纹理、村庄纹理、fog darkness、选择描边和各类 marker 颜色。

绘制顺序：

1. 根据 `_playerCoord` 和控件尺寸计算 camera origin，使玩家大致居中；可见 rect 要额外包含 `viewport_padding_cells`。
2. `_draw_cells`：
   - unexplored：纯黑矩形。
   - explored/visible：绘制背景纹理或 fallback 深色底；explored 再叠加黑色透明遮罩。
   - 每格绘制细网格线。
3. `_draw_settlements`：遍历 `world_data.settlements`；只绘制 visible/explored 的 footprint 格。颜色按 tier：村绿、镇蓝、城棕、主城紫、世界据点红、都会金、未知灰。村庄有纹理时使用纹理，否则绘制色块；标签只在 origin 非 unexplored 时绘制。
4. `_draw_mobile_entities`：只绘制 visible 的世界事件、遭遇锚点、world NPC；遭遇锚点用红色外圈和深色内圈；事件用金色标记；NPC 用蓝白人形标记。
5. `_draw_player`：仅当 player visible 且玩家格可见时绘制；有纹理用纹理，无纹理用圆形/三角 fallback。
6. `_draw_selection`：合法 selected coord 上绘制黄色描边。

输入：鼠标左键 emit `cell_clicked(coord)`，右键 emit `cell_right_clicked(coord)`；越界或 grid 缺失时忽略。


## 实现级补充：数据对象与字段投影

### WorldBuildData 投影

`WorldMapSpawnSystem.WorldBuildData` 是生成期 typed 聚合，最终通过 `ToDictionary()` 投影为存档 payload。重建时不要让 UI 直接依赖 `WorldBuildData` 实例；UI 和 save 只看投影字典。投影规则必须满足：

- `Settlements` 投影到 `settlements: Array<Dictionary>`，空列表也写空数组。
- `WorldNpcs` 投影到 `world_npcs: Array<Dictionary>`。
- `EncounterAnchors` 直接放入 `encounter_anchors: Array<EncounterAnchorData>`，不要提前转为普通字典，否则现有视图会按 GodotObject 读取失败。
- `WorldEvents` 投影到 `world_events: Array<Dictionary>`。
- `MountedSubmaps` 投影到 `mounted_submaps: Dictionary<submap_id, Dictionary>`；submap id 为空的 entry 跳过。
- 每次新建 world data 都写入空 `active_submap_id`、空 `submap_return_stack`、`world_step=0`、`next_equipment_instance_serial=1`。

### SettlementInstanceData 字段

重建据点实例时字段必须按以下来源产生：

| 字段 | 来源 / 规则 |
|---|---|
| `entity_id` | `"settlement_" + settlement_id`。|
| `template_id` | `SettlementConfig.GetTemplateId()` 去空白后的值。|
| `settlement_id` | 始终为 `template_id_XX`，序号从 `01` 开始；同模板多实例必须不冲突。|
| `display_name` | 模板名、默认名称池或 fallback；必须非空可显示。|
| `tier` / `tier_name` | 来自 `SettlementConfig.tier` / `GetTierName()`。|
| `faction_id` | 固定分布用规则 faction；程序化起始村为 `player`，其他默认为 `neutral`。|
| `country_id` | 固定分布使用规则中的国家 id；当前程序化据点写入空字符串，即无国家归属。不得从 faction、显示名或名称池推导。|
| `origin` | footprint 左上角 cell。|
| `footprint_size` | `SettlementConfig.GetFootprintSize()`。|
| `is_player_start` | 程序化起始村 true；固定分布通常 false，除非生成逻辑显式指定。|
| `settlement_state` | 起始状态字典；起始据点 `visited=true`，非起始据点 `visited=false`，并写入 reputation、shop seed、last refresh step。|
| `facilities` | 放置成功的设施实例数组。|
| `available_services` | 从设施交互与服务 NPC 派生。|
| `service_npcs` | 从各 facility 的 bound service NPC 汇总。|

### FacilityInstanceData 与 service 投影

设施投影字段必须包含 `template_id`、`facility_id`、`display_name`、`category`、`interaction_type`、`slot_id`、`slot_tag`、`local_coord`、`world_coord`、`settlement_id`、`service_npcs`。

放置规则：

1. 每个 facility 只能占用一个 slot；`usedSlotIds` 防止重复占位。
2. slot 必须在 settlement footprint 内；`world_coord = settlement_origin + local_coord`。
3. `FacilityConfig.min_settlement_tier` 大于 settlement tier 时不能放置。
4. `allowed_slot_tags` 非空时，slot tag 必须命中。
5. guaranteed facilities 先放置；optional pool 在剩余 slot 中按数量上限放置。
6. optional facility 按 `WeightedFacilityEntry.weight` 加权随机抽取；权重总和 <= 0 时停止抽取，成功放置后从 optional pool 移除同 facility id，避免重复抽中。
7. 服务 action id 优先由 NPC 的 `interaction_script_id` 查固定映射；未命中时把 `service_type` 转 snake_case 并生成 `service:<service_type>`，service_type 为空时使用 `service:service`。
8. 如果所有设施/NPC 都没有 `party_warehouse` 交互，生成器必须追加一个 fallback 服务：facility id 为 `<settlement_id>__settlement_service_desk`，NPC id 为 `<settlement_id>__settlement_quartermaster`，action id 为 `service:warehouse`。

### WorldEventData 投影

世界事件实例至少包含：`event_id`、`display_name`、`world_coord`、`event_type`、`target_submap_id`、`is_discovered`、`discovery_condition_id`、`prompt_title`、`prompt_text`。当前发现条件只有 `always_true` 等价行为；同步 active world 时会刷新发现状态，只有 discovered 事件会进入坐标 lookup 和渲染。

### MountedSubmapData 投影

mounted submap entry 必须包含：

- `submap_id`：非空稳定 id。
- `display_name`：展示名；为空时 fallback 到 id。
- `generation_config_path`：Godot 资源路径；第一次进入时用 `GD.Load<Resource>` 读取，类型必须是 `WorldMapGenerationConfig`。
- `return_hint_text`：子地图内提示文本，默认“点击任意地点返回原位置。”。
- `is_generated`：初始 false，生成后 true。
- `world_data`：未生成时为空字典，生成后为子地图 world data。
- `player_coord`：生成后写子地图出生点；每次离开子地图前更新为当前玩家坐标。

## 实现级补充：生成细节

### Library 解析

`BuildLibraries()` 每次生成前必须清空以下缓存：facility id map、settlement id map、默认 bundle、默认 wild spawn bundle、各 tier 名称池、resolved facility/settlement/wild spawn 列表。解析顺序：

1. 加载默认 main world settlement bundle、wild spawn bundle 和名称池资源。
2. 如果 `inject_default_main_world_content=true`，把默认 bundle 中的 library/distribution 附加到当前 config 有效列表。
3. 把 `generation_config.facility_library` 与默认 facility library 合并为 `_resolvedFacilityLibrary`。
4. 把 `generation_config.settlement_library` 与默认 settlement library 合并为 `_resolvedSettlementLibrary`。
5. 把 `generation_config.wild_monster_distribution` 与默认 wild spawn bundle 合并为 `_resolvedWildSpawnRules`。
6. 对 facility 和 settlement 建 `template_id -> config` 字典；空 id 资源跳过，重复 id 后写入者覆盖先写入者。

### 固定据点生成

固定生成只消费 `settlement_distribution`：

1. 遍历每个 `SettlementDistributionRule` 资源。
2. 读取 `settlement_id` 并从 settlement library 找模板；找不到则跳过。
3. 使用 `preferred_origin`、规则 `faction_id` 与规则 `country_id` 创建实例；空 `country_id` 保持为空，不做推导。
4. 若 `gridSystem.CanPlaceFootprint(origin, footprint)` 失败，记录错误并跳过该实例。
5. 成功后调用 `gridSystem.RegisterFootprint(entityId, origin, footprint)`，再生成设施和服务。

固定分布不自动补起始村；`player_start_coord` 会在没有起始据点时作为玩家起点 fallback。

### 程序化据点生成

程序化生成必须先尝试创建玩家起始村：

1. 按 tier 分组 settlement template。
2. 取 village tier 的第一个模板作为玩家村。
3. 计算 centered origin：把 footprint 放在世界中心附近，同时夹在合法范围内。
4. 以 faction `player` 和 `is_player_start=true` 创建该村。
5. 然后按 tier 顺序 `METROPOLIS -> WORLD_STRONGHOLD -> CAPITAL -> CITY -> TOWN -> VILLAGE` 放置剩余据点。
6. 村庄 target count 如果已经放了玩家村，需要减 1。
7. 每个 tier 内模板按 `tierIndex % tierTemplates.Count` 轮转选择。
8. `FindProceduralOrigin` 最多采样 192 次；候选必须可放置、在世界内、与已有 settlement 中心点距离满足 `max(当前 tier spacing, 已有 settlement tier spacing)`。失败时记录 warning 并继续，不整体失败。

### 世界 NPC 生成

每个据点会尝试生成一个世界 NPC，用于地图提示/信息查看：

1. 取据点 footprint 的右下角 `origin + footprint_size - Vector2I.One`。
2. 按候选方向 `Right, Down, Left, Up, (1,1)` 找第一个世界内且 `grid.GetOccupantRoot(candidate)==""` 的空格。
3. 找不到空格则该据点不生成 NPC。
4. NPC 名称按 `巡路信使 / 驿站商人 / 边地向导 / 地图学者 / 补给联络员` 轮转。
5. NPC 字段：`entity_id=world_npc_N`、`kind=service_hint`、`faction_id=settlement.faction_id`、`vision_range=1`、`coord=spawnCoord`。

### 野外遭遇生成

野外遭遇生成必须构造一个快速拒绝器：

- 收集所有 settlement footprint cell。
- 对每个 `min_distance_to_settlement` 缓存 blocked cells。
- blocked cells 使用欧氏距离平方 `< minDistance^2` 的区域，而不是曼哈顿距离；这点要与当前实现一致。
- 候选还必须在世界内、未被 settlement footprint 占用、未与已选 encounter 重叠。

固定生成与程序化生成对 `WildSpawnRule.chunk_coords` 的语义不同，重建时必须区分：

- 固定世界（`procedural_generation_enabled=false`）：只遍历规则里的 `chunk_coords`；如果某条规则没有 chunk 坐标，该规则不会生成普通野怪。
- 程序化世界：遍历全部 world chunks，并按 chunk 的 Y 位置选择 procedural wild spawn rule；每个 chunk 先用 `procedural_wild_spawn_chunk_chance_denominator` 做概率门控，命中后才尝试生成 `density_per_chunk` 个锚点。
- `PickMonsterCoordForChunk` 从该 chunk 的合法候选列表中按 `PosMod(offsetSeed * 3 + chunkX + chunkY, candidates.Count)` 选点；固定世界 offsetSeed 是本 chunk 内 offset，程序化世界 offsetSeed 是 chunkSeed + offset。

锚点字段：

- `entity_id` 应稳定且唯一，建议包含 region/chunk/index。
- `display_name = monster_name`，空时 fallback “野怪”。
- `world_coord` 为候选 cell。
- `faction_id = hostile`。
- `encounter_profile_id` 从规则复制；roster 不复制到 anchor，由正式 battle encounter 内容解析。
- `region_tag` 从规则复制。
- `vision_range` 从规则复制，最小 0。
- `encounter_kind = single`。
- `growth_stage = 0`，`suppressed_until_step = 0`。

起始遭遇保证逻辑只在 `guarantee_starting_wild_encounter=true` 时启用；它应在玩家起点周围 `[min,max]` 曼哈顿距离环内寻找合法 cell，避免落在 settlement blocked cells 或已有 encounter 上；如果该范围内已经有 encounter，则不重复补。

settlement encounter 也属于当前生成行为：若规则显式声明 `settlement_encounter_profile_id/display_name` 且尚不存在 `encounter_kind=settlement` 的锚点，则生成 `wild_settlement_N`，复制该正式 encounter id/display name，并令 `vision_range=max(rule.vision_range, 2)`。运行时不再按 `wolf_pack` 或其他 roster/template id 硬编码据点内容。

## 实现级补充：WorldMapDataContext

`WorldMapDataContext` 是 active world 的读写门面，必须维持以下不变量：

- `root_world_data` 永远指向 GameSession 当前根世界字典。
- `active_world_data` 指向根世界或 active submap 的 `world_data`。
- `active_map_id == ""` 表示根世界；非空表示 active submap。
- `active_generation_config` 必须与 active world data 匹配。
- coord lookup 在每次 active world 切换、world event discovery 改变、settlement state 改变、encounter anchor 删除后重建。

### SyncActiveWorldContext

同步流程必须精确执行：

1. 从 `root_world_data.active_submap_id` 读取 active id；缺字段时为空。
2. 若 active id 非空但 `mounted_submaps[active_id]` 缺失，则把 active id 清空并写回 root。
3. 解析 `active_world_data`：root 模式为 root；submap 模式为 mounted entry 的 `world_data`，如果缺失则 fallback root。
4. 解析 `active_generation_config`：root 模式用传入 root config；submap 模式从 mounted entry 的 config path 加载并缓存。
5. 设置 `active_map_display_name`：root 为“大地图”，submap 用 entry display name 或 id。
6. 用 active config 调 `gridSystem.Setup(world_size_in_chunks, chunk_size)`。
7. 刷新 world event discovery。
8. 重建 settlement/NPC/encounter/world event lookups。
9. 注册 settlement footprints 到 grid；只有 `CanPlaceFootprint` 成功才注册，避免坏存档污染 grid。
10. 如果传入 player coord 越界，使用 active map player coord fallback；如果 selected coord 越界，则设为 player coord。
11. 返回规范化后的 player/selected coord。

### Lookup 规则

- settlement lookup：每个 footprint cell 都映射到同一 settlement record；后出现的 settlement 会覆盖同坐标旧值，但正常数据不应重叠。
- settlement by id：key 是 `settlement_id`。
- world NPC lookup：key 是 `coord`，只登记 `WorldMapNpcData.Exists=true` 的 NPC。
- encounter lookup：key 是 `EncounterAnchorData.world_coord`，默认包含 cleared anchor；调用方决定是否过滤。
- world event lookup：只登记 discovered event。

### 子地图事务

进入子地图：

1. 校验 submap id 非空。
2. 调 `EnsureSubmapGenerated`；失败返回中文错误消息，不改变 active_submap_id。
3. 读取 mounted entry；缺失返回错误。
4. 把 `{ map_id: sourceMapId, coord: sourceCoord }` push 到 root `submap_return_stack`。
5. 设置 root `active_submap_id=submapId`。
6. 目标坐标优先 mounted entry `player_coord`，其次目标 `world_data.player_start_coord`，最后 `Vector2I.Zero`。
7. 返回 success，包含目标坐标和目标显示名。

返回子地图：

1. 若当前不在子地图，失败。
2. 将当前玩家坐标写入 active submap mounted entry 的 `player_coord`。
3. 读取 root return stack；空则失败。
4. 弹出最后一个 return entry。
5. 设置 root `active_submap_id = returnEntry.map_id`；空表示回主世界。
6. 返回目标 map id 与坐标。

调用方收到成功后必须立即 `_sync_active_world_context()`、刷新 fog、保存 session world data/player coord，并在 player/world 保存成功后调用 `CommitRuntimeStateInternal("submap_entry")` 或 `CommitRuntimeStateInternal("submap_return")`；任何一步失败都返回 command failure 并写“已进入/返回但世界状态持久化失败”类状态，避免 root 和 submap 状态半写。

## 实现级补充：Runtime 命令与状态机

### Modal 与 battle gate

所有 world 命令都必须先检查：

- runtime/generation config 是否存在。
- `IsBattleActive()`：战斗中禁止世界移动、选择、打开据点、查看 NPC。
- `IsModalWindowOpenInternal()`：窗口打开时禁止打开另一个世界窗口或移动。

失败返回 `RuntimeCommandResult.Failure(message)`，不抛异常给 UI。

### Command log 边界

`GameRuntimeCommandLogger` 拥有 command scope、before/after runtime context、battle batch context 合并和 pending command battle batches。它只弱借 `IGameRuntimeCommandLogPort`：facade port 在同步调用时把 session/world/modal/battle/selection 与单位资源投影成 detached `CommandLogRuntimeSnapshot`、`CommandLogBattleSnapshot` 和 `CommandLogBattleUnitSnapshot`，logger 不读取 live `GameRuntimeFacade`、`GameSession`、`WorldMapDataContext`、`BattleState` 或 `BattleUnitState`。

battle command 开始时清空 logger-owned pending batches；执行期间每个 `BattleEventBatch` 立即冻结为 plain context；结束时按原顺序写入 `battle_batches`、最后一个 `battle_batch` 和按 unit id 后写覆盖的 `battle_changed_units`，然后清空当前 scope 并恢复上一层 scope。日志 schema 与 session sink 不变，Godot Dictionary/Array 只在同步 normalization、JSON projection 和既有 Godot-facing返回值中短暂存在。

### CommandWorldMoveTyped

输入：`direction: Vector2I`、`count: int`。

行为：

1. direction 不能为 zero。
2. count 归一为 `[1, 256]`。
3. 循环执行 `_move_player(direction)`。
4. 每步后如果战斗或 modal 打开，停止剩余移动。
5. 返回 ok；单步失败的状态消息由 `_move_player` 写入 `_current_status_message`。

`_move_player` 的等价实现：

- 目标 = `_player_coord + direction`。
- 若目标不可 walkable，状态写“不能移动到该位置”类消息并返回。
- 普通移动成功时更新 `_player_coord` 与 `_selected_coord`；进入新据点时只更新 `_selected_coord=targetCoord` 并保留 source/target entry context，等待据点入口处理。
- 成功普通移动或进入新据点都会增加 active world `world_step`；越界移动不推进。推进世界时间时必须调用 `WorldTimeSystem.AdvanceWorldStep`，随后让 `WildEncounterGrowthSystem.ApplyStepAdvance` 更新 active encounter anchors；如果跨过天数且 character management 可用，还要应用 daily practice growth 并持久化 party state。
- 调 `_RefreshFog()`，该方法会基于 leader member id 和 active config `player_vision_range` 重建 visibility 并保存 fog state。
- 持久化 player coord 和 world data。
- 检查目标格：先处理“进入新据点”（打开据点窗口，不更新 `_player_coord` 到据点内，保留 settlement entry source/target context），普通移动后再刷新 world event discovery、fog，然后依次检查 triggerable world event 与 encounter anchor；触发 modal/battle prompt 后停止连续移动。

### CommandWorldSelectTyped

- 拒绝战斗和 modal。
- coord 必须 `grid.IsCellWalkable`，否则返回“超出当前世界范围”。
- 成功仅更新 `_selected_coord` 和 status，不移动玩家、不推进 world_step、不刷新 fog。

### 左键 / 右键行为

`_on_world_map_cell_clicked(coord)`：

1. battle 或 modal 时 no-op。
2. 如果当前在 submap，左键任意 cell 尝试 `ReturnFromActiveSubmapTyped()`；失败时显示错误。这是当前子地图返回交互，不是选择格子。
3. root 模式下设置 `_selected_coord=coord`。
4. 如果 coord 当前 visible 且 `_try_open_settlement_at(coord)` 成功，则打开据点并结束。
5. 否则状态写“已选中格子 (x,y)”。

`_on_world_map_cell_right_clicked(coord)`：

1. battle 或 modal 时 no-op。
2. coord 必须 visible，否则写“该格当前不在视野中。”。
3. 当前右键只尝试 `_try_open_character_info_at_world_coord(coord)`；成功则打开人物信息，失败写“当前格没有可查看人物。”。右键不移动、不触发事件、不打开据点。

### 据点打开

`CommandOpenSettlementTyped(coord)`：

- coord 为 `(-1,-1)` 时使用 selected coord。
- 调 `_try_open_settlement_at(targetCoord)`。
- `_try_open_settlement_at` 应通过 `WorldMapDataContext.GetSettlementAt(coord)` 读取 footprint lookup。
- 找不到 settlement 时设置失败状态。
- 找到后设置 active settlement id，必要时 `MarkSettlementVisited`，active modal kind 切为 settlement，构建 settlement window data。

### 世界事件与 submap prompt

当玩家普通移动到 discovered 且 triggerable 的 `enter_submap` event 坐标：

- runtime 设置 `GameRuntimePendingSubmapPrompt`，其中包含 target submap id、title、text、source coord、source map id。
- `WorldMapSystem.RenderFromRuntime` 根据 active modal id 打开 `SubmapEntryWindow`。
- confirm 调 `CommandConfirmSubmapEntryTyped`；cancel 只关闭 pending prompt，不改变 world data。
- 左键点击事件图标本身当前不会直接打开事件 prompt；事件 prompt 来自移动进入该格后的 runtime 检查。

### 战斗 prompt 与 encounter anchor

- `EncounterAnchorData.is_cleared=true` 的普通 encounter 不应触发战斗。
- `suppressed_until_step > current world_step` 的 settlement encounter 不应触发战斗。
- 命中 encounter anchor 后先设置 battle save lock 并保存 player coord/world data，再开始 battle generation；如果战斗未能进入 active/pending 状态，必须释放 battle save lock 并 flush game state。
- battle generation 开始时 active modal kind 先进入 `BattleLoading`；生成成功后进入 `BattleStartConfirm`，prompt 标题为“开始战斗”，描述提示确认后 TU 按整数 tick 推进，取消按钮不可见且 shade 不可 dismiss。
- 确认战斗开始时清空 pending prompt，active modal kind 归 `None`，battle modal state 归 `None`，并把 timeline frozen 置 false；状态文本为“战斗开始，TU 现在按每秒 5 点推进。”。
- 战斗完成后按 `BattleEncounterWorldResolutionDefinition` 为 `PlayerSuccess / PlayerFailure / Draw` 分别选择 `Preserve / Clear / Suppress`。`Clear` 删除 anchor；`Suppress` 交给 `WildEncounterGrowthSystem.ApplyBattleSuppression` 更新 growth/suppression；`Preserve` 不改 anchor。行为不再由 encounter kind 或 winner 字符串隐式推断。

## 实现级补充：WorldMapSystem 场景适配

### Ready / ExitTree

`_Ready()` 失败条件只记录错误，不创建半初始化 runtime：缺 `GameSession`、无 active world、无 generation config 都应停止。成功路径要保证：

- `battle_map_panel.HideBattle()` 在初始世界模式调用。
- `runtime_log_dock.ClearLogs()` 清空旧日志。
- battle loading overlay 初始化为隐藏且 0%。
- bottom action bar 在非战斗时可见；party button 在 battle 或 modal 时 disabled。

`_ExitTree()` 必须 dispose proxy 和 runtime，并清空 runtime 引用，避免回调到已经释放的 Control。

### RenderFromRuntime

渲染入口必须允许 headless/no-node 场景 no-op：如果 runtime 缺失、world map view 或 battle panel 缺失，直接返回。

世界模式：

1. 显示 world background 和 world map view，隐藏 battle panel。
2. 根据 proxy 更新 status label、bottom action bar、party button。
3. 如果 `refresh_world=true`，调用 `world_map_view.RefreshWorld(proxy.GetWorldData())`。
4. 总是调用 `world_map_view.SetRuntimeState(playerCoord, selectedCoord, playerVisible)`。
5. 根据 active modal id 打开/关闭 settlement/shop/forge/stagecoach/warehouse/party/reward/promotion/submap/character info 等窗口。
6. 刷新 runtime log dock。

战斗模式：

1. 隐藏 world background、world map view 和 submap hint。
2. 显示 battle panel，传入 battle state、selected coord、selected skill、target coords、overlay target coords、active encounter name 等。
3. 根据 command result 的 `battle_refresh_mode` 决定 full/overlay/no refresh。
4. 更新 responsive log layout 使用 battle margin。

### Responsive log dock

log dock 的右侧布局常量必须保持：世界模式 top/bottom margin 为 60，右 margin 为 12；battle 模式 top 96、bottom 184；非折叠 battle 模式高度 clamp 到 `[360, 520]`。

## 实现级补充：WorldMapView 计算细节

### Camera 与可见区域

- camera origin = `playerCoord + (0.5,0.5) - viewportCellSpan/2`。
- origin X/Y clamp 到 `[0, max(worldSizeCells - viewportCellSpan, 0)]`。
- viewport cell span = `(Size.X/cell_size, Size.Y/cell_size)`，每轴最少 1，最多 world size。
- visible rect start = floor(cameraOrigin) - padding；end = ceil(cameraOrigin + span) + padding；start/end 都 clamp 到 world bounds。
- `_local_to_cell(mousePosition)` 使用 `floor(cameraOrigin + position/cell_size)`。

### Fog 与绘制过滤

- cell background：unexplored 完全黑；explored/visible 才画地表。
- explored settlement 要 darken 0.45 且 alpha 0.85；visible settlement 用原 tier color。
- settlement label 只在 origin 非 unexplored 且至少一个 footprint cell 被绘制时显示。
- world event、encounter、NPC marker 必须要求 visible，不是 explored；这避免玩家看到旧视野中的动态对象。
- player marker 受 `_playerVisibleOnMap` 和 viewport bounds 限制；当前实现不额外检查 fog visible，重建时保持该行为。

### Draw helper fallback

- cell background texture 缺失时画 `Color(0.11,0.14,0.18,1)`。
- background texture source rect 需要按 `cell_background_trim` 裁边；裁边后尺寸非正时退回整图绘制。
- village tier 且 `village_settlement_texture` 存在时用纹理；explored 时叠加 35% 黑。
- player texture 存在时按 `player_texture_draw_size` 居中绘制；否则画黄色菱形并描深色边。

## 实现级补充：错误处理与兼容策略

- 所有 schema reader 只接受当前字段；字段缺失、类型错误、空必填 id 都应返回 null/false 或记录错误，不自动补旧字段。
- Godot `Variant` 读取必须检查 `VariantType`，不要依赖异常。
- `StringName` id 和 string id 边界要明确：内容 catalog 和 encounter roster 用 typed `StringName`；UI 文本和 display name 用 string。
- 坏 submap entry 不应静默丢失 root `mounted_submaps`；生成/进入失败应保留原数据并返回错误。
- active world 切换前先保存当前 fog state，切换后再 setup 新 fog，避免把主世界 fog 写入子地图或反向污染。
- 单个命令失败不应导致 UI 崩溃；proxy 的 runtime unavailable fallback 返回“运行时尚未初始化。”。

## 实现级补充：回归映射

| 行为 | 推荐回归入口 |
|---|---|
| Grid footprint、fog 持久态、EncounterAnchor schema | `tests/world_map/schema/run_world_map_low_level_defensive_regression.cs`、`tests/world_map/schema/run_encounter_anchor_schema_regression.cs` |
| 世界生成 typed 输出、默认内容注入 | `tests/world_map/runtime/run_world_map_spawn_typed_regression.cs`、`tests/world_map/runtime/run_world_map_shared_content_injection_regression.cs` |
| Runtime proxy 命令返回与自动 render | `tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs` |
| Submap 进入/返回/保存事务 | `tests/world_map/runtime/run_world_submap_regression.cs`、`tests/world_map/runtime/run_world_map_save_transaction_regression.cs` |
| 世界事件与遭遇触发 | `tests/world_map/runtime/run_game_runtime_world_event_regression.cs`、`tests/world_map/runtime/run_game_runtime_world_encounter_regression.cs` |
| 据点入口与 settlement command handler | `tests/world_map/runtime/run_world_map_settlement_entry_regression.cs`、`tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs` |
| View tier color 与渲染配置 | `tests/world_map/runtime/run_world_map_view_color_config_regression.cs` |
| Loading overlay / battle handoff | `tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.cs`、`tests/world_map/runtime/run_world_map_battle_start_confirm_regression.cs` |

## 验证与回归入口

重建或修改世界地图模块后至少运行：

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/schema/run_world_map_low_level_defensive_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_spawn_typed_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_submap_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_view_color_config_regression.cs
```

若修改据点服务，还需运行 settlement runtime/schema 相关 regression；若修改 battle handoff，还需运行 battle start/loading overlay 相关世界地图 regression。

## 兼容性与安全约束

- 不新增旧字段别名、旧 payload fallback 或 schema migration，除非用户明确要求。
- 世界地图存档 schema 校验失败应 fail fast，不静默丢字段。
- `WorldMapSystem` 不持有领域规则真相；不要把 battle、warehouse、settlement、quest 的业务计算塞回场景脚本。
- runtime 内容读取必须走 `GameContentCatalog` typed 视图；不要从 UI dictionary 或 string-key fallback 重建正式内容索引。
- 不提交 `.godot/`、本地 save、截图临时文件。
