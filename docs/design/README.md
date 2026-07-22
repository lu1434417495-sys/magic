# 当前架构与实现文档

更新日期：`2026-07-23`

本目录只保存已经落地、可由当前源码和测试核实的项目架构与实现说明。待实现或部分实现的设计统一放在 [`../proposals/`](../proposals/README.md)，内容策划统一放在 [`../content/`](../content/README.md)。

## 总入口

- [`project_context_units.md`](project_context_units.md)：按问题类型选择源码、邻接单元和细节文档。
- [`foundations/skill_centric_game_architecture.md`](foundations/skill_centric_game_architecture.md)：当前整游戏的玩法系统定位与架构原则。

## 平台与生命周期

- [`platform/godotsharp_lifecycle.md`](platform/godotsharp_lifecycle.md)：进程内容根、plain C# 快照、投影租约和退出屏障。

## 世界与据点

- [`world/world_map_module.md`](world/world_map_module.md)：世界地图当前模块规格。
- [`world/settlement_module.md`](world/settlement_module.md)：据点服务当前模块规格。

## 角色成长

- [`progression/character_module.md`](progression/character_module.md)：Party、CharacterManagement、成长与奖励主链。
- [`progression/trait_system.md`](progression/trait_system.md)：通用 trait 的内容、持久态、合并和战斗投影。
- [`progression/faith_system.md`](progression/faith_system.md)：信仰内容、升阶校验和奖励入队。
- [`progression/fate_runtime.md`](progression/fate_runtime.md)：Fortuna、Misfortune、low-luck 与战斗 fate 事件链。

## 仓库与装备

- [`inventory/warehouse_equipment_module.md`](inventory/warehouse_equipment_module.md)：共享仓库、装备实例与物品使用主链。

## 战斗

- [`battle/runtime_module.md`](battle/runtime_module.md)：战斗模块拓扑、生命周期和世界写回。
- [`battle/objective_runtime.md`](battle/objective_runtime.md)：正式 BattleEncounter、P0 歼灭目标、原子终局和 typed outcome。
- [`battle/skill_runtime.md`](battle/skill_runtime.md)：技能内容、可用性、preview、execution 与 AI 共用边界。
- [`battle/equipment_ability_runtime.md`](battle/equipment_ability_runtime.md)：装备能力内容 ABI、战斗投影和 typed action 执行链。
- [`battle/weapon_dice_and_equipment.md`](battle/weapon_dice_and_equipment.md)：武器 profile、武器骰和战斗内换装口径。
- [`battle/ai_score_parameters.md`](battle/ai_score_parameters.md)：当前 AI 评分参数与训练接线。
- [`battle/balance_simulation.md`](battle/balance_simulation.md)：已落地的战斗模拟和调参工具。

## 收录标准

- 文档描述的是当前存在的 owner、数据形状和调用链。
- 文档中的主要文件路径可以在仓库中找到。
- 文档不把计划中的 Phase、未来 schema 或未决方案写成当前事实。
- 细节发生变化时更新对应系统文档；只有所有权、主链或推荐读集变化时才同步修改 `project_context_units.md`。
