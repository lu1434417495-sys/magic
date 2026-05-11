# CU-10 Warehouse / Equipment Consensus Solution - 2026-05-11

## Problem

`warehouse_equipment_subagent_review_2026-05-11.md` 暴露的问题不是单一 bug，而是四条边界同时松动：

- 持久装备 `instance_id` 的 party-wide 所有权不够硬，仓库、成员装备位、多个成员之间可能持有同一个实例。
- 战斗内换装只刷新了部分投影，装备依赖技能和正式施放 gate 可能不同步。
- item / weapon / skill book 内容校验过浅，拼错 category、坏 weapon profile、手写技能书冲突可能进入正式内容。
- 仓库 runtime 命令和战斗 loot commit 的失败语义偏乐观，存在内存成功但落盘或结算失败的灰区。

本轮对抗性讨论后的共识：不要做通用事务框架，不做兼容迁移，也不自动修复坏 payload。按当前 current schema 政策收紧所有权、内容边界和命令成功定义：正式路径必须 fail closed，合法 overflow 用结构化成功表达。

## Current Ownership

- `PartyState` 拥有 party 聚合不变量，包括 warehouse 与所有 members/equipment 的持久实例唯一性。
- `WarehouseState` 和 `EquipmentState` 只拥有各自容器的局部 shape 校验，不承担跨 party 所有权裁判。
- `GameSession` 拥有世界级装备实例 ID 分配；分配器只生成新 ID，不负责修复已有坏状态。
- `PartyWarehouseService` / `PartyEquipmentService` 拥有持久仓库与成员装备之间的正式 move 操作。
- `BattleState.party_backpack_view` 与 `BattleUnitState.equipment_view` 是战斗内背包/装备真相源。
- `BattleSessionFacade` / `GameRuntimeFacade` 拥有战斗结算、battle-local writeback、loot commit、encounter removal 和 flush 顺序。
- `ItemContentRegistry` 拥有 item/template 注册前 schema 校验；`WeaponProfileDef` 拥有 weapon profile 本地 schema；跨表 skill book 校验由内容校验编排层负责。
- `GameRuntimeWarehouseHandler` 拥有 UI/headless 仓库命令的成功语义：命令成功必须包含持久化成功。

## Core Invariants

- 一个非空 `EquipmentInstanceState.instance_id` 在同一 `PartyState` 内只能有一个 owner。
- 持久装备 owner 只能是 `WarehouseState.equipment_instances` 或某个成员 `EquipmentEntryState.equipment_instance`，二选一。
- 装备流转是 move，不是 copy。装备、卸装、替换都不能留下同 ID 的双 owner。
- 战斗内换装是 battle-local 事务，只能在 `party_backpack_view` 与 `equipment_view` 之间移动完整装备实例。
- 技能可用性必须以当前 `BattleUnitState` 为准；`requires_equipped_shield` 既要影响列表/preview，也要影响正式 issue/cast gate。
- 换装、装备损坏或强制移除后，属性快照、武器投影、技能列表、资源上限必须同步；HP/MP/stamina/aura/AP 只 clamp，不 refill。
- `item_category` 是闭集：空值可归一为 `misc`，显式值只允许 `misc / equipment / skill_book`。
- `weapon_profile` 是武器投影的唯一真相源；所有正式 weapon item/template 都必须通过完整 profile schema。
- skill book 是 item 与 skill 的跨表合同；手写 `skill_book_*` 不能静默覆盖自动生成。
- 仓库 add/discard/use 成功必须包含 `_persist_party_state()` 成功；持久化失败要回滚内存并返回失败。
- loot commit 的 `ok=false` 是战斗完成 hard failure。容量 overflow 若以 `overflow_entries` 且 `ok=true` 表达，则仍是成功。

## Disputed Options

### A. 只补局部容器校验

在 `WarehouseState` 和 `EquipmentState` 内各自查重复。

结论：反对。它只能抓同一容器内重复，抓不到仓库与成员装备共享同一实例、两个成员共享同一实例等最危险状态。

### B. 只在开战工厂过滤装备依赖技能

让 `BattleUnitFactory` 开战时按装备过滤技能，不改正式施放 gate。

结论：反对。战斗内换装、装备损坏、stale command 都能绕过开战时列表。正式 resolver 必须重新检查当前装备。

### C. loot commit 失败后记录错误但继续胜利

战斗胜利照常移除 encounter、flush save，只把 loot 错误写进状态。

结论：反对。这会变成“胜利但掉落丢失”，玩家无法重试，和 canonical battle result 的事务语义冲突。

### D. item registry 直接依赖 skill registry

把 skill book 跨表校验塞进 `ItemContentRegistry`。

结论：反对。它会把 CU-13 progression registry 初始化顺序拉进 CU-10 item registry。推荐把 item 本表 schema 放在 `ItemContentRegistry + WeaponProfileDef`，跨表合同放在内容校验编排层。

### E. over-max stack 自动拆栈或 clamp

读到 `quantity > max_stack` 时自动拆成多栈、截断，或当兼容迁移处理。

结论：反对。当前政策是不做旧 schema 兼容；正常 service 入库已经遵守 `max_stack`。坏 payload 或坏测试 fixture 应由 item-def-aware 校验拒绝，除非另行确认“超大单栈占一格”是正式设计。

## Recommended Design

### 1. Party-wide equipment instance ownership

- 在 `PartyState.from_dict()` 完成 warehouse 与 members 解析后，调用 `_validate_unique_equipment_instance_owners()`。
- 扫描：
  - `party_state.warehouse_state.equipment_instances`
  - 每个成员 `equipment_state` 的完整 entry equipment instance
  - 同一成员多个入口装备位
  - 不同成员装备位
- 用 `String(instance_id)` 建 `seen` 字典；第二次注册同 ID 直接解码失败。
- 不自动选择保留项、不重分配、不按 `item_id` 猜测哪份是真的。
- `PartyWarehouseService.add_equipment_instance()` / `deposit_equipment_instance()` 在持久仓库模式下补轻量 guard：非空既有 ID 已被当前 party 任一 owner 持有时拒绝插入。
- battle-local backpack view 不盲扫持久 `PartyState` 装备位，避免战斗局部 move 被误判。

### 2. Battle equipment refresh and shield gate

- 在 `BattleRuntimeSkillTurnResolver.get_skill_cast_block_reason()` 增加 `requires_equipped_shield` 正式 gate。
- gate 从 `active_unit.get_equipment_view()` 读取当前 off-hand / occupied slots，再用 item defs 判断是否仍装备盾牌。
- 战斗换装成功后调用统一 refresh helper，而不是只刷新属性/武器：
  - attributes snapshot
  - weapon projection / basic attack projection
  - known/available active skills
  - unlocked resources / max resources
- refresh 后 HP/MP/stamina/aura/AP 按新上限 clamp；换装 AP 扣费不能被刷新覆盖，也不能 refill。
- 同一 helper 服务装备损坏、双手武器顶掉盾、强制卸装等路径，避免列表和正式 gate 分裂。

### 3. Battle loot commit failure semantics

- `GameRuntimeBattleLootCommitService` 返回 `ok=false` 时，facade 必须 fail closed：
  - 不移除 encounter。
  - 不释放 battle save lock。
  - 不清 battle result/context。
  - 不 `flush_game_state()`。
  - 返回结构化 `commit_error_code` / blocked item or instance context。
- battle-local writeback conflict 仍先于 loot commit 阻断。
- 背包容量 overflow 仍以 `overflow_entries` 且 `ok=true` 表达，可以正常完成战斗。
- 不新增 pending loot claim、邮件箱补偿或 legacy fallback schema；这类设计需要另行确认。

### 4. Warehouse runtime command persistence

- `GameRuntimeWarehouseHandler` 的 add/discard/use 命令成功定义改为“内存变更和持久化都成功”。
- 每个命令在变更前捕获最小必要 party/warehouse 快照。
- `_persist_party_state()` 失败时：
  - 回滚内存到变更前状态。
  - 重绑 service/cache。
  - 返回 `ok=false` 或 `success=false`，附 `error_code=persist_failed`。
- 不引入全局事务框架；先做局部命令事务。
- item-only 便利 discard 不扩大为装备兼容路径；正式装备实例选择继续要求明确 `instance_id`。

### 5. Static item / weapon / skill book validation

- `ItemDef` 增加 category 白名单 helper；`ItemContentRegistry` 注册 merged item/template 前拒绝未知显式 category。
- `WeaponProfileDef` 增加 `validate_schema(context_label)`：
  - `weapon_type_id`、`training_group`、`range_type`、`family`、`damage_tag` 非空。
  - `range_type` 至少限制为 `melee / ranged`。
  - `damage_tag` 限制为现有物理伤害标签集合。
  - `attack_range >= 1`。
  - `one_handed_dice` 或 `two_handed_dice` 至少一组有效。
- `ItemContentRegistry` 对所有 `is_weapon()` item/template 调用 profile 校验，不只校验 melee。
- equipment item 若只有一个入口槽且声明 `occupied_slot_ids`，`occupied_slot_ids` 必须包含该入口槽。
- `SkillBookItemFactory` 提供校验 helper：
  - 所有 `skill_book` item 的 `granted_skill_id` 必须存在。
  - 对应 skill 的 `learn_source == "book"`。
  - canonical `skill_book_<skill_id>` 若手写存在，必须指向同一个 skill；否则 collision 失败。
- `content_validation_runner.gd` 显式扫描 items 与 item templates，并接收 skill defs 做 skill book cross-table 校验。
- `GameSession` 内容快照也应把这类 cross-table error 计入 item/content domain，避免只有测试能发现。

### 6. Headless and text observability

- headless `battle equip/unequip` 应改走 `GameRuntimeFacade.issue_battle_command()` 或等价正式 facade 路径。
- 若保留 direct `battle_runtime.issue_command()` 辅助路径，必须新增 parity 回归证明 direct 与 facade 结果一致；推荐优先改走 facade。
- `GameTextSnapshotRenderer` 增强最小可观测字段：
  - `[WAREHOUSE]` equipment entry 打印 `instance_id`、`rarity`、`current_durability`。
  - `[LOOT]` 打印 `commit_ok`、`error_code`、`blocked_item_id` 或 `blocked_instance_id`、`committed_count`。
- headless `battle finish player` 的 loot priming 可暂标为测试辅助路径，不在本轮扩展成正式 per-kill parity。

### 7. Over-max stack policy

- 当前推荐策略：`quantity > ItemDef.max_stack` 在 item-def-aware 校验层视为坏 payload / 坏 fixture。
- 不在 `WarehouseStackState.from_dict()` 内做该判断，因为该层没有 item def 上下文。
- 不自动拆栈、不 clamp、不迁移。
- 如果未来要支持“超大单栈只占一格”，需要作为正式容量语义重新设计并更新 CU-10。

## Minimal Slice

1. `PartyState`：party-wide `instance_id` 唯一性校验。
2. `PartyWarehouseService`：持久模式下拒绝插入已被 party 持有的既有 instance ID。
3. `BattleRuntimeSkillTurnResolver`：正式 `requires_equipped_shield` gate。
4. battle equipment refresh helper：换装/损坏后刷新属性、武器、技能和资源 clamp。
5. `GameRuntimeFacade` / `BattleSessionFacade`：loot commit hard failure 阻断战斗完成。
6. `GameRuntimeWarehouseHandler`：add/discard/use 持久化失败回滚并返回失败。
7. `ItemDef` / `WeaponProfileDef` / `ItemContentRegistry` / validation runner：category、weapon profile、skill book cross-table 校验。
8. headless battle equip path 与 text snapshot 可观测性增强。

## Files To Change

- `scripts/player/progression/party_state.gd`
- `scripts/player/warehouse/warehouse_state.gd` only if helper exposure is needed
- `scripts/player/equipment/equipment_state.gd` only if helper exposure is needed
- `scripts/systems/inventory/party_warehouse_service.gd`
- `scripts/systems/inventory/party_equipment_service.gd` only if equip/unequip helper exposure is needed
- `scripts/systems/game_runtime/game_runtime_warehouse_handler.gd`
- `scripts/systems/game_runtime/game_runtime_facade.gd`
- `scripts/systems/game_runtime/battle_session_facade.gd`
- `scripts/systems/game_runtime/game_runtime_battle_loot_commit_service.gd`
- `scripts/systems/battle/runtime/battle_change_equipment_resolver.gd`
- `scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`
- `scripts/systems/battle/runtime/battle_unit_factory.gd`
- `scripts/player/warehouse/item_def.gd`
- `scripts/player/warehouse/item_content_registry.gd`
- `scripts/player/equipment/weapon_profile_def.gd`
- `scripts/player/warehouse/skill_book_item_factory.gd`
- `tests/runtime/validation/content_validation_runner.gd`
- `scripts/systems/game_runtime/headless/headless_game_test_session.gd`
- `scripts/utils/game_text_snapshot_renderer.gd`

## Tests To Add Or Run

- `tests/equipment/run_party_equipment_regression.gd`
  - duplicate instance ID in warehouse.
  - duplicate across warehouse and member equipment.
  - duplicate across two members.
  - duplicate across same member entry slots.
  - normal equip/replace/unequip round-trip keeps rarity and durability.
- `tests/warehouse/run_party_warehouse_regression.gd`
  - persistent `add_equipment_instance()` rejects existing ID.
  - add/discard/use persist failure returns failure and rolls back memory.
  - auto skill books still generate and can be used.
  - handwritten canonical skill book collision does not silently shadow generated book.
- `tests/battle_runtime/runtime/run_battle_runtime_smoke.gd` or focused battle command regression:
  - shield skill disappears after unequipping shield.
  - shield skill appears after equipping shield in battle.
  - stale command with no shield fails formal gate and does not spend resources.
  - resource max reduction clamps without refill and does not refund AP cost.
  - equipment destruction / two-handed replacement invalidates shield skills.
  - loot commit hard failure leaves encounter, lock, result/context, and save untouched.
  - loot overflow remains successful.
- `tests/runtime/validation/run_resource_validation_regression.gd`
  - unknown `item_category` fails.
  - ranged weapon missing family/dice/range fields fails.
  - skill book with missing skill fails.
  - skill book for non-book skill fails.
  - canonical skill book collision fails.
- `tests/warehouse/run_item_template_inheritance_regression.gd`
  - templates remain non-runtime items.
  - template schema is still validated.
  - existing weapon templates continue to pass.
- Headless/text regression:
  - `battle equip/unequip` path uses facade or proves direct/facade parity.
  - `[WAREHOUSE]` includes equipment instance fields.
  - `[LOOT]` includes commit status and error code.

Battle simulation and balance runners are not needed for this slice.

## Deferred / Policy Decisions

- Whether `GameSession` should fail startup on content validation errors in dev/runtime or only expose the errors to validation runners.
- Whether over-max stack should ever be a formal capacity mode. Current consensus is reject, not split.
- Whether to add a pending loot claim / compensation system for loot commit failures. Current consensus is fail closed without new persistent schema.
- Whether headless `battle finish player` should fully model per-kill loot priming. Current consensus is to keep it as test helper unless a parity task is opened.

## Project Context Units Impact

No context map edit is required until implementation lands.

When implemented, update CU-10 to reflect:

- `PartyState` owns party-wide equipment instance uniqueness.
- warehouse runtime commands are local transactions whose success includes persist success.
- battle-local equipment refresh updates skill availability and resource clamps.
- item/template validation now includes category, weapon profile, occupied slot, and skill book cross-table contracts.

If battle loot commit failure ordering changes the battle resolution call chain, also update CU-15 / CU-21 relationship notes.
