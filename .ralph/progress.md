# Ralph Progress

按时间顺序追加每轮结果。

## 2026-04-17T01:08:02+08:00 | PVS_01A | agent_failed
title: 把通用 forge interaction_script_id 接到 SettlementForgeService
Codex exec failed. stderr: E:\game\magic\.ralph\runs\20260417-010756-PVS_01A.stderr.log

## 2026-04-17T01:10:22+08:00 | PVS_01A | agent_failed
title: 把通用 forge interaction_script_id 接到 SettlementForgeService
Codex exec failed. stderr: E:\game\magic\.ralph\runs\20260417-011017-PVS_01A.stderr.log

## 2026-04-17T01:10:48+08:00 | PVS_01A | agent_failed
title: 把通用 forge interaction_script_id 接到 SettlementForgeService
Codex exec failed. stderr: E:\game\magic\.ralph\runs\20260417-011044-PVS_01A.stderr.log

## 2026-04-17T01:20:59+08:00 | PVS_01B | blocked
title: 让通用 forge modal 进入 snapshot 与 text snapshot
Codex returned blocked: No code changed. I rebuilt context from `docs/design/project_context_units.md` and the repo AGENTS guide, then inspected the accessible snapshot files, but I could not safely patch the story because local workspace shell access is failing and the accessible GitHub `main` copy does not contain the forge modal code referenced by `PVS_01B`.

## 2026-04-17T01:31:19+08:00 | PVS_01B | done
title: 让通用 forge modal 进入 snapshot 与 text snapshot
Updated `scripts/systems/game_runtime_snapshot_builder.gd` so forge snapshot data can fall back to shared shop-style context when that context is tagged `panel_kind/submission_source = forge`, and so forge-tagged data no longer leaks into the shop snapshot. Added a runtime regression in `tests/runtime/run_game_runtime_snapshot_builder_regression.gd` that covers a generic forge modal/text snapshot entry instead of only `master_reforge`.

## 2026-04-17T01:34:45+08:00 | PVS_01C | done
title: 新增首批通用 forge 配方资源与设施标签约束
Added `data/configs/recipes/forge_smith_iron_greatsword.tres` as the first non-`master_reforge` formal forge recipe, tightened `scripts/player/warehouse/recipe_content_registry.gd` to require non-empty unique `required_facility_tags`, and extended `tests/warehouse/run_item_recipe_schema_regression.gd` so the new recipe’s item refs and `forge` facility tag are validated through both `RecipeContentRegistry` and `GameSession` cache paths.

## 2026-04-17T01:48:16+08:00 | PVS_01D | done
title: 跑通通用 forge 配方的据点端到端执行
Routed generic forge (`service:repair_gear`) through `SettlementForgeService`, kept forge UI/feedback/action IDs service-specific, inferred forge tags from generic smith interactions even on non-`craft` facilities, and added world-map/text regressions for atomic warehouse consumption, shared-warehouse output, logs, and reload persistence.

## 2026-04-17T02:00:17+08:00 | PVS_02A | done
title: 落地 contract board 的正式 modal 开关链路
Wired `service_contract_board` into the formal settlement modal chain by adding a `contract_board` runtime modal/context, rendering it through `WorldMapSystem` with the shared `ShopWindow` shell, exposing snapshot/text snapshot data, adding close-to-settlement handling, and covering the flow in settlement-handler and text-runtime regressions. Updated `docs/design/project_context_units.md` because CU-06/CU-15 now formally include the contract-board modal path.

## 2026-04-17T02:04:42+08:00 | PVS_02B | done
title: 让 contract board 按 provider_interaction_id 过滤任务列表
Filtered contract-board entries deterministically from `QuestDef.provider_interaction_id` in `game_runtime_settlement_command_handler.gd`, carried the active `provider_interaction_id` into contract-board window/snapshot data, hardened `game_runtime_snapshot_builder.gd` to read contract-board context through safe runtime helpers, exposed the provider line in text snapshots, and added regressions covering provider-specific filtering plus runtime/text snapshot visibility for the current board list.

## 2026-04-17T02:15:08+08:00 | PVS_03A | done
title: 支持从 contract board 正式接取任务
Wired contract-board confirm through `ShopWindow -> WorldMapSystem -> GameRuntimeSettlementCommandHandler -> GameRuntimeFacade.command_accept_quest`, refreshed contract-board entry/status feedback after each accept attempt, and added regressions for duplicate, completed, repeatable, and real headless board-accept flows. `docs/design/project_context_units.md` stayed valid as-is.

## 2026-04-17T02:20:20+08:00 | PVS_03B | done
title: 让 bounty registry 复用正式任务板表面
Routed `service_bounty_registry` through the existing `contract_board` modal in `scripts/systems/game_runtime_settlement_command_handler.gd`, removed it from the unimplemented-service bucket, seeded `contract_regional_bounty` in `scripts/player/progression/progression_content_registry.gd`, and extended the settlement/text regressions so bounty-provider entries stay isolated from the normal contract board list. `docs/design/project_context_units.md` stayed valid as-is.

## 2026-04-17T02:23:37+08:00 | PVS_03C | done
title: 为 board 接取路径补 text/headless 回归
Added `tests/text_runtime/scenarios/contract_board_accept.txt` as a focused headless text scenario that verifies `world open -> contract board open -> accept quest -> snapshot fields change`, and updated `tests/text_runtime/README.md` to document the correct scenario-run command.

## 2026-04-17T02:37:11+08:00 | PVS_04A | done
title: 把 quest completion 与 reward claim 拆成正式状态
Formalized quest completion into three stages by adding `PartyState.claimable_quests`, moving objective-complete flow to claimable instead of directly into `completed_quest_ids`, exposing `active/claimable/completed` through runtime, snapshot, text snapshot, and contract board state, and updating focused regressions plus `docs/design/project_context_units.md` to match.

## 2026-04-17T02:44:30+08:00 | PVS_04B | done
title: 实现 quest gold reward materializer
Added formal quest reward claiming with gold materialization in `CharacterManagementModule`, exposed `GameRuntimeFacade.command_claim_quest()`, and routed contract-board claimable entries through claim instead of re-accept. Updated focused runtime regressions for direct quest claim and contract-board claim flow. `docs/design/project_context_units.md` stays valid as-is.

## 2026-04-17T02:54:08+08:00 | PVS_04C | done
title: 实现 quest item reward materializer
Implemented quest item reward claiming through `CharacterManagementModule` + `PartyWarehouseService`, writing claimed item rewards into the shared warehouse, returning explicit overflow failure when capacity blocks the reward, surfacing reward summaries in `GameRuntimeFacade`, aligning quest item reward schema helpers, and adding focused regressions.

## 2026-04-17T03:02:51+08:00 | PVS_04D | done
title: 实现 quest pending_character_reward materializer
Implemented quest `pending_character_reward` materialization in `CharacterManagementModule`, added `QuestDef` schema validation for that reward type, surfaced quest-claim summaries/errors through `GameRuntimeFacade`, and added focused progression/runtime regressions proving claimed quest growth rewards enter `pending_character_rewards` and continue through the existing reward modal flow. `docs/design/project_context_units.md` stayed valid and `.ralph/prd.json` was not edited.

## 2026-04-17T03:14:56+08:00 | PVS_05A | done
title: 为 submit_item 目标增加正式仓库扣除入口
新增 submit_item 正式提交流程：由 CharacterManagementModule 统一预览需求、原子扣减 shared warehouse 并推进 quest objective，GameRuntimeFacade/SettlementCommandHandler 接入任务板入口；补了 quest schema 校验、文本快照兼容和相关回归，并更新了 project_context_units 的 ownership 说明。

## 2026-04-17T03:20:31+08:00 | PVS_05B | done
title: 把 submit_item 扣除结果接到 objective progress 与失败回归
补强 `submit_item` 的 progression/runtime 回归：在 `tests/progression/run_progression_tests.gd` 新增 CharacterManagementModule 级回归，覆盖部分进度成功提交、objective_progress/last_progress_context 写入、数量不足与错误物品两条失败路径；在 `tests/runtime/run_game_runtime_quest_progress_regression.gd` 补充成功后 objective_progress/context 断言，并新增错误物品失败回归。`docs/design/project_context_units.md` 无需更新。

## 2026-04-17T03:29:21+08:00 | PVS_06A | done
title: 为 research 服务分配正式 interaction_script_id 与 settlement 路由
Added a formal `service_research -> service:research` settlement path by introducing `SettlementResearchService`, wiring `GameRuntimeSettlementCommandHandler`/`WorldMapSpawnSystem` to it, and switching all world-map research NPC configs from `service_unlock_archive` to `service_research`. Extended settlement-handler regression coverage to verify both direct research routing and real world generation exposure.

## 2026-04-17T03:36:45+08:00 | PVS_06B | done
title: 实现 research 奖励构造器
Implemented the research reward constructor in `SettlementResearchService`: it now selects the next available research outcome for the target member, builds canonical `pending_character_rewards` for knowledge (`field_manual`) or skill (`warrior_guard_break`) unlocks, preserves formal source fields (`source_type/source_id/source_label`), includes member-facing summary text, and disables the service when the member has no remaining research content. Extended settlement-handler and progression regressions to cover both knowledge and skill research rewards plus downstream achievement chaining. `docs/design/project_context_units.md` stayed valid as-is.

## 2026-04-17T03:48:49+08:00 | PVS_06C | done
title: 为 research 服务补 reward flow 与 text regression
Added research-specific coverage in `tests/runtime/run_game_runtime_reward_flow_regression.gd` for research-shaped pending rewards entering, blocking on settlement modal, presenting in reward flow, and confirming. Extended `tests/text_runtime/run_text_command_regression.gd` with a focused headless research pass that verifies settlement-state timing, structured snapshot fields, and text snapshot rendering before/after the reward modal; `docs/design/project_context_units.md` stays valid as-is.

## 2026-04-17T03:54:15+08:00 | PVS_07A | done
title: 补齐第一批单手武器 seed 内容
Added `data/configs/items/militia_axe.tres` as the first new one-handed axe seed with explicit price/tag/equipment metadata, backfilled `bronze_sword` with explicit one-handed sword tags, extended `tests/equipment/run_party_equipment_regression.gd` to assert one-handed weapon class coverage/metadata, and updated `docs/design/project_context_units.md` for the CU-10 read set.

## 2026-04-17T03:57:33+08:00 | PVS_07B | done
title: 补齐第二批单手武器 seed 内容
Added two new one-handed weapon seeds (`watchman_mace`, `scout_dagger`), expanded formal shop references so `militia_axe`/mace/dagger are surfaced by settlement shops, raised equipment regression coverage from 2 to 4 one-handed weapon classes, and updated the CU-10 read-set in `docs/design/project_context_units.md`.
