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

## 2026-04-17T04:02:58+08:00 | PVS_07C | done
title: 补齐头部护具与新装备元数据校验
Added `leather_cap` as the first formal head-slot armor seed, surfaced it in the existing town/city shop seed tables, extended equipment and item/schema regressions to cover head-slot metadata/equip/round-trip behavior, and updated the CU-10 read-set in `docs/design/project_context_units.md`.

## 2026-04-17T04:09:14+08:00 | PVS_08A | done
title: 补齐消耗品 seed 到最小可玩集合
Added four consumable ItemDef seeds (`bandage_roll`, `travel_ration`, `torch_bundle`, `antidote_herb`), extended the focused shop/warehouse regressions so the new consumables are asserted in formal shop rotation and shared-warehouse schema coverage, and updated the CU-10 read-set in `docs/design/project_context_units.md`.

## 2026-04-17T04:16:47+08:00 | PVS_08B | done
title: 补齐第一批材料 seed 内容
Added first-batch material seeds `beast_hide`, `hardwood_lumber`, and `linen_cloth`, backfilled `iron_ore` with stable `tags`/`crafting_groups`, enforced in `ItemContentRegistry` that `material` items must declare crafting groups, surfaced the new materials through settlement shop seed data, extended warehouse/schema regressions for material coverage, and updated `docs/design/project_context_units.md` CU-10 read-set entries.

## 2026-04-17T04:23:20+08:00 | PVS_08C | done
title: 补齐第二批材料 seed 内容
Added second-batch material seeds `forge_coal` and `whetstone`, added formal forge recipes `forge_militia_axe` and `forge_watchman_mace` that reference the new materials, and updated focused warehouse/recipe/forge regressions to require 6 material categories plus formal recipe exposure/execution.

## 2026-04-17T04:34:05+08:00 | PVS_08D | done
title: 新增任务物品 seed 并补交叉引用回归
Added three formal quest-item seeds (`sealed_dispatch`, `bandit_insignia`, `moonfern_sample`), required `quest_item` ItemDefs to declare `quest_groups`, and extended focused warehouse/quest/text-snapshot regressions to cross-reference them.

## 2026-04-17T04:38:44+08:00 | PVS_09A | done
title: 新增前排承伤/近战冲锋敌人模板
Added a sixth formal enemy template `wolf_vanguard` plus dedicated `frontline_bulwark` AI seed in `scripts/enemies/enemy_content_registry.gd`, and extended `tests/battle_runtime/run_battle_runtime_ai_regression.gd` to verify template count, formal template resolution, melee charge opening, and low-HP self-guard behavior. `docs/design/project_context_units.md` stayed valid and was not changed.

## 2026-04-17T04:44:54+08:00 | PVS_09B | done
title: 新增远程压制/治疗控制敌人模板
Added two formal enemy templates, `mist_harrier` and `mist_weaver`, plus dedicated `ranged_suppressor` and `healer_controller` brains in `scripts/enemies/enemy_content_registry.gd`. Extended `tests/battle_runtime/run_battle_runtime_ai_regression.gd` to require 8+ templates and verify stable template resolution plus ranged-suppression, control, and healing AI behavior.

## 2026-04-17T04:49:13+08:00 | PVS_09C | done
title: 新增第二个正式 encounter roster 并接到 world encounter 映射
Added a second formal wild encounter roster `mist_hollow`, exposed explicit world->roster mapping via `WildSpawnRule.encounter_profile_id`, mapped mist-beast world rules to that roster in the shipped world configs, and extended the focused wild-encounter regression to cover roster registration, mixed-unit buildout, and south-wild explicit mapping.

## 2026-04-17T04:59:20+08:00 | PVS_10A | done
title: 实现 `narrow_assault` 地形 profile 生成器
Added `narrow_assault` support in `BattleTerrainGenerator` with a deterministic choke-point assault layout, explicit left/right staging, breach objective/props, and focused `run_battle_board_regression` coverage for the new profile. Also fixed shared water classification so canyon/default generator water cells enter the topology reclassification pass during validation.

## 2026-04-17T05:04:38+08:00 | PVS_10B | done
title: 把 `narrow_assault` 接到测试入口与 board regression
Added a real `narrow_assault` test entry in `tests/battle_runtime/run_battle_runtime_smoke.gd` by starting battle runtime with explicit `battle_terrain_profile`, and tightened `tests/battle_runtime/run_battle_board_regression.gd` to cover narrow-assault spawn rings, explicit tent/torch/objective placement, supported prop IDs, and rendered prop-layer coverage.

## 2026-04-17T05:11:47+08:00 | PVS_11A | done
title: 实现 `holdout_push` 地形 profile 生成器
Added `holdout_push` support to `BattleTerrainGenerator` as a scripted attacker-vs-holdout layout with elevated defender ground, a wall-defined defensive line, mud approach, spike barricade frontage, explicit objective/tent/torch placement, and focused board/runtime regression coverage for the new profile.

## 2026-04-17T05:13:31+08:00 | PVS_11B | done
title: 把 `holdout_push` 接到测试入口与 board regression
No code changes were needed. Verified the repo already wires `holdout_push` into `tests/battle_runtime/run_battle_runtime_smoke.gd` via explicit `battle_terrain_profile`, and `tests/battle_runtime/run_battle_board_regression.gd` already covers layout stability, deployment rings, objective/tent/torch placement, mud/spike terrain, and prop-contract assertions for `holdout_push`.

## 2026-04-17T05:18:49+08:00 | PVS_12A | done
title: 抽出 BattleHitResolver 并锁定现有命中口径
Added `BattleHitResolver` as the single hit-logic owner, routed `BattleRuntimeModule` hit rolls and `BattleRepeatAttackResolver` stage hit checks through it, and updated `docs/design/project_context_units.md` for the CU-15/CU-16 ownership change.

## 2026-04-17T05:30:19+08:00 | PVS_12B | done
title: 让 preview、HUD 与 runtime 共用 BattleHitResolver
BattleHitResolver now builds the shared repeat-attack hit preview summary (`summary_text` + staged hit rates), `BattlePreview` exposes that resolver output, and `BattleHudAdapter`/battle panel/snapshot plumbing consume the same data instead of formatting their own hit semantics. Added battle UI regression coverage that compares HUD output against runtime preview for `saint_blade_combo`.

## 2026-04-17T05:38:59+08:00 | PVS_13A | done
title: 把命中公式切到 BAB + 负 AC + d20
Switched `BattleHitResolver` to deterministic BAB + descending AC + d20 repeat-attack checks, moved preview/log wording onto the resolver payload so battle log and preview read the same required-roll result, and updated the focused warrior/UI regressions. `docs/design/project_context_units.md` stays valid as-is.

## 2026-04-17T05:44:05+08:00 | PVS_13B | done
title: 为新命中模型补 natural 1/20 与 seeded regression
`BattleHitResolver` now exposes explicit natural-1/natural-20 outcome dispositions and boundary resolution text, and `tests/battle_runtime/run_battle_runtime_smoke.gd` now covers deterministic seeded d20 sequences plus 95%/5% boundary-hit regressions. `docs/design/project_context_units.md` stayed valid and was not changed.

## 2026-04-17T05:48:44+08:00 | PVS_14A | done
title: 实现 stamina_cost 的正式扣费路径
Exposed battle-unit `current_stamina` and `stamina_max` in the headless battle snapshot, rendered stamina in the text battle snapshot, added `current_stamina` to battle command-log unit summaries, and added a facade-level regression that drives a real stamina-cost skill through the formal battle command path and verifies state/snapshot/log updates.

## 2026-04-17T05:58:16+08:00 | PVS_14B | done
title: 实现 cooldown_tu 的正式递减路径
Implemented the formal `cooldown_tu` decrement path in `BattleRuntimeModule` by anchoring each unit to `BattleUnitState.last_turn_tu`, seeding mid-turn anchors on command entry, and reducing cooldowns from elapsed TU with a zero-TU turn-switch fallback. Added focused smoke and facade regressions to cover cooldown write, TU-driven decrement, zero-TU queued turn switches, and HUD cooldown refresh. `docs/design/project_context_units.md` stays valid as-is.

## 2026-04-17T06:10:16+08:00 | PVS_14C | done
title: 为 stamina/cooldown 补选择层与回归约束
Selection now rejects stamina/cooldown-blocked skills without mutating battle selection, HUD skill slots surface explicit disabled reasons (`ST不足`, `CD n`, tooltip reason), AI skill actions prefilter blocked skills before previewing, and focused battle/UI/text regressions now cover stamina-insufficient and cooldown-active paths. `.ralph/prd.json` was left untouched; `docs/design/project_context_units.md` stays valid as-is.

## 2026-04-17T06:15:20+08:00 | PVS_15A | done
title: 为 Aura 建立正式状态字段与 snapshot 表面
Formalized Aura exposure across the battle/headless surface: `BattleUnitState` now serializes `aura_max`, battle snapshots now include `current_aura`/`aura_max`, battle command-log unit summaries now include `current_aura`, text battle snapshots render `au=current/max`, and `tests/battle_runtime/run_battle_skill_protocol_regression.gd` now covers both BattleUnitState Aura serialization and real aura-cost snapshot/log updates.

## 2026-04-17T06:23:45+08:00 | PVS_15B | done
title: 实现 Aura 消耗、gating 与回归
Patched the battle selection/session handoff so a selected skill that becomes Aura-blocked now returns a formal battle command failure instead of an `ok` overlay result, and added focused Aura regressions for selection gating, runtime post-selection failure, and AI fallback behavior. `docs/design/project_context_units.md` stayed valid as-is.

## 2026-04-17T06:36:13+08:00 | PVS_16A | done
title: 引入状态效果语义表
Added a formal CU-16 status semantics owner in `scripts/systems/battle_status_semantic_table.gd`, routed `BattleDamageResolver` and `BattleRuntimeModule` through it for stack/duration/tick handling, integrated `burning`/`slow`/`staggered`, added `tests/battle_runtime/run_status_effect_semantics_regression.gd`, and updated `docs/design/project_context_units.md` for the new ownership/read-set.

## 2026-04-17T06:46:56+08:00 | PVS_16B | done
title: 把状态持续时间管理接到 battle runtime 并补回归
Unified battle status duration handling onto a single owner-turn-end path in battle runtime, added serialized `skip_next_turn_end_decay` metadata for self-applied statuses, expanded `BattleStatusSemanticTable` to the current baseline buff/debuff set, extended resolver sidecars to preserve timing metadata, added battle smoke coverage for status duration plus `BattleUnitState` round-trip, and refreshed `docs/design/project_context_units.md`.

## 2026-04-17T06:53:41+08:00 | PVS_17A | done
title: 补齐 line 与 cone 范围图形收集器
Added `scripts/systems/battle_target_collection_service.gd` as the shared `line`/`cone` collector, routed `BattleRuntimeModule` ground-effect coord building and `GameRuntimeBattleSelection` selected-ground target readback through it, and added focused regression coverage that matches selection coords against runtime preview output for both shapes.
