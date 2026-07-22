# 战斗武器骰与战斗内换装设计口径

更新日期：`2026-06-12`
核对日期：`2026-07-22`

## 状态

- 当前状态：`Implemented Design Record`
- 范围：BG3 风格武器 profile、武器骰、战斗内换装、battle-local 队伍共享背包、敌方攻击装备投影与掉落边界。
- 本文记录当前 C# 主线的设计真相源。旧 `.gd` 文件名、旧 `ItemDef` 顶层武器字段、`TYPE_EQUIP` / `TYPE_UNEQUIP` 拆分方案，以及旧 PR 分期讨论均不再作为实现依据。

## 真相源

### 物品与武器 Profile

- `scripts/player/warehouse/ItemDef.cs`
  - `ItemDef.weapon_profile` 是物品侧武器运行时真相源。
  - 不恢复旧 `weapon_attack_range` / `weapon_physical_damage_tag` 顶层字段 fallback。
- `scripts/player/warehouse/WeaponProfileDef.cs`
  - 持有 `weapon_type_id`、`training_group`、`range_type`、`family`、`damage_tag`、`attack_range`、`one_handed_dice`、`two_handed_dice`、`properties_mode`、`properties`。
  - 模板继承由 `WeaponProfileDef.Merge(...)` / `ItemContentRegistry` 的模板合并链完成。
- `scripts/player/warehouse/WeaponDamageDiceDef.cs`
  - 持有 `dice_count`、`dice_sides`、`flat_bonus`。
  - dice 校验是程序集内部规则，不作为 public Godot helper 扩散。
- `data/configs/items_templates/weapon_type_*_base.tres`
  - 当前覆盖 `docs/reference/rules/weapon_types_damage.md` 中整理的 31 类 BG3 基础 weapon type。

### 战斗投影

- `scripts/systems/battle/core/BattleUnitState.cs`
  - 战斗读取字段包括 `weapon_profile_kind`、`weapon_item_id`、`weapon_profile_type_id`、`weapon_attack_range`、`weapon_one_handed_dice`、`weapon_two_handed_dice`、`weapon_is_versatile`、`weapon_uses_two_hands`、`weapon_physical_damage_tag`。
- `scripts/systems/battle/core/WeaponProjection.cs`
  - 承载物品/敌方/空手/天生武器到战斗单位的投影数据。
- `scripts/systems/progression/CharacterManagementModule.cs`
  - 玩家侧装备投影从当前成员 `EquipmentState` 与 typed item catalog 生成。
- `scripts/systems/battle/runtime/BattleUnitFactory.cs`
  - 战斗开始时把成员装备与武器投影写入 `BattleUnitState`。
- `scripts/systems/battle/rules/BattleRangeService.cs`
  - 战斗射程读取 `BattleUnitState.weapon_attack_range` 并叠加临时修正，不回读旧属性字段或旧物品字段。

## 伤害与武器骰

基础公式：

```text
base_damage = weapon_dice_if_add_weapon_dice
            + effect_def.power
            + skill_dice
            + skill_dice_bonus
```

- `CombatEffectDef.add_weapon_dice = true` 是加入武器骰的唯一入口。
- `physical` damage 不自动加入武器骰。
- 多段 damage effect 各自读取 `add_weapon_dice`，允许每段重复计算当前武器骰。
- 暴击额外再掷一组 weapon dice / skill dice；`power` 与骰子 flat bonus 不因暴击重复。
- `CombatEffectDef.use_weapon_physical_damage_tag = true` 只负责把伤害类型替换为当前武器投影标签。
- `CombatEffectDef.requires_weapon = true` 才表达必须装备武器；空手与天生武器可提供射程 / 伤害骰，但不能满足 `requires_weapon`，也不参与武器熟练 / 武器精通。

事件 payload 口径：

- 单段 damage event 写出 `damage_dice_high_total_roll`、`skill_damage_dice_is_max`、`weapon_damage_dice_is_max` 及对应 reason 字段。
- 没有对应骰组时，相关布尔字段必须为 `false`。
- 顶层 result 只做 OR 汇总，不携带单段 reason。

伤害预览口径：

- `scripts/systems/battle/rules/BattleDamagePreviewRangeService.cs` 只计算非暴击基础伤害范围。
- 预览不调用正式 resolver，不消耗 RNG，不读取 target/status/shield/mastery/report。
- UI/HUD 需要展示时经由 `BattleHudAdapter` 做外层 payload 投影。

## 战斗内换装

当前采用完整战斗换装方案。

- 命令类型：`BattleCommand.TYPE_CHANGE_EQUIPMENT`
- 规则实现：`scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs`
- 玩家 UI 入口：`scripts/systems/battle/presentation/BattleHudAdapter.cs` 暴露换装快照，`scripts/ui/BattleMapPanel.cs` 发送换装命令。
- Headless 入口：`scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
  - `battle equip <slot_id> <item_id> [instance_id=...]`
  - `battle unequip <slot_id> [instance_id=...]`

规则：

- 战斗中所有装备槽都允许换装。
- 只能给当前行动单位自己换装。
- 每次成功换装统一消耗 `2 AP`。
- AP 不足时命令失败，不产生部分换装。
- 换装后若 `current_ap <= 0`，立即结束当前行动单位行动。
- 双手武器、副手、versatile 联动是同一个换装命令的副作用，仍只计费一次。
- 自动卸下的装备进入 battle-local 队伍共享背包 view。
- 背包容量或实例唯一所有权校验失败时，整条命令回滚。
- 装备需求以角色管理层的稳定有效属性快照校验。规则先从当前 battle-local `equipment_view` 复制 detached view，移除候选最终占用槽位会替换的装备，但不加入候选本身；因此被替换装备和候选自身都不能帮助跨过门槛，其他不冲突装备可以。
- 需求校验不读取 `BattleUnitState.attribute_snapshot`，避免临时战斗状态满足永久装备门槛。preview 与执行共用同一个 evaluator；HUD 的装备预览缓存签名同时包含稳定属性 fingerprint，身份、职业、永久奖励或 trait 变化会令缓存失效。
- 换装后重建属性快照；`current_hp > new_hp_max` 时 clamp 到新上限，未超过新上限时保持当前 HP，不比例缩放，也不因提高上限而治疗。

## Battle-local 背包与战后回写

当前 `PartyState.warehouse_state` 的运行时语义是队伍共享背包：队伍随身携带的堆叠物与装备实例池。历史类名中仍可能使用 `Warehouse`，但新文档和新逻辑应按“队伍共享背包”理解。

- 战斗开始时，`BattleState.party_backpack_view` 从 `PartyState.warehouse_state` 复制。
- 每个友军的 `BattleUnitState.equipment_view` 从 `PartyMemberState.equipment_state` 复制。
- 战斗中换装只修改 `BattleState.party_backpack_view` 与对应单位的 `equipment_view`，不直接 mutate party 背包或成员装备。
- 战斗结束后，`scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs` 把 battle-local 背包与装备 view 回写到 `PartyState`。
- 据点入口当前通过 `party_warehouse` 打开的仍是同一份队伍共享背包。
- 未来“据点共享仓库”应是独立世界 / 据点状态，不复用 `PartyState.warehouse_state`；战斗中不能访问据点共享仓库。

## 存档与兼容边界

- 不支持战斗中存档。
- 战斗开始时启用 battle save lock；lock 中 `GameSession.save_game_state()` 只标记 dirty，不落盘。
- lock 中 `flush_game_state()` 返回 busy；战斗结束解锁后再统一持久化 pending dirty state。
- 不扩展 `SaveSerializer` 的 battle payload 来保存战斗中换装状态。
- 不添加旧武器字段 fallback：
  - 旧 `weapon_attack_range` / `weapon_physical_damage_tag` 不作为运行时来源。
  - 资源校验和仓库模板回归应拒绝旧裸字段路径。

## 敌方攻击装备与掉落

- `scripts/enemies/EnemyTemplateDef.cs`
  - `attack_equipment_item_id` 是非 `beast` 敌人的攻击装备来源。
  - 非 `beast` 模板必须显式引用一个有效武器 `ItemDef.weapon_profile`。
  - 旧 `attribute_overrides.weapon_attack_range` / `weapon_physical_damage_tag` 是配置错误。
- `beast` 模板默认投影天生武器：
  - kind: `natural_weapon`
  - dice: `1D6`
  - 默认 damage tag: `physical_blunt`
  - range type: melee
  - range: `1`
  - 可通过 `natural_weapon_damage_tag` 或标签覆写为 pierce / slash / blunt。
- 若运行时遇到缺失或无效的非 `beast` 攻击装备，投影降级为空手：
  - kind: `unarmed`
  - dice: `1D4`
  - damage tag: `physical_blunt`
  - range: `1`
- 敌人的攻击装备只影响战斗投影，不自动成为死亡掉落。
- 敌人死亡掉落只读 `EnemyTemplateDef.drop_entries`；如果要让攻击装备掉落，必须显式写进掉落表或实现单独缴械 / 掉落规则。

## 推荐回归

改动 weapon profile、weapon dice、战斗内换装或 battle-local 背包语义时，优先跑以下 focused 回归：

```powershell
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
godot --headless -s res://tests/warehouse/run_party_warehouse_batch_swap_regression.cs
godot --headless -s res://tests/equipment/run_party_equipment_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_battle_weapon_dice_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_battle_damage_preview_range_contract_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_battle_equipment_requirement_rules_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_unit_factory_weapon_projection_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_loot_drop_luck_regression.cs
godot --headless -s res://tests/text_runtime/commands/run_battle_equipment_text_command_regression.cs
```

战斗 simulation / balance runner 不属于这类设计口径的常规验证。只有在调整数值策略或明确要做平衡实验时才运行。

## 相关文档

- `docs/reference/rules/weapon_types_damage.md`
- `docs/proposals/inventory/equipment_system.md`
- `docs/archive/design-decisions/2026-05-11/warehouse_equipment.md`
- `docs/design/project_context_units.md`
