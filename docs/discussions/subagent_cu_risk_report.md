# Subagent CU Risk Review Report

Date: 2026-04-28

Scope: read-only bug and risk review of the current dirty working tree, split by the 21 compute units in `docs/design/project_context_units.md`. One subagent reviewed each CU. This report aggregates their conclusions; no code fixes were made.

## Executive summary

Highest-priority blockers:

1. Mounted submap placeholders now crash save normalization/serialization. Multiple CUs independently found that ungenerated mounted submaps still use `world_data = {}`, but `SaveSerializer.normalize_world_data()` now requires `next_equipment_instance_serial` and can crash/quit on the placeholder.
2. Enemy AI can reach action dead ends. `basic_attack` costs more stamina than several real enemy templates have, and taunt behavior is inconsistent with existing AI tests.
3. Equipment instance identity is only partially fixed. Equipped state now stores a full `EquipmentInstanceState`, so rarity/durability/wear survive equip/save/unequip, but non-battle equip/discard/sell/swap paths still select equipment by `item_id` and cannot express a specific `instance_id`.
4. Progression and mastery attribution have several correctness risks: non-cumulative skill overrides, all-miss ground attacks counting as skill success, same-faction support skills receiving no mastery, and repeat-combo mastery being recorded before the relevant hit resolves.
5. World fog has state and rendering gaps around submap switching, persistence, hidden settlement actions, and multi-cell settlement footprints.
6. Test/commit hygiene has blockers: required new files are untracked, and two new regression functions are not called by their runners.

## Cross-CU blockers

### Mounted submap placeholder crash

Reported by CU-02, CU-03, CU-04, CU-06, CU-10, and CU-21.

- `[high] scripts/systems/save_serializer.gd:712` - `_normalize_mounted_submaps()` calls `normalize_world_data()` on every mounted submap `world_data`, including current `is_generated=false` placeholders created as `{}` by `scripts/systems/world_map_spawn_system.gd:1095`. The new required `next_equipment_instance_serial` validation treats this as corrupt world data and can crash/quit before the submap is generated.
- `[high] scripts/systems/save_serializer.gd:900` - serialization repeats the same risk by passing mounted submap placeholders through `serialize_world_data()`.
- Affected content includes `ashen_intersection_world_map_config.tres` and any current world with ungenerated mounted submaps.
- Confirmed failure: `godot --headless --script tests/world_map/runtime/run_world_submap_regression.gd` crashes in `SaveSerializer.normalize_world_data()`.
- Headless/text risk: `tests/text_runtime/run_submap_text_command_regression.gd` and `tests/text_runtime/run_text_save_load_regression.gd` can abort before their assertions run.

Assumption: strict rejection of real root `world_data` missing `next_equipment_instance_serial` is intentional. This finding is about a current runtime placeholder shape, not old-save compatibility.

### Enemy action availability and taunt contract drift

Reported by CU-15, CU-16, CU-17, and CU-20.

- `[high] data/configs/skills/basic_attack.tres:21` - `basic_attack` costs 5 stamina, but real beast/caster templates such as `wolf_raider`, `wolf_pack`, `wolf_alpha`, `mist_beast`, `mist_weaver`, and `wolf_shaman` have 0-4 max stamina. The fallback action can be unaffordable, causing enemies to move/wait instead of attacking.
- `[high] data/configs/enemies/templates/wolf_pack.tres:24` - `wolf_pack` and `wolf_raider` have 3 stamina while their pressure path includes `warrior_heavy_strike` and injected `basic_attack`; `warrior_heavy_strike` also requires an equipped weapon while beasts use natural weapons.
- `[high] data/configs/enemies/brains/ranged_controller.tres:25` - ranged/caster fallback attacks maintain range 3-5 while fallback weapon projection is melee range 1, so depleted ranged enemies may stay away and wait. Same risk appears in `healer_controller.tres` and `ranged_suppressor.tres`.
- `[medium] data/configs/enemies/brains/frontline_bulwark.tres:20` - `wolf_vanguard` has 5 stamina but `warrior_taunt` costs 30 and `warrior_guard` costs 50, so seeded vanguards cannot execute their advertised defensive role.
- `[high] scripts/systems/battle_ai_context.gd:62` - `resolve_forced_target_unit()` now returns `null`, while existing `EnemyAiAction._sort_target_units()` and AI regressions still expect taunt to force target selection.
- `[medium] scripts/systems/battle_state.gd:124` - soft-taunt disadvantage compares only `defender.unit_id` to `source_unit_id` and does not validate whether the taunt source exists, is alive, or is hostile.

Test evidence:

- `godot --headless --script tests/battle_runtime/run_battle_runtime_ai_regression.gd` fails two taunt assertions.
- `run_battle_board_regression.gd` passed.
- `run_battle_spawn_reachability_regression.gd` passed.
- `run_wild_encounter_regression.gd` passed.
- `run_skill_schema_regression.gd` passed.

Open decision: taunt needs one explicit contract. If the design changed from hard target lock to soft disadvantage, CU docs and tests need updating together.

## Per-CU findings

### CU-01 Login shell, world presets, saves, display settings

- `[high] scripts/systems/save_serializer.gd:250` - strict invalid-save handling calls the crash path for missing/invalid `next_equipment_instance_serial`, bypassing `login_screen.gd:195` where save-list selection expects `ERR_INVALID_DATA` and a user-facing error message.
- `[high] scripts/player/warehouse/equipment_instance_state.gd:83` - invalid persisted equipment payloads, such as missing `rarity`, can crash/quit during `load_save()` instead of being rejected through the same login save-load surface.

Test gap: no regression drives CU-01 save selection through an invalid save and asserts graceful `ERR_INVALID_DATA` handling without quitting.

### CU-02 GameSession, save, serialization, content cache

- Same mounted submap placeholder crash as the cross-CU blocker.
- Risk exists on both load normalization and serialization.

Test gap: serial tests cover `test_world_map_config.tres`, but not a mounted-submap preset.

### CU-03 World config resources and preset data

- Same mounted submap placeholder crash as the cross-CU blocker.
- `[medium] scripts/systems/game_session.gd:1264` - content validation reports progression/item/recipe/enemy domains, but not world preset/bundle data. Bad settlement distributions, facility references, wild spawn references, or name-pool problems can silently degrade generated worlds.

Test evidence:

- `run_world_map_shared_content_injection_regression.gd` passed.
- `run_world_submap_regression.gd` failed with the mounted-submap crash.

### CU-04 World generation, settlement service injection, encounter anchors

- Same mounted submap placeholder crash as the cross-CU blocker. The generator creates ungenerated submap shells with `world_data = {}`.

Test gap: new equipment serial tests do not cover mounted submap placeholders.

### CU-05 World grid and fog infrastructure

- `[high] scripts/systems/game_runtime_facade.gd:4112` - `_enter_submap()` calls `_sync_active_world_context()`, which resets fog, but enter/return paths do not call `_refresh_fog()`. After submap confirm/return, the active map can have no visible cells until another refresh.
- `[high] scripts/systems/world_map_fog_system.gd:20` - `setup()` clears faction fog state and explored/revealed cells are not persisted in `world_data`. Paid reveal effects and normal exploration can disappear across save/load or active-map resync.
- `[medium] scripts/systems/game_runtime_settlement_command_handler.gd:203` - settlement actions resolve against selected settlement without requiring an open settlement modal or fog visibility. Text/runtime callers can select unseen coordinates and execute services remotely.
- `[medium] scripts/ui/world_map_view.gd:186` - multi-cell settlements render using only the origin cell fog state, leaking unexplored cells or hiding visible footprint edges.

Test gaps: submap fog refresh, reveal persistence, hidden settlement action rejection, and multi-cell footprint fog rendering.

### CU-06 Runtime orchestration and scene adapter

- Same mounted submap placeholder crash as the cross-CU blocker.
- Additional residual risk: battle HUD equipment preview calls runtime preview during snapshot/overlay rebuilds; cost under large battle-local backpacks was not validated.

### CU-07 World map rendering

- `[medium] scripts/ui/world_map_view.gd:186` - multi-cell settlement rendering uses only origin fog state. This overlaps CU-05 and can create clickable-but-invisible settlement edges.

Test evidence:

- Passed: `run_world_map_view_color_config_regression.gd`.
- Passed: `run_world_map_input_routing_regression.gd`.
- Passed: `run_world_map_low_level_defensive_regression.gd`.
- Passed: `run_world_map_settlement_entry_regression.gd`.

Test gap: no pixel/draw regression for partial fog over multi-cell settlements.

### CU-08 Settlement and character info windows

- `[high] scripts/systems/settlement_forge_service.gd:162` - forge modal drops the member selected in `SettlementWindow`. `ShopWindow` confirms with `member_id = ""`, so the handler falls back to the leader. Forge/reforge achievements or misfortune hooks can be attributed to the wrong character.
- `[medium] scripts/systems/settlement_research_service.gd:35` - research enabled/disabled state is calculated only for the default member. Switching selected members refreshes the summary, not service metadata, so the button can be incorrectly disabled or enabled.

Test gap: multi-member settlement service tests. Current tests mostly use `hero`.

### CU-09 Party management, achievements, profession, reward windows

- `[high] scripts/ui/party_management_window.gd:908` - moving the current leader to reserve emits `leader_change_requested` before `roster_change_requested`. The synchronous render path can reload the old roster before the final active/reserve arrays are emitted, leaving the selected member active.
- `[medium] scripts/ui/party_management_window.gd:505` - skill tab dereferences `skill_def.level_descriptions` without checking `skill_def != null`; missing skill definitions or early window display can crash.
- `[medium] scripts/player/progression/unit_progress.gd:275` - `from_dict()` silently defaults missing `unlocked_combat_resource_ids` to HP/stamina. That is an implicit compatibility path for a new formal field.
- `[low] scripts/ui/party_management_window.gd:450` - occupied physical slots are counted as equipped items, so a two-handed weapon can display as two equipped items.

Test gaps: leader-to-reserve through connected world-map signals, missing skill def rendering, and occupied-slot equipment display.

### CU-10 Warehouse, item definitions, equipment flow

- Same mounted submap placeholder crash as the cross-CU blocker due adjacent save wiring.
- `[medium] scripts/systems/party_warehouse_service.gd:133` - `remove_item(item_id, quantity)` deletes equipment instances by matching `item_id`, so discard/sell/remove cannot target a specific instance.
- `[medium] scripts/systems/party_warehouse_service.gd:204` - `take_equipment_instance_by_item(item_id)` removes the first matching instance, so party equip can take the wrong duplicate same-item equipment.
- `[medium] scripts/systems/party_warehouse_service.gd:274` - batch swap payloads are `Array[item_id]`, so equipment withdraw/deposit cannot preserve instance selection semantics; deposit can also create a fresh default instance instead of transferring a chosen one.
- `[medium] scripts/ui/party_warehouse_window.gd:312` / `scripts/systems/game_runtime_warehouse_handler.gd:138` - the warehouse UI/runtime surface groups equipment by `item_id` and sends only `item_id` for discard-style actions. The player/runtime cannot choose between two `bronze_sword` instances with different rarity/durability.

Resolved since the original report: equipped state no longer drops equipment instance metadata. `EquipmentEntryState` persists the full `equipment_instance` payload, and unequip can return the same instance object back to `WarehouseState`.

Test gaps: same-item equipment instances with different rarity/durability through explicit instance-id equip, discard/sell/remove by instance, batch swap transfer, save round-trip, and UI/snapshot selection.

### CU-11 Party and character runtime data model

- Resolved since the original report: equipped equipment now stores a full `EquipmentInstanceState` via `EquipmentEntryState.equipment_instance`; `item_id` and `instance_id` are projections/read keys, not the truth source for instance fields.
- `[medium] scripts/systems/party_warehouse_service.gd:204` / `scripts/systems/party_equipment_service.gd:205` - the remaining CU-11 risk is ownership surface drift: `WarehouseState` and `EquipmentEntryState` own full instances, but `PartyEquipmentService.preview_equip/equip_item` still accepts only `item_id` and asks the warehouse to choose an instance.
- `[medium] scripts/player/progression/unit_progress.gd:273` - `unlocked_combat_resource_ids` is defaulted/filtered on load instead of strictly validating or recomputing from learned skill costs. Learned MP/Aura skills can enter battle with hidden resource bars.
- `[medium] scripts/systems/pending_character_reward.gd:68` - invalid pending reward entries are skipped, so malformed queues can partially apply and permanently lose omitted rewards.

Test gaps: explicit duplicate instance selection, displaced-instance round trip, malformed resource unlocks, and malformed reward queue rejection.

### CU-12 Character management, achievements, reward merge bridge

- `[high] scripts/systems/battle_runtime_module.gd:3544` - ground-targeted weapon-attack skills treat the presence of `attack_success` as applied even when `attack_success == false`. All-miss ground/AOE attacks can still record skill success, achievement progress, battle-rating cast counts, and mastery reward attribution.
- `[medium] scripts/systems/game_session.gd:1069` - starting skills are written directly into `UnitProgress` and only `sync_active_core_skill_ids()` is called. MP/Aura unlock state can remain stale.
- `[medium] scripts/systems/battle_skill_mastery_service.gd:120` - guard mastery checks physical incoming effect and guarding state, but not actual hit result. Missed, blocked, or zero-damage physical attacks can still grant guard mastery.

Test gaps: all-miss ground/AOE attribution, starting MP/Aura skills, guard mastery miss/block/zero-damage cases.

### CU-13 Progression content, conditions, seed data

- `[high] scripts/player/progression/combat_skill_def.gd:100` - `level_overrides` are documented/authored as minimum-level overrides, but only the single highest matching block is returned. Later levels lose earlier reductions, for example `warrior_aura_slash` level 5 loses the level 3 aura reduction.
- `[medium] scripts/player/progression/attribute_requirement.gd:16` - `max_value` defaults to 0 but is always enforced. Min-only authored requirements with default max are impossible. Same pattern exists in `reputation_requirement.gd` and `profession_active_condition.gd`.
- `[low] scripts/player/progression/skill_content_registry.gd:352` - unknown `effect_type` values fall through validation.

Open decision: confirm whether `level_overrides` are cumulative. Current seed content appears to assume they are.

Test gaps: cumulative multi-field overrides, impossible condition ranges, effect-specific schema validation.

### CU-14 Progression rules and attribute services

- `[medium] scripts/systems/game_session.gd:1107` - random starting book skills are written directly into `UnitProgress`, bypassing `ProgressionService.refresh_runtime_state()`. MP/Aura-cost starting skills can have hidden resource bars.
- `[medium] scripts/systems/battle_skill_mastery_service.gd:381` - `per_target_rank` returns 0 for same-faction targets, but self/ally skills such as `warrior_backstep`, `warrior_battle_recovery`, and `warrior_war_cry` use `status_applied` / `effect_applied` mastery triggers.
- `[medium] scripts/player/progression/unit_progress.gd:275` - `unlocked_combat_resource_ids` fallback lacks a schema/version decision and can load inconsistent state.

Test gaps: random-start MP/Aura skills, missing-field resource unlock behavior, and ally/self support-skill mastery.

### CU-15 Battle runtime orchestration

- `[high] scripts/systems/battle_unit_factory.gd:251` - fallback enemies can receive `basic_attack` while stamina max defaults to 0. The cast gate blocks the command, leaving fallback enemies with unusable attacks.
- `[medium] scripts/systems/battle_runtime_module.gd:1699` - battle-local unequip can create backpack equipment with empty `instance_id`; `warehouse_state.gd:28` filters empty IDs, so items can disappear during duplicate/save/writeback.
- `[medium] scripts/systems/battle_skill_mastery_service.gd:383` - same-faction support-skill mastery issue overlaps CU-14.

Test gaps: fallback/template enemy `basic_attack` affordability, battle-local unequip/writeback with invalid instance IDs, and self/ally mastery gain.

### CU-16 Battle state, edge rules, damage, AI rules

- `[high] scripts/systems/battle_ai_context.gd:62` - taunt forced-target routing now returns `null`, but AI sorting still relies on it and existing tests still assert hard taunt targeting.
- `[high] data/configs/enemies/templates/wolf_pack.tres:24` - low-stamina wolf templates cannot afford `basic_attack` or `warrior_heavy_strike`.
- `[medium] scripts/systems/battle_state.gd:124` - stale/dead/non-hostile taunt source can apply disadvantage broadly until status expiry.
- `[medium] scripts/systems/encounter_roster_builder.gd:352` - template-built enemies set `current_mp` but do not sync unlocked resource IDs, so roster casters can have MP without visible MP resources.
- `[medium] scripts/systems/battle_repeat_attack_resolver.gd:68` - repeat-attack bonus mastery is recorded before the stage hit resolves. A stage-5 miss can still grant the stage bonus.

Test gaps: taunter-death disadvantage case, real enemy stamina/action availability, roster caster MP visibility, repeat-combo stage-5 miss mastery.

### CU-17 Terrain profile, roster, prop injection

- Same `basic_attack` affordability problem as CU-16/CU-20.
- `[medium] scripts/systems/battle_ai_context.gd:62` - taunt AI regression is currently red.
- `[medium] scripts/systems/battle_terrain_generator.gd:1548` - direct anchor-based generation can ignore `region_tag`. `_build_encounter_context()` writes `battle_terrain_profile = ""`, and `_resolve_terrain_profile_id()` treats that as authoritative before checking `monster.region_tag`.

Test evidence:

- Passed: `run_battle_board_regression.gd`.
- Passed: `run_battle_spawn_reachability_regression.gd`.
- Failed: `run_battle_runtime_ai_regression.gd` with two taunt assertions.

Test gaps: anchor-only terrain profile resolution and formal-template attack affordability.

### CU-18 Battle display chain

- `[medium] scripts/ui/battle_hud_adapter.gd:1311` - multi-unit hover preview resolves already queued target IDs before the hovered `selected_coord`. After target A is queued, hovering target B can show target A's hit/damage/fate preview badge on target B.

Test gap: multi-unit skill hover after one queued target should assert the badge follows the hovered target. Screenshot/canyon capture remains unverified.

### CU-19 Automation regressions and capture helpers

- `[high] tests/battle_runtime/run_warrior_advanced_skill_regression.gd:12` - runner preloads `res://scripts/systems/battle/runtime/battle_skill_mastery_service.gd`, but that script is currently untracked. A tracked-only commit would fail to load.
- `[high] tests/progression/run_skill_schema_regression.gd:96` - schema runner requires `basic_attack`, but `data/configs/skills/basic_attack.tres` is currently untracked.
- `[medium] tests/battle_runtime/run_battle_change_equipment_requirement_regression.gd:1` - new focused runner is untracked and not listed under CU-19 in `docs/design/project_context_units.md`.
- `[medium] tests/battle_runtime/run_warrior_advanced_skill_regression.gd:314` - `_test_saint_blade_combo_runtime_consumes_follow_up_aura_on_miss()` is never called from `_run()`.
- `[medium] tests/battle_runtime/run_battle_skill_protocol_regression.gd:194` - `_test_facade_ground_aoe_selection_highlight_preview_and_execution_share_range()` is never called from `_run()`.

Residual risk: capture helpers were not run; they write capture artifacts under `res://`.

### CU-20 Enemy templates, AI brain, action seed content

- Same enemy `basic_attack` affordability and taunt drift issues as the cross-CU blocker.
- `[medium] scripts/enemies/enemy_content_registry.gd:235` - brain/template validation does not verify action `skill_ids` or template `skill_ids` against loaded skill definitions. Omitting untracked `basic_attack.tres` would not fail enemy validation.
- `[low] scripts/enemies/enemy_template_def.gd:200` - drop schema validation checks shape/quantity but not whether `drop_entries[*].item_id` exists in `item_defs`.

Test evidence:

- `run_battle_runtime_ai_regression.gd` fails with two taunt assertions.
- `run_wild_encounter_regression.gd` passed.
- `run_skill_schema_regression.gd` passed.

Test gap: seed-level AI regression that instantiates actual roster/template data and verifies each enemy brain has at least one legal pressure/support action under real stamina and weapon projection.

### CU-21 Headless runtime, text commands, snapshots

- Same mounted submap placeholder crash as the cross-CU blocker.
- Battle-local text commands already expose `battle equip <slot_id> <item_id> [instance_id=...]`, but coverage does not prove explicit `instance_id=` selection when multiple backpack entries share the same `item_id`.
- Headless helper paths that omit `instance_id` still auto-pick the first matching equipment instance for convenience. That behavior should remain test-only or be tightened; it should not become the formal player/runtime contract for instance-bearing equipment.
- Snapshot drift risk: HUD/snapshot preview code duplicates `CHANGE_EQUIPMENT_AP_COST := 2` instead of using the battle runtime constant.

Recommended verification after fixing mounted submaps:

```bash
godot --headless --script tests/text_runtime/run_submap_text_command_regression.gd
godot --headless --script tests/text_runtime/run_text_save_load_regression.gd
godot --headless --script tests/text_runtime/run_battle_equipment_text_command_regression.gd
godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
```

## Open design decisions

- Should invalid current saves/data return `ERR_INVALID_DATA` through UI/headless surfaces, or should fatal crash/quit remain acceptable for strict schema violations?
- Is `is_generated=false` mounted submap `world_data = {}` a valid current sentinel? If yes, save normalization needs an explicit branch for placeholders rather than legacy fallback.
- Is taunt now hard target forcing or soft disadvantage? Code, tests, docs, and AI scoring need one contract.
- Should enemy `basic_attack` be free/cheap enough for every formal enemy template, or should enemy stamina budgets and fallback actions be reauthored?
- Are `level_overrides` cumulative minimum-level patches or complete replacements? Current resources appear to rely on cumulative behavior.
- Equipment ownership decision for equipped state is now resolved: equipped slots store full `EquipmentInstanceState`. The open decision is how aggressively to remove or reject `item_id`-only equipment mutation commands now that instance identity exists.
- Is fallback loading for `unlocked_combat_resource_ids` intended compatibility? If so, it needs explicit approval and a migration/recompute rule.

## Suggested fix order

1. Fix mounted submap placeholder normalization/serialization, then run the world submap and text save/load regressions.
2. Resolve taunt contract and enemy fallback affordability together, then rerun `run_battle_runtime_ai_regression.gd` and add real-template AI coverage.
3. Finish equipment instance identity at the command/UI/service boundary: add instance-id equip/discard/sell/swap APIs, keep full-instance equipped storage, then add duplicate-instance selection and rarity/durability round-trip tests.
4. Fix progression override/attribute/mastery attribution issues, then add focused skill-level and mastery tests.
5. Fix fog refresh/persistence/visibility gates, then add submap and multi-cell footprint fog tests.
6. Track or remove the currently untracked required files and wire the uncalled regression functions into their runners.
