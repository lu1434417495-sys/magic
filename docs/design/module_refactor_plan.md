# 模块重构当前待办

> 更新日期：2026-04-26
> 范围：当前仓库 `scripts/` 运行时主链、相关 headless/regression，以及 `docs/design/project_context_units.md` 已声明的所有权边界
> 说明：本文只保留当前仍有效的 todo。已完成、已失效或已被当前实现取代的旧项已移除。

## 关联上下文单元

- CU-06：世界/战斗运行时总编排与场景适配
- CU-15：战斗运行时总编排
- CU-16：战斗状态模型、边规则、伤害、AI 规则层

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文只记录仍有效的结构债务和建议执行顺序。

---

## 一、使用方式

- 当前模块边界以 `docs/design/project_context_units.md` 为真相源。
- 本文件只记录：
  - 仍未完成、且值得继续执行的功能缺口
  - 当前仍成立的中期结构债务
- 如果后续运行时主链再次发生边界迁移，应先更新 `project_context_units.md`，再回写本文件。

---

## 二、当前高优先级待办

当前没有待闭合的功能缺口。`MRP_01` 起的全部条目已经合入主线，原 P0.1（`command_return_from_submap()` battle/modal 守卫）与 P1.2（`game_text_snapshot_renderer.gd` 的 battle-start confirm 渲染）均已落地，对应回归脚本：

- `tests/world_map/runtime/run_world_map_input_routing_regression.gd`
- `tests/text_runtime/run_text_command_regression.gd`

后续如果主链再爆出新的紧急边界缺口，应作为新的 P 项追加到本节，而不是复用旧编号。

---

## 三、当前结构债务

这些工作不是立刻阻塞主链的 bug，但仍然值得保留为中期结构债务。

### S1 `BattleHitResolver` 已经被 fate 流程绕开

`battle_hit_resolver.gd` 仍然存在，并保留了：

- 命中率合成
- 普通 / repeat-attack 的 d20 vs 检定值
- natural 1 / natural 20 的固定走向

但 `2444323 feat: extend fate battle runtime` 之后，`battle_damage_resolver.gd:236-280` 引入了一条独立的命中流程：

- 在 `_resolve_attack_outcome()` 内部直接 `_roll_attack_die()`
- 自己做 crit-gate-die、d20、fumble 低端区间和 crit 高端阈值
- 通过 `BATTLE_FATE_ATTACK_RULES_SCRIPT` / `FATE_ATTACK_FORMULA_SCRIPT` 计算 luck 影响

结果是命中真相源现在分裂成两份：

- `BattleHitResolver`：负责 repeat-attack 命中预览与基础命中检定
- `BattleDamageResolver._resolve_attack_outcome()`：fate 路径下的暴击 / 大失败 / 命中三选一

**当前 owner**

- `scripts/systems/battle_hit_resolver.gd`
- `scripts/systems/battle_damage_resolver.gd`
- `scripts/systems/battle_fate_attack_rules.gd`
- `scripts/systems/fate_attack_formula.gd`

**建议方向**

- 把 `_resolve_attack_outcome()` 内的 d20 / crit-gate 判定收敛回 `BattleHitResolver`，让 damage_resolver 只消费已 resolved 的命中结果。
- `battle_fate_attack_rules` 与 `fate_attack_formula` 留作纯规则计算模块，不直接消费 die roll。
- 命中预览（repeat-attack 和 fate-on-hit）从同一处函数返回，避免 HUD / log 两侧再次出现"两份命中文案"的情况。

### S2 `GameRuntimeFacade` 仍在膨胀

| 指标 | 上一次刷新 | 当前 |
| --- | --- | --- |
| 行数 | 约 3.4k | **3918** |
| 字段数 | 约 50 | **67** |
| 函数数 | 约 320 | **381** |

`b820a95`..`b10f1dc` 这一波 fate 工作直接把以下三个新服务挂成了 facade 字段：

- `_fortuna_guidance_service`
- `_misfortune_guidance_service`
- `_low_luck_event_service`

facade 已经持有 4 个 `*_handler` 子模块（settlement / warehouse / party / reward_flow），但 fate 这一族服务没有走相同模式，反而回到了"直接挂字段 + 直接持有 setup 调用"的旧路径。

**当前 owner**

- `scripts/systems/game_runtime_facade.gd`

**约束（不要再回退）**

- 战斗选择状态继续留在 `GameRuntimeBattleSelection(+State)`
- 仓库流程继续留在 `GameRuntimeWarehouseHandler`
- 奖励 / 晋升流程继续留在 `GameRuntimeRewardFlowHandler`
- 据点动作继续留在 `GameRuntimeSettlementCommandHandler`
- 快照继续留在 `GameRuntimeSnapshotBuilder`

**建议方向**

- 新增规则不要再以"直接成为 facade 字段"作为默认形状，先评估能不能落到既有 handler / module。
- 当前优先把 fate 这一族（见 S3）拉成独立模块，让 facade 重新降到 60 字段以下。

### S3 fate 系统散在 facade 与 runtime_module 之间

fate 主线现在跨在两个 god-object 之间：

- `battle_runtime_module.gd` 持有：
  - `_fortune_service`、`_misfortune_service`
  - `LOW_LUCK_RELIC_RULES_SCRIPT` 一堆规则常量与 flag 查询
  - 4732/4741 行的 `LOOT_SOURCE_KIND_FATE_STATUS_DROP` 掉落集成
- `game_runtime_facade.gd` 持有：
  - `_fortuna_guidance_service`、`_misfortune_guidance_service`、`_low_luck_event_service`
  - calamity shard 章节槽 (`CALAMITY_SHARD_CHAPTER_FLAG_PREFIX`)
  - 缺 calamity 时的 ordinary battle 转换

整体语义"运气 / 厄运 / fate 攻击 / 低运气事件"是一族，但 owner 被 battle_runtime 与 facade 各持一半，导致：

- 字段同步必须双向跑（runtime_module 的 calamity 状态 + facade 的章节槽 flag）
- 测试也分别落到 `tests/battle_runtime/` 与 `tests/progression/`，没有一处可以"只看 fate 子系统"的入口
- 新增 fate 规则时容易二次加常量、二次 wire setup

**当前 owner**

- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/game_runtime_facade.gd`
- `scripts/systems/fortune_service.gd` / `scripts/systems/misfortune_service.gd`
- `scripts/systems/fortuna_guidance_service.gd` / `scripts/systems/misfortune_guidance_service.gd`
- `scripts/systems/low_luck_event_service.gd` / `scripts/systems/low_luck_relic_rules.gd`
- `scripts/systems/battle_fate_event_bus.gd` / `scripts/systems/battle_fate_attack_rules.gd`

**建议方向**

- 拉一个 `FateRuntimeModule`（或 `FateCoordinator`）持有 fortune / misfortune / guidance / low_luck_event 的全部状态与 setup。
- 让 facade 与 battle_runtime_module 通过它的接口取数，不再各自直接持有 service 字段。
- calamity shard 章节槽这种"跨 battle"的进度位仍然存放在 `PartyState`，但写入入口收敛到 fate 模块单点。

### S4 battle-loot 常量在 facade 与 runtime_module 各定义一份

`battle_runtime_module.gd:107-116` 与 `game_runtime_facade.gd:46-52` 同时声明了同义但前缀不同的常量：

| runtime_module | game_runtime_facade | 字面量 |
| --- | --- | --- |
| `LOOT_DROP_TYPE_ITEM` | `BATTLE_LOOT_DROP_TYPE_ITEM` | `&"item"` |
| `LOOT_DROP_TYPE_RANDOM_EQUIPMENT` | `BATTLE_LOOT_DROP_TYPE_RANDOM_EQUIPMENT` | `&"random_equipment"` |
| `LOOT_DROP_TYPE_EQUIPMENT_INSTANCE` | `BATTLE_LOOT_DROP_TYPE_EQUIPMENT_INSTANCE` | `&"equipment_instance"` |
| `LOOT_SOURCE_KIND_CALAMITY_CONVERSION` | `BATTLE_LOOT_SOURCE_KIND_CALAMITY_CONVERSION` | `&"calamity_conversion"` |
| `LOOT_SOURCE_ID_ORDINARY_BATTLE` | `BATTLE_LOOT_SOURCE_ID_ORDINARY_BATTLE` | `&"ordinary_battle"` |
| `LOOT_SOURCE_ID_ELITE_BOSS_BATTLE` | `BATTLE_LOOT_SOURCE_ID_ELITE_BOSS_BATTLE` | `&"elite_boss_battle"` |
| `CALAMITY_SHARD_ITEM_ID` | `BATTLE_LOOT_CALAMITY_SHARD_ITEM_ID` | `&"calamity_shard"` |

字面量一致，后续如果一边改字面量、另一边忘改，就是一类典型的"看似没改 schema 实则切了枚举"的隐性 bug。

**当前 owner**

- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/game_runtime_facade.gd`
- 间接依赖：`scripts/systems/battle_resolution_result.gd`

**建议方向**

- 在 `scripts/systems/battle_resolution_result.gd` 旁边新建 `battle_loot_constants.gd`（或挂在 `battle_resolution_result.gd` 内的常量段），作为 loot drop_type / source_kind / source_id / known item id 的单一真相源。
- runtime_module 与 facade 都通过 `BattleLootConstants.LOOT_DROP_TYPE_ITEM` 引用。
- 同步把 `tests/` 里硬编码的 `"random_equipment"` / `"calamity_shard"` 替换成同一个常量。

### S5 旧脚本残留的 `.uid` 与空脚手架目录

以下条目已经在 `project_context_units.md` 标注为"可以直接忽略"，但仓库里仍有物理残留，每次仓库扫描都会出现"为什么这文件还在"的噪音：

- 4 个孤儿 `.uid`（对应 .gd 已不存在）：
  - `scripts/systems/battle_state_factory.gd.uid`
  - `scripts/systems/pending_mastery_reward.gd.uid`
  - `scripts/systems/pending_mastery_reward_entry.gd.uid`
  - `scripts/systems/settlement_window_system.gd.uid`
- 3 个未使用的场景目录：`scenes/enemies/`、`scenes/levels/`、`scenes/player/`
- 4 个未使用的资源目录：`assets/audio/`、`assets/effects/`、`assets/fonts/`、`assets/sprites/`
- `tests/tmp_overlay_check.gd`：60 行手撕 facade 私有字段的临时调试脚本，已经入库但没有 runner 入口
- `.gitignore` 仍保留 `!data/saves/fixed_test_world_save.dat` 例外，但该 fixture 当前已被删除（`git status` 显示 ` D`）

**建议方向**

- 一次性清理：`git rm` 剩余孤儿 `.uid`、`tests/tmp_overlay_check.gd`、空目录占位文件；同步从 `project_context_units.md` 的"sidecar / 兼容层 / 漂移点"小节移除已不再存在的引用。
- 修正 `.gitignore` 里 fixture 例外（要么把 fixture 加回来，要么去掉例外行）。

## 四、建议执行顺序

### Phase A — 一次性机械清理

1. 执行 S5：清理孤儿 `.uid` / 空目录 / `tmp_overlay_check.gd` / `.gitignore` fixture 例外。

### Phase B — 收敛 fate 子系统

1. 执行 S4：抽离 `battle_loot_constants.gd`，让 facade / runtime_module / tests 共用一份常量。
2. 执行 S3：拉 `FateRuntimeModule`，把 fortune / misfortune / guidance / low_luck_event 全部收口。
3. 复查 S2：fate 收口后 facade 字段数应回到 60 以下；如果没有，再从 settlement / reward 链路里挑下一族切。

### Phase C — 收敛命中真相源

1. 执行 S1：把 `_resolve_attack_outcome()` 内的 d20 / crit-gate 判定推回 `BattleHitResolver`，让 fate 命中走同一个入口。
2. 同步整理 `tests/battle_runtime/run_fate_attack_formula_regression.gd` 与命中预览相关回归，确保口径单点。

---

## 五、结论

- 当前仓库已经没有"立刻阻塞主链"的功能缺口；旧 plan 里的 MRP_01..08 与 P0.1 / P1.2 全部落地。
- 现阶段最值得处理的是 fate 这一波带来的三个回归：
  - `BattleHitResolver` 被 damage_resolver 绕开（S1）
  - `GameRuntimeFacade` 字段数从约 50 涨到 67（S2）
  - fate 服务散在 facade 与 runtime_module 之间，没有共同 owner（S3）
- 其余条目（loot 常量重复、孤儿 `.uid`）属于一次性机械清理，可以单独成提交。
