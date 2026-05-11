# CU-10 Warehouse / Equipment 多子代理审查汇总

日期：`2026-05-11`

范围：CU-10 队伍共享背包、物品定义、装备基础流转，以及与 CU-06 / CU-15 / CU-21 的 runtime、battle-local、headless 桥接点。

状态：4 个子代理均为只读静态审查；未改运行时代码，未运行 Godot 测试。本文为去重后的模块级结论。

子代理：

- Jason `019e1467-06e1-7f91-821a-6e509db5d7b7`：仓库服务、堆叠、实例 ID、loot commit。
- Kant `019e1467-0708-7ac1-b278-fd56dc1b49bd`：装备槽位、battle-local 换装、资源刷新。
- Socrates `019e1467-57a2-7843-a153-4412f6f3dd38`：物品内容、模板、weapon profile、技能书生成。
- Ramanujan `019e1467-57d1-75a1-8246-9a1115acb71d`：runtime/UI/headless 桥接、文本快照、loot commit 表面。

## 归并结论

P0：未发现。

P1 优先修：

- [scripts/player/warehouse/warehouse_state.gd](E:/game/magic/scripts/player/warehouse/warehouse_state.gd:82) / [scripts/player/equipment/equipment_state.gd](E:/game/magic/scripts/player/equipment/equipment_state.gd:181) / [scripts/player/progression/party_state.gd](E:/game/magic/scripts/player/progression/party_state.gd:469)  
  装备实例 ID 没有 party-wide 唯一性校验。仓库、成员装备位、多个成员之间可同时持有同一 `instance_id`，后续按实例查找/删除会删错、返回 mismatch，或出现“已装备又可丢弃/出售”的所有权分裂。此项也和 CU-02 审查结论重复命中，应作为存档 schema 负例优先补。

- [scripts/systems/game_runtime/game_runtime_battle_loot_commit_service.gd](E:/game/magic/scripts/systems/game_runtime/game_runtime_battle_loot_commit_service.gd:95) / [scripts/systems/game_runtime/game_runtime_facade.gd](E:/game/magic/scripts/systems/game_runtime/game_runtime_facade.gd:1277)  
  loot commit 失败仍不阻断战斗完成保存。commit service 会回滚仓库并返回 `ok=false`，但 facade 仍可能保存 party/world、移除 encounter 并 flush。失败模式是玩家拿不到 loot，遭遇也无法重试。需要明确策略：阻断战斗完成，或正式承认“无 loot 胜利”。

- [scripts/systems/battle/runtime/battle_change_equipment_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_change_equipment_resolver.gd:94) / [scripts/systems/battle/runtime/battle_unit_factory.gd](E:/game/magic/scripts/systems/battle/runtime/battle_unit_factory.gd:134) / [scripts/systems/battle/runtime/battle_skill_turn_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_turn_resolver.gd:96)  
  战斗换装后没有刷新装备依赖技能可用性，正式施放门禁也缺 `requires_equipped_shield` 检查。开战带盾后卸盾仍可能施放盾牌技能；开战无盾后装备盾牌也不会恢复盾牌技能。

- [scripts/player/warehouse/item_def.gd](E:/game/magic/scripts/player/warehouse/item_def.gd:111) / [scripts/player/warehouse/item_content_registry.gd](E:/game/magic/scripts/player/warehouse/item_content_registry.gd:179)  
  `item_category` 没有白名单校验。拼错的 equipment / skill_book 会被当作普通物品注册进 runtime，绕过装备或技能书校验。

- [scripts/player/warehouse/item_content_registry.gd](E:/game/magic/scripts/player/warehouse/item_content_registry.gd:201)  
  `weapon_profile` 校验太浅。当前主要检查 melee tag 的 range / damage tag，没有强制所有武器具备有效 family、weapon type、至少一组 damage dice，也没有覆盖 ranged weapon 的核心字段。坏远程武器可能可装备但战斗投影缺骰或 family。

- [scripts/player/warehouse/skill_book_item_factory.gd](E:/game/magic/scripts/player/warehouse/skill_book_item_factory.gd:29) / [scripts/player/warehouse/item_content_registry.gd](E:/game/magic/scripts/player/warehouse/item_content_registry.gd:179)  
  技能书缺跨表校验。手写 `skill_book_*` 只要求 `granted_skill_id` 非空，不校验技能存在或 `learn_source == "book"`；同 ID 手写 item 还会让自动技能书生成跳过。

- [scripts/systems/game_runtime/game_runtime_warehouse_handler.gd](E:/game/magic/scripts/systems/game_runtime/game_runtime_warehouse_handler.gd:100)  
  仓库 runtime 命令先改内存、后持久化，且持久化失败仍返回成功。`warehouse add/discard/use` 会让 UI/headless 以为成功，但重载后改动丢失。

- [scripts/systems/game_runtime/headless/headless_game_test_session.gd](E:/game/magic/scripts/systems/game_runtime/headless/headless_game_test_session.gd:311)  
  `battle equip/unequip` 文本命令直接走 `battle_runtime.issue_command()`，绕过正式 runtime proxy/facade/battle session bridge。它覆盖 resolver，但不覆盖正式批处理副作用、modal/promotion/battle-end hook。

P2 待定策略：

- [scripts/player/warehouse/warehouse_stack_state.gd](E:/game/magic/scripts/player/warehouse/warehouse_stack_state.gd:43) / [scripts/systems/inventory/party_warehouse_service.gd](E:/game/magic/scripts/systems/inventory/party_warehouse_service.gd:60)  
  stack payload 只要求正整数，不按 `ItemDef.max_stack` 校验。坏存档可把物品压成单栈超大数量，只占 1 格。若要拒绝，需要 item-def-aware decode；若允许，需要明确容量语义。

- [scripts/systems/game_runtime/game_runtime_warehouse_handler.gd](E:/game/magic/scripts/systems/game_runtime/game_runtime_warehouse_handler.gd:407) / [scripts/systems/inventory/party_warehouse_service.gd](E:/game/magic/scripts/systems/inventory/party_warehouse_service.gd:159)  
  runtime 丢弃入口绕过服务层“唯一装备实例 item_id-only 便利路径”。仓库里只有一件装备时，不传 `instance_id` 的 runtime discard 仍失败；服务层 `remove_item(item_id, 1)` 可成功。

- [scripts/systems/battle/runtime/battle_change_equipment_resolver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_change_equipment_resolver.gd:97) / [scripts/systems/battle/runtime/battle_unit_factory.gd](E:/game/magic/scripts/systems/battle/runtime/battle_unit_factory.gd:81)  
  战斗换装只 clamp HP，没有 clamp MP / stamina / aura，也未同步 action stats。当前正式内容触发面可能较窄，但一旦装备影响这些上限会出错。

- [scripts/player/warehouse/item_content_registry.gd](E:/game/magic/scripts/player/warehouse/item_content_registry.gd:220) / [scripts/player/warehouse/item_def.gd](E:/game/magic/scripts/player/warehouse/item_def.gd:199)  
  内容校验不要求 `occupied_slot_ids` 包含入口槽。坏配置可能导致 preview 按一个占槽集合计算，提交时 `EquipmentState` 自动补入口槽，预览/提交不一致。

- [scripts/systems/game_runtime/headless/headless_game_test_session.gd](E:/game/magic/scripts/systems/game_runtime/headless/headless_game_test_session.gd:476)  
  `battle finish player` 的 headless loot priming 与正式 per-kill 掉落不同。它在没有 active loot 时用 encounter preview 填 `_active_loot_entries`，可能测试到 commit-time fallback，而不是正式击杀掉落链。

- [scripts/utils/game_text_snapshot_renderer.gd](E:/game/magic/scripts/utils/game_text_snapshot_renderer.gd:468) / [scripts/utils/game_text_snapshot_renderer.gd](E:/game/magic/scripts/utils/game_text_snapshot_renderer.gd:614)  
  文本仓库快照不输出 `instance_id/rarity/durability`，loot 文本段也不输出 commit error code。实例级错误和 loot commit 失败难以用文本断言稳定捕捉。

- [tests/runtime/validation/content_validation_runner.gd](E:/game/magic/tests/runtime/validation/content_validation_runner.gd:177)  
  resource validation helper 的 item 域可能没有独立覆盖 `items_templates` 漂移，未使用模板坏数据可能被报告面漏掉。

## 已有覆盖

- `tests/warehouse/run_party_warehouse_regression.gd` 覆盖了仓库 schema、容量、world-level 装备实例 ID、battle-local backpack view、展示 entries、重复装备实例 remove/sell 要求 instance_id、种子物品基础 schema。
- `tests/equipment/run_party_equipment_regression.gd` 覆盖了装备/卸装、双手占槽、实例保真、重复同 item 需要 instance_id、装备 schema round-trip。
- `tests/battle_runtime/runtime/run_battle_runtime_smoke.gd` 覆盖 battle-local 换装的 AP、容量回滚、双手/versatile、HP clamp 与 report。
- `tests/text_runtime/commands/run_battle_equipment_text_command_regression.gd` 覆盖文本 battle equip/unequip 的解析与基础执行，但未覆盖正式 facade parity。

## 建议落地顺序

1. 先修所有权和事务边界：party-wide `instance_id` 唯一性、loot commit 失败策略、仓库命令持久化失败的返回值/回滚。
2. 再补 battle-local 换装规则：装备依赖技能刷新和正式施放门禁，随后处理 MP/stamina/aura/action stats clamp。
3. 再收紧 item content validation：`item_category` 白名单、weapon profile schema、skill book 跨表引用、`occupied_slot_ids` 包含入口槽。
4. 最后增强 headless/text 可观测性：battle equip 走正式桥或加 parity test，仓库文本输出实例字段，loot 文本输出 commit status。

## 需要补的测试

- duplicate `instance_id` 横跨仓库 / 装备位 / 多成员装备位时 save decode 或 `PartyState.from_dict()` 失败。
- loot commit `ok=false` 时 encounter 不被移除，或明确断言保存“无 loot 胜利”。
- 仓库 `add/discard/use` 持久化失败时命令返回失败并回滚，或明确保留 dirty 内存语义。
- 战斗换装卸盾后盾牌技能不可施放；战斗中装备盾牌后盾牌技能可用；`requires_equipped_shield` 在 runtime cast gate 生效。
- 非法 `item_category`、ranged weapon 缺 family/dice/range、技能书 missing/non-book skill、手写 `skill_book_*` collision。
- `occupied_slot_ids` 不含入口槽的 item fixture 被 registry 拒绝。
- over-max stack payload 的策略测试。
- headless `battle equip` 与正式 facade/proxy 路径 parity。
- 文本 `[WAREHOUSE]` 输出装备 `instance_id/rarity/durability`，`[LOOT]` 输出 `commit_ok/commit_error_code`。

## Project Context Units Impact

本轮确认 `docs/design/project_context_units.md` 的 CU-10 有一处漂移：武器模板描述仍写成少量 BG3 子集，实际 `items_templates` 与测试已经覆盖更广的模板集合。本文档落地后应同步更新该句描述。
