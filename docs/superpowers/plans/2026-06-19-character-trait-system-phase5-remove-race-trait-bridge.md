# Character Trait System Phase 5 Race Trait Bridge Removal Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the old `RaceTraitDef` / `RaceTraitContentRegistry` / `race_trait_defs` formal bridge after battle effective traits are canonical.

**Architecture:** Runtime and validation code should use `TraitDef`, `TraitEffectKind`, `TraitContentRules`, and `trait_defs` only. Identity definitions may still reference `trait_ids`, but those IDs validate against generic `TraitDef` with `identity` source scope; battle trigger dispatch consumes denormalized effective payload and generic trait effect kinds.

**Tech Stack:** Godot 4.6 C#, strict Godot payload validation, focused headless regression runners.

## Global Constraints

- No compatibility fallback for `race_trait_defs`, `RaceTraitDef`, or old race trait resources.
- Do not add legacy alias, fallback migration, or old payload/schema support.
- Dictionary usage remains limited to resource/payload/test boundaries; runtime logic must use typed getters and DTOs.
- Do not run numeric battle simulation as part of routine verification.

---

### Task 1: Genericize Trigger Dispatch Constants

**Files:**
- Modify: `scripts/player/progression/TraitTriggerContentRules.cs`
- Modify: `scripts/systems/battle/runtime/TraitTriggerHooks.cs`
- Modify: `tests/battle_runtime/skills/run_trait_trigger_regression.cs`
- Test: `tests/battle_runtime/skills/run_trait_trigger_regression.cs`

**Interfaces:**
- Consumes: `TraitContentRules.ToEffectKind(StringName)`, `TraitContentRules.ToStringName(TraitEffectKind)`.
- Produces: `TraitTriggerContentRules.GetDispatchTriggerRules(): IReadOnlyList<TraitTriggerDispatchRule>`.

- [ ] Write failing expectations that trait trigger tests no longer reference `RaceTraitDef` or `RaceTraitEffectKind`.
- [ ] Change `TraitTriggerContentRules` dispatch map keys from `RaceTraitEffectKind` to `TraitEffectKind`.
- [ ] Change dispatch trait ids and trigger query methods to use `TraitContentRules`.
- [ ] Change `TraitTriggerHooks` constants to use `TraitContentRules.ToStringName(TraitEffectKind.*)`.
- [ ] Run `dotnet build magic.csproj` and `godot --headless -s res://tests/battle_runtime/skills/run_trait_trigger_regression.cs`.

### Task 2: Move Character Creation Human Versatility To TraitDef

**Files:**
- Modify: `scripts/ui/CharacterCreationWindow.cs`
- Test: `dotnet build magic.csproj`

**Interfaces:**
- Consumes: `ProgressionContentRegistry.GetTraitDefsTyped()`.
- Removes: `_get_race_trait_def(...)` and `RaceTraitDef` reads from character creation.

- [ ] Replace `_get_race_trait_def` with `_get_trait_def`.
- [ ] In `_selected_identity_has_human_versatility()`, check direct trait id first, then `TraitDef.effect_type == "human_versatility"`.
- [ ] Run `dotnet build magic.csproj`.

### Task 3: Remove Progression Race Trait Bridge

**Files:**
- Modify: `scripts/player/progression/ProgressionContentRegistry.cs`
- Modify: `tests/runtime/validation/ContentValidationRunner.cs`
- Modify: `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- Test: `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- Test: `tests/runtime/validation/run_resource_validation_regression.cs`

**Interfaces:**
- Keeps: `ProgressionContentRegistry.GetTraitDefsTyped()`.
- Removes: `_race_trait_defs`, `GetRaceTraitDefsTyped()`, `RaceTraitContentRegistry` ownership, and race-trait phase2 validation.

- [ ] Update tests so `race_trait_defs` is not part of replacement buckets and `GetRaceTraitDefsTyped()` is not expected.
- [ ] Remove race trait dictionary/index/registry fields and lifecycle calls from `ProgressionContentRegistry`.
- [ ] Remove `race_trait_defs` from `ReplaceDefinitionBuckets()` and validation runner projection.
- [ ] Keep identity `trait_ids` validation against `_traitDefIndex` and `TraitSourceKind.Identity`.
- [ ] Run `dotnet build magic.csproj`, progression content typed regression, and resource validation regression.

### Task 4: Remove Old RaceTrait Resource And Fixtures

**Files:**
- Delete: `scripts/player/progression/RaceTraitDef.cs`
- Delete: `scripts/player/progression/RaceTraitContentRegistry.cs`
- Delete: `data/configs/race_traits/*.tres`
- Delete or rewrite: `tests/progression/identity/run_race_trait_content_registry_regression.cs`
- Delete or rewrite: `tests/progression/fixtures/identity_registry_invalid/race_traits/*.tres`
- Modify: `tests/progression/identity/run_trait_content_registry_regression.cs`
- Test: `tests/progression/identity/run_trait_content_registry_regression.cs`

**Interfaces:**
- Keeps: `data/configs/traits/*.tres` as the only trait resource directory.
- Removes: official and fixture references to `script_class="RaceTraitDef"` and `RaceTraitDef.cs`.

- [ ] Update trait registry regression to assert official generic trait count and identity source scope without bridge parity.
- [ ] Remove the old race trait registry regression or rewrite it as identity-domain validation without `RaceTraitContentRegistry`.
- [ ] Delete old race trait resources and invalid race trait fixture directory.
- [ ] Run `rg -n "RaceTraitDef|RaceTraitEffectKind|RaceTraitContentRegistry|race_trait_defs|data/configs/race_traits" scripts tests data project.godot docs/design/project_context_units.md`.
- [ ] Run `dotnet build magic.csproj` and `godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs`.

### Task 5: Documentation And Verification Matrix

**Files:**
- Modify: `docs/design/project_context_units.md`
- Modify: `.git/sdd/progress.md`

**Verification:**
- `dotnet build magic.csproj`
- `godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs`
- `godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- `godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs`
- `godot --headless -s res://tests/battle_runtime/skills/run_trait_trigger_regression.cs`
- `godot --headless -s res://tests/battle_runtime/skills/run_passive_status_orchestrator_regression.cs`
- `godot --headless -s res://tests/battle_runtime/runtime/run_battle_unit_factory_weapon_projection_regression.cs`

- [ ] Update context units so trait content ownership names `TraitDef/TraitContentRegistry/data/configs/traits` only.
- [ ] Record Phase 5 progress after focused tests pass.
- [ ] Run the verification matrix and do not claim completion until every command exits 0.
