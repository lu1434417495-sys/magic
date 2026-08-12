# CU-10 Warehouse / Equipment 检视剩余问题

原始检视日期：`2026-05-11`  
当前代码复核：`2026-07-20`

范围：CU-10 队伍共享背包、物品定义、装备基础流转，以及 CU-06 / CU-15 / CU-21 的 runtime、battle-local 和 headless 桥接点。

本文只保留对当前 C# owner 重新核验后仍成立的问题。已由当前实现覆盖的旧意见及其重复测试建议已移除。

## P2 / 测试入口缺口

### Headless battle equip 绕过正式命令边界

- 当前 owner：`scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs:481-606`。
- `ChangeBattleEquipmentTyped(...)` 自行校验战斗状态、构造 `BattleCommand`，然后直接调用 `BattleRuntimeModule.IssueCommand(...)` 和 `GameRuntimeFacade.ApplyBattleBatch(...)`。
- 这能覆盖 resolver，但不能证明正式 facade / proxy / battle-session 入口与 headless 行为一致。
- 修复方向：让 headless 命令调用正式 typed gateway，或增加一条同输入、同结果的 parity regression。

## P2 / 策略项

### Stack payload 与 `ItemDef.max_stack` 脱节

- 存档状态只能验证数量为正整数，没有 item-definition-aware decode。
- 坏 payload 可把超过 `max_stack` 的数量压在单栈中，影响容量语义。
- 需先确认是拒绝这类存档，还是在加载后按当前 `max_stack` 重分栈；后者属于兼容/迁移行为，实施前需用户确认。

### Runtime discard 与 service 便利语义不一致

- 当前 owner：`scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs:740-751`、`scripts/systems/inventory/PartyWarehouseService.cs:443-482`。
- runtime 对装备始终走 `RemoveEquipmentInstanceTyped(...)`，即使仓库中只有一件该物品，不传 `instance_id` 也会失败。
- 这更接近 UI 交互策略而不是正确性 bug；需决定是保持“装备必须显式选实例”，还是与 item-id-only 便利入口对齐。

### Headless battle finish 会人工填充 loot

- 当前 owner：`scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs:1023-1050`。
- 没有 active loot 时，headless 流程会用 encounter preview 直接填充 `_active_loot_entries`。
- 这可以测 commit-time fallback，但不能代表正式 per-kill drop 链。测试名称和断言应明确两者边界。

### 文本快照仍缺少实例级可观测性

- `GameRuntimeWarehouseHandler` 的 plain snapshot 已有 `instance_id`、`rarity`、`current_durability`，但 `GameTextSnapshotRenderer.BuildWarehouseLines(...)` 仍只输出 item/quantity/stack 信息。
- `BuildLootLines(...)` 仍没有 commit error code。
- 因此实例级错误和 loot commit 失败很难用文本回归稳定断言。

## 建议验证

- headless `battle equip/unequip` 与正式 typed gateway 的 parity。
- over-max stack payload 的明确策略测试。
- headless loot fallback 与正式 per-kill drop 分开断言。
- 文本 `[WAREHOUSE]` 输出装备 `instance_id/rarity/durability`，`[LOOT]` 输出 commit 结果。

## Project Context Units Impact

本次只清理过时检视结论，没有改变 runtime owner 或读集边界，不需要修改 `docs/design/project_context_units.md`。
