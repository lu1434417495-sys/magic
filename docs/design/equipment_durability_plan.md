# 角色装备耐久设计草案

更新日期：`2026-04-13`

## 状态

- 当前状态：`Draft / Deferred`
- 本文档只固定耐久、锋锐、坚韧的设计方向与 ownership，不进入当前实现排期。
- 在继续优化前，不建议直接按讨论结论把逻辑塞进 `ItemDef`、UI 或现有仓库堆栈结构。

## Problem

- 装备需要具备耐久度，耐久归零后直接损坏并从装备槽位移除。
- 护甲需要具备 `坚韧` 属性。
- 近战武器需要具备 `锋锐` 属性。
- 当近战命中时，若 `锋锐 > 坚韧`，则把差值累加到目标护甲的磨损进度；每累计满 `20` 点，护甲掉 `1` 点耐久。
- 武器自己的耐久损耗需要单独设计，且不能让武器比护甲更容易无意义地瞬间损坏。

## Current Ownership

- `scripts/player/warehouse/item_def.gd`
  - 当前只表达物品模板字段，如 `item_id`、`equipment_slot_ids`、`attribute_modifiers`。
  - 不能安全承载“当前耐久”，因为同名装备会共享模板数据。
- `scripts/player/equipment/equipment_state.gd`
  - 当前本质上是 `slot -> item_id`。
  - 没有装备实例，没有 `instance_id`，也没有实例级运行时字段。
- `scripts/player/warehouse/warehouse_stack_state.gd`
  - 当前本质上是 `item_id + quantity`。
  - 非堆叠装备也只是多个同名堆栈，没有实例身份。
- `scripts/systems/party_equipment_service.gd`
  - 当前负责仓库与装备槽位之间的流转。
  - 以后应继续作为装备实例的正式事务入口。
- `scripts/systems/party_warehouse_service.gd`
  - 当前只处理库存增减，不处理实例级装备状态。
  - 若耐久要保留到卸装后，仓库必须能保存装备实例。
- `scripts/systems/character_management_module.gd`
  - 当前负责基于 `EquipmentState` 重建属性快照，并在战斗单位刷新时重拉角色属性。
- `scripts/systems/battle_runtime_module.gd`
  - 当前持有技能定义、命中目标、战斗单位与战斗后写回入口。
  - 是常规近战磨损触发的正确 runtime owner。
- `docs/design/skills_implementation_plan.md`
  - 已规划 `break_equipment_on_hit` 这种显式装备破坏效果。
  - 新耐久系统必须和“显式破坏装备”兼容，而不是互相覆盖。

## Hard Constraints

- 同名装备也必须拥有各自独立的当前耐久。
- 角色正式装备真相源仍应留在 `PartyState -> PartyMemberState -> EquipmentState`。
- UI 继续只读展示和发起命令，不直接改装备状态。
- 材料、药草等普通堆叠物品不应该为了耐久被迫全部实例化。
- 战斗中的耐久变更必须能在战斗结束后稳定写回正式 `PartyState`。
- 常规磨损与显式技能破坏必须共享“实例销毁”语义：
  - 损坏后直接从装备槽位移除
  - 不回共享仓库
  - 后续若存在 `instance_id`，应销毁对应实例

## Invariants

- 装备未损坏前，属性仍由当前装备正常提供。
- 装备损坏后，属性快照必须立刻失去对应加成。
- 耐久不是永久成长字段，不写入 `UnitProgress`。
- 旧的 headless / 文本命令装备链路仍应能读到稳定的装备摘要结构。
- 常规耐久磨损不应把所有装备部位都卷入首版结算。

## Options

### Option A：继续保留 `item_id`，额外挂一张耐久表

- 数据位置：
  - 角色端按 `member_id + slot_id` 记耐久
  - 仓库端按“堆栈索引”或“插槽顺序”记耐久
- 逻辑位置：
  - `PartyEquipmentService` 和 `PartyWarehouseService` 外围再补映射层
- 主要问题：
  - 换装、回仓、堆栈压缩和存档 round-trip 后很容易失去稳定引用
  - 同名装备无法可靠区分
  - 这是最脆弱的补丁式方案

### Option B：只把装备做成实例，普通堆叠物品维持现状

- 数据位置：
  - 新增 `EquipmentInstanceState`
  - `EquipmentState` 保存 `slot -> instance`
  - `WarehouseStackState` 对非堆叠装备保存实例载荷
- 逻辑位置：
  - `PartyWarehouseService` 负责实例创建与仓库流转
  - `PartyEquipmentService` 负责实例装备、卸装、损坏移除
  - 新增 `EquipmentDurabilityService` 负责耐久规则
- 优点：
  - 是当前仓库和装备体系下最小且稳定的实例化切法
  - 只扩大装备域，不把材料域一起重写
- 主要风险：
  - 需要显式迁移旧存档与旧测试

### Option C：全物品实例化

- 数据位置：
  - 仓库里所有物品都用实例表达
- 逻辑位置：
  - 仓库、奖励、掉落、命令行、UI 全链路改造
- 优点：
  - 长期扩展性最好
- 主要问题：
  - 对当前项目是明显过度设计
  - 为了耐久功能承担了不必要的大范围改造成本

## Recommended Design

- 选择 `Option B`。
- 目标不是“一步做完装备深度化”，而是先把“实例化装备 + 常规耐久磨损 + 损坏移除”稳定下来。

### 1. 静态模板字段

- 在 `ItemDef` 中新增最小静态字段：
  - `max_durability`
  - `equipment_traits: Array[StringName]`
  - `armor_toughness`
  - `weapon_sharpness`
- 首版推荐 trait：
  - `armor`
  - `melee_weapon`
  - `ranged_weapon`
  - `shield`
  - `indestructible`

### 2. 装备实例字段

- 新增 `EquipmentInstanceState`，最少包含：
  - `instance_id`
  - `item_id`
  - `current_durability`
  - `armor_wear_progress`
  - `weapon_wear_progress`
- 说明：
  - `current_durability` 是当前剩余耐久
  - `armor_wear_progress` 用于累计“锋锐压穿护甲”的差值
  - `weapon_wear_progress` 用于累计“武器自身磨耗”

### 3. State Ownership

- `ItemDef`
  - 只保存模板值，不保存当前耐久。
- `EquipmentState`
  - 角色正式装备真相源。
  - 从 `slot -> item_id` 升级为 `slot -> EquipmentInstanceState`。
  - 继续保留 `get_equipped_item_id()` 这类兼容读接口，避免把现有调用方一次性全部打碎。
- `WarehouseStackState`
  - 对普通堆叠物维持 `item_id + quantity`。
  - 对非堆叠装备增加实例字段或实例载荷。
- `PartyWarehouseService`
  - 负责新装备实例创建、回仓、出仓和旧数据升级。
- `PartyEquipmentService`
  - 负责实例级装备、卸装、替换、损坏销毁。
- `EquipmentDurabilityService`
  - 负责常规耐久规则，不把该逻辑塞进 UI 或 `ItemDef`。
- `BattleRuntimeModule`
  - 只负责在命中后触发耐久服务，并在需要时刷新战斗单位快照。

### 4. 常规近战磨损触发条件

- 只有满足以下条件时才进入常规耐久结算：
  - 本次效果实际造成了正伤害
  - 技能 `SkillDef.tags` 包含 `melee`
  - 目标是可持久化成员，或目标至少存在可修改的正式装备状态
- 首版不把 `ranged`、法术、地形效果、纯状态效果并入常规磨损。
- 首版不为饰品引入常规接触磨损。

### 5. 受击护甲位选择

- 首版不做命中部位系统，使用固定优先级：
  - `body`
  - `head`
  - `off_hand`（仅当该装备带 `shield` 或 `armor` trait）
- 选中的护甲实例同时承担：
  - `坚韧` 取值来源
  - 耐久损失承受者

### 6. 护甲耐久公式

- 当近战命中时：
  - `armor_delta = max(attacker_sharpness - defender_toughness, 0)`
  - 把 `armor_delta` 累加到护甲实例的 `armor_wear_progress`
  - 每累计满 `20` 点，护甲 `current_durability -= 1`
  - 余数保留在 `armor_wear_progress`
- 这部分直接对应需求：
  - `锋锐高于坚韧的值会累加`
  - `每累加超过 20 点会掉一点耐久`

### 7. 武器耐久公式

- 武器耐久首版推荐公式：
  - `weapon_delta = 5 + max(defender_toughness - attacker_sharpness, 0)`
  - 把 `weapon_delta` 累加到武器实例的 `weapon_wear_progress`
  - 每累计满 `30` 点，武器 `current_durability -= 1`
  - 余数保留在 `weapon_wear_progress`
- 设计理由：
  - 武器每次近战接触都应该有稳定磨耗，所以固定底数 `5`
  - 目标越硬、武器越钝，磨损越快，所以额外叠加 `max(坚韧 - 锋锐, 0)`
  - 护甲阈值用 `20`、武器阈值用 `30`，能让护甲更明显地体现“被压穿”，同时避免武器过快报废

### 8. 锋锐和坚韧的默认来源

- 攻击者 `锋锐` 来源优先级：
  - 主手已装备近战武器实例的 `weapon_sharpness`
  - 技能或效果参数中的 `contact_sharpness`
  - 默认值 `10`
- 防御者 `坚韧` 来源优先级：
  - 被选中护甲实例的 `armor_toughness`
  - 若没有有效护甲，则为 `0`
- 这样敌方无正式武器实例时，也能通过技能参数或默认值磨损玩家护甲。

### 9. 多段与多目标

- 同一条技能命令中：
  - 每个“成功造成正伤害的 hit”都单独累计一次耐久磨损
  - 多段攻击按命中段数累计
  - 多目标攻击按各自目标分别累计
- 但“是否损坏并移除装备”建议在该次命令所有命中结算后统一提交：
  - 避免先打碎装备、后续同一命令又读取到已变化属性，导致顺序敏感

### 10. 装备损坏语义

- 当 `current_durability <= 0` 时：
  - 该装备实例立即判定为损坏
  - 直接从 `EquipmentState` 中移除
  - 不回共享仓库
  - 不视为普通卸装
- 首版不做：
  - 低耐久降属性
  - 红耐久告警特效
  - 自动修理

### 11. 战斗内刷新语义

- 只有在装备真正损坏并移除时，才需要刷新单位属性快照。
- 刷新时必须遵守：
  - 重新计算属性上限
  - 现有 `current_hp / current_mp / current_stamina / current_ap` 只做上限裁剪，不做重置
- 原因：
  - 当前 `CharacterManagementModule.refresh_battle_unit()` 会重建快照
  - 若实现时仍顺手重置 `AP` 或体力，会把装备损坏错误地变成“变相回资源 / 重置资源”

### 12. 与显式装备破坏效果兼容

- `break_equipment_on_hit` 仍然保留为显式技能语义。
- 常规耐久磨损与显式装备破坏的差异：
  - 常规磨损：按 `锋锐 / 坚韧` 和累计阈值逐步掉耐久
  - 显式破坏：按技能规则直接摧毁装备
- 两者共享的底层结果应一致：
  - 最终都是销毁某个已装备实例
  - 最终都不回共享仓库
  - 若存在 `indestructible`，应在装备层统一拦截

### 13. 存档与迁移

- 当前 `PartyState.version = 2`。
- 若正式实现耐久，建议升级到 `version = 3`。
- 迁移建议：
  - 旧 `equipment_state` 中的 `item_id` 裸值，升级为“满耐久实例”
  - 旧仓库中的非堆叠装备堆栈，也升级为“满耐久实例”
  - 普通堆叠物品不变
- 不建议长期保留“旧结构 + 新结构”双套主逻辑。

## Minimal Slice

- 首版只让以下装备参与常规耐久：
  - 主手近战武器
  - `body / head / shield` 中按优先级选中的一件护甲
- 首版只认 `SkillDef.tags` 里的 `melee`，不单独发明一套新的近战判定系统。
- 首版只在装备损坏时刷新属性快照，不做低耐久惩罚。
- 首版不做修理系统。
- 首版不把全部仓库物品实例化。

## Files To Change

- `scripts/player/warehouse/item_def.gd`
- `scripts/player/equipment/equipment_state.gd`
- `scripts/player/warehouse/warehouse_stack_state.gd`
- `scripts/player/warehouse/warehouse_state.gd`
- `scripts/player/progression/party_state.gd`
- `scripts/systems/party_warehouse_service.gd`
- `scripts/systems/party_equipment_service.gd`
- `scripts/systems/character_management_module.gd`
- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/game_runtime_facade.gd`
- `scripts/ui/party_management_window.gd`
- `data/configs/items/*.tres` 中需要参与耐久的装备定义
- 新增 `scripts/player/equipment/equipment_instance_state.gd`
- 新增 `scripts/systems/equipment_durability_service.gd`

## Tests To Add Or Run

### 现有回归必须继续通过

- `tests/equipment/run_party_equipment_regression.gd`
- `tests/warehouse/run_party_warehouse_regression.gd`
- `tests/text_runtime/run_text_command_regression.gd`

### 实现时应新增的耐久回归

- 近战命中时，护甲按 `max(锋锐 - 坚韧, 0)` 正确累计并在 `20` 点阈值掉耐久
- 近战命中时，武器按 `5 + max(坚韧 - 锋锐, 0)` 正确累计并在 `30` 点阈值掉耐久
- 多段近战技能会按命中段数累计武器磨损
- 多目标近战技能会对各目标护甲分别累计磨损
- 耐久归零后，装备直接从目标槽位移除，不回仓
- 装备损坏后角色属性快照会更新，但不会错误重置 `AP`、体力或生命
- 旧版 `equipment_state` 与旧仓库装备堆栈在 round-trip 后能升级为满耐久实例

## Deferred Questions

- 是否要让 `off_hand` 的盾牌在常规接触磨损中承担比 `body` 更高的受击优先级
- 是否要让特定 `melee` 子标签，例如 `thrust / breaker / heavy`，对 `锋锐` 或阈值产生额外修正
- 是否要把敌方的天然爪牙、骨刺、岩肤等“非装备锋锐 / 坚韧”进一步数据化，而不是暂时依赖默认值和技能参数
- 是否在正式实现前，先把“装备实例化”独立成一个无耐久的前置改造阶段
