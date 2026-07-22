# Character Trait System Phase 4a Battle Effective Traits Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Keep this as a bridge phase: add the new battle path beside the old trait arrays, do not delete old fields yet.

**Goal:** Add `effective_trait_instances` as canonical battle trait payload, bridge trigger dispatch to read it first, and project effective traits from `BattleUnitFactory` without deleting `race_trait_ids/subrace_trait_ids/bloodline_trait_ids/ascension_trait_ids`.

**Global Constraints**

- No compatibility migrations, legacy aliases, or old-payload fallback beyond the explicit Phase 4a bridge where old arrays remain as parity projections.
- Do not delete old four trait arrays in Phase 4a.
- `effective_trait_instances` is canonical for new battle trait dispatch; `effective_trait_ids` is derived from it and must not drive behavior.
- Battle payload must be self-contained: trigger dispatch reads denormalized `effect_type`, `trigger_type`, `charge_scope`, `charge_reset_timing`, and `roll_values`, not `TraitContentRegistry`.
- Age-stage traits remain out of scope.
- Do not run battle simulation or balance runners.
- Do not commit unless explicitly requested.

## Task 1: BattleUnitState Effective Trait Payload

- Add `effective_trait_instances` and `effective_trait_ids` to `BattleUnitState`.
- Add them to `ToDictFields`, `DuplicateState()`, `ToDictionary()`, and `FromDictionary()`.
- Add strict helper validation for each effective trait payload entry:
  - exact fields: `trait_id`, `effective_instance_key`, `source_type`, `source_id`, `effect_type`, `trigger_type`, `charge_scope`, `charge_reset_timing`, `rank`, `stacks`, `roll_values`
  - unique non-empty effective keys
  - valid source/effect/trigger/charge enum values
  - `rank >= 1`, `stacks >= 1`, `roll_values` dictionary
- Enforce `effective_trait_ids == DeriveTraitIdsFromPayload(effective_trait_instances)`.
- Regression: extend/add battle unit state schema test for round-trip, clone, duplicate key rejection, derived id mismatch rejection, invalid denormalized enum rejection.

## Task 2: TraitTriggerHooks Effective Payload Bridge

- Add internal `UnitEffectiveTrait` DTO.
- Parse `BattleUnitState.effective_trait_instances` into typed effective instances.
- `OnNaturalOne`, `OnCrit`, `OnFatalDamage` should dispatch from effective payload when non-empty; old trait arrays remain bridge fallback only when effective payload is empty.
- Charge key must be `EffectiveInstanceKey`.
- `OnBattleStartResult` and `OnTurnStartResult` seed/reset by `charge_scope` and `charge_reset_timing`, not by `trigger_type`.
- Regression: extend trait trigger regression for:
  - stack-by-instance same trait independent charge keys
  - passive trigger with battle-start/turn-start charge still seeds
  - old arrays still work only when effective payload is empty

## Task 3: BattleUnitFactory Projection

- Add `IBattleRuntimeCharacterGateway` method to build effective trait set for a member/equipment view, or expose a typed payload helper from `CharacterManagementModule`.
- `BattleUnitFactory` writes `effective_trait_instances` and derived `effective_trait_ids` during ally build, `RefreshBattleUnit`, and `RefreshEquipmentProjection`.
- Keep `PassiveStatusOrchestrator` old static identity projections intact for Phase 4a parity.
- Regression: add/extend unit factory regression proving player unit and equipment refresh project effective payload.

## Task 4: AI Guard And Focused Verification

- Add `effective_trait_instances` and `effective_trait_ids` to `BattleAiMutationGuard` snapshot/restore/fingerprint with deep copies.
- Run focused verification:
  - `dotnet build magic.csproj`
  - battle unit state schema contract regression
  - trait trigger regression
  - battle unit factory weapon/projection regression
  - relevant Phase 3 trait regressions
