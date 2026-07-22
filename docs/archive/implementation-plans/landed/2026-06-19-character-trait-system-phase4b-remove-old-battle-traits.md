# Character Trait System Phase 4b Old Battle Trait Field Removal Plan

Goal: remove `BattleUnitState.race_trait_ids`, `subrace_trait_ids`, `bloodline_trait_ids`, and `ascension_trait_ids`; `effective_trait_instances` becomes the only battle trait behavior source.

## Tasks

1. Add/update schema and passive-status tests so old four trait arrays are rejected/absent, while identity static projections keep non-trait outputs.
2. Remove old four fields from `BattleUnitState` fields, clone, `ToDictionary()`, `FromDictionary()`, and AI mutation guard snapshots.
3. Remove old-array fallback from `TraitTriggerHooks` and delete the old `GetUnitTraitIds` / `UnitHasTrait` path.
4. Remove trait-id projection from `PassiveStatusOrchestrator`, `RaceTraitResolver`, and `AscensionTraitResolver`; preserve vision/proficiency/save-advantage/damage-resistance/racial skill charges.
5. Run focused verification: `dotnet build`, battle unit schema, trait trigger, passive status orchestrator, AI guard, battle unit factory, and Phase 3 trait regressions.
