# GodotSharp Lifecycle Phase 3 Process Content and Typed Snapshots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all non-Enemy/AI runtime Resource catalogs with one process-managed raw content borrow anchor and an immutable typed `ContentSnapshot` shared across sessions.

**Architecture:** `ProcessContentHost` is the sole project-side managed borrow anchor for path-backed content/engine assets and seals after load/validate/project. Raw `.tres` remains immutable authoring input. `ContentSnapshotBuilder` projects every non-AI domain to typed managed definitions; `GameSession/GameRoot/GameContentCatalog` borrow the snapshot instead of rebuilding Resource indexes. Enemy/AI Resources remain one exact `LegacyDebt` until Phase 4.

**Tech Stack:** Godot 4.6.2 Mono, C#/.NET 8, Godot ResourceLoader, immutable/read-only CLR collections, headless regressions.

## Global Constraints

- Phases 1 and 2 must be green before starting.
- Preserve save version 12, gameplay rules, catalog IDs, content validation errors, AI fingerprint, and snapshots.
- V1 has one raw `ProcessContentHost` and one active snapshot per Godot process; no content hot reload.
- Tests needing different raw content run in another process; same-process tests use pure CLR synthetic snapshots.
- Raw loaded Resources are borrowed from Godot ResourceLoader/cache. Do not call `Dispose()` on path-backed cached roots.
- Do not mutate loaded `.tres`; apply defaults/merge in typed projections.
- Phase 3 must migrate all non-AI Resource domains, including race/subrace/age/bloodline/ascension/stage/faith/barrier/contingency.
- The single legacy Enemy/AI boundary, including BattleSim profiles that embed raw AI score/brain/action data, is the only permitted Phase 3 `LegacyDebt`.
- Do not run battle simulation or balance runners.
- Treat each task's `Files` list as its staging manifest. Before every commit, inspect `git diff --cached --name-only`, `git diff --cached`, and `git diff --cached --check`; never commit a directory-wide add without matching cached paths/hunks to that manifest.
- Preserve the current user-owned EnemyTemplate and two AI regression edits. Tasks 3/6 that overlap them must stage only the content-migration hunks or stop before committing if separation is impossible.
- Every new C# regression runner derives from `LifecycleTestSceneTree` and exits through `RequestTestExit(_test.Finish(label))`.

---

### Task 1: Restricted content value graph and SkillDefinition cleanup

**Files:**
- Create: `scripts/systems/content/ContentValueNormalizer.cs`
- Modify: `scripts/player/progression/SkillDefinition.cs`
- Modify: `scripts/player/progression/SkillDef.cs`
- Modify: `scripts/player/progression/CombatEffectDef.cs`
- Modify: `scripts/player/progression/SkillContentRegistry.cs:274-315`
- Modify: `scripts/systems/progression/SkillLevelDescriptionFormatter.cs`
- Modify: `scripts/ui/PartyManagementWindow.cs`
- Modify: `scripts/systems/battle/ai/BattleAiChargeActionEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiChargePathAoeActionEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.cs`
- Modify: `scripts/systems/battle/core/BattleRepeatAttackStageSpec.cs`
- Modify: `scripts/systems/battle/rules/BattleHitResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- Modify: `scripts/systems/battle/rules/PhantasmalKillExecutionRules.cs`
- Modify: `scripts/systems/battle/runtime/BattleChargeResolver.cs`
- Modify: `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
- Modify: `scripts/systems/battle/runtime/BattleRepeatAttackResolver.cs`
- Modify: `scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs`
- Modify: `scripts/systems/battle/runtime/SkillPassiveResolver.cs`
- Modify: `scripts/systems/battle/terrain/BattleTerrainEffectSystem.cs`
- Create: `tests/runtime/validation/run_skill_definition_plain_value_graph_regression.cs`
- Test: `tests/runtime/validation/run_skill_catalog_query_regression.cs`
- Test: `tests/progression/core/run_level_description_template_regression.cs`
- Test: `tests/progression/core/run_skill_description_consistency_regression.cs`
- Test: `tests/progression/schema/run_contingency_content_validator_regression.cs`

**Interfaces:**
- Consumes: raw Variant/Dictionary fields only while projecting a SkillDef.
- Produces: recursively normalized list/map/scalar parameters with no Godot collection or `Variant.Type.Object`.

- [ ] **Step 1: Write recursive red cases**

Cover null/bool/int/float/string/StringName/math value/list/map, nested object rejection with exact path, and unchanged skill fingerprint/level descriptions.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_skill_definition_plain_value_graph_regression.cs
```

Expected: current shallow Variant maps accept/retain disallowed wrappers.

- [ ] **Step 3: Implement the normalizer**

```csharp
internal static class ContentValueNormalizer
{
    internal static object NormalizeVariant(Variant value, string path);
    internal static IReadOnlyDictionary<string, object> NormalizeDictionary(
        Godot.Collections.Dictionary source,
        string path
    );
    internal static IReadOnlyList<object> NormalizeArray(
        Godot.Collections.Array source,
        string path
    );
}
```

Allowed values: null, bool, integral/floating numbers, string, StringName, approved Godot math values, nested read-only list/map. `Variant.Type.Object` and unknown types throw `InvalidDataException` containing the full path. Never fallback to `ToString()`.

Change these properties to restricted read-only graphs:

```text
SkillDefinition.LevelDescriptionConfigs
ContingencyAutomationDefinition.AllowedParameterBindings
CombatSkillDefinition.LevelOverrides
CombatCastVariantDefinition.Parameters
CombatEffectDefinition.Parameters
```

Move `NormalizeSkillDef` defaults into `SkillDefinition.FromResource`; do not write back to the borrowed `SkillDef`.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_skill_definition_plain_value_graph_regression.cs
godot --headless -s res://tests/runtime/validation/run_skill_catalog_query_regression.cs
godot --headless -s res://tests/progression/core/run_level_description_template_regression.cs
godot --headless -s res://tests/progression/core/run_skill_description_consistency_regression.cs
python tests/run_regression_suite.py --pattern progression --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
git add scripts/systems/content/ContentValueNormalizer.cs scripts/player/progression scripts/systems/progression scripts/ui/PartyManagementWindow.cs scripts/systems/battle tests/runtime/validation tests/progression
git commit -m "refactor: normalize skill content to plain values"
```

---

### Task 2: Progression, identity, quest, faith, barrier, and contingency definitions

**Files:**
- Create: `scripts/player/progression/definitions/TraitDefinition.cs`
- Create: `scripts/player/progression/definitions/ProfessionDefinition.cs`
- Create: `scripts/player/progression/definitions/QuestDefinition.cs`
- Create: `scripts/player/progression/definitions/RaceDefinition.cs`
- Create: `scripts/player/progression/definitions/SubraceDefinition.cs`
- Create: `scripts/player/progression/definitions/AgeProfileDefinition.cs`
- Create: `scripts/player/progression/definitions/BloodlineDefinition.cs`
- Create: `scripts/player/progression/definitions/BloodlineStageDefinition.cs`
- Create: `scripts/player/progression/definitions/AscensionDefinition.cs`
- Create: `scripts/player/progression/definitions/AscensionStageDefinition.cs`
- Create: `scripts/player/progression/definitions/StageAdvancementDefinition.cs`
- Create: `scripts/player/progression/definitions/FaithDeityDefinition.cs`
- Create: `scripts/player/progression/definitions/FaithRankDefinition.cs`
- Create: `scripts/player/progression/definitions/BarrierProfileDefinition.cs`
- Create: `scripts/player/progression/definitions/ContingencySetupTemplateDefinition.cs`
- Create: `scripts/player/progression/definitions/AchievementDefinition.cs`
- Create: `scripts/player/progression/definitions/AchievementRewardDefinition.cs`
- Create: `scripts/player/progression/definitions/AgeStageRuleDefinition.cs`
- Create: `scripts/player/progression/definitions/RacialGrantedSkillDefinition.cs`
- Create: `scripts/player/progression/definitions/ProfessionActiveConditionDefinition.cs`
- Create: `scripts/player/progression/definitions/ProfessionGrantedSkillDefinition.cs`
- Create: `scripts/player/progression/definitions/ProfessionPromotionRequirementDefinition.cs`
- Create: `scripts/player/progression/definitions/ProfessionRankGateDefinition.cs`
- Create: `scripts/player/progression/definitions/ProfessionRankRequirementDefinition.cs`
- Create: `scripts/player/progression/definitions/AttributeRequirementDefinition.cs`
- Create: `scripts/player/progression/definitions/ReputationRequirementDefinition.cs`
- Create: `scripts/player/progression/definitions/TagRequirementDefinition.cs`
- Create: `scripts/player/progression/definitions/TraitDamageResistanceEntryDefinition.cs`
- Create: `scripts/player/progression/definitions/TraitPassiveStatusEffectDefinition.cs`
- Create: `scripts/player/progression/definitions/TraitRollValueSchemaEntryDefinition.cs`
- Create: `scripts/player/progression/definitions/TraitSaveBonusEntryDefinition.cs`
- Create: `scripts/player/progression/definitions/BarrierLayerDefinition.cs`
- Create: `scripts/player/progression/definitions/BarrierOutcomeDefinition.cs`
- Create: `scripts/player/progression/FaithContentRegistry.cs`
- Modify: `scripts/player/progression/TraitDef.cs`
- Modify: `scripts/player/progression/ProfessionDef.cs`
- Modify: `scripts/player/progression/AchievementDef.cs`
- Modify: `scripts/player/progression/AchievementRewardDef.cs`
- Modify: `scripts/player/progression/QuestDef.cs`
- Modify: `scripts/player/progression/RaceDef.cs`
- Modify: `scripts/player/progression/SubraceDef.cs`
- Modify: `scripts/player/progression/AgeProfileDef.cs`
- Modify: `scripts/player/progression/AgeStageRule.cs`
- Modify: `scripts/player/progression/RacialGrantedSkill.cs`
- Modify: `scripts/player/progression/BloodlineDef.cs`
- Modify: `scripts/player/progression/BloodlineStageDef.cs`
- Modify: `scripts/player/progression/AscensionDef.cs`
- Modify: `scripts/player/progression/AscensionStageDef.cs`
- Modify: `scripts/player/progression/StageAdvancementModifier.cs`
- Modify: `scripts/player/progression/FaithDeityDef.cs`
- Modify: `scripts/player/progression/FaithRankDef.cs`
- Modify: `scripts/player/progression/BarrierProfileDef.cs`
- Modify: `scripts/player/progression/BarrierLayerDef.cs`
- Modify: `scripts/player/progression/BarrierOutcomeDef.cs`
- Modify: `scripts/player/progression/ContingencySetupTemplateDef.cs`
- Modify: `scripts/player/progression/ProfessionActiveCondition.cs`
- Modify: `scripts/player/progression/ProfessionGrantedSkill.cs`
- Modify: `scripts/player/progression/ProfessionPromotionRequirement.cs`
- Modify: `scripts/player/progression/ProfessionRankGate.cs`
- Modify: `scripts/player/progression/ProfessionRankRequirement.cs`
- Modify: `scripts/player/progression/AttributeRequirement.cs`
- Modify: `scripts/player/progression/ReputationRequirement.cs`
- Modify: `scripts/player/progression/TagRequirement.cs`
- Modify: `scripts/player/progression/TraitDamageResistanceEntryDef.cs`
- Modify: `scripts/player/progression/TraitPassiveStatusEffectDef.cs`
- Modify: `scripts/player/progression/TraitRollValueSchemaEntry.cs`
- Modify: `scripts/player/progression/TraitSaveBonusEntryDef.cs`
- Modify: `scripts/systems/progression/ProgressionIdentityCatalogData.cs`
- Modify: `scripts/player/progression/AgeContentRegistry.cs`
- Modify: `scripts/player/progression/AscensionContentRegistry.cs`
- Modify: `scripts/player/progression/BarrierContentRegistry.cs`
- Modify: `scripts/player/progression/BloodlineContentRegistry.cs`
- Modify: `scripts/player/progression/ContingencyTemplateContentRegistry.cs`
- Modify: `scripts/player/progression/ProfessionContentRegistry.cs`
- Modify: `scripts/player/progression/QuestContentRegistry.cs`
- Modify: `scripts/player/progression/RaceContentRegistry.cs`
- Modify: `scripts/player/progression/StageAdvancementContentRegistry.cs`
- Modify: `scripts/player/progression/SubraceContentRegistry.cs`
- Modify: `scripts/player/progression/TraitContentRegistry.cs`
- Modify: `scripts/player/progression/ProgressionContentRegistry.cs`
- Modify runtime consumers: `scripts/player/progression/QuestContentValidator.cs`
- Modify runtime consumers: `scripts/player/progression/QuestProviderContentRules.cs`
- Modify runtime consumers: `scripts/player/progression/QuestState.cs`
- Modify runtime consumers: `scripts/player/progression/TraitContentRules.cs`
- Modify runtime consumers: `scripts/player/progression/TraitInstanceState.cs`
- Modify runtime consumers: `scripts/player/warehouse/ItemTraitContentValidator.cs`
- Modify runtime consumers: `scripts/systems/attributes/AttributeService.cs`
- Modify runtime consumers: `scripts/systems/attributes/AttributeSourceContext.cs`
- Modify runtime consumers: `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
- Modify runtime consumers: `scripts/systems/battle/core/BattleBarrierInstanceState.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/AscensionTraitResolver.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleBarrierService.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleTraitPassiveProjectionService.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleUnitFactory.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/RaceTraitResolver.cs`
- Modify runtime consumers: `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`
- Modify runtime consumers: `scripts/systems/content/GameContentCatalog.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/BattleSessionFacade.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeFacade.Contingency.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeFacade.QuestReward.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`
- Modify runtime consumers: `scripts/systems/inventory/EquipmentTraitRollService.cs`
- Modify runtime consumers: `scripts/systems/inventory/ItemTraitDetailText.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.CharacterCreation.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.ContentValidation.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs`
- Modify runtime consumers: `scripts/systems/progression/AgeStageResolver.cs`
- Modify runtime consumers: `scripts/systems/progression/AscensionApplyService.cs`
- Modify runtime consumers: `scripts/systems/progression/BloodlineApplyService.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterCreationIdentityOptionService.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterManagementModule.ContentDefs.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterManagementModule.Helpers.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterManagementModule.NestedTypes.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterTraitService.cs`
- Modify runtime consumers: `scripts/systems/progression/ContingencyContentRules.cs`
- Modify runtime consumers: `scripts/systems/progression/EffectiveTrait.cs`
- Modify runtime consumers: `scripts/systems/progression/FaithService.cs`
- Modify runtime consumers: `scripts/systems/progression/IdentityPayloadValidator.cs`
- Modify runtime consumers: `scripts/systems/progression/PassiveSourceContext.cs`
- Modify runtime consumers: `scripts/systems/progression/PracticeGrowthService.cs`
- Modify runtime consumers: `scripts/systems/progression/ProfessionAssignmentService.cs`
- Modify runtime consumers: `scripts/systems/progression/ProfessionRuleService.cs`
- Modify runtime consumers: `scripts/systems/progression/ProgressionService.cs`
- Modify runtime consumers: `scripts/systems/progression/ProgressionServiceFactory.cs`
- Modify runtime consumers: `scripts/systems/progression/QuestAcceptRequirementEvaluator.cs`
- Modify runtime consumers: `scripts/systems/progression/QuestProgressService.cs`
- Modify runtime consumers: `scripts/systems/progression/RacialSkillGrantService.cs`
- Modify runtime consumers: `scripts/systems/progression/StageAdvancementApplyService.cs`
- Modify runtime consumers: `scripts/systems/settlement/SettlementShopService.cs`
- Modify runtime consumers: `scripts/systems/world/EncounterRosterBuilder.cs`
- Modify runtime consumers: `scripts/ui/CharacterCreationWindow.cs`
- Modify runtime consumers: `scripts/ui/PartyManagementWindow.cs`
- Test: `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- Test: `tests/progression/identity/run_trait_content_registry_regression.cs`
- Test: `tests/runtime/validation/run_quest_content_validator_typed_regression.cs`
- Test: `tests/progression/schema/run_identity_sub_registry_schema_regression.cs`
- Test: `tests/progression/fate/run_faith_service_regression.cs`
- Test: `tests/progression/fate/run_faith_service_reward_regression.cs`

**Interfaces:**
- Consumes: validated authoring Resources at build time.
- Produces: immutable `*Definition` dictionaries/lists for every non-AI progression domain.

- [ ] **Step 1: Add snapshot graph and field-parity failures**

For each Resource/definition pair, assert ID/key parity, all exported gameplay fields project, source Resource remains byte/field-equivalent after projection, returned collections reject mutation, and recursive graph audit finds no GodotObject/Array/Dictionary/Object Variant.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs
godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs
```

- [ ] **Step 3: Add immutable projection pattern**

Each definition exposes a `FromResource` projector inside its definition file and copies every authored field/nested child into read-only managed values. `TraitDefinition` locks the representative full-field contract:

```csharp
public sealed record TraitDefinition(
    StringName TraitId,
    string DisplayName,
    string Description,
    IReadOnlyList<StringName> Categories,
    IReadOnlyList<StringName> AllowedSourceKinds,
    StringName EffectType,
    StringName TriggerType,
    StringName StackPolicy,
    StringName ChargeScope,
    StringName ChargeResetTiming,
    StringName HighestRollCompareKey,
    int VisionRange,
    int ProficiencyChoiceCount,
    IReadOnlyList<AttributeModifierDefinition> AttributeModifiers,
    IReadOnlyList<StringName> SaveAdvantageTags,
    IReadOnlyList<TraitDamageResistanceEntryDefinition> DamageResistanceEntries,
    IReadOnlyList<TraitSaveBonusEntryDefinition> SaveBonusEntries,
    IReadOnlyList<TraitPassiveStatusEffectDefinition> PassiveStatusEffects,
    IReadOnlyList<TraitRollValueSchemaEntryDefinition> RollValueSchema
)
;

internal static class TraitDefinitionProjector
{
    internal static TraitDefinition FromResource(TraitDef source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TraitDefinition(
            source.trait_id,
            source.display_name,
            source.description,
            source.categories.ToArray(),
            source.allowed_source_kinds.ToArray(),
            source.effect_type,
            source.trigger_type,
            source.stack_policy,
            source.charge_scope,
            source.charge_reset_timing,
            source.GetHighestRollCompareKey(),
            source.vision_range,
            source.proficiency_choice_count,
            source.attribute_modifiers
                .Where(value => value != null)
                .Select(AttributeModifierDefinition.FromResource)
                .ToArray(),
            source.save_advantage_tags.ToArray(),
            source.damage_resistance_entries
                .Where(value => value != null)
                .Select(TraitDamageResistanceEntryDefinition.FromResource)
                .ToArray(),
            source.save_bonus_entries
                .Where(value => value != null)
                .Select(TraitSaveBonusEntryDefinition.FromResource)
                .ToArray(),
            source.passive_status_effects
                .Where(value => value != null)
                .Select(TraitPassiveStatusEffectDefinition.FromResource)
                .ToArray(),
            source.roll_value_schema
                .Where(value => value != null)
                .Select(TraitRollValueSchemaEntryDefinition.FromResource)
                .ToArray()
        );
    }
}

public FaithService(
    IReadOnlyDictionary<StringName, FaithDeityDefinition> faithDeities
)
{
    _faithDeities = faithDeities
        ?? new ReadOnlyDictionary<StringName, FaithDeityDefinition>(
            new Dictionary<StringName, FaithDeityDefinition>()
        );
}
```

`FaithContentRegistry` scans/validates Faith Resources through `IContentResourceLoader` during host build; `FaithService` receives immutable definitions and loses its constructor-time `Rebuild`/`ResourceLoader.Load` path. `BattleBarrierService` receives the snapshot barrier index instead of constructing `BarrierContentRegistry`. `ProgressionContentRegistry` remains a host build-time loader/projector/validator; it no longer becomes a session/runtime content owner. `ProgressionIdentityCatalogData` contains definitions only. The typed regression stores the exact runtime-consumer paths listed in this task and fails if any of them contains a raw authored type in a field, parameter, return type, generic argument, or constructor call.

- [ ] **Step 4: Run domain green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs
godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs
godot --headless -s res://tests/runtime/validation/run_quest_content_validator_typed_regression.cs
godot --headless -s res://tests/progression/schema/run_identity_sub_registry_schema_regression.cs
godot --headless -s res://tests/progression/fate/run_faith_service_regression.cs
godot --headless -s res://tests/progression/fate/run_faith_service_reward_regression.cs
python tests/run_regression_suite.py --pattern progression --jobs 16 --finalizer-crash-retries 0
rg -n "\b(TraitDef|ProfessionDef|QuestDef|RaceDef|SubraceDef|AgeProfileDef|BloodlineDef|AscensionDef|StageAdvancementModifier|FaithDeityDef|FaithRankDef|BarrierProfileDef|ContingencySetupTemplateDef)\b" scripts/systems scripts/ui --glob "*.cs"
dotnet build magic.csproj
# Stage only the exact Task 2 Files manifest; patch-stage protected user hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: project progression content definitions"
```

---

### Task 3: Item/Recipe definitions and derived merge

**Files:**
- Create: `scripts/player/warehouse/ItemDefinition.cs`
- Create: `scripts/player/warehouse/RecipeDefinition.cs`
- Create: `scripts/player/warehouse/TraitRollGroupDefinition.cs`
- Create: `scripts/player/warehouse/TraitRollGroupEntryDefinition.cs`
- Create: `scripts/player/warehouse/WeaponProfileDefinition.cs`
- Create: `scripts/player/warehouse/WeaponDamageDiceDefinition.cs`
- Create: `scripts/player/equipment/EquipmentRequirementDefinition.cs`
- Create: `scripts/player/equipment/EquipmentAttributeRequirementDefinition.cs`
- Modify: `scripts/player/warehouse/ItemDef.cs`
- Modify: `scripts/player/warehouse/RecipeDef.cs`
- Modify: `scripts/player/warehouse/TraitRollGroupDef.cs`
- Modify: `scripts/player/warehouse/TraitRollGroupEntryDef.cs`
- Modify: `scripts/player/warehouse/WeaponProfileDef.cs`
- Modify: `scripts/player/warehouse/WeaponDamageDiceDef.cs`
- Modify: `scripts/player/equipment/EquipmentRequirement.cs`
- Modify: `scripts/player/equipment/EquipmentAttributeRequirementDef.cs`
- Modify: `scripts/player/warehouse/ItemContentRegistry.cs`
- Modify: `scripts/player/warehouse/RecipeContentRegistry.cs`
- Modify: `scripts/player/warehouse/SkillBookItemFactory.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs`
- Modify: `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
- Modify: `scripts/systems/persistence/GameSession.ContentValidation.cs`
- Modify runtime consumers: `scripts/enemies/EnemyContentRegistry.cs`
- Modify runtime consumers: `scripts/enemies/EnemyTemplateDef.cs`
- Modify runtime consumers: `scripts/player/progression/QuestContentValidator.cs`
- Modify runtime consumers: `scripts/player/warehouse/ItemTraitContentValidator.cs`
- Modify runtime consumers: `scripts/player/warehouse/SkillBookItemContentValidator.cs`
- Modify runtime consumers: `scripts/player/warehouse/WarehouseStateItemValidator.cs`
- Modify runtime consumers: `scripts/systems/battle/core/WeaponDice.cs`
- Modify runtime consumers: `scripts/systems/battle/fate/FateRuntimeModule.cs`
- Modify runtime consumers: `scripts/systems/battle/fate/MisfortuneGuidanceService.cs`
- Modify runtime consumers: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Modify runtime consumers: `scripts/systems/battle/rules/BattleEquipmentRequirementRules.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleEquipmentAbilityRuntimeService.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleSkillAvailabilityService.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/BattleUnitFactory.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/EquipmentAbilityUsageRuntime.cs`
- Modify runtime consumers: `scripts/systems/battle/runtime/IBattleRatingCharacterGateway.cs`
- Modify runtime consumers: `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`
- Modify runtime consumers: `scripts/systems/content/GameContentCatalog.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeFacade.BattleResolution.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Modify runtime consumers: `scripts/systems/game_runtime/GameRuntimeWarehouseHandler.cs`
- Modify runtime consumers: `scripts/systems/inventory/EquipmentTraitRollService.cs`
- Modify runtime consumers: `scripts/systems/inventory/ItemTraitDetailText.cs`
- Modify runtime consumers: `scripts/systems/inventory/PartyEquipmentService.cs`
- Modify runtime consumers: `scripts/systems/inventory/PartyItemUseService.cs`
- Modify runtime consumers: `scripts/systems/inventory/PartyWarehouseService.cs`
- Modify runtime consumers: `scripts/systems/inventory/WarehouseInventoryEntry.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.CharacterCreation.cs`
- Modify runtime consumers: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterManagementModule.ContentDefs.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify runtime consumers: `scripts/systems/progression/CharacterTraitService.cs`
- Modify runtime consumers: `scripts/systems/progression/MisfortuneBlackOmenService.cs`
- Modify runtime consumers: `scripts/systems/settlement/SettlementForgeService.cs`
- Modify runtime consumers: `scripts/systems/settlement/SettlementShopService.cs`
- Modify runtime consumers: `scripts/systems/world/EncounterRosterBuilder.cs`
- Modify runtime consumers: `scripts/ui/PartyManagementWindow.cs`
- Preserve/merge user changes: `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`
- Preserve/merge user changes: `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`
- Test: `tests/runtime/validation/run_item_recipe_registry_typed_regression.cs`
- Test: `tests/warehouse/run_skill_book_item_helpers_regression.cs`
- Test: `tests/warehouse/run_warehouse_state_item_validator_regression.cs`
- Test: `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- Test: `tests/battle_runtime/runtime/run_battle_change_equipment_requirement_regression.cs`
- Test: `tests/battle_runtime/rules/run_battle_equipment_requirement_rules_regression.cs`

**Interfaces:**
- Consumes: raw item/recipe authoring plus Phase 2 `AttributeModifierDefinition`.
- Produces: immutable items/recipes and pure merge/generated-skill-book outputs.

- [ ] **Step 1: Add failing immutability/derived-type tests**

Assert template merge and skill-book generation return definitions, never mutate inputs, preserve price/tags/weapon dice/profile/trait-roll/equipment requirements/equipment-ability bindings, and contain no Resource. The typed regression scans the exact runtime-consumer paths above and rejects raw `ItemDef`, `RecipeDef`, `WeaponProfileDef`, `WeaponDamageDiceDef`, `EquipmentRequirement`, and `EquipmentAttributeRequirementDef` signatures/construction.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_item_recipe_registry_typed_regression.cs
godot --headless -s res://tests/warehouse/run_skill_book_item_helpers_regression.cs
```

Expected: current merge/generator creates `ItemDef` and duplicates nested Resources.

- [ ] **Step 3: Replace Resource merge/generation**

```csharp
internal static ItemDefinition MergeWithTemplate(
    ItemDefinition template,
    ItemDefinition instance
);

internal static IReadOnlyDictionary<StringName, ItemDefinition>
    BuildGeneratedItemDefinitions(
        IReadOnlyDictionary<StringName, SkillDefinition> skills,
        IReadOnlyDictionary<StringName, ItemDefinition> existingItems
    );

internal static WeaponProfileDefinition MergeWithTemplate(
    WeaponProfileDefinition template,
    WeaponProfileDefinition instance
);

internal EquipmentRequirementCheckResult CheckResult(
    PartyMemberState memberState
);
```

`ItemDefinition` stores `WeaponProfileDefinition`, `WeaponDamageDiceDefinition`, `EquipmentRequirementDefinition`, trait-roll definitions, and existing plain equipment-ability pack/binding definitions. Copy nested lists/maps/definitions; never `Duplicate()` Resource. Move equipment requirement behavior to the immutable definition. Registries own raw objects only during host build.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_item_recipe_registry_typed_regression.cs
godot --headless -s res://tests/warehouse/run_skill_book_item_helpers_regression.cs
godot --headless -s res://tests/warehouse/run_warehouse_state_item_validator_regression.cs
godot --headless -s res://tests/progression/schema/run_equipment_ability_content_registry_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_change_equipment_requirement_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_battle_equipment_requirement_rules_regression.cs
rg -n "\b(ItemDef|RecipeDef|WeaponProfileDef|WeaponDamageDiceDef|EquipmentRequirement|EquipmentAttributeRequirementDef)\b" scripts/systems scripts/ui --glob "*.cs"
dotnet build magic.csproj
# Stage only the exact Task 3 Files manifest; patch-stage protected user hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: project item and recipe definitions"
```

---

### Task 4: World generation and battle special-profile definitions

**Files:**
- Create: `scripts/systems/world/WorldGenerationDefinition.cs`
- Create: `scripts/systems/world/SettlementDefinition.cs`
- Create: `scripts/systems/world/SettlementDistributionDefinition.cs`
- Create: `scripts/systems/world/WeightedFacilityDefinition.cs`
- Create: `scripts/systems/world/FacilityDefinition.cs`
- Create: `scripts/systems/world/FacilityNpcDefinition.cs`
- Create: `scripts/systems/world/FacilitySlotDefinition.cs`
- Create: `scripts/systems/world/WildSpawnRuleDefinition.cs`
- Create: `scripts/systems/world/MountedSubmapDefinition.cs`
- Create: `scripts/systems/world/WorldEventDefinition.cs`
- Create: `scripts/systems/world/WorldMapSettlementBundleDefinition.cs`
- Create: `scripts/systems/world/WorldMapSettlementNamePoolDefinition.cs`
- Create: `scripts/systems/world/WorldMapWildSpawnBundleDefinition.cs`
- Modify: `scripts/utils/WorldMapGenerationConfig.cs`
- Modify: `scripts/utils/SettlementConfig.cs`
- Modify: `scripts/utils/SettlementDistributionRule.cs`
- Modify: `scripts/utils/WeightedFacilityEntry.cs`
- Modify: `scripts/utils/FacilityConfig.cs`
- Modify: `scripts/utils/FacilityNpcConfig.cs`
- Modify: `scripts/utils/FacilitySlotConfig.cs`
- Modify: `scripts/utils/WildSpawnRule.cs`
- Modify: `scripts/utils/MountedSubmapConfig.cs`
- Modify: `scripts/utils/WorldEventConfig.cs`
- Modify: `scripts/utils/WorldMapSettlementBundle.cs`
- Modify: `scripts/utils/WorldMapSettlementNamePool.cs`
- Modify: `scripts/utils/WorldMapWildSpawnBundle.cs`
- Modify: `scripts/utils/WorldMapContentValidator.cs`
- Modify: `scripts/systems/persistence/GameSession.cs` generation-config methods/fields
- Modify: `scripts/systems/persistence/GameSession.SaveIndexAndFileIO.cs`
- Modify: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs`
- Modify: `scripts/systems/persistence/SaveSerializer.cs`
- Modify: `scripts/systems/world/WorldMapDataContext.cs`
- Modify: `scripts/systems/world/WorldMapSpawnSystem.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.WorldSubmap.cs`
- Modify: `scripts/ui/WorldMapView.cs`
- Modify: `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs`
- Modify: `scripts/systems/battle/core/special_profiles/BattleSpecialProfileRuntimeView.cs`
- Modify: `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifest.cs`
- Modify: `scripts/systems/battle/core/meteor_swarm/MeteorSwarmProfileData.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Test: `tests/runtime/validation/run_world_map_content_validator_typed_regression.cs`
- Test: `tests/world_map/runtime/run_world_map_shared_content_injection_regression.cs`
- Test: `tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs`

**Interfaces:**
- Consumes: raw world/special profile Resources during host build.
- Produces: immutable world definitions and `IBattleSpecialProfileView` without persistent GDictionary snapshots.

- [ ] **Step 1: Add failing no-Resource/no-GDictionary tests**

Assert `GameSession` stores generation path/id plus a borrowed definition; every mounted submap/default settlement/default wild-spawn/name-pool dependency is canonical-loaded/projected before host seal; recursive config cycles report canonical paths; `WorldMapDataContext`/`WorldMapSpawnSystem` perform no runtime load; special profile catalog exposes only the typed view; and `MeteorSwarmProfileData` returns read-only copies.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_world_map_content_validator_typed_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs
```

- [ ] **Step 3: Add typed boundaries**

```csharp
public sealed class WorldGenerationDefinition
{
    internal static WorldGenerationDefinition FromResource(
        string canonicalPath,
        WorldMapGenerationConfig source,
        IContentResourceLoader loader
    );
}

public interface IBattleSpecialProfileView
{
    bool TryGetMeteorSwarmProfile(
        StringName profileId,
        out MeteorSwarmProfileData profile
    );
}
```

`WorldGenerationDefinition.FromResource` recursively projects the exact authored graph listed under Files, uses `loader.LoadCanonical<WorldMapGenerationConfig>` for every mounted submap path, and loads the formal default settlement/wild-spawn/name-pool bundles during the same host build when `inject_default_main_world_content` is enabled. Track the canonical path stack and fail on cycles. `WorldMapDataContext` and `WorldMapSpawnSystem` receive definition indexes; delete their `GD.Load`/`ResourceLoader.Load` paths.

Delete long-lived `GetBattleSpecialProfileRegistrySnapshot` and runtime GDictionary snapshot APIs after all callers use `IBattleSpecialProfileView`.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_world_map_content_validator_typed_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_shared_content_injection_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs
rg -n "\b(GD|ResourceLoader)\.Load" scripts/systems/world scripts/systems/game_runtime/GameRuntimeFacade.WorldSubmap.cs --glob "*.cs"
dotnet build magic.csproj
# Stage only the exact Task 4 Files manifest.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: project world and special profile content"
```

---

### Task 5: ProcessContentHost, EngineAssetResolver, and ContentSnapshot

**Files:**
- Create: `scripts/systems/content/ProcessContentHost.cs`
- Create: `scripts/systems/content/IContentResourceLoader.cs`
- Create: `scripts/systems/content/ContentRootDiagnostic.cs`
- Create: `scripts/systems/content/EngineAssetResolver.cs`
- Create: `scripts/systems/content/ContentSnapshot.cs`
- Create: `scripts/systems/content/ContentSnapshotBuilder.cs`
- Create: `scripts/systems/content/ILegacyEnemyContentCatalog.cs`
- Modify: every registry listed in Tasks 1-4 to accept `IContentResourceLoader`
- Modify: `scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs`
- Create: `tests/runtime/validation/run_process_content_host_regression.cs`
- Create: `tests/runtime/validation/run_non_ai_content_snapshot_regression.cs`

**Interfaces:**
- Consumes: all immutable non-AI definitions.
- Produces: one sealed process host, canonical raw roots/assets, one typed snapshot, and a narrow Phase 4 legacy enemy interface.

- [ ] **Step 1: Write canonical/seal/snapshot red tests**

Assert same canonical path increments root count once; `BuildAndSeal` called twice returns the same snapshot/epoch without new roots; load after seal throws; a projector failure rolls host state/root count back to the pre-build baseline without disposing cached Resources; `GetSnapshot` before build/after release throws; `ReleaseSnapshot` and `Dispose` are idempotent; active borrowers block disposal in strict mode; process-shared engine assets use one borrow anchor; snapshot collections are immutable; and the graph auditor finds no Resource/Godot collection/Object Variant.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_process_content_host_regression.cs
godot --headless -s res://tests/runtime/validation/run_non_ai_content_snapshot_regression.cs
```

- [ ] **Step 3: Implement exact host API**

```csharp
internal interface IContentResourceLoader
{
    T LoadCanonical<T>(string resourcePath) where T : Resource;
}

internal sealed class ProcessContentHost : IContentResourceLoader, IDisposable
{
    internal long Epoch { get; }
    internal bool IsSealed { get; }
    internal int CanonicalRootCount { get; }
    internal EngineAssetResolver EngineAssets { get; }
    internal ILegacyEnemyContentCatalog LegacyEnemyContent { get; }

    public T LoadCanonical<T>(string resourcePath) where T : Resource;
    internal ContentSnapshot BuildAndSeal();
    internal ContentSnapshot GetSnapshot();
    internal IReadOnlyList<ContentRootDiagnostic> GetCanonicalRootDiagnostics();
    internal void ReleaseSnapshot();
    public void Dispose();
}

internal sealed record ContentRootDiagnostic(
    string CanonicalPath,
    string ResourceType,
    ReferenceRole Role
);

internal sealed class EngineAssetResolver : IDisposable
{
    internal int CanonicalAssetCount { get; }
    internal T ResolveBorrowed<T>(string resourcePath) where T : Resource;
    public void Dispose();
}

internal sealed class ContentSnapshotBuilder
{
    internal ContentSnapshotBuilder(IContentResourceLoader loader);
    internal ContentSnapshot Build(long epoch);
}

internal interface ILegacyEnemyContentCatalog
{
    IReadOnlyDictionary<StringName, EnemyTemplateDef> EnemyTemplates { get; }
    IReadOnlyDictionary<StringName, EnemyAiBrainDef> EnemyBrains { get; }
    IReadOnlyDictionary<StringName, WildEncounterRosterDef> EncounterRosters { get; }
    IReadOnlyDictionary<StringName, BattleSimProfileDef> SimulationProfiles { get; }
}
```

`LoadCanonical` normalizes the path, returns the existing root if present, and records Godot resource cache as native owner plus host borrow anchor. Every migrated registry receives `IContentResourceLoader`; direct `GD.Load`/`ResourceLoader.Load` calls remain only inside the host/resolver. `BuildAndSeal` builds into local maps, publishes/seals only after all validation/projection succeeds, and is idempotent after success. Failure clears only anchors created by that attempt and leaves the host unsealed/retryable. `ReleaseSnapshot` drops the host's snapshot reference once all borrowers are gone. `Dispose` is idempotent and clears managed anchors without disposing cached path-backed roots.

`EngineAssetResolver.ResolveBorrowed` canonicalizes path-backed Texture/PackedScene/AudioStream/Material loads independently from the sealed authored-content phase; it rejects new loads after coordinator `Quiescing` and clears anchors at process shutdown without disposing ResourceLoader-owned roots.

`ApplicationLifetimeCoordinator` constructs one `ProcessContentHost` in `_Ready`, calls `BuildAndSeal` once before accepting session commands, and exposes an internal read-only `ProcessContentHost ContentHost` property for session binding/lifecycle diagnostics. Tests may borrow that host/snapshot but cannot replace or rebuild it.

`ContentSnapshot` exposes these immutable roots:

```csharp
long Epoch { get; }
IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
IReadOnlyDictionary<StringName, TraitDefinition> Traits { get; }
IReadOnlyDictionary<StringName, ProfessionDefinition> Professions { get; }
IReadOnlyDictionary<StringName, AchievementDefinition> Achievements { get; }
IReadOnlyDictionary<StringName, QuestDefinition> Quests { get; }
IReadOnlyDictionary<StringName, RaceDefinition> Races { get; }
IReadOnlyDictionary<StringName, SubraceDefinition> Subraces { get; }
IReadOnlyDictionary<StringName, AgeProfileDefinition> AgeProfiles { get; }
IReadOnlyDictionary<StringName, BloodlineDefinition> Bloodlines { get; }
IReadOnlyDictionary<StringName, AscensionDefinition> Ascensions { get; }
IReadOnlyDictionary<StringName, StageAdvancementDefinition> StageAdvancements { get; }
IReadOnlyDictionary<StringName, FaithDeityDefinition> FaithDeities { get; }
IReadOnlyDictionary<StringName, BarrierProfileDefinition> BarrierProfiles { get; }
IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> ContingencyTemplates { get; }
IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
IReadOnlyDictionary<StringName, RecipeDefinition> Recipes { get; }
IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> EquipmentAbilityPacks { get; }
IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentAbilityBindings { get; }
IReadOnlyDictionary<string, WorldGenerationDefinition> WorldGenerations { get; }
IBattleSpecialProfileView BattleSpecialProfiles { get; }
```

It contains no legacy enemy Resource; `ILegacyEnemyContentCatalog` is a separate, exact, audited Phase 4 boundary.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_process_content_host_regression.cs
godot --headless -s res://tests/runtime/validation/run_non_ai_content_snapshot_regression.cs
rg -n "\b(GD|ResourceLoader)\.Load" scripts/player/progression scripts/player/warehouse scripts/systems/progression/FaithService.cs scripts/systems/world scripts/systems/game_runtime/GameRuntimeFacade.WorldSubmap.cs scripts/systems/persistence/GameSession.cs scripts/utils/WorldMapContentValidator.cs --glob "*.cs"
dotnet build magic.csproj
git add scripts/systems/content scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs scripts/player scripts/utils tests/runtime/validation
git commit -m "feat: add process content host and snapshot"
```

---

### Task 6: Bind GameSession/GameRoot/GameContentCatalog to the process snapshot

**Files:**
- Modify: `scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs`
- Modify: `scripts/systems/persistence/GameSession.cs:196-255`
- Modify: `scripts/systems/persistence/GameSession.ContentValidation.cs`
- Modify: `scripts/systems/content/GameRoot.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs` non-AI catalog types
- Create: `tests/shared/SyntheticContentSnapshotFactory.cs`
- Create: `tests/shared/GameSessionTestFactory.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_charge_path_aoe_behavior_regression.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_enemy_template_runtime_regression.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_melee_charge_behavior_regression.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_melee_screening_behavior_regression.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_random_chain_behavior_regression.cs`
- Modify: `tests/battle_runtime/ai/run_battle_ai_wait_behavior_regression.cs`
- Modify/preserve user changes: `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`
- Modify: `tests/battle_runtime/ai/run_move_to_range_progress_regression.cs`
- Modify: `tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs`
- Modify: `tests/battle_runtime/runtime/run_save_index_resilience_regression.cs`
- Modify: `tests/runtime/facade/run_battle_permadeath_regression.cs`
- Modify: `tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs`
- Modify: `tests/runtime/persistence/run_invalid_save_graceful_regression.cs`
- Modify: `tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs`
- Test helper: `tests/shared/LifecycleMeasurementBarrier.cs`
- Create: `tests/runtime/validation/run_content_snapshot_session_recreate_regression.cs`

**Interfaces:**
- Consumes: Task 5 host/snapshot and `ILegacyEnemyContentCatalog`.
- Produces: coordinator-controlled content initialization and session catalog binding without per-session raw registry rebuild.

- [ ] **Step 1: Add session A/B and synthetic-snapshot failures**

Assert production session receives the coordinator snapshot, session A close + `LifecycleMeasurementBarrier` + session B reuse keeps the same epoch/root count, and same-process tests cannot create a second raw host but can inject a pure synthetic snapshot.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_content_snapshot_session_recreate_regression.cs
```

Expected: current constructor independently creates/rebuilds registries.

- [ ] **Step 3: Move construction behind explicit bind**

```csharp
// GameSession
internal void BindContent(
    ContentSnapshot snapshot,
    ILegacyEnemyContentCatalog legacyEnemyContent
);

// GameContentCatalog
internal void BindSnapshot(
    GameSession session,
    ContentSnapshot snapshot,
    ILegacyEnemyContentCatalog legacyEnemyContent
);

// ApplicationLifetimeCoordinator
internal void AttachSession(GameSession session);
internal ValueTask CloseSessionAsync(GameSession session);
```

The coordinator owns host initialization and binds GameSession before runtime commands are accepted. Only one attached session may exist; attaching a second live session fails. `CloseSessionAsync` is idempotent for the attached instance, calls normal close, frees/awaits its Node, clears the active-session field, and never enters process shutdown. Production initial autoload and recreated sessions use the same API. Remove non-AI raw registry fields/index rebuilds from session/catalog; preserve revision/invalid-after-GameRoot-close behavior. The A/B test uses `LifecycleMeasurementBarrier` between close and recreate.

`GameSessionTestFactory.CreateBorrowingProcessSnapshot(SceneTree tree)` binds the coordinator's existing snapshot/legacy enemy catalog without creating another raw host. `GameSessionTestFactory.CreateSynthetic(ContentSnapshot snapshot, ILegacyEnemyContentCatalog legacyEnemyContent)` accepts only pure CLR fixtures from `SyntheticContentSnapshotFactory`. Migrate the 15 exact test files listed above; a source assertion rejects `new GameSession(` outside the factory and production autoload construction.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_content_snapshot_session_recreate_regression.cs
godot --headless -s res://tests/runtime/validation/run_game_root_content_catalog_regression.cs
godot --headless -s res://tests/runtime/validation/run_skill_catalog_query_regression.cs
dotnet build magic.csproj
git add scripts/systems/lifecycle scripts/systems/persistence scripts/systems/content scripts/systems/game_runtime tests/runtime/validation
git commit -m "refactor: bind sessions to process content snapshot"
```

---

### Task 7: Phase 3 LegacyEnemy debt, query soak, and context map

**Files:**
- Modify: `scripts/systems/content/ILegacyEnemyContentCatalog.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`
- Create: `tests/runtime/lifecycle/run_content_snapshot_query_soak_regression.cs`
- Modify: `docs/design/project_context_units.md`

**Interfaces:**
- Consumes: Tasks 1-6.
- Produces: exact Phase 4 debt inventory and phase-3 canonical/query/session gates.

- [ ] **Step 1: Register the only remaining content debt**

```text
owner_id: ProcessContentHost.LegacyEnemyContentRegistry
domain: ProcessContent
delete_phase: 4
```

It may contain only EnemyContentSeed, EnemyTemplate/Brain/State/Transition/GenerationSlot/Action/ScoreProfile, WildEncounterRoster, and BattleSimProfile/override-patch Resources that directly depend on that AI graph. The debt record enumerates these exact borrower owners:

- `scripts/systems/content/GameContentCatalog.cs` enemy template/brain/roster getters;
- `scripts/systems/persistence/GameSession.ContentValidation.cs` enemy validation;
- `scripts/systems/game_runtime/GameRuntimeFacade.cs` and
  `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`;
- `scripts/systems/world/EncounterRosterBuilder.cs`;
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs` and `BattleRuntimeModule.ContentSync.cs`;
- `scripts/systems/battle/ai/BattleAiService.cs`, `BattleAiActionAssembler.cs`,
  `BattleAiDecisionEngine.cs`, `BattleAiStateResolver.cs`, and `BattleAiRuntimeActionPlan.cs`;
- `scripts/systems/battle/sim/BattleSimContentProvider.cs` and `BattleSimOverrideApplier.cs`.

No other non-AI Resource debt is allowed.

- [ ] **Step 2: Add the 10,000-query and session recreate gate**

Run 10,000 catalog/special-profile queries and repeated session bind/close; assert canonical root count, snapshot object count, and migrated-domain wrapper count do not grow.

- [ ] **Step 3: Run the phase gate**

```powershell
$env:MAGIC_LIFECYCLE_STRICT='1'
godot --headless -s res://tests/runtime/lifecycle/run_content_snapshot_query_soak_regression.cs
godot --headless -s res://tests/runtime/validation/run_content_snapshot_session_recreate_regression.cs
godot --headless -s res://tests/runtime/validation/run_non_ai_content_snapshot_regression.cs
python tests/run_regression_suite.py --pattern progression --jobs 16 --finalizer-crash-retries 0
python tests/run_regression_suite.py --pattern runtime/validation --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
git diff --check
```

Expected: migrated snapshot domains have zero Resource/Object Variant and no growth; only the exact Enemy/AI debt remains.

- [ ] **Step 4: Update current owner/read sets and commit**

Update CU-02/CU-03/CU-10/CU-13 and verify that the lifecycle spec Phase 3 list still includes every migrated non-AI definition.

```powershell
git add scripts/systems/content tests/runtime docs/design/project_context_units.md docs/superpowers/specs/2026-07-10-godotsharp-lifecycle-architecture-design.md
git commit -m "test: enforce process content snapshot boundary"
```
