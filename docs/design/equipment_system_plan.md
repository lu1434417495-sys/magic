# 角色装备系统设计

更新日期：`2026-04-14`

## 状态

- 当前状态：`Draft / Consolidated`
- 本文档现为唯一装备设计入口，已吸收原 `Phase 2` 与耐久分文档内容。
- 当前已经落地并可视为正式基线的能力：
  - 共享仓库出入仓换装
  - 固定装备槽位
  - 静态属性修正
  - 存档 round-trip
  - headless / 文本命令换装
  - 种子装备与自动化回归
- 当前正式实现目标是 `Phase 2：正式可玩装备系统`。
- 装备耐久、装备实例化与常规战斗磨损仍为 `Deferred`，必须建立在 Phase 2 的占槽 / 预览 / 原子事务主链之上。

## 目标

- 把当前已经落地的“共享仓库 + 固定槽位 + 静态属性加成”整理成正式基线。
- 把 Phase 2 的多槽位占用、资格校验、预览链路与独立装备窗口并入同一份总设计。
- 明确耐久、锋锐、坚韧与实例化的 ownership 和前置条件，但不把它们提前混入当前排期。
- 保持装备系统与当前项目主干一致：
  - 真相源在 `PartyState -> PartyMemberState -> EquipmentState`
  - 物品定义仍以 `ItemDef` 为入口
  - 角色属性仍通过 `CharacterManagementModule -> AttributeService` 统一结算
  - 装备的取得与回收仍通过共享仓库完成

## Summary

- 装备系统以 `PartyState -> PartyMemberState -> EquipmentState` 为唯一真相源。
- 装备物品来源于共享仓库，换装本质上是“仓库与角色槽位之间的受约束转移”。
- 当前正式基线仍是“静态模板装备 + 固定槽位 + 静态属性修正”。
- Phase 2 在同一条主链上补齐：
  - 多槽位占用
  - 角色资格校验
  - dry-run 预览
  - 多件回仓原子事务
  - 独立装备窗口
- 耐久、锋锐、坚韧与实例化不在当前实现排期内，但它们的状态边界已经在本文档中预先固定。
- 不再维护三份并行装备设计文档；后续装备相关设计更新统一落在本文档。

## 作用范围

- 本系统负责：
  - 装备位管理
  - 共享仓库与角色槽位之间的物品流转
  - 装备对角色属性与战斗快照的影响
  - 装备相关 UI 数据结构
  - 装备相关的存档恢复与文本命令入口
  - 显式技能效果造成的装备破坏规则
- 本系统暂不负责：
  - 消耗品使用
  - 战斗内临时拾取与即时换装
  - 随机掉落表与商店经济平衡
  - 强化、镶嵌、洗练、词缀生成
  - 套装触发与唯一实例追踪
  - 修理系统与低耐久惩罚

## 设计原则

- 单一真相源：角色当前装备只允许由 `EquipmentState` 表达，不能在 UI、战斗状态、属性快照里再维护第二份装备表。
- 规则集中：装备合法性由装备规则层和装备服务统一裁决，不允许窗口脚本直接改槽位。
- 与仓库解耦但串联：仓库决定物品库存与容量，装备系统决定角色是否能穿、穿到哪、是否与现有装备冲突。
- 与成长系统并行：装备可以影响属性、标签、临时技能，但不直接改写角色永久成长数据。
- 渐进式扩展：先稳住固定槽位与属性修正，再扩到装备限制、技能授予、品质实例化与耐久。
- 不长期兼容旧结构：若装备 schema 升级，需要显式迁移或直接废弃旧存档，而不是在正式逻辑里长期保留兼容分支。

## Current Status

- 已完成：
  - 装备状态对象 `EquipmentState`
  - 固定槽位规则 `EquipmentRules`
  - `PartyEquipmentService` 与共享仓库联动
  - `CharacterManagementModule` 属性快照接入
  - 文本命令装备入口
  - 种子装备配置与回归测试
- 暂未完成：
  - 图形化装备管理窗口
  - 装备需求与互斥规则
  - 双手占槽模型
  - 掉落 / 商店 / 任务奖励正式接入
  - 装备授予技能与品质体系
  - 装备实例化、耐久与修理
  - 装备破坏抗性与唯一实例装备

## State Ownership

- `ItemDef`
  - 物品静态定义真相源。
  - 负责声明物品是否属于装备、允许从哪些入口槽装备、真实占用哪些槽，以及有哪些静态修正。
- `EquipmentState`
  - 角色当前装备真相源。
  - 当前基线只表达“槽位 -> item_id”。
  - Phase 2 升级后改为“入口槽位 -> 装备条目”。
- `EquipmentRequirement`
  - Phase 2 新增的专用资格资源。
  - 用于表达职业、属性、体型等装备要求。
- `EquipmentInstanceState`
  - Deferred。
  - 未来用于表达实例级耐久、磨损进度与唯一身份。
- `PartyEquipmentService`
  - 装备规则执行入口。
  - 负责槽位决策、资格校验、预览、仓库流转、原子提交、装备摘要输出与属性修正展开。
- `PartyWarehouseService`
  - 仓库真相源与容量规则 owner。
  - 不判断角色职业、体型、装备互斥，只处理库存能否增减与事务是否可提交。
- `CharacterManagementModule`
  - 消费装备结果，重建角色属性快照、技能表和战斗单位快照。
  - Phase 2 继续负责属性预览。
- `BattleRuntimeModule`
  - 战斗中显式装备破坏与未来常规耐久磨损的 runtime owner。
- `GameRuntimeFacade`
  - 负责命令入口、窗口互斥、战斗中禁换装、文案与 modal 接线。

## Hard Constraints

- 角色正式装备真相源仍然必须留在 `PartyState -> PartyMemberState -> EquipmentState`。
- 共享仓库仍是装备来源与回收入口，UI 不得直接改 `EquipmentState`。
- 战斗中仍然禁止玩家主动换装。
- Phase 2 不引入耐久、不引入装备实例化、不引入战斗内拾取。
- 多槽位替换必须保持原子性：
  - 任何一件被替换装备无法回仓
  - 或任何校验失败
  - 整次换装都必须回滚
- 同名装备也必须能为未来实例化预留稳定扩展位，不能把后续耐久继续锁死在 `slot -> item_id` 的旧表示上。

## Invariants

- `get_equipped_item_id(slot_id)` 这类兼容读接口仍应保留，避免一次性打碎现有调用方。
- 同一件双手装备只能结算一次属性修正，不能因占两个槽位而双算。
- 预览结果与真正执行结果必须来自同一套规则，不允许 UI 自己再实现一份占槽与资格逻辑。
- 资格校验不能允许“靠待装备物自己的加成满足自己”。
- 若待装备物会挤掉现有装备，属性资格判定必须基于“最终负载”而不是“替换前快照”。
- 装备损坏后直接从正式装备状态移除，不回共享仓库；显式破坏与未来常规耐久共享这一底层语义。

## 槽位与数据模型

### 槽位模型

- 当前正式槽位：
  - `main_hand`
  - `off_hand`
  - `head`
  - `body`
  - `accessory_1`
  - `accessory_2`
- 固定槽位仍然保留，因为它能保证：
  - UI 布局稳定
  - 文本快照稳定
  - 存档结构稳定
- 后续扩展不删除这些槽位，而是在其上增加“占用关系”语义：
  - 单手武器通常只占 `main_hand`
  - 盾牌 / 副手法器占 `off_hand`
  - 双手武器占 `main_hand + off_hand`
  - 双饰品仍然是两个独立槽位
- 正式区分两个概念：
  - `equipment_slot_ids`：物品允许被点击放入哪些入口槽
  - `occupied_slot_ids`：装备后真实占用哪些槽位

### `ItemDef`

- 当前继续沿用的字段：
  - `item_id`
  - `display_name`
  - `description`
  - `icon`
  - `is_stackable`
  - `max_stack`
  - `item_category`
  - `equipment_slot_ids`
  - `attribute_modifiers`
- 装备基线约束：
  - `item_category == "equipment"`
  - 至少声明一个合法槽位
  - 首版仍建议不可堆叠，`max_stack == 1`
- Phase 2 推荐新增：
  - `occupied_slot_ids: Array[String] = []`
  - `equip_requirement`
  - `equipment_type_id`
- 字段语义：
  - `equipment_slot_ids`
    - 表示允许作为入口点击的槽位。
  - `occupied_slot_ids`
    - 非空时表示该装备最终会占用的固定槽位集合。
    - Phase 2 约束：若声明了非空 `occupied_slot_ids`，当前只允许 `equipment_slot_ids.size() == 1`。
  - `equip_requirement`
    - 单独资源，避免把资格字段继续散落在 `ItemDef` 顶层。
  - `equipment_type_id`
    - 用于候选过滤、文案和后续掉落池 / 品质分组，不参与首版核心规则。
- 未来但不在当前排期的字段：
  - `exclusive_tags`
  - `granted_skill_ids`
  - `granted_passive_tags`
  - `rarity_tier`
  - `break_resistance_percent`
  - `slot_break_weight_override`
  - `instance_mode`
  - `max_durability`
  - `equipment_traits`
  - `armor_toughness`
  - `weapon_sharpness`

### `EquipmentState`

- 当前基线形状：
  - `equipped_slots.<slot_id> = <item_id>`
- Phase 2 推荐升级为“入口槽位条目”：
  - `equipped_slots.<entry_slot_id> = { item_id, occupied_slot_ids }`
- 示例：
  - `equipped_slots.main_hand = { item_id = "iron_greatsword", occupied_slot_ids = ["main_hand", "off_hand"] }`
- 这样做的收益：
  - 同一件装备只存一次
  - 属性只结算一次
  - 展示槽位能稳定追溯到其 owner 条目
  - 后续耐久只需要把条目扩成 `item_id + instance_id`
- 对外兼容 API 继续保留：
  - `get_equipped_item_id(slot_id)`
  - `get_filled_slot_ids()`
  - 但内部要先解析“展示槽位归哪条入口条目所有”

### `EquipmentRequirement`

- 推荐新增 `scripts/player/equipment/equipment_requirement.gd`
- Phase 2 首版只支持：
  - `required_profession_ids: Array[StringName]`
  - `required_attribute_rules: Array[AttributeRequirement]`
  - `minimum_body_size`
  - `maximum_body_size`
- 判定语义：
  - `required_profession_ids`
    - 只检查成员当前已拥有的职业记录。
  - `required_attribute_rules`
    - 基于“最终预览负载”的属性快照判断。
    - 已经被挤掉的装备不再算，待装备物自己的属性加成也不参与资格判定。
  - `body_size`
    - 直接读 `PartyMemberState.body_size`。

### `EquipmentInstanceState`（Deferred）

- 推荐新增最小字段：
  - `instance_id`
  - `item_id`
  - `current_durability`
  - `armor_wear_progress`
  - `weapon_wear_progress`
- 说明：
  - `current_durability` 是当前剩余耐久
  - `armor_wear_progress` 用于累计护甲磨损
  - `weapon_wear_progress` 用于累计武器磨耗
- Deferred 方向选择“只把装备做成实例，普通堆叠物品维持现状”，不做全物品实例化。

## 装备规则层

### 1. 内容校验

- 装备类物品必须能通过 `ItemContentRegistry` 校验。
- 非装备物品不能进入任何装备流程。
- 槽位声明必须是合法槽位。
- `occupied_slot_ids`、`equip_requirement`、后续 `exclusive_tags` 等字段也应纳入内容校验，而不是等到运行时报错。

### 2. 角色资格校验

- 当前基线：
  - 不限制职业
  - 不限制体型
  - 不限制属性
  - 不限制知识或成就
- Phase 2 首版支持：
  - 职业 / 职业标签要求
  - 基础属性阈值要求
  - 体型上下限
- 判定统一由 `PartyEquipmentService` 执行，并返回稳定错误码与 blockers 给 UI / headless。

### 3. 槽位决策与互斥

- 当前基线：
  - 若未指定槽位，优先选择第一个空槽
  - 若没有空槽，则覆盖声明顺序中的第一个槽位
- Phase 2 升级为“占用集替换”：
  - 先解析待装备物将占用哪些槽位
  - 再找出所有受影响的现有装备条目
  - 若任一旧装备需要回仓但事务不可提交，则整次换装失败
- 典型互斥规则：
  - 双手武器与副手装备互斥
  - 巨盾与双持副手互斥
  - 指定独占标签的饰品之间互斥

### 4. 预览与执行链路

- `PartyEquipmentService` 作为正式预览入口，推荐新增：
  - `preview_equip(member_id, item_id, requested_slot_id = &"")`
  - `preview_unequip(member_id, slot_id)`
  - `list_candidate_entries(member_id, slot_id)`
- `preview_equip()` 至少返回：
  - `success`
  - `error_code`
  - `blockers`
  - `entry_slot_id`
  - `occupied_slot_ids`
  - `displaced_entries`
  - `result_equipment_state`
  - `result_equipped_entries`
- `equip_item()` 不再自己重拼一套逻辑，而是：
  - 先走 `preview_equip()`
  - 通过后再按 preview 结果执行仓库事务与正式写入

### 5. 仓库事务

- 当前基线规则继续成立：
  - 装备时先从共享仓库移除目标物品
  - 若目标槽已有装备，则旧装备尝试回仓
  - 回仓失败时整次换装回滚
  - 卸装前先确认仓库可接收目标物品
- Phase 2 要把单件 preview 升级为“组合预览 + 原子提交”：
  - 在 `PartyWarehouseService` 内部对 `WarehouseState` 副本顺序模拟
  - 统一评估“先移除待装备物，再加入全部被替换装备”
  - 只有整体通过时，`PartyEquipmentService` 才真正落盘

### 6. 装备效果模型

- 已实现基线：
  - 静态属性修正来自 `ItemDef.attribute_modifiers`
  - 装备修正与职业、技能修正共走 `AttributeService`
  - 装备不会直接改写角色基础属性
- 正式扩展方向：
  - 静态层：属性、抗性、命中、闪避、移动、视野等常驻修正
  - 授予层：临时授予主动技能 / 被动技能 / 标签
  - 规则层：改变伤害类型、攻击距离、动作权限、状态抗性等
- 其中：
  - 静态层优先继续复用 `AttributeModifier`
  - 授予层建议通过 `granted_skill_ids` 并在角色快照构建时合并
  - 规则层不得散落在 UI 或战斗面板里，应统一落在角色快照或战斗单位生成链上

### 7. 与角色成长系统的关系

- 装备不是永久成长，不进入角色的固定技能学习记录或职业历史。
- 装备授予的主动技能若进入后续实现，应视为“临时可用技能”：
  - 卸下装备后失效
  - 不提升该技能的永久等级
  - 不参与技能合并的永久拥有关系
- 装备导致的属性变化仍应能影响：
  - 当前属性快照
  - 开战时生成的战斗单位属性
  - 命中、伤害、行动顺序等派生结果

### 8. 与战斗系统的关系

- 开战前：
  - `CharacterManagementModule` 基于当前 `EquipmentState` 构建属性快照与技能快照
- 开战时：
  - 战斗单位读取已结算后的属性与可用技能
- 战斗中：
  - 默认不允许换装
  - 战斗日志与详情面板只读展示装备结果，不反向改装备
- 战斗后：
  - 战斗结果默认不直接改写装备
  - 显式技能效果造成的装备破坏是正式例外，应在结算链路中写回装备真源

### 9. 装备破坏语义

- 装备破坏是显式技能效果，不等同于耐久损耗，也不等同于普通卸装。
- 首版推荐规则：
  - 单次效果默认最多破坏 1 件装备
  - 只从目标当前已装备槽位中选择候选
  - 破坏成功后，装备直接从 `EquipmentState` 中移除
  - 被破坏装备不回共享仓库
- 概率模型建议拆成两层：
  - 槽位层：不同部位有不同的被抽中权重或破坏系数
  - 装备层：不同 `rarity_tier` 或 `break_resistance_percent` 提供不同抗性
- 默认方向：
  - 高稀有度装备更难被破坏
  - 主手、副手、头部比身躯和饰品更容易成为破坏目标

### 10. 与获取来源的关系

- 当前基线：
  - 装备物品主要通过种子内容和测试命令注入共享仓库
- 后续正式接入时，建议统一支持：
  - 战利品掉落
  - 商店购买
  - 任务奖励
  - 制作 / 合成
- 原则：
  - 装备获取先进入共享仓库，再由玩家决定是否换装
  - 不推荐把绝大多数装备做成“自动穿上”
  - 自动装备只适合作为特殊奖励脚本或新手教学流程

## UI 与运行时

### 装备窗口目标

- 不继续把复杂换装逻辑塞进 `PartyManagementWindow` 或仓库窗口。
- 推荐新增独立 `PartyEquipmentWindow`。
- 推荐布局：
  - 左侧：队伍成员列表
  - 中间：当前成员的装备槽位、当前属性摘要、职业 / 标签限制
  - 右侧：共享仓库中过滤后的可装备候选
  - 底部：换装前后对比、错误提示、来源说明
- 必须支持的交互：
  - 选中成员
  - 点击槽位查看当前已装备物品
  - 过滤出该槽位可装备物品
  - 预览属性变化
  - 明确显示被替换物品会回到仓库
  - 明确显示无法装备的原因
- 首版 UI 不要求拖拽，列表点击即可。

### Runtime 接线

- `PartyManagementWindow` 增加 `equipment_requested(member_id)`。
- `GameRuntimeFacade` 新增装备窗口 modal 与对应 window data builder。
- 装备窗口负责：
  - 选择成员
  - 选择槽位
  - 展示当前装备
  - 展示共享仓库中过滤后的候选
  - 展示资格失败原因
  - 展示属性预览
  - 发出 equip / unequip 请求
- modal 首版不需要完整栈：
  - 从队伍管理进入装备窗口时，runtime 只保留一个轻量 return 标记
  - 装备窗口关闭时回到队伍管理，并恢复同一成员选中态

### 错误码与 blockers

- 保留单一 `error_code` 作为顶层错误类别，同时补 `blockers`：
  - `missing_profession`
  - `attribute_below_min`
  - `attribute_above_max`
  - `body_size_too_small`
  - `body_size_too_large`
  - `warehouse_blocked_swap`
- `GameRuntimeFacade` 继续负责把这些 blocker 转成玩家可读文本。
- headless 断言优先检查稳定的 `error_code / blockers`，不要直接绑死最终中文文案。

### 文本命令与自动化入口

- 当前已存在并继续保留：
  - `warehouse add <item_id> <quantity>`
  - `party equip <member_id> <item_id> [slot_id]`
  - `party unequip <member_id> <slot_id>`
- `snapshot party` 继续输出：
  - `attributes`
  - `equipment`
  - `equipment_count`
- 后续若加入装备需求、占槽或冲突规则，headless 命令必须同步输出稳定错误信息，确保回归脚本能断言失败原因。

## 持久化与版本策略

- 当前正式存档结构基线：
  - `PartyState`
    - `PartyMemberState`
      - `equipment_state`
        - `equipped_slots.<slot_id> = <item_id>`
- 当前已知 `PartyState.version = 2`。
- 若 Phase 2 启用“入口槽位条目”结构，应将其视为新的正式装备 schema，并直接迁移当前版本数据。
- 若后续再启用实例化与耐久，应继续进行下一次显式版本升级。
- 推荐演进方向：
  - Phase 2：
    - `equipped_slots.<entry_slot_id> = { item_id, occupied_slot_ids }`
  - Durability / Instance：
    - `equipped_slots.<entry_slot_id> = { item_id, instance_id, occupied_slot_ids }`
- 不建议长期保留“旧结构 + 新结构”双套主逻辑。

## 耐久与实例化（Deferred）

### 状态与边界

- 当前状态：`Draft / Deferred`
- 本节只固定耐久、锋锐、坚韧的设计方向与 ownership，不进入当前实现排期。
- 若继续推进装备系统，应先完成 Phase 2 的占槽 / 资格 / 预览 / 装备窗口主链。
- 耐久不能直接塞进 `ItemDef` 的模板值、现有 `slot -> item_id` 表示或当前仓库堆栈结构。

### 约束

- 同名装备也必须拥有各自独立的当前耐久。
- 角色正式装备真相源仍应留在 `PartyState -> PartyMemberState -> EquipmentState`。
- UI 继续只读展示和发起命令，不直接改装备状态。
- 材料、药草等普通堆叠物品不应该为了耐久被迫全部实例化。
- 战斗中的耐久变更必须能在战斗结束后稳定写回正式 `PartyState`。

### 推荐实现方向

- 只把装备做成实例，普通堆叠物品维持现状。
- `PartyWarehouseService` 负责实例创建、回仓与出仓。
- `PartyEquipmentService` 负责实例级装备、卸装、替换与损坏移除。
- 新增 `EquipmentDurabilityService` 负责常规耐久规则。

### 静态模板字段

- `ItemDef` 在耐久阶段最小新增：
  - `max_durability`
  - `equipment_traits`
  - `armor_toughness`
  - `weapon_sharpness`
- 首版推荐 trait：
  - `armor`
  - `melee_weapon`
  - `ranged_weapon`
  - `shield`
  - `indestructible`

### 常规近战磨损触发条件

- 只有满足以下条件时才进入常规耐久结算：
  - 本次效果实际造成了正伤害
  - 技能 `SkillDef.tags` 包含 `melee`
  - 目标是可持久化成员，或目标至少存在可修改的正式装备状态
- 首版不把 `ranged`、法术、地形效果、纯状态效果并入常规磨损。
- 首版不为饰品引入常规接触磨损。

### 护甲位选择与公式

- 首版不做命中部位系统，使用固定优先级：
  - `body`
  - `head`
  - `off_hand`（仅当该装备带 `shield` 或 `armor` trait）
- 护甲磨损公式：
  - `armor_delta = max(attacker_sharpness - defender_toughness, 0)`
  - 把 `armor_delta` 累加到护甲实例的 `armor_wear_progress`
  - 每累计满 `20` 点，护甲 `current_durability -= 1`
  - 余数保留

### 武器公式

- 武器耐久首版推荐公式：
  - `weapon_delta = 5 + max(defender_toughness - attacker_sharpness, 0)`
  - 把 `weapon_delta` 累加到武器实例的 `weapon_wear_progress`
  - 每累计满 `30` 点，武器 `current_durability -= 1`
  - 余数保留
- 这样能让护甲更明显体现“被压穿”，同时避免武器过快报废。

### 锋锐与坚韧来源

- 攻击者 `锋锐` 来源优先级：
  - 主手近战武器实例的 `weapon_sharpness`
  - 技能或效果参数中的 `contact_sharpness`
  - 默认值 `10`
- 防御者 `坚韧` 来源优先级：
  - 被选中护甲实例的 `armor_toughness`
  - 若没有有效护甲，则为 `0`

### 多段、多目标与提交时机

- 每个“成功造成正伤害的 hit”都单独累计一次耐久磨损。
- 多段攻击按命中段数累计。
- 多目标攻击按各自目标分别累计。
- 但“是否损坏并移除装备”建议在该次命令所有命中结算后统一提交，避免同一命令内出现顺序敏感。

### 损坏与刷新语义

- 当 `current_durability <= 0` 时：
  - 该装备实例立即判定为损坏
  - 直接从 `EquipmentState` 中移除
  - 不回共享仓库
  - 不视为普通卸装
- 只有在装备真正损坏并移除时，才需要刷新单位属性快照。
- 刷新时必须重新计算属性上限，但只对 `current_hp / current_mp / current_stamina / current_ap` 做上限裁剪，不做重置。

### 与显式装备破坏兼容

- `break_equipment_on_hit` 仍然保留为显式技能语义。
- 常规耐久磨损与显式装备破坏的差异：
  - 常规磨损：按 `锋锐 / 坚韧` 和累计阈值逐步掉耐久
  - 显式破坏：按技能规则直接摧毁装备
- 两者共享的底层结果应一致：
  - 最终都是销毁某个已装备实例
  - 最终都不回共享仓库
  - 若存在 `indestructible`，应在装备层统一拦截

## 阶段规划

### Phase 1：稳定基线

- 固定槽位
- 共享仓库联动
- 静态属性修正
- 当前版本存档稳定
- headless / 自动化回归

### Phase 2：正式可玩装备系统

- `EquipmentState` 升级为“入口槽位条目”
- `ItemDef` 补 `occupied_slot_ids`、`equip_requirement`、`equipment_type_id`
- 职业 / 属性 / 体型装备限制
- 双手武器与占用槽位模型
- `preview_equip` / `preview_unequip`
- 多件回仓原子事务
- 独立装备窗口
- 装备前后属性对比

### Phase 3：装备深度化

- 装备授予技能
- 品质分级与更丰富的装备类型
- 独占标签与更复杂的互斥规则
- Named gear 或唯一实例装备
- 装备破坏抗性与特殊保护标签

### Deferred Extension：耐久与实例化

- 装备实例化
- 常规耐久磨损
- 锋锐 / 坚韧
- 修理系统前置数据结构
- 与显式装备破坏统一的销毁语义

### Phase 4：高复杂度扩展

- 随机词缀
- 套装效果
- 强化、镶嵌
- 战斗中有限换装或特化职业机制

## 实施切片

### 当前推荐切片

- 先完成 Phase 2，不提前把耐久、品质或授予技能一起打包。
- Phase 2 最小落地包：
  - `EquipmentState` 升级到“入口槽位条目”
  - `occupied_slot_ids` 先只覆盖 `main_hand + off_hand` 的双手武器
  - `equip_requirement` 首版只启用职业 / 属性 / 体型
  - `PartyEquipmentService` 补 `preview_equip`、`preview_unequip`
  - `PartyWarehouseService` 补组合预览与原子提交
  - 新增独立装备窗口与属性对比

### 后续耐久切片

- 在 Phase 2 稳定后，再推进：
  - `EquipmentInstanceState`
  - `EquipmentDurabilityService`
  - 仓库中的装备实例载荷
  - 常规近战磨损
  - 损坏移除与战斗后写回

## Files To Change

### Phase 2

- `scripts/player/equipment/equipment_state.gd`
- `scripts/player/warehouse/item_def.gd`
- `scripts/player/warehouse/item_content_registry.gd`
- `scripts/systems/party_equipment_service.gd`
- `scripts/systems/party_warehouse_service.gd`
- `scripts/systems/character_management_module.gd`
- `scripts/systems/game_runtime_facade.gd`
- `scripts/ui/party_management_window.gd`
- 新增 `scripts/player/equipment/equipment_requirement.gd`
- 新增 `scenes/ui/party_equipment_window.tscn`
- 新增 `scripts/ui/party_equipment_window.gd`
- 如需要为 headless 暴露窗口态，再补：
  - `scripts/systems/game_text_command_runner.gd`
  - `scripts/utils/game_text_snapshot_renderer.gd`

### Durability Follow-up

- `scripts/player/warehouse/warehouse_stack_state.gd`
- `scripts/player/warehouse/warehouse_state.gd`
- `scripts/player/progression/party_state.gd`
- `scripts/systems/battle_runtime_module.gd`
- 新增 `scripts/player/equipment/equipment_instance_state.gd`
- 新增 `scripts/systems/equipment_durability_service.gd`
- `data/configs/items/*.tres` 中需要参与耐久的装备定义

## 测试计划

### 当前必须持续通过的基线测试

- 装备种子物品通过 `ItemContentRegistry` 校验
- 装备 / 卸装后仓库数量与角色槽位状态守恒
- 多槽位饰品自动装备时优先填空槽
- 装备属性能进入 `AttributeService` 快照，并在卸装后回退
- `PartyState` round-trip 后保留全部装备槽位
- 文本命令链路能够执行加仓、装备、卸装并反映到快照

### Phase 2 新增测试项

- 双手武器装备后：
  - `main_hand` 和 `off_hand` 都应被视为占用
  - 但属性修正只能结算一次
- 双手武器替换已有主手 + 副手时：
  - 被替换的 2 件装备必须整体回仓
  - 任一回仓失败都应完整回滚
- 职业要求不满足时：
  - `preview_equip()` 返回稳定 `error_code + blockers`
  - 实际 `equip_item()` 不得改写任何状态
- 属性要求必须按“最终预览负载且不含待装备物自带加成”判定
- 装备窗口的候选过滤、属性预览与最终结算一致
- `EquipmentState` 新结构 round-trip 后：
  - 单槽装备仍能恢复
  - 双手装备的 `occupied_slot_ids` 能恢复
  - 旧版单槽字典输入仍至少可升级为新结构

### Durability Follow-up 测试项

- 近战命中时，护甲按 `max(锋锐 - 坚韧, 0)` 正确累计并在 `20` 点阈值掉耐久
- 近战命中时，武器按 `5 + max(坚韧 - 锋锐, 0)` 正确累计并在 `30` 点阈值掉耐久
- 多段近战技能会按命中段数累计武器磨损
- 多目标近战技能会对各目标护甲分别累计磨损
- 耐久归零后，装备直接从目标槽位移除，不回仓
- 装备损坏后角色属性快照会更新，但不会错误重置 `AP`、体力或生命
- 旧版 `equipment_state` 与旧仓库装备堆栈在 round-trip 后能升级为满耐久实例

## Project Context Units Impact

- 本设计仍然落在既有单元边界内：
  - `CU-10` 共享仓库、物品定义与装备基础流转
  - `CU-09` 队伍管理窗口层
  - `CU-06` runtime 接线与 modal
  - `CU-19` 装备 / 仓库 / 文本回归
- 若正式实现耐久与实例化，还要同时补：
  - `CU-11`
  - `CU-12`
  - `CU-15`
  - `CU-16`
- 只有当正式实现决定新增 headless 装备窗口命令或快照域时，才需要把 `CU-21` 一起更新。

## 推荐下一步

1. 先按本文档的 Phase 2 切片完成 `EquipmentState` 表示升级、图形化装备窗口与属性对比，保证装备系统真正可用。
2. 再在同一 Phase 2 包内补齐职业限制、属性要求、双手武器与原子回仓事务，完成玩法闭环。
3. 在 Phase 2 稳定后，再讨论装备授予技能、品质分级、唯一实例与耐久，不提前把系统复杂度拉满。
