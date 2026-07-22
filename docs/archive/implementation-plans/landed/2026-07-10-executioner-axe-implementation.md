# Executioner Axe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the approved Executioner's Axe as data-driven equipment content with a visible Death Sentence action and hidden triggered skills for judgment, backlash, and fear.

**Architecture:** Reuse equipment target marks for ownership and expiry, with each typed mark owning its remaining TU while the status is only a display mirror. Add generic typed `critical_hit_override` and `trigger_skill` equipment actions, and route triggered effects through normal `SkillDef` resolution. Carry judgment provenance through the attack result so the existing on-kill equipment path can trigger the hidden fear skill without exposing it through skill availability. Hidden SkillDefs use the blocked `internal` learn source. Keep execute and elite/boss fallback in separate hidden SkillDefs because the content validator requires execute effects to have no siblings; the execute effect explicitly disables soul fracture.

**Tech Stack:** Godot 4.6, C#, typed Godot resources, headless C# regression runners.

## Global Constraints

- Death Sentence: range 1, 1AP, 60 stamina, 300TU cooldown, 60TU mark.
- Marked successful weapon hit becomes critical; misses do not consume the mark.
- Ordinary/elite/boss execute thresholds are 100%/50%/25% after weapon damage.
- Elite/boss fallback is `3D12 negative_energy` on failed DC16 Constitution.
- Backlash is DC14 Will, `2D12+2 physical_slash`, and may reduce the wielder to 0 HP.
- Successful execution triggers enemy-only DC14 Will fear in diamond radius 3 for 60TU.
- Internal skills are catalog-only, never granted, selectable, learnable, or mastery-bearing.

---

### Task 1: Red regression for the complete weapon contract

**Files:**
- Create: `tests/battle_runtime/runtime/run_executioner_axe_weapon_ability_regression.cs`

**Interfaces:**
- Consumes: existing item/trait/skill/equipment registries and `WeaponAbilityCommandTestSupport`.
- Produces: one end-to-end regression covering content, projection, real commands, expiry, and hidden skill visibility.

- [x] Write assertions for all contracts in the approved spec.
- [x] Run `godot --headless --script tests/battle_runtime/runtime/run_executioner_axe_weapon_ability_regression.cs` and confirm it fails because the content is absent.

### Task 2: Generic typed equipment actions and mark-expiry trigger

**Files:**
- Create: `scripts/player/progression/equipment_abilities/CriticalHitOverrideActionPayloadDef.cs`
- Create: `scripts/player/progression/equipment_abilities/TriggerSkillActionPayloadDef.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityAuthoringDefs.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityBuiltInHandlerSpecs.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs`
- Modify: `scripts/systems/battle/core/AttackCheckInput.cs`
- Modify: `scripts/systems/battle/core/BattleAttackCheckPolicyContext.cs`
- Modify: `scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs`
- Modify: `scripts/systems/battle/rules/BattleHitResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/runtime/BattleEquipmentAbilityRuntimeService.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`

**Interfaces:**
- Produces: typed forced-critical metadata, crit-lock-aware preview projection, `trigger_skill` action resolution, independently timed `on_target_mark_expired` reactions, and optional target-mark removal from `clear_status`.

- [x] Add registry/schema assertions to the failing weapon regression.
- [x] Implement projection and validation for the two actions and expiry trigger.
- [x] Make forced critical metadata survive preview/runtime attack-check copies.
- [x] Resolve internal unit/ground SkillDefs without action costs, cooldowns, availability, or mastery.
- [x] Dispatch mark expiry before the mirrored status is erased.
- [x] Run the focused regression until the generic mechanism cases pass.

### Task 3: Executioner content resources

**Files:**
- Create: `data/configs/items/weapon_unique_greataxe_executioner.tres`
- Create: `data/configs/traits/weapon_axe_executioner_execution.tres`
- Create: `data/configs/traits/weapon_axe_executioner_death_sentence.tres`
- Create: `data/configs/traits/weapon_axe_executioner_self_execution.tres`
- Create: `data/configs/skills/weapon_axe_executioner_death_sentence.tres`
- Create: `data/configs/skills/weapon_axe_executioner_judgment_resolution.tres`
- Create: `data/configs/skills/weapon_axe_executioner_judgment_fallback.tres`
- Create: `data/configs/skills/weapon_axe_executioner_self_execution.tres`
- Create: `data/configs/skills/weapon_axe_executioner_execution_fear.tres`
- Create: `data/configs/equipment_abilities/executioner_axe_pack.tres`

**Interfaces:**
- Consumes: generic forced critical, trigger skill, target mark, save, execute, and status mechanisms.
- Produces: all player-visible and internal content for `weapon_unique_greataxe_executioner_384`.

- [x] Add the item and three player-visible traits.
- [x] Add the visible Death Sentence SkillDef with exact costs and TU values.
- [x] Add catalog-only judgment, backlash, and fear SkillDefs.
- [x] Configure ordinary/elite/boss post-hit branches entirely in the equipment pack.
- [x] Configure judgment-only on-kill provenance to trigger fear.
- [x] Run the focused regression and both content registries.

### Task 4: Cross-path verification and context map

**Files:**
- Modify: `docs/design/project_context_units.md`
- Modify as needed: focused shared test helpers that copy attack-check metadata.

**Interfaces:**
- Produces: documented ownership for triggered equipment skills and stable validation evidence.

- [x] Update CU-15/CU-16 with forced-critical, triggered-skill, and mark-expiry ownership.
- [x] Run the executioner regression.
- [x] Run `godot --headless --script tests/progression/schema/run_equipment_ability_content_registry_regression.cs`.
- [x] Run the nearest skill content registry regression.
- [x] Run `dotnet build magic.csproj`.
- [x] Run `rg -n "weapon_unique_greataxe_executioner_384|weapon\.axe\.executioner|binding\.weapon\.axe\.executioner" scripts` and confirm no weapon-specific runtime branch.
- [x] Run `git diff --check`.

### Task 5: Independent review hardening

- [x] Make execute soul fracture optional and assert that successful judgment saves and death prevention add no status.
- [x] Add the blocked `internal` learn source and verify every catalog-only skill is rejected by `ProgressionService`.
- [x] Make forced-critical preview respect crit lock and keep the mark on a non-critical hit.
- [x] Move finite target-mark timing into typed mark state and verify two equipment sources expire independently.
- [x] Reconcile shared mirror status from the longest remaining typed mark after consume, replace, expiry, or source cleanup.
- [x] Reconcile equipment projection and target marks centrally after durability destruction across every damage resolver consumer.
- [x] Let final forced-critical provenance override an immediate weapon attack fallback without losing ordinary outer on-kill attribution.
- [x] Re-run Executioner, Oathscar, execute schema/rules, attack-policy parity, and content-registry regressions.
