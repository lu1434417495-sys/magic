# CU-10 Warehouse / Equipment 未解决问题

原始检视：`2026-05-11` · 最近复核：`2026-08-08`

范围：CU-10 队伍共享背包、物品定义、装备基础流转，以及 CU-06 / CU-15 / CU-21 的 runtime、battle-local 和 headless 桥接点。

本文只保留经当前代码核验仍成立的问题。已修复或已撤销的结论直接删除，不在此归档。

以下三项**都不影响玩家**，全部是测试保真度与可观测性缺口。

## headless battle equip 绕过正式命令边界

- 当前 owner：`scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs:534-659`。
- `ChangeBattleEquipmentTyped(...)` 自行校验 phase / modal / control_mode，自行构造 `BattleCommand`，然后直接调用 `BattleRuntimeModule.IssueCommand(...)` 和 `GameRuntimeFacade.ApplyBattleBatch(...)`。
- 这能覆盖 resolver，但不能证明正式 facade / proxy / battle-session 入口与 headless 行为一致。正式链路上任何 gate 写错或漏掉，headless 回归照样全绿。
- 修复方向：让 headless 命令调用正式 typed gateway，或增加一条同输入、同结果的 parity regression。

## headless battle finish 人工填充 loot

- 当前 owner：`HeadlessGameTestSession.cs:1061-1109`（`PrimeHeadlessBattleLootIfNeeded`）。
- 没有 active loot 时，headless 流程用 encounter preview 直接填充 `_active_loot_entries`。
- 这可以测 commit-time fallback，但不能代表正式 per-kill drop 链。测试名称和断言应明确两者边界。

## 文本快照缺实例级可观测性

- `GameTextSnapshotRenderer.cs:697` 的 warehouse entry 只输出 item/quantity/stack 信息；`BuildLootLines(...)`（`:967-984`）没有 commit error code。
- 数据本身已经具备：`PartyWarehouseService.cs:1188-1190` 的 `WarehouseInventoryEntry` 已带 `instance_id` / `rarity` / `current_durability`，缺的只是 renderer 打印。
- 因此实例级错误和 loot commit 失败很难用文本回归稳定断言 —— 上面两项将来补断言时会先卡在这里。

## 建议验证

- headless `battle equip/unequip` 与正式 typed gateway 的 parity。
- headless loot fallback 与正式 per-kill drop 分开断言。
- 文本 `[WAREHOUSE]` 输出装备 `instance_id/rarity/durability`，`[LOOT]` 输出 commit 结果。
