# 系统架构子代理审计意见归档（2026-05-11）

## 来源

- `Confucius` (`019e12f3-c595-7b21-bdc4-24a4db7cd5c1`)
- `Archimedes` (`019e12f3-dd98-7450-8593-f795e35d71b5`)
- `Mencius` (`019e12f3-f766-7b00-98b5-12fb8b50292b`)
- `Anscombe` (`019e12f4-10fc-7243-b783-1e7775c23ba5`)

本文件汇总四个子代理对当前 Godot 项目架构的只读审计意见。所有子代理均已返回结果。审计前均要求读取 `docs/design/project_context_units.md`，并按各自模块范围展开。

已处理项：

- 战斗结算结果事务边界：`BattleSessionFacade` 先 consume 后 finalize 的问题已按方案 A 修复，改为 finalize 成功后才 consume，并让失败分支释放 battle save lock、保留 result。

## Confucius：Battle Runtime / AI / Terrain / Enemy Content

### 当前所有权判断

- `BattleRuntimeModule` 仍是战斗运行时真相源，持有状态、命令入口、预览、terrain 初始化、AI/sidecar 调度。
- runtime sidecar 当前更多是“拆文件”，不是稳定模块边界；大量 sidecar 仍通过 `_runtime._private` 读写核心状态。
- 移动规则真相在 `BattleMovementService`，但 AI action 资源存在第二套候选格和路径语义。
- terrain 真相由 `BattleTerrainGenerator` 写入 `BattleState.cells/terrain_profile_id`，显示侧由 CU-18 render profile/catalog 解释视觉。
- 敌人内容真相应是 `EnemyContentRegistry`、`EnemyTemplateDef`、`EncounterRosterBuilder`，但 runtime 仍保留默认敌人 fallback。
- 战后回写由 `BattleSessionFacade` 消费结算结果，再交给 `GameRuntimeFacade`、writeback/loot commit 服务落地。

### 发现

1. `[高]` 战斗结果先 consume 再 finalize，失败会丢 canonical result 且可能卡 save lock。  
   该项已修复。

2. `[高]` formal enemy content 失败会被默认敌人路径掩盖。  
   `BattleRuntimeModule.start_battle()` 在 encounter builder 没产出 enemy 时 fallback 到 unit factory。`BattleUnitFactory` 又能从 context/default 构造敌人并填默认属性。这会让缺模板、坏 roster、坏 registry 的正式内容变成“能打但不对”的敌人。

3. `[高]` AI 移动候选生成与 runtime 移动语义不一致。  
   runtime 移动成本合并 grid、terrain effect、status、quickstep；但 `MoveToRangeAction`、`MoveToAdvantagePositionAction` 自己 BFS，只用 `grid_service.get_unit_move_cost`，最终 preview 只能拒绝非法命令，不能修正 AI 排名、目标选择和路径预算。

4. `[中]` sidecar 过度耦合，模块边界基本失效。  
   `BattleSkillExecutionOrchestrator`、`BattleTimelineDriver` 等 sidecar 直接调用 runtime 私有字段和私有方法，隐式公共 API 扩散。

5. `[中]` 热路径存在重复排序、全图扫描和字符串 key 分配风险。  
   timeline 每 tick 多次 sorted unit keys；timed terrain 每 tick 排序扫描所有 cell keys；movement reachable 搜索用字符串 state key。UI hover 或 AI 批量 preview 下会放大。

6. `[中]` `EnemyTemplateDef` 内部 fallback 创建 `ItemContentRegistry`，验证所有权外溢。  
   模板验证如果找不到 item def，会自己创建 registry，绕过测试注入的内容子集。

7. `[低]` terrain/build 到 display 的合同缺少显式覆盖。  
   新增 terrain profile/prop 时，规则侧可生成但显示侧未必识别。

### 建议最小切片

1. 先修战后事务。已完成。
2. 给 enemy fallback 加显式 fixture/test-only 开关；formal world encounter 缺内容时返回错误，不走默认 unit factory。
3. 把 AI action 的 reachable/path 查询收口到 `BattleMovementService` 或 `BattleAiContext` 的 movement facade，删除 action 本地 BFS。
4. 从 timeline sidecar 开始收边界：不再直接改 `_runtime._battle_resolution_result`，改为返回事件/decision。
5. 加 per-turn ordered unit cache、active timed terrain coord set、reachable movement cache；移除热入口反复 sidecar setup。
6. 移除 `EnemyTemplateDef` 内部 `ItemContentRegistry.new()` fallback，让 registry 显式传入依赖并报告缺失。

## Archimedes：Game Runtime / Persistence / Proxy / Snapshot

### 发现

1. `[P1]` 战后 `battle-local` 写回失败会在 `set_battle_save_lock(false)` 前直接 return。  
   该项已修复。

2. `[P1]` 子地图进入/返回把 `_player_coord` 与 `root_world_data.active_submap_id/submap_return_stack` 分两次保存。  
   `GameSession` 的 setter 会立即写盘，第二次 `set_world_data()` 失败时，磁盘可能留下“子地图坐标 + 主地图 active_submap_id”的半写坏状态。

3. `[P1]` `SaveSerializer` 对 `mounted_submaps` 的严格校验破损。  
   发现坏 generated submap 时只 `push_error` 并返回 `{}`，上层仍把 `{}` 当正常 `mounted_submaps` 写入归一化结果，导致静默丢失子地图。

4. `[P2]` `SaveSerializer` 对 `map_seed` 和 `next_equipment_instance_serial` 用 `int()` 转换但不检查原始类型。  
   字符串数字、float、bool 可能被兼容成正整数；`world_step` 已经严格检查 `is int`，这里应一致。

5. `[P2]` `WorldMapSystem` 仍直接调用 `_runtime.command_shop_sell()` / `_runtime.command_warehouse_discard_*()` 并手动 `_render_from_runtime()`，绕过 `WorldMapRuntimeProxy` 的统一命令结果和渲染回调。

6. `[P2]` `GameRuntimeFacade/Proxy` 暴露太多可变真相源。  
   `get_world_data()`、`get_party_state()`、`get_battle_state()`、`get_character_management()`、`get_party_warehouse_service()` 等直接透出 live 对象，让 UI/headless/sidecar 可以绕过命令、校验、持久化路径改状态。

7. `[P3]` runtime snapshot 不是纯读合同。  
   battle snapshot 构建时创建 `BattleHudAdapter` 并传入实时 `preview_battle_command` callback；如果 preview 未来有缓存、日志、随机或分配副作用，快照会变成隐式执行路径。

### 职责泄漏

- `GameSession` 不只是持久化 owner，还通过 live `world_data/party_state` 和装备 ID allocator 暴露可变全局状态。
- `GameRuntimeFacade` 同时是 facade、service locator、modal owner、save bridge、battle resolver，公共 getter/setter 太多。
- `WorldMapRuntimeProxy` 是 `callv` 转发器，不是真正窄接口；读接口缺失会静默返回默认值。
- `WorldMapSystem` 大体是场景层，但仍持有 autoload `GameSession`，给 UI 注入 session/content resolver，并有少量直接 runtime 命令。

### 建议最小切片

1. 先修战斗结算失败路径。已完成。
2. 增加 `GameSession.set_world_runtime_state(world_data, player_coord, player_faction_id)` 之类原子 setter，把子地图进入/返回和普通移动相关字段一次写盘。
3. 收紧 `SaveSerializer`：`map_seed`、`next_equipment_instance_serial`、`mounted_submaps[*].is_generated` 必须原始类型正确；子地图归一化失败直接让 `normalize_world_data()` 返回 `{}`。
4. 清掉 `WorldMapSystem` 的 `_runtime.command_*` 直连，只允许构造/释放 runtime；所有交互走 Proxy。
5. 把 snapshot 改成纯 DTO 构建，禁止传 live command callback；补 reload 后 submap 状态、坏 save schema、writeback failure lock 释放三类回归。

## Mencius：Progression / Inventory / Equipment / Rewards

### 发现

1. `[P1]` 奖励队列允许未知 `entry_type`，应用时静默丢奖励。  
   `QuestDef`、`CharacterManagementModule`、`PendingCharacterRewardEntry.from_dict()` 都接受任何非空 entry type；真正应用时只处理已知类型，默认分支跳过，随后无条件移除 pending reward。错误 quest 配置或坏存档可以生成“可领取但无效果”的奖励，玩家点击后队列被删除。

2. `[P1]` 装备 `instance_id` 唯一性靠外围约定，不在 save/model 边界封口。  
   `WarehouseState.from_dict()` 不检查重复 instance_id；`EquipmentState.from_dict()` 不检查多个装备条目共享同一 instance_id；`PartyState.from_dict()` 装载 backpack 和 member equipment 后也没有跨 surface 唯一性校验。

3. `[P2]` warehouse 允许装备进 stacks，equipment service 又把 `count_item()` 当实例存在判断。  
   坏存档或直接模型写入会让 UI/preview 认为装备存在，执行时才失败。

4. `[P2]` pending reward 存档 schema 不如 CU 描述严格，`reward_id` 也不是唯一地址。  
   pending reward 和 entry 不拒绝额外字段；`PartyState` 追加 pending rewards 不检查重复 reward_id，删除时只删第一个匹配项。

5. `[P2]` battle-local equipment/writeback 测试覆盖不闭环。  
   现有测试有 battle-local 保留、文本命令写回、耐久破坏局部验证，但缺一条端到端断言 `instance_id`、`rarity`、`current_durability` 和所有权 surface 的回归。

6. `[P3]` service fallback allocator 会和已装备实例撞 ID。  
   `PartyWarehouseService` 没有注入 allocator 时用本地 serial，但 existence check 只看 warehouse，不看 member equipped。正确性依赖调用者记得注入 `GameSession.allocate_equipment_instance_id()`。

### 建议最小切片

1. 定义唯一 `SUPPORTED_PENDING_CHARACTER_REWARD_ENTRY_TYPES`，同时用于 `QuestDef`、`PendingCharacterRewardEntry.from_dict()`、CMM normalization/apply；未知类型应校验失败，或 apply 返回错误且保留队列。
2. 加 PartyState 级别 equipment ownership invariant，在反序列化/normalize 后扫描 warehouse 与所有 member equipment，拒绝非空重复 instance_id。
3. 拆分 `count_item()` 语义，equip preview 只看 equipment instance count；在有 item_defs 的服务边界增加 warehouse content validation，拒绝 equipment item 出现在 stacks。
4. pending reward 和 entry 加 exact-field 校验；`PartyState.from_dict()` 和 enqueue 阶段拒绝重复 reward_id。
5. 新增战斗结束装备写回回归，覆盖 rare/damaged instance、战斗内修改/卸装/破坏、写回后实例字段和所有权 surface。
6. fallback allocator existence check 扫描 `_party_state.member_states[*].equipment_state`，或让创建装备实例的生产路径强制要求 allocator。

## Anscombe：UI / Headless / Proxy / Rendering

### 发现

1. `[P1]` `WorldMapSystem` 仍绕过 `WorldMapRuntimeProxy`，破坏“唯一正式命令/读取接口”合同。  
   shop sell 和 warehouse discard 直接调 `_runtime.command_*` 并手动 render，而不是走 proxy 的返回类型保护和 render callback。

2. `[P1]` Headless 链路直接改运行时私有/半私有状态，容易制造“headless 过、正式 UI 失败”的假阳性。  
   例如直接写 `unit_base_attributes.custom_stats["storage_space"]`、直接改 `battle_state.phase/winner/timeline`、直接 `battle_runtime.issue_command()` 再手动 record/refresh/status、直接写 `_active_loot_entries`。

3. `[P2]` 多个 UI 窗口仍持有业务状态或规则镜像。  
   `PartyManagementWindow` 有本地 roster 规则和上限判断；`CharacterCreationWindow` 在 UI 内组装 identity/body size/versatility payload 与属性修正预览；`SettlementWindow` 在窗口层重写 enabled/reason/state_label；`CharacterInfoWindow` 在窗口层校验并重算 fate 公式/提示文案。

4. `[P2]` 战斗 HUD/hover 路径把规则预览塞进高频 UI 刷新。  
   board hover/click 扫全量 cells；hover 后 refresh overlay，`BattleMapPanel` 即使 overlay refresh 也完整 build snapshot；`BattleHudAdapter` 调用命中、伤害、fate、装备预览规则，存在热路径风险。

5. `[P2]` 渲染刷新策略偏全量重建。  
   `BattleBoardController._redraw()` 清所有 TileMapLayer 和动态节点再重建 terrain/props/units/highlights；`WorldMapView` 对 settlements/events/anchors/npcs 每次全数组扫描再过滤。

6. `[P3]` 回归入口不少，但截图/文本表面仍有盲区。  
   canyon capture 只是辅助，不是断言型回归；文本 renderer 仍兼容旧 `command_text/log_text` 字段，和 CU-18 当前说明有漂移。

7. `[P3]` Scene-script contract 当前基本干净，但依赖显式节点名和动态创建，属于脆弱边界。  
   `BattleMapPanel` 动态创建 SubViewport/board，scene diff 看不到完整子树，因此 board contract 测试必须继续保留。

### 建议最小切片

1. 给 proxy 补 `command_shop_sell`、`command_warehouse_discard_one/all`，场景层只调 proxy，并补 UI 回归断言这些命令走统一 render callback。
2. 把容量夹具、强制结束战斗、预置 loot、battle equip/unequip 做成 facade/test-only helper 或正式 proxy 命令；headless 只调用这些入口。
3. 优先改队伍窗口，让 runtime/facade 返回 roster preview/apply result；据点服务改为 runtime 产出“已选成员 resolved service row”；人物信息 fate section 全部由 builder 产出，窗口只渲染 sections。
4. 给 board 建 visual-pick 空间索引，或先用 `InputLayer.local_to_map()` 限定邻域；HUD 增加 overlay-only snapshot，hover 时跳过装备面板和非必要队列重建；加 hover 性能回归。
5. 保持 selection/hover 只走 marker path；props/units 做简单 node pool；world_data context/proxy 提供 visible entity index 或 coord bucket。
6. 把 canyon capture 升级为非空/签名/关键像素断言；给 login/settlement/shop/party 加最小 viewport 截图或长文本不重叠检查；移除或专测 text snapshot 的旧字段 fallback。

## 建议处理顺序

1. 子地图进入/返回原子保存：修复世界状态多字段提交半写风险。
2. formal enemy content 失败不得 fallback 默认敌人。
3. pending reward entry type 白名单与 apply 失败保留队列。
4. equipment instance_id 跨 surface 唯一性 invariant。
5. WorldMapSystem 直连 runtime 命令改走 Proxy。
6. headless 私有状态写入收口到 facade/test helper。
7. AI movement 查询收口到 `BattleMovementService`。
8. `SaveSerializer` 严格 schema 修复。
