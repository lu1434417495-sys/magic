# 玩家共享仓库系统设计

更新日期：`2026-04-07`

## Summary

- 全队共享一个仓库，不拆个人背包和据点仓库。
- 仓库总容量由全部队员的隐藏属性 `storage_space` 求和决定，包含上阵和替补成员。
- 仓库内物品不计算重量，只按堆栈占格。
- 物品是否可堆叠、最大堆叠数由物品配置决定。

## State Ownership

- 仓库状态归属 `PartyState`，作为队伍级真相源持有。
- 成员只贡献容量，不拥有独立仓库状态。
- `GameSession` 负责仓库和队伍状态的统一持久化。
- `WorldMapSystem` 负责运行期打开仓库窗口和处理据点/队伍入口。

## Data Model

- 新增 `WarehouseState`，保存仓库当前的堆栈集合。
- 新增 `WarehouseStackState`，字段固定为：
  - `item_id`
  - `quantity`
- `WarehouseState` 只序列化非空堆栈，不显式保存空槽位。
- `PartyState` 新增 `warehouse_state` 字段。
- 新增 `ItemDef` 配置，字段固定为：
  - `item_id`
  - `display_name`
  - `description`
  - `icon`
  - `is_stackable`
  - `max_stack`
- 新增 `ItemContentRegistry`，负责扫描 `data/configs/items/` 并建立 `item_id -> ItemDef` 索引。
- 隐藏容量属性 key 固定为 `storage_space`。
- `storage_space` 来源于角色自定义属性，不在人物管理界面显示，也不在成员详情文本中暴露。

## Rules

- 容量单位固定为“每个堆栈占 1 格”。
- 添加物品时，先补满同类未满堆栈，再创建新堆栈。
- `is_stackable == false` 的物品统一按 `max_stack = 1` 处理。
- `is_stackable == true` 时，单堆上限由 `max_stack` 决定。
- 添加物品时如果容量不足，允许部分加入，并返回剩余未加入数量。
- 仓库当前占用等于现有堆栈数，不等于物品总件数。
- 容量下降导致 `used_slots > total_capacity` 时，不删除已有物品。
- 超容状态下允许查看和移除物品，不允许继续新增堆栈。
- 仓库不引入重量、负重、体积系数等第二套限制。

## Runtime Entry

- 仓库支持两个入口：
  - 队伍管理窗口
  - 据点服务
- 队伍管理窗口通过新增信号 `warehouse_requested` 打开仓库。
- 据点服务通过 `interaction_script_id = "party_warehouse"` 打开同一个仓库窗口。
- 仓库窗口纳入现有 modal 管理，不与其他弹窗并存。
- 从据点进入仓库时，沿用现有 `SettlementWindow -> WorldMapSystem` 动作分发链路。

## Persistence

- 旧存档缺少 `warehouse_state` 时，默认按空仓库处理。
- `PartyState.version` 升到 `2`，用于标识新增仓库字段后的模型版本。
- 全局 `SAVE_VERSION` 保持不变，不为本次新增字段单独提升整档版本。
- 仓库堆栈只保存 `item_id` 与数量，不保存物品定义副本。

## Public Interfaces

- `PartyState.warehouse_state`
- `GameSession.get_item_defs()`
- `PartyManagementWindow.warehouse_requested`
- settlement service payload 新增 `interaction_script_id`
- 新增 `PartyWarehouseService`，对外至少提供：
  - `get_total_capacity()`
  - `get_used_slots()`
  - `get_free_slots()`
  - `is_over_capacity()`
  - `count_item(item_id)`
  - `preview_add_item(item_id, quantity)`
  - `add_item(item_id, quantity)`
  - `remove_item(item_id, quantity)`

## Test Plan

- 旧存档加载后，缺少 `warehouse_state` 不报错，仓库默认为空。
- 新存档 round-trip 后，堆栈顺序、`item_id` 和数量保持一致。
- `storage_space` 缺失、为负数、队伍为空时，容量按 `0` 处理，系统不崩溃。
- 非堆叠物品始终一件一堆。
- 可堆叠物品能正确补堆、开新堆，并遵守 `max_stack`。
- `preview_add_item` 与 `add_item` 的剩余数量计算一致。
- 容量按全部队员的 `storage_space` 求和，上阵/替补切换不改变容量来源范围。
- 超容状态下允许移除，不允许继续新增。
- 队伍管理窗口与据点服务都能打开同一个仓库窗口。
- 人物管理界面和成员详情中不显示 `storage_space`。

## Current Status

- 首版共享仓库已落地，状态、存档兼容、物品注册、容量统计与堆叠规则已经接通。
- `PartyManagementWindow` 与据点服务都已能打开同一个仓库 modal。
- 当前仓库窗口支持：
  - 查看容量、占用、超容状态
  - 查看堆栈详情
  - 丢弃单件
  - 丢弃某类物品的全部库存
- 已有回归脚本覆盖旧存档兼容、堆叠规则、超容规则、round-trip 持久化与双入口打开链路。
- 当前实现仍属于 v1：
  - 已支持“仓库状态管理”
  - 尚未接入“外部产出/消耗流程”

## Next Work

1. 接入入仓来源。
   - 战斗结算战利品入仓
   - 地图拾取入仓
   - 商店购买入仓
   - 脚本奖励入仓
2. 接入出仓去向。
   - 可使用道具的消耗流程
   - 装备与仓库联动
   - 商店出售与任务提交扣仓
3. 提升仓库窗口交互。
   - 拆分堆栈
   - 拖拽排序或手动整理
   - 过滤、搜索、分类页签
   - 更完整的图标与物品信息排版
4. 扩充内容配置。
   - 补全 `data/configs/items/` 下的正式物品定义
   - 建立缺失 `item_id`、重复 `item_id`、失效图标路径的内容校验流程
5. 补更多自动化验证。
   - 非法 `item_id` 的兼容行为
   - 更复杂的超容恢复场景
   - 世界地图真实运行链路下的 modal 互斥回归

## Assumptions

- 首版仓库即共享背包，不再拆个人包和据点仓。
- 首版不做重量系统。
- 首版不做拖拽排序。
- 首版不做装备与仓库联动。
- 首版不做商店买卖流程。
- 首版不做地图拾取或战利品入仓流程。
