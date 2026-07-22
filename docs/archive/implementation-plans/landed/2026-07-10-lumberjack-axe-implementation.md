# Lumberjack Axe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the confirmed Lumberjack's Axe item, traits, data-driven equipment abilities, capped repeatable AP refund, and focused headless regression.

**Architecture:** The weapon remains content-driven. A source-aware `status_stacks` fact gates follow-up accuracy, damage, and kill rewards; a generic capped AP restore mode runs from the existing on-kill dispatcher. No runtime code branches on the weapon, trait, binding, or status id.

**Tech Stack:** Godot 4.6.2 Mono, C#, `.tres` resources, standalone Godot headless regression runners.

## Global Constraints

- Item id is `weapon_unique_battleaxe_lumberjack_383`; price is 38000.
- Weapon profile is melee range 1, `1D8+2` one-handed, `1D10+2` two-handed, versatile.
- Chop notch is one stack for exactly `60TU` and records `source_unit_id`.
- Follow-up attacks against the holder's own notch gain `+1` attack and `1D4 physical_slash`.
- Plant targets take `2D6 physical_slash` on every true weapon hit.
- A qualifying marked-target kill restores 1 AP with no per-turn trigger limit, capped at normal action points; stamina is never refunded.
- First-hit kills, wrong-source marks, non-weapon kills, and another weapon instance do not refund AP.
- Do not add weapon-specific C# branches or compatibility aliases.

---

### Task 1: Finalize Design And Write The Failing Regression

**Files:**
- Modify: `docs/content/weapons/implemented/2026-07-10-lumberjack-axe-design.md`
- Create: `tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs`

**Interfaces:**
- Consumes: current `ItemContentRegistry`, `ProgressionContentRegistry`, `BattleRuntimeModule`, and `WeaponAbilityCommandTestSupport`.
- Produces: a failing executable specification for content ids, projection, source-aware notch behavior, damage, and AP recovery.

- [x] **Step 1: Create a real-content fixture**

Load the normal item/progression registries, equip `weapon_unique_battleaxe_lumberjack_383`, and configure deterministic hit and damage resolvers. Do not construct fake binding definitions.

- [x] **Step 2: Assert the complete content and projection contract**

Assert item price 38000, three stable trait/binding ids, battleaxe family/range, both grip dice, versatile projection, source projection, and unequip cleanup.

- [x] **Step 3: Assert combat behavior**

Use real basic attacks plus direct attack-policy inspection to prove first-hit marking, `60TU`, one stack, source id, follow-up `+1` and `1D4`, plant `2D6`, source isolation, takeover, miss behavior, and no first-hit-kill mark.

- [x] **Step 4: Assert repeatable capped AP recovery**

Feed three distinct defeated marked targets through `ResolveOnKill(...)` with matching weapon provenance. Starting at 0/2 AP must produce 1, then 2, then remain 2. Repeat with a wrong-source mark and non-attack provenance to prove no recovery.

- [x] **Step 5: Run the new runner and verify RED**

Run:

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs
```

Expected: non-zero exit with assertions that the Lumberjack item/bindings and capped AP behavior are absent. The runner itself must compile and initialize.

### Task 2: Add Generic Source-Aware Facts And Capped On-Kill AP Restore

**Files:**
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityAuthoringDefs.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs`
- Modify: `scripts/systems/battle/runtime/BattleEquipmentAbilityRuntimeService.cs`

**Interfaces:**
- Consumes: `EquipmentAbilityFactQueryDef.status_id`, `BattleStatusEffectState.source_unit_id`, `ModifyActionPointsActionPayloadDefinition`, and `BattleKillProvenance`.
- Produces: `EquipmentAbilityFactQueryDef.require_source_unit_match`, runtime `RequireSourceUnitMatch`, and mode `restore_current_action_points_capped` available during `on_kill / after_kill`.

- [x] **Step 1: Project the source-match fact flag**

Add authoring/runtime boolean properties and copy the value in `ProjectFactQuery(...)`:

```csharp
[Export] public bool require_source_unit_match { get; set; }
public bool RequireSourceUnitMatch { get; init; }
```

- [x] **Step 2: Enforce source matching in `status_stacks`**

When `RequireSourceUnitMatch` is true, return 0 unless the queried status exists and `status.source_unit_id == sourceUnit.unit_id`; otherwise preserve current `status_stacks` behavior.

- [x] **Step 3: Register the capped AP mode**

Allow `restore_current_action_points_capped` in payload validation and require `amount > 0`. Resolve it as:

```csharp
int cap = Math.Max(target.attribute_snapshot?.GetValue(AttributeService.ACTION_POINTS) ?? 0, 0);
int next = Math.Min(target.current_ap + payload.Amount, Math.Max(cap, target.current_ap));
```

Only mutate and return the unit when `next > current_ap`.

- [x] **Step 4: Dispatch AP modification from on-kill reactions**

Add the generic `modify_action_points` branch to `ResolveOnKillActions(...)` and append the changed source unit id to the current `BattleEventBatch`.

- [x] **Step 5: Build after the generic runtime slice**

Run:

```powershell
dotnet build magic.csproj
```

Expected: exit 0, zero warnings, zero errors. The weapon regression may still fail because content is not present.

### Task 3: Add Lumberjack Item, Traits, And Equipment Pack

**Files:**
- Create: `data/configs/items/weapon_unique_battleaxe_lumberjack.tres`
- Create: `data/configs/traits/weapon_axe_lumberjack_chopping_rhythm.tres`
- Create: `data/configs/traits/weapon_axe_lumberjack_plant_slayer.tres`
- Create: `data/configs/traits/weapon_axe_lumberjack_felling_momentum.tres`
- Create: `data/configs/equipment_abilities/lumberjack_axe_pack.tres`

**Interfaces:**
- Consumes: source-aware `status_stacks`, `attack_roll_bonus.require_weapon_damage`, `add_damage_dice`, `apply_status`, kill provenance facts, and `restore_current_action_points_capped`.
- Produces: item/trait/binding ids listed in Global Constraints.

- [x] **Step 1: Add the item and three traits**

Use the battleaxe base profile with explicit family/range/damage/grip dice. Descriptions must state exact `60TU`, `1D4`, `2D6`, and capped repeatable AP recovery.

- [x] **Step 2: Define shared fact conditions**

Create a target `status_stacks` query for `lumberjack_chop_notch` with `require_source_unit_match = true`, target HP percent `> 0`, target `plant` classification, and matching kill source attack/equipment facts.

- [x] **Step 3: Define chopping rhythm actions**

Before-hit attack bonus and after-hit `1D4` both require the source-matched notch. A later-priority after-hit action refreshes a one-stack `60TU` notch only while target HP percent is greater than zero.

- [x] **Step 4: Define plant and felling actions**

Plant Slayer adds `2D6 physical_slash`. Felling Momentum runs `restore_current_action_points_capped` with `amount = 1` on every matching on-kill event and declares no once scope or charge.

- [x] **Step 5: Run the focused runner and iterate to GREEN**

Run the Lumberjack runner until all behavior assertions pass. Fix implementation/content, not the expected contract.

### Task 4: Update Context And Verify The Complete Slice

**Files:**
- Modify: `docs/design/project_context_units.md`

**Interfaces:**
- Consumes: completed generic ABI and runtime behavior.
- Produces: current owner/read-set documentation for future equipment content.

- [x] **Step 1: Document the new generic boundaries**

Add source-matched `status_stacks` to CU-13 and capped on-kill AP restore to CU-15. Do not add the individual weapon id to the context map.

- [x] **Step 2: Run focused regressions**

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_frostbite_weapon_ability_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_bonecrusher_weapon_ability_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_eternity_edge_weapon_ability_regression.cs
```

Expected: every runner reports `PASS`; known Godot finalizer leak noise is recorded separately from assertion failures.

- [x] **Step 3: Run compile verification**

```powershell
dotnet build magic.csproj
```

Expected: exit 0, zero warnings, zero errors.

- [x] **Step 4: Audit scope and hardcoding**

Use `git diff --check`, inspect only intended paths, and search scripts for Lumberjack item/trait/binding ids. Expected: no whitespace errors and no weapon-specific id in C#.

- [x] **Step 5: Commit only the Lumberjack slice when requested**

Stage explicit paths only. Never stage unrelated dirty-worktree changes.

No commit was requested for this implementation pass, so all changes remain unstaged in the existing mixed worktree.
