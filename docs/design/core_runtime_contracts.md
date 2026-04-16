# Core Runtime Contracts

更新日期：`2026-04-16`

## Purpose

- 这份文档把 `Gate 1` 需要冻结的共享 contract 写成代码与设计都可复用的命名基线。
- 本文只定义字段口径与 owner，不推进 save/snapshot 升级；`C5` 之前不要把这些字段直接扩散到共享 choke point。

## SettlementServiceResult

- owner:
  - `GameRuntimeSettlementCommandHandler`
- truth source:
  - 聚落动作执行后的 canonical 结果对象。
- required fields:
  - `success: bool`
  - `message: String`
  - `persist_party_state: bool`
  - `persist_world_data: bool`
  - `persist_player_coord: bool`
  - `inventory_delta: Dictionary`
  - `gold_delta: int`
  - `pending_character_rewards: Array`
  - `quest_progress_events: Array`
  - `service_side_effects: Dictionary`
- compatibility:
  - 当前链路只返回 `pending_character_rewards`。
  - runtime / battle / settlement 不再保留 `pending_mastery_rewards` 兼容键。
- `quest_progress_events` baseline event shape:
  - `event_type: "accept" | "progress" | "complete"`
  - `quest_id: StringName`
  - `objective_id: StringName` for `progress`
  - `progress_delta: int` / `target_value: int` for `progress`
  - `world_step: int` optional
  - `context: Dictionary` optional
  - domain-specific passthrough keys such as `member_id` / `action_id` / `enemy_template_id` / `settlement_id` may be mirrored into `last_progress_context`

## BattleResolutionResult

- owner:
  - `BattleRuntimeModule`
  - `BattleSessionFacade`
- truth source:
  - battle end 后、正式 world/party commit 之前的 staging 结果。
- required fields:
  - `winner_faction_id: StringName`
  - `encounter_resolution: StringName`
  - `loot_entries: Array`
  - `overflow_entries: Array`
  - `pending_character_rewards: Array`
  - `quest_progress_events: Array`
  - `world_mutations: Array`
  - `party_resource_commit: Dictionary`
- compatibility:
  - battle 主链只继续消费 `pending_character_rewards` 数组。
  - `GameRuntimeFacade` 直接接收 `BattleResolutionResult`。
- `quest_progress_events` uses the same baseline event shape as `SettlementServiceResult`.

## QuestDef / QuestState

- owner:
  - `scripts/player/progression/quest_def.gd`
  - `scripts/player/progression/quest_state.gd`
  - `scripts/systems/quest_progress_service.gd`
- current scope:
  - schema 已接入 `PartyState`、save payload、headless snapshot / text snapshot 与 `quest` 命令域。
  - seed `QuestDef` 已接入 `ProgressionContentRegistry` / `GameSession` 缓存。
  - settlement / battle 成功链已能自动产出并消费最小 `quest_progress_events`。
  - 尚未接入 UI workflow、正式任务板或奖励发放闭环。
- `QuestDef` baseline:
  - `quest_id`
  - `provider_interaction_id`
  - `tags`
  - `accept_requirements`
  - `objective_defs`
  - `reward_entries`
  - `is_repeatable`
- `QuestState` baseline:
  - `quest_id`
  - `status_id`
  - `objective_progress`
  - `accepted_at_world_step`
  - `completed_at_world_step`
  - `reward_claimed_at_world_step`
  - `last_progress_context`

## RecipeDef

- owner:
  - `scripts/player/warehouse/recipe_def.gd`
- current scope:
  - 只冻结 item / facility / output schema，不接 settlement 执行器。
- baseline fields:
  - `recipe_id`
  - `display_name`
  - `input_item_ids`
  - `input_item_quantities`
  - `output_item_id`
  - `output_quantity`
  - `required_facility_tags`
  - `failure_reason`

## ItemDef Extensions

- owner:
  - `scripts/player/warehouse/item_def.gd`
  - `SettlementShopService` 作为当前直接消费者
- fields to freeze:
  - `buy_price`
  - `sell_price`
  - `tags`
  - `crafting_groups`
  - `quest_groups`
- compatibility:
  - 若新字段为空，买卖价仍回退到 `base_price` 推导。
  - 旧资源无需立刻重写；`ItemDef` 访问器承担兼容。

## Invariants

- 不把 domain rule 回塞到 `GameRuntimeFacade`。
- 不在 UI 节点直接拼装正式 contract。
- 新 contract 的 canonical 字段名先在 owner 内冻结，再进入 save/snapshot/headless。
- `pending_character_rewards` 是正式边界名。

## Project Context Units Impact

- 当前改动只新增 schema/contract owner，还没有改变 runtime 主链或推荐 read-set。
- `docs/design/project_context_units.md` 目前可保持不变；等 quest/recipe 真正接入 `PartyState`、save、headless 时再更新。
