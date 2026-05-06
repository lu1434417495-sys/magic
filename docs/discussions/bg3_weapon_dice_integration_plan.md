# BG3 武器骰整合 + 战斗换装实现结论

更新日期：`2026-04-26`

## 状态

- 当前状态：`Implemented Record`
- 范围：记录 BG3 风格武器 profile / 武器骰、战斗内换装、队伍共享背包 battle-local view、敌方攻击装备投影与掉落边界的最终实现口径。
- 旧讨论中的 PR1 / PR2 / PR2-a / PR2-b 拆分、旧 `ItemDef` 顶层武器字段、以及 `TYPE_EQUIP` / `TYPE_UNEQUIP` 备选方案已经失效；本文件只保留当前落地结论。

---

## 1. 当前真相源

### 物品与武器 profile

- `scripts/player/warehouse/item_def.gd`
  - `ItemDef.weapon_profile` 是武器运行时真相源。
  - `ItemDef` 不再暴露旧 `weapon_attack_range` / `weapon_physical_damage_tag` 顶层字段；访问器只读 `weapon_profile`，不做旧字段 fallback。
- `scripts/player/warehouse/weapon_profile_def.gd`
  - 持有 `weapon_type_id`、`training_group`、`range_type`、`family`、`damage_tag`、`attack_range`、`one_handed_dice`、`two_handed_dice`、`properties_mode`、`properties`。
  - 模板继承由 `WeaponProfileDef.merge()` 负责，`ItemContentRegistry.merge_with_template()` 只委托 profile 合并。
- `scripts/player/warehouse/weapon_damage_dice_def.gd`
  - 持有 `dice_count`、`dice_sides`、`flat_bonus`。
- 当前种子武器已覆盖 `docs/design/weapon_types_damage.md` 中 31 类 BG3 基础 weapon type：
  - 每类 weapon type 都有一个 `data/configs/items_templates/weapon_type_*_base.tres` 模板。
  - 每个模板至少有一个正式 `data/configs/items/*.tres` 装备实例引用。
  - 仍保留原有正式实例：`bronze_sword` -> Shortsword、`iron_greatsword` -> Greatsword、`militia_axe` -> Handaxe、`watchman_mace` -> Mace。
  - `scout_dagger` 仍不作为正式种子物品；Dagger 类型由 `iron_dagger` 落地。

### 战斗投影

- `BattleUnitState` 持有战斗读取字段：
  - `weapon_profile_kind`
  - `weapon_item_id`
  - `weapon_profile_type_id`
  - `weapon_attack_range`
  - `weapon_one_handed_dice`
  - `weapon_two_handed_dice`
  - `weapon_is_versatile`
  - `weapon_uses_two_hands`
  - `weapon_physical_damage_tag`
- 玩家侧投影从 `CharacterManagementModule.get_member_weapon_projection_for_equipment_view()` 生成。
- 战斗射程统一由 `BattleRangeService` 读取 `BattleUnitState.weapon_attack_range` 并叠加临时修正，不再从 `attribute_snapshot.weapon_attack_range` 或旧物品字段读取。

---

## 2. 伤害与骰子事件

最终公式：

```text
base_damage = weapon_dice_if_add_weapon_dice
            + effect_def.power
            + skill_dice
            + skill_dice_bonus
```

- `params.add_weapon_dice = true` 是唯一开启武器骰的入口；`physical` damage 不自动加武器骰。
- 多段 damage effect 独立读取 `add_weapon_dice`，允许每段重复计算当前武器骰。
- 暴击时额外再掷一组 weapon dice / skill dice；`power` 与骰子 flat bonus 不因暴击重复。
- `params.use_weapon_physical_damage_tag = true` 只负责把伤害类型替换为当前武器投影标签。
- `params.requires_weapon = true` 才表达必须装备武器；空手与天生武器可以提供射程 / 伤害骰，但不能满足 `requires_weapon`，也不参与武器熟练 / 武器精通。
- 单段 damage event 独立写出：
  - `damage_dice_high_total_roll`
  - `skill_damage_dice_is_max`
  - `weapon_damage_dice_is_max`
  - 对应 reason 字段
- 没有对应骰组时相关字段必须为 false；顶层 result 只做 OR 汇总，不携带单段 reason。
- `BattleDamagePreviewRangeService` 只给非暴击基础伤害范围，不调用正式 resolver，不消耗 RNG，不读取 target/status/shield/mastery/report。

---

## 3. 战斗内换装

已选方案：完整战斗换装。

- 命令形态：统一使用 `BattleCommand.TYPE_CHANGE_EQUIPMENT`，通过 payload 表达 `equip` / `unequip`、slot、item、instance。
- 入口：
  - 玩家 UI：`BattleHudAdapter` 暴露换装快照，`BattleMapPanel` 发 `TYPE_CHANGE_EQUIPMENT`。
  - Headless：`battle equip <slot_id> <item_id> [instance_id=...]` 与 `battle unequip <slot_id> [instance_id=...]`。
- 规则：
  - 战斗中所有装备槽都允许换装。
  - 只能给当前行动单位自己换装。
  - 每次成功换装统一消耗 `2 AP`。
  - AP 不足时命令失败，不产生部分换装。
  - 换装后若 `current_ap <= 0`，立即结束当前行动单位行动。
  - 双手武器 / 副手 / versatile 联动是同一个换装命令的副作用，仍只计费一次。
  - 自动卸下的装备进入 battle-local 队伍共享背包 view；背包容量或实例唯一所有权校验失败时整条命令回滚。
  - 换装重建属性快照；`current_hp > new_hp_max` 时 clamp 到新上限，未超过新上限时保持当前 HP，不比例缩放，也不因提高上限而治疗。

---

## 4. 队伍共享背包与未来据点共享仓库

当前实现里 `PartyState.warehouse_state` 的语义是队伍共享背包：队伍随身携带的堆叠物与装备实例池。历史脚本 / 文件名仍使用 `warehouse`，但文档和新逻辑应按“队伍共享背包”理解。

- 战斗开始时，`BattleState.party_backpack_view` 从 `PartyState.warehouse_state` 复制。
- 每个友军的 `BattleUnitState.equipment_view` 从 `PartyMemberState.equipment_state` 复制。
- 战斗中换装只修改 `BattleState.party_backpack_view` 与对应单位的 `equipment_view`，不直接 mutate party 背包或成员装备。
- 战斗结束后，`GameRuntimeFacade` 把 battle-local 背包与装备 view 回写到 `PartyState`。
- 据点入口当前通过 `party_warehouse` 打开的仍是同一份队伍共享背包。
- 未来“据点共享仓库”是独立世界 / 据点状态，不复用 `PartyState.warehouse_state`；战斗中不能访问据点共享仓库。

---

## 5. 存档与兼容边界

- 不支持战斗中存档。
- 战斗开始时启用 battle save lock；lock 中 `GameSession.save_game_state()` 只标记 dirty，不落盘。
- lock 中 `flush_game_state()` 返回 busy；战斗结束解锁后再统一持久化 pending dirty state。
- 本轮不扩 `SaveSerializer` 的 battle payload。
- 不添加旧武器字段 fallback：
  - 旧 `weapon_attack_range` / `weapon_physical_damage_tag` 不作为运行时来源。
  - 资源校验和仓库模板回归都应拒绝或忽略旧裸字段路径。

---

## 6. 敌方攻击装备与掉落

- `EnemyTemplateDef.attack_equipment_item_id` 是非 `beast` 敌人的攻击装备来源。
- 非 `beast` 模板必须显式引用一个有效武器 `ItemDef.weapon_profile`；旧 `attribute_overrides.weapon_attack_range` / `weapon_physical_damage_tag` 是配置错误。
- `beast` 模板默认投影天生武器：
  - `natural_weapon`
  - `1D6`
  - 默认 `physical_blunt`
  - melee, range 1
  - 可通过 `natural_weapon_damage_tag` 或标签覆写为 pierce / slash / blunt
- 若运行时仍遇到缺失或无效的非 `beast` 攻击装备，投影降级为空手：
  - `unarmed`
  - `1D4`
  - `physical_blunt`
  - range 1
- 敌人的攻击装备只影响战斗投影，不自动成为死亡掉落。
- 敌人死亡掉落只读 `EnemyTemplateDef.drop_entries`；如果要让攻击装备掉落，必须显式写进掉落表或实现单独缴械 / 掉落规则。

---

## 7. 已解决的工程问题

- PR1 / PR2 / PR2-a / PR2-b 拆分问题已关闭：当前主线已经落地到 `WeaponProfileDef`、battle-local 背包 / 装备 view、`TYPE_CHANGE_EQUIPMENT`、UI / headless 接入与战后回写。
- 命令形态已关闭：采用单一 `TYPE_CHANGE_EQUIPMENT`，不拆 `TYPE_EQUIP` / `TYPE_UNEQUIP`。
- 战后回写不变量已关闭：实例冲突、容量异常或所有权不一致视为内部状态错误，不是正常玩法分支。

---

## 8. 建议回归命令

文档收口或后续改动 weapon dice / battle-local 换装语义时，建议跑以下 focused headless 回归：

```bash
godot --headless --script tests/warehouse/run_item_template_inheritance_regression.gd
godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
godot --headless --script tests/equipment/run_party_equipment_regression.gd
godot --headless --script tests/runtime/run_resource_validation_regression.gd
godot --headless --script tests/battle_runtime/run_battle_weapon_dice_regression.gd
godot --headless --script tests/battle_runtime/run_battle_damage_preview_range_contract_regression.gd
godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/battle_runtime/run_wild_encounter_regression.gd
godot --headless --script tests/battle_runtime/run_battle_loot_drop_luck_regression.gd
godot --headless --script tests/text_runtime/run_battle_equipment_text_command_regression.gd
```

本次 WPNDICE_25 文档收口没有运行 battle simulation；不把以下 battle simulation 入口放进常规回归：

```bash
godot --headless --script tests/battle_runtime/run_battle_simulation_regression.gd
godot --headless --script tests/battle_runtime/run_battle_ai_vs_ai_simulation_regression.gd
godot --headless --script tests/battle_runtime/run_battle_balance_simulation.gd
```

原因：这些是数值模拟、AI 对战模拟和 balance/report analysis 入口，适合做平衡实验或模拟报告分析；本故事只是文档收口，不改数值策略，也不需要生成 simulation report。

---

## 9. 关键引用

- BG3 武器原始资料：`docs/design/weapon_types_damage.md`
- 物品定义：`scripts/player/warehouse/item_def.gd`
- 武器 profile：`scripts/player/warehouse/weapon_profile_def.gd`
- 武器骰资源：`scripts/player/warehouse/weapon_damage_dice_def.gd`
- 物品模板合并：`scripts/player/warehouse/item_content_registry.gd`
- 队伍共享背包服务：`scripts/systems/party_warehouse_service.gd`
- 战斗单位状态：`scripts/systems/battle_unit_state.gd`
- 战斗状态：`scripts/systems/battle_state.gd`
- 战斗换装命令：`scripts/systems/battle_command.gd`
- 战斗换装事务：`scripts/systems/battle_runtime_module.gd`
- 战斗射程：`scripts/systems/battle_range_service.gd`
- 伤害结算：`scripts/systems/battle_damage_resolver.gd`
- 伤害预览：`scripts/systems/battle_damage_preview_range_service.gd`
- 敌人模板：`scripts/enemies/enemy_template_def.gd`
- 战后回写：`scripts/systems/game_runtime_facade.gd`
- headless battle 换装入口：`scripts/systems/game_text_command_runner.gd`
