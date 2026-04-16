# 模块重构状态与后续计划

> 更新日期：2026-04-16
> 分析范围：当前仓库 `scripts/` 运行时主链、对应 headless/regression，以及 `docs/design/project_context_units.md` 已声明的所有权边界
> 说明：旧版 `Phase 0-4` 中的大部分结构拆分已经落地。本文件不再重复“假设尚未开始”的拆分计划，而改为记录当前仓库的真实状态、剩余问题和后续结构债务。

---

## 一、使用方式

- 当前模块边界以 `docs/design/project_context_units.md` 为真相源。
- 本文件只记录：
  - 已经完成、因此不应再重复推进的拆分结果
  - 当前仍然有效的修复项
  - 尚未完成、但仍值得保留的结构性后续工作
- 如果后续运行时主链再次发生边界迁移，应先更新 `project_context_units.md`，再回写本文件。

---

## 二、当前模块快照

以下体量统计基于 2026-04-16 当前仓库文件行数，仅用于判断“是否仍然偏重”，不再作为唯一拆分依据。

| 文件 | 当前体量 | 当前定位 | 当前判断 |
|------|----------|----------|----------|
| `scripts/systems/game_runtime_facade.gd` | 2306 行 | 世界/战斗 runtime 总协调器 | 仍偏重，但已不是旧版分析中的单体 God Object |
| `scripts/systems/battle_runtime_module.gd` | 2155 行 | 战斗执行流与指令分发 | 仍偏重，但多个独立子系统已拆出 |
| `scripts/systems/world_map_system.gd` | 826 行 | 场景接线、输入捕获、UI 渲染同步 | 已明显瘦身，不再是指令 owner |
| `scripts/systems/world_map_runtime_proxy.gd` | 369 行 | 场景层稳定读写表面 | 当前正式 scene/runtime 边界的一部分 |
| `scripts/systems/game_session.gd` | 1111 行 | 存档槽、运行时缓存、世界启动 | 序列化已拆出，但 session 仍是高风险单元 |
| `scripts/systems/save_serializer.gd` | 687 行 | save payload / meta / legacy decode | 已成为正式序列化 owner |
| `scripts/player/progression/design_skill_catalog.gd` | 457 行 | catalog loader + spec 转换桥 | 已显著瘦身，但仍未完全数据资产化 |
| `scripts/systems/character_management_module.gd` | 935 行 | 队伍成长、奖励、成就、属性桥接 | 仍偏重，但战斗单位构建已移出 |

---

## 三、已完成的重构落地

### 3.1 `GameRuntimeFacade` 已完成第一轮拆分

以下子模块已经存在并参与正式主链，不应再按旧文档当作“待创建模块”处理：

- `battle_session_facade.gd`
- `game_runtime_battle_selection.gd`
- `game_runtime_battle_selection_state.gd`
- `game_runtime_settlement_command_handler.gd`
- `game_runtime_warehouse_handler.gd`
- `game_runtime_party_command_handler.gd`
- `game_runtime_reward_flow_handler.gd`
- `game_runtime_snapshot_builder.gd`

当前结论：

- `GameRuntimeFacade` 仍是总协调器，但战斗选择、据点动作、仓库命令、队伍管理、奖励流和快照构建已经下沉。
- 后续不应再把队伍/奖励/仓库/据点细则重新塞回 `GameRuntimeFacade`。

### 3.2 `BattleRuntimeModule` 已完成第一轮子系统拆分

以下子系统已经存在：

- `battle_charge_resolver.gd`
- `battle_repeat_attack_resolver.gd`
- `battle_terrain_effect_system.gd`
- `battle_rating_system.gd`
- `battle_unit_factory.gd`

当前结论：

- `BattleRuntimeModule` 仍负责回合调度、技能路由和统一批处理。
- 冲锋、连击、地形效果、评分、单位构建不应再回卷到 module 主文件。
- 仍未真正抽出的独立真相源是“命中系统 / 命中预览”。

### 3.3 `WorldMapSystem` 已不再是 runtime 命令 owner

当前正式边界是：

```text
WorldMapSystem
  -> WorldMapRuntimeProxy
      -> GameRuntimeFacade
```

当前结论：

- `WorldMapSystem` 主要承担 `_ready`、输入捕获、窗口信号绑定、场景渲染同步。
- 旧文档中“`world_map_system.gd` 与 facade 1:1 委托重叠”的判断已过时。
- 当前存在的命令面重复主要是 `WorldMapRuntimeProxy -> GameRuntimeFacade` 的稳定适配层，不应误判为旧的双 owner 问题。

### 3.4 `GameSession` 的序列化拆分已落地

- `save_serializer.gd` 已存在并承担 save payload / meta / legacy decode。
- 旧文档中的 “Phase 4a: 抽出 SaveSerializer” 已完成。

### 3.5 `DesignSkillCatalog` 已完成首轮去大文件化

当前结构已经变为：

- `design_skill_catalog.gd`
- `design_skill_catalog_warrior_specs.gd`
- `design_skill_catalog_archer_specs.gd`
- `design_skill_catalog_mage_specs.gd`

当前结论：

- 旧文档中“1227 行单文件 catalog”的判断已过时。
- 但它仍然属于“代码承载数据”的过渡阶段，尚未完成资源化/资产化。

### 3.6 `CharacterManagementModule` 已移出战斗单位构建主职责

当前正式战斗单位刷新入口在 `battle_unit_factory.gd`，`CharacterManagementModule` 已不再承担旧文档中点名的 `build_battle_party / refresh_battle_unit` 主链职责。

---

## 四、当前仍有效的问题

以下问题在当前仓库中仍然成立，且应优先于新的大规模重构动作处理。

### P0.1 `game_runtime_facade.gd` — `command_return_from_submap()` 仍缺少 battle/modal 守卫

**症状**

- `command_return_from_submap()` 当前在确认“确实位于子地图”后，直接进入 `_return_from_active_submap()`。
- 它没有检查：
  - 当前是否仍在 battle active
  - 当前是否仍有 modal 打开
- 这意味着 headless/文本命令路径理论上仍可能在不安全时机修改：
  - `active_submap_id`
  - 玩家坐标
  - active map 上下文

**当前 owner**

- `scripts/systems/game_runtime_facade.gd`

**执行步骤**

1. 在 `command_return_from_submap()` 的子地图检查之后追加 battle guard。
2. 追加 modal guard。
3. 拒绝时统一返回 `_command_error(...)`，不要静默 return。
4. 增加专门的 headless regression，显式覆盖：
   - battle active 时调用
   - modal 打开时调用
   - 正常 submap return

**验收**

- battle active 时，`command_return_from_submap()` 返回失败，battle/world 状态不变。
- modal 打开时，`command_return_from_submap()` 返回失败，submap 上下文不变。
- 正常 submap return 仍然成功。

**备注**

- 现有 `tests/world_map/runtime/run_world_submap_regression.gd` 当前被 `ashen_intersection_world_map_config.tres` 解析错误阻塞，不能单独作为此项的唯一验收。

---

### P0.2 仓库窗口“使用技能书”路径存在库存不同步回归

**症状**

- `godot --headless --script tests/warehouse/run_party_warehouse_regression.gd` 当前失败。
- 失败断言为：
  - `仓库窗口使用技能书后应同步扣除库存。 | actual=1 expected=0`

**当前 owner 候选**

- `scripts/systems/game_runtime_warehouse_handler.gd`
- `scripts/systems/party_item_use_service.gd`
- `scripts/systems/party_warehouse_service.gd`
- 仓库 modal 的运行时刷新 / 持久化链路

**执行步骤**

1. 先对比：
   - 直接 service 路径使用技能书
   - 仓库窗口按钮路径使用技能书
2. 确认成功使用后是否同时完成：
   - 从 `PartyState.warehouse_state` 正式扣除物品
   - 运行时快照/窗口数据刷新
   - 需要时持久化回写
3. 修复后保持“重复学习失败时不吞库存”的既有契约。

**验收**

- `tests/warehouse/run_party_warehouse_regression.gd` 通过。

---

### P1.1 `design_skill_catalog.gd` — 仍未处理根级 `kind = cast_variant`

**症状**

- `design_skill_catalog.gd` 当前 `match kind` 只处理：
  - `active`
  - `special`
  - `cast_variant_hint`
  - `ground_variant`
- `design_skill_catalog_mage_specs.gd` 中仍存在根级：
  - `kind = cast_variant`
  - `skill_id = mage_fossil_to_mud`
- 这些条目当前不会被注册为技能定义。

**当前 owner**

- `scripts/player/progression/design_skill_catalog.gd`

**执行步骤**

1. 为根级 `kind = cast_variant` 增加分支。
2. 继续沿用当前 repo 的兼容语义：
   - 它是“根级 alias / 兼容表达”
   - 不是新增一套独立 runtime
3. 为 `mage_fossil_to_mud` 增加回归，确保 `get_skill_def(skill_id)` 可查到。

**验收**

- `mage_fossil_to_mud` 可被 catalog/registry 成功查到。
- progression 回归继续通过。

---

### P1.2 `game_text_snapshot_renderer.gd` — 文本快照仍未渲染 battle-start confirm

**症状**

- `GameRuntimeSnapshotBuilder` 已经输出：
  - `start_confirm_visible`
  - `start_prompt`
- `GameTextSnapshotRenderer` 当前 BATTLE 段仍未渲染这两个字段。
- 文本 runtime/REPL 无法直接看到 battle-start confirm 流。

**当前 owner**

- `scripts/utils/game_text_snapshot_renderer.gd`

**执行步骤**

1. 在 BATTLE 段渲染中读取 `start_confirm_visible`。
2. 若为 true，渲染 battle-start confirm 行。
3. 保持已有文本快照稳定性，不改变无关字段顺序。

**验收**

- 触发 battle-start confirm 时，文本快照可见对应提示。
- `tests/text_runtime/run_text_command_regression.gd` 通过。

---

## 五、当前验证阻塞

以下问题不是本文件原始旧计划的一部分，但它们会直接影响“还能否继续把某些模块当稳定基线”。

### V0.1 `ashen_intersection_world_map_config.tres` 当前存在解析错误

**现状**

- `tests/world_map/runtime/run_world_submap_regression.gd` 当前失败于资源加载阶段。
- 失败点不是 submap runtime 本身，而是：
  - `data/configs/world_map/ashen_intersection_world_map_config.tres:472`
  - Godot 文本资源解析失败

**影响**

- 当前无法用该回归直接验证 `command_return_from_submap()` 的 battle/modal guard。
- 任何依赖该 world config 的运行链验证都先被数据错误短路。

**建议**

- 先修 world config，或新增不依赖该资源的独立 submap regression fixture。

---

## 六、已被当前实现取代的旧项

下表用于明确：这些旧结论不应再作为当前执行计划继续推进。

| 旧项 | 当前状态 | 当前真相 |
|------|----------|----------|
| `0.1 close_settlement_modal` 重复定义 | 已完成 | 当前 `game_runtime_facade.gd` 只有一处 `close_settlement_modal()` |
| `0.3 v5 装备堆叠存档应迁移到 v6` | 已被当前契约取代 | 当前正式行为是“明确拒绝 legacy warehouse equipment stacks”，对应 warehouse regression 也以 `ERR_INVALID_DATA` 为预期 |
| `0.4 get_entry_slot_ids()` 多槽重复返回 | 已失效 | 当前 `EquipmentState` 已采用入口槽模型；`get_entry_slot_ids()` 不再复现旧文档描述的重复结算问题 |
| `0.5 冲锋失败应完整回滚` | 已被当前战斗语义取代 | 当前 smoke 回归明确要求“中途受阻保留已完成位移，不回滚”；`battle_charge_resolver.gd` 也已不再使用旧文档描述的 snapshot/restore API |
| `Phase 1` 抽 `RuntimeSnapshotBuilder / Settlement / Warehouse / BattleSession` | 已完成 | 对应子模块都已存在并在主链使用 |
| `Phase 2` 抽 `Charge / TerrainEffect / Rating / UnitFactory` | 已完成 | 这些子系统均已存在；`RepeatAttackResolver` 也已拆出 |
| `Phase 3` world_map_system 瘦身为纯 View | 已以新形态完成 | 现在是 `WorldMapSystem -> WorldMapRuntimeProxy -> GameRuntimeFacade` 三层；不要按旧方案重新合并 |
| `Phase 4a` 抽 `SaveSerializer` | 已完成 | `save_serializer.gd` 已是正式 owner |
| `Phase 4b` 设计技能 catalog 数据化 | 部分完成 | 已拆成 loader + specs，但尚未 fully assetized |

---

## 七、当前保留的结构性后续工作

这些工作不是立刻阻塞主链的 bug，但仍然值得保留为中期结构债务。

### S1 `BattleHitResolver` 仍未成为单一真相源

当前命中相关逻辑仍分散在：

- `battle_runtime_module.gd`
- `battle_damage_resolver.gd`
- 未来可能继续影响 `BattlePreview / HUD / AI`

如果后续要正式引入更复杂的命中体系，例如：

- `THAC0 + 负 AC + d20`
- natural 1 / natural 20
- 优势 / 劣势
- 逐目标命中预览
- deterministic battle-seeded RNG

则应把命中判定、命中预览和日志口径统一抽到 `battle_hit_resolver.gd`。

### S2 `DesignSkillCatalog` 仍处于“代码承载数据”的过渡态

当前 spec 文件已经把单文件压力拆开，但仍属于：

- 大量字典字面量
- 代码文件承载内容数据
- review / diff 负担较高

中期目标仍应是：

1. 把技能/职业/成就内容迁移到声明式资源或数据文件。
2. catalog/registry 只保留加载、校验、缓存和兼容桥接逻辑。

### S3 `GameRuntimeFacade` 仍需控制继续膨胀

当前 facade 虽已拆分，但仍是大型协调器。后续规则新增时应坚持以下边界：

- 战斗选择状态继续留在 `GameRuntimeBattleSelection(+State)`
- 仓库流程继续留在 `GameRuntimeWarehouseHandler`
- 奖励/晋升流程继续留在 `GameRuntimeRewardFlowHandler`
- 据点动作继续留在 `GameRuntimeSettlementCommandHandler`
- 快照继续留在 `GameRuntimeSnapshotBuilder`

不要把“只是为了省一次跳转”的逻辑重新塞回 facade。

---

## 八、建议执行顺序

### Phase A — 修复当前红灯

1. 修复 `ashen_intersection_world_map_config.tres` 解析错误，或补一个不依赖该配置的 submap regression。
2. 为 `command_return_from_submap()` 增加 battle/modal guard，并补 headless regression。
3. 修复仓库窗口使用技能书后的库存同步问题。

### Phase B — 修复当前中优先级缺口

1. 为 `design_skill_catalog.gd` 增加 `kind = cast_variant` 支持。
2. 为 `game_text_snapshot_renderer.gd` 增加 battle-start confirm 文本输出。

### Phase C — 再考虑结构债务

1. 评估是否正式抽出 `BattleHitResolver`。
2. 继续推进 `DesignSkillCatalog` 资源化/资产化。

---

## 九、当前验证状态（2026-04-16）

### 已确认通过

- `godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd`
- `godot --headless --script tests/progression/run_progression_tests.gd`
- `godot --headless --script tests/equipment/run_party_equipment_regression.gd`
- `godot --headless --script tests/text_runtime/run_text_command_regression.gd`

### 当前失败

- `godot --headless --script tests/warehouse/run_party_warehouse_regression.gd`
  - 当前可见失败点：仓库窗口技能书使用后库存未同步扣除
- `godot --headless --script tests/world_map/runtime/run_world_submap_regression.gd`
  - 当前失败原因：`ashen_intersection_world_map_config.tres` 解析错误

---

## 十、结论

- 旧版 `module_refactor_plan.md` 中描述的多数“大拆分动作”已经完成，继续照旧文档施工会造成重复劳动或误判。
- 当前最值得处理的不是再开新一轮大规模重构，而是先清理：
  - submap return 安全守卫
  - warehouse modal 库存同步
  - `cast_variant` 根级兼容
  - battle-start confirm 文本快照缺口
- 后续结构工作应以“保持现有边界不回退”为前提，而不是重新把系统合并回旧的大文件形态。
