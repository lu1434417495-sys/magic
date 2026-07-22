# GodotSharp Lifecycle Phase 4 Enemy and AI Typed Content Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Project all Enemy/AI/roster authored Resources into immutable definitions, switch every world/battle/AI/simulation consumer to those definitions, and delete the last Phase 3 raw-content LegacyDebt.

**Architecture:** `EnemyContentRegistry` remains a ProcessContentHost build-time loader/validator, then projects templates, brains, states, transitions, generation slots, actions, score profiles, drops, and rosters into `ContentSnapshot`. Runtime action plans store one immutable `EnemyAiActionDefinition` plus metadata; authored and generated actions share the same model. Typed evaluators replace Resource virtual dispatch and instance-ID metadata.

**Tech Stack:** Godot 4.6.2 Mono, C#/.NET 8, immutable CLR records/read-only collections, headless AI/runtime regressions.

## Global Constraints

- Phases 1-3 must be green; `ProcessContentHost.LegacyEnemyContentRegistry` must be the only content `LegacyDebt`.
- Preserve all enemy IDs, schema validation messages, state/transition ordering, generation behavior, action fingerprints, AI decisions, loot, roster growth, and battle results.
- Authored `.tres` stays unchanged and immutable; definitions contain asset path/UID, never Texture/PackedScene wrappers.
- No `Enemy*Def`, `EnemyAiAction`, or `WildEncounterRosterDef` may remain in runtime/world/AI signatures at phase end.
- Generated and authored actions use one definition model; do not add a fallback branch.
- Preserve `jobs=16, retries=0` AI behavior; do not run battle simulation/balance runners unless separately requested.
- Preserve unrelated worktree edits and merge narrowly around EnemyTemplate files.
- Treat each task's `Files` list as its staging manifest. Before every commit, inspect `git diff --cached --name-only`, `git diff --cached`, and `git diff --cached --check`; do not blindly execute directory-wide `git add` lines.
- The current EnemyTemplate and two AI regression edits are user-owned. Stage only the typed-content hunks; if an overlap cannot be split without including the user's change, stop before committing.
- Every new C# regression runner derives from `LifecycleTestSceneTree` and exits through `RequestTestExit(_test.Finish(label))`.

---

### Task 1: Immutable Enemy/AI/roster definition graph

**Files:**
- Create: `scripts/enemies/definitions/EnemyTemplateDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiBrainDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiStateDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiTransitionRuleDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiTransitionConditionDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiGenerationSlotDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiActionDefinition.cs`
- Create: `scripts/enemies/definitions/EnemyAiActionKind.cs`
- Create: `scripts/enemies/definitions/UseUnitSkillActionDefinition.cs`
- Create: `scripts/enemies/definitions/UseGroundSkillActionDefinition.cs`
- Create: `scripts/enemies/definitions/UseMultiUnitSkillActionDefinition.cs`
- Create: `scripts/enemies/definitions/MoveToMultiUnitSkillPositionActionDefinition.cs`
- Create: `scripts/enemies/definitions/UseRandomChainSkillActionDefinition.cs`
- Create: `scripts/enemies/definitions/UseChargeActionDefinition.cs`
- Create: `scripts/enemies/definitions/UseChargePathAoeActionDefinition.cs`
- Create: `scripts/enemies/definitions/MoveToRangeActionDefinition.cs`
- Create: `scripts/enemies/definitions/MoveToAdvantagePositionActionDefinition.cs`
- Create: `scripts/enemies/definitions/UseGroundRepositionSkillActionDefinition.cs`
- Create: `scripts/enemies/definitions/RetreatActionDefinition.cs`
- Create: `scripts/enemies/definitions/WaitActionDefinition.cs`
- Create: `scripts/enemies/definitions/BattleAiScoreProfileDefinition.cs`
- Create: `scripts/enemies/definitions/DropEntryDefinition.cs`
- Create: `scripts/enemies/definitions/WildEncounterRosterDefinition.cs`
- Create: `scripts/enemies/definitions/WildEncounterRosterStageDefinition.cs`
- Create: `scripts/enemies/definitions/WildEncounterRosterUnitEntryDefinition.cs`
- Modify: `scripts/enemies/EnemyContentSeed.cs`
- Modify: `scripts/enemies/EnemyTemplateDef.cs`
- Modify: `scripts/enemies/EnemyAiBrainDef.cs`
- Modify: `scripts/enemies/EnemyAiStateDef.cs`
- Modify: `scripts/enemies/EnemyAiTransitionRuleDef.cs`
- Modify: `scripts/enemies/EnemyAiTransitionConditionDef.cs`
- Modify: `scripts/enemies/EnemyAiGenerationSlotDef.cs`
- Modify: `scripts/enemies/EnemyAiAction.cs`
- Modify: `scripts/enemies/DropEntryDef.cs`
- Modify: `scripts/enemies/WildEncounterRosterDef.cs`
- Modify: `scripts/enemies/WildEncounterRosterStageDef.cs`
- Modify: `scripts/enemies/WildEncounterRosterUnitEntryDef.cs`
- Modify: `scripts/enemies/actions/UseUnitSkillAction.cs`
- Modify: `scripts/enemies/actions/UseGroundSkillAction.cs`
- Modify: `scripts/enemies/actions/UseMultiUnitSkillAction.cs`
- Modify: `scripts/enemies/actions/MoveToMultiUnitSkillPositionAction.cs`
- Modify: `scripts/enemies/actions/UseRandomChainSkillAction.cs`
- Modify: `scripts/enemies/actions/UseChargeAction.cs`
- Modify: `scripts/enemies/actions/UseChargePathAoeAction.cs`
- Modify: `scripts/enemies/actions/MoveToRangeAction.cs`
- Modify: `scripts/enemies/actions/MoveToAdvantagePositionAction.cs`
- Modify: `scripts/enemies/actions/UseGroundRepositionSkillAction.cs`
- Modify: `scripts/enemies/actions/RetreatAction.cs`
- Modify: `scripts/enemies/actions/WaitAction.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreProfile.cs`
- Modify: `scripts/enemies/EnemyContentRegistry.cs`
- Modify: `scripts/systems/content/ProcessContentHost.cs`
- Modify: `scripts/systems/content/ContentSnapshotBuilder.cs`
- Modify: `scripts/systems/content/ContentSnapshotBuilder.cs`
- Modify: `scripts/systems/content/ContentSnapshot.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Modify: `scripts/systems/persistence/GameSession.ContentValidation.cs`
- Test: `tests/runtime/validation/run_enemy_content_registry_typed_regression.cs`
- Test: `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`
- Test: `tests/battle_runtime/ai/run_enemy_ai_transition_schema_regression.cs`
- Test: `tests/battle_runtime/ai/run_enemy_ai_generation_slots_schema_regression.cs`
- Test: `tests/battle_runtime/runtime/run_wild_encounter_roster_typed_regression.cs`

**Interfaces:**
- Consumes: validated raw Enemy/AI authoring Resources inside ProcessContentHost build only.
- Produces: one recursive immutable enemy catalog in `ContentSnapshot`.

- [ ] **Step 1: Add field-parity and graph-audit failures**

Assert each Resource projects every gameplay field, source Resources remain unchanged, asset references become canonical path/UID, all collections are read-only, schema error order/text remains stable, a fresh `ProcessContentHost.BuildAndSeal()` snapshot contains the formal enemy/brain/roster IDs, and the recursive definition graph contains no GodotObject/Godot collection/Object Variant.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_enemy_content_registry_typed_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs
```

Expected: catalog still exposes raw Resources.

- [ ] **Step 3: Add action kind and immutable base contract**

```csharp
internal enum EnemyAiActionKind
{
    UseUnitSkill,
    UseGroundSkill,
    UseMultiUnitSkill,
    MoveToMultiUnitSkillPosition,
    UseRandomChainSkill,
    UseCharge,
    UseChargePathAoe,
    MoveToRange,
    MoveToAdvantagePosition,
    UseGroundRepositionSkill,
    Retreat,
    Wait,
}

internal abstract class EnemyAiActionDefinition
{
    protected EnemyAiActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        EnemyAiActionKind kind,
        IReadOnlyList<StringName> declaredSkillIds
    )
    {
        ActionId = actionId;
        ScoreBucketId = scoreBucketId;
        ActionIntent = actionIntent;
        Kind = kind;
        DeclaredSkillIds = declaredSkillIds;
    }

    internal StringName ActionId { get; }
    internal StringName ScoreBucketId { get; }
    internal StringName ActionIntent { get; }
    internal EnemyAiActionKind Kind { get; }
    internal IReadOnlyList<StringName> DeclaredSkillIds { get; }
}
```

Each sealed action definition has the base fields plus these exact normalized fields:

| Definition | Additional fields |
|---|---|
| `UseUnitSkillActionDefinition` | `SkillIds`, `TargetSelector`, `MinimumEffectiveTargetCount`, `MaximumFriendlyFireTargetCount`, `AllowFriendlyLethal`, `DesiredMinDistance`, `DesiredMaxDistance`, `DistanceReference` |
| `UseGroundSkillActionDefinition` | `SkillIds`, `MinimumHitCount`, `AllowEmptyGroundControl`, `AllowGroundControlSupplementPartialHits`, `MinimumGroundControlScore`, `MinimumAllyThreatHitCount`, `MaximumFriendlyFireTargetCount`, `AllowFriendlyLethal`, `ThreatMinimumSafeDistance`, `ThreatSafeDistanceMargin`, `DesiredMinDistance`, `DesiredMaxDistance`, `DistanceReference` |
| `UseMultiUnitSkillActionDefinition` | `SkillIds`, `TargetSelector`, `DesiredMinDistance`, `DesiredMaxDistance`, `DistanceReference`, `CandidatePoolLimit`, `CandidateGroupLimit` |
| `MoveToMultiUnitSkillPositionActionDefinition` | all `UseMultiUnitSkillActionDefinition` fields plus `TargetCountWeight` |
| `UseRandomChainSkillActionDefinition` | `SkillIds`, `TargetSelector`, `DesiredMinDistance`, `DesiredMaxDistance`, `DistanceReference`, `MinimumCandidateCount` |
| `UseChargeActionDefinition` | `SkillId`, `TargetSelector`, `MinimumChargeMoveDistance` |
| `UseChargePathAoeActionDefinition` | `SkillIds`, `TargetSelector`, `MinimumHitCount`, `DesiredMinDistance`, `DesiredMaxDistance` |
| `MoveToRangeActionDefinition` | `AiEvaluationMode`, `TargetSelector`, `DesiredMinDistance`, `DesiredMaxDistance`, `RangeSkillIds`, `ScreeningMode`, `EnableAoeSetupPositioning`, `AoeSetupMinTargetCount`, `AoeSetupTargetCountWeight`, `AoeSetupImprovementWeight`, `AoeSetupFriendlyFirePenalty`, `ScreeningMinHpBasisPoints`, `ScreeningAllyMinAttackRange`, `ScreeningEnemyMaxContactRange`, `ScreeningThreatDistanceBuffer`, `ScreeningPathBonus` |
| `MoveToAdvantagePositionActionDefinition` | `TargetSelector`, `DesiredMinDistance`, `DesiredMaxDistance`, `RangeSkillIds`, `MinimumSafeDistance`, `SafeDistanceMargin`, `MinSurvivalMarginGainToEscape`, `MinDistanceProgressWhenBeyondBand`, `PositioningMode`, `HighGroundWeight`, `SafetyWeight`, `DistanceBandWeight`, `CandidateLimit` |
| `UseGroundRepositionSkillActionDefinition` | `SkillIds`, `TargetSelector`, `MinimumSafeDistance`, `SafeDistanceMargin`, `DesiredMaxDistanceBonus`, `ActionBaseScore`, `MinSurvivalMarginGainToEscape` |
| `RetreatActionDefinition` | `TargetSelector`, `MinimumSafeDistance`, `UseDynamicThreatSafeDistance`, `SafeDistanceMargin` |
| `WaitActionDefinition` | `ActiveRestActionBaseScore`, `ActiveRestMinStaminaResidue` |

All list parameters are copied to read-only CLR collections. `EnemyAiBrainDefinition` exposes a read-only state map/transition list and `TryGetState`. `EnemyTemplateDefinition` contains plain attributes/skills/resistances/drops/weapon projection and asset path/UID only.

- [ ] **Step 4: Project the complete graph in EnemyContentRegistry**

`ProcessContentHost` loads/validates the raw enemy registry before seal. `ContentSnapshotBuilder` invokes the registry projector once, adds the complete typed graph to `ContentSnapshot` in deterministic source order, and rejects duplicate IDs. Keep old raw getters only until Task 5 deletion.

The Phase 4 snapshot additions are exact:

```csharp
IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates { get; }
IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyBrains { get; }
IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> EncounterRosters { get; }
```

Each `EnemyAiBrainDefinition` embeds its immutable `BattleAiScoreProfileDefinition`; `BattleSimProfileDefinition` embeds its own baseline definition. Do not invent a second global score-profile registry.

- [ ] **Step 5: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_enemy_content_registry_typed_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_ai_transition_schema_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_ai_generation_slots_schema_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_wild_encounter_roster_typed_regression.cs
dotnet build magic.csproj
# Stage only the exact Task 1 Files manifest; patch-stage protected user hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: project enemy and AI definitions"
```

---

### Task 2: RuntimeActionEntry and ActionPlan without Resources

**Files:**
- Modify: `scripts/systems/battle/ai/BattleAiRuntimeActionEntry.cs`
- Modify: `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs`
- Modify: `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- Modify: `scripts/utils/RuntimeResourceFactories.cs`
- Test: `tests/battle_runtime/ai/run_battle_ai_runtime_action_plan_regression.cs`
- Test: `tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs`
- Test: `tests/battle_runtime/ai/run_enemy_ai_generation_slots_content_regression.cs`

**Interfaces:**
- Consumes: Task 1 brain/action definitions.
- Produces: definition-backed entries/plans and metadata keyed by stable typed identity.

- [ ] **Step 1: Add failing no-Resource/no-instance-ID assertions**

Assert entry has no `ResourceAction`, plan owns no EnemyAiAction/Resource/transient scope, metadata is attached to an entry/definition key, authored/generated actions share the same type, and fingerprint remains stable.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_runtime_action_plan_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs
```

- [ ] **Step 3: Replace entry/plan API**

```csharp
internal sealed class BattleAiRuntimeActionEntry
{
    internal BattleAiRuntimeActionEntry(
        EnemyAiActionDefinition action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        Action = action;
        Metadata = metadata;
    }

    internal EnemyAiActionDefinition Action { get; }
    internal BattleAiRuntimeActionPlan.RuntimeActionMetadata Metadata { get; }
    internal StringName ActionId => Action.ActionId;
    internal StringName ScoreBucketId => Action.ScoreBucketId;
}

internal void AddStateActions(
    StringName stateId,
    IEnumerable<EnemyAiActionDefinition> actions
);

internal void AddAction(
    StringName stateId,
    EnemyAiActionDefinition action,
    RuntimeActionMetadata metadata = null
);

internal IReadOnlyList<BattleAiRuntimeActionEntry> GetActionEntries(StringName stateId);
```

Delete `ResourceAction`, `FromResource`, Resource lists, metadata-by-instance-ID, `OwnRuntimeAction`, action transient scope, and runtime EnemyAi Resource factory. Generated action assembly constructs immutable definitions.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_runtime_action_plan_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_ai_generation_slots_content_regression.cs
dotnet build magic.csproj
# Stage only the exact Task 2 Files manifest.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: use typed AI runtime actions"
```

---

### Task 3: Typed DecisionEngine, StateResolver, and evaluators

**Files:**
- Modify: `scripts/systems/battle/ai/BattleAiDecisionEngine.cs`
- Modify: `scripts/systems/battle/ai/BattleAiService.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreProjection.cs`
- Modify: `scripts/systems/battle/ai/BattleAiStateResolver.cs`
- Modify: `scripts/systems/battle/ai/BattleAiContext.cs`
- Modify: `scripts/systems/battle/ai/BattleAiUnitSkillCandidateEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiGroundSkillActionEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiMultiUnitSkillEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiMoveToMultiUnitSkillPositionEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiRandomChainSkillEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiChargeActionEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiChargePathAoeActionEvaluator.cs`
- Modify: `scripts/systems/battle/ai/BattleAiMoveToRangeCandidateEvaluator.cs`
- Create: `scripts/systems/battle/ai/BattleAiWaitActionEvaluator.cs`
- Create: `scripts/systems/battle/ai/BattleAiRetreatActionEvaluator.cs`
- Create: `scripts/systems/battle/ai/BattleAiMoveToAdvantageActionEvaluator.cs`
- Create: `scripts/systems/battle/ai/BattleAiMoveToRangeActionEvaluator.cs`
- Create: `scripts/systems/battle/ai/BattleAiGroundRepositionActionEvaluator.cs`
- Modify: `scripts/enemies/EnemyAiActionHelper.cs` to accept definitions/entries only
- Modify: `scripts/enemies/EnemyAiAction.cs` to keep authoring/schema projection only
- Modify: every concrete authoring action file listed in Task 1 to remove runtime dispatch
- Test: `tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs`
- Test: `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`
- Test: `tests/battle_runtime/ai/run_move_to_range_progress_regression.cs`
- Test: `tests/battle_runtime/ai/run_battle_ai_wait_behavior_regression.cs`
- Create: `tests/battle_runtime/ai/run_battle_ai_retreat_behavior_regression.cs`
- Create: `tests/battle_runtime/ai/run_battle_ai_advantage_behavior_regression.cs`
- Test: `tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs`

**Interfaces:**
- Consumes: Task 2 entries and Phase 2 decision lifetime.
- Produces: typed evaluator dispatch with no Resource virtual fallback.

- [ ] **Step 1: Add failing authored-action parity tests**

For every `EnemyAiActionKind`, load formal content, obtain the typed entry, execute the matching evaluator, and compare command/score/trace/fingerprint to baseline. Assert Resource fallback flags/checkpoints are absent.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs
```

- [ ] **Step 3: Dispatch by typed kind**

```csharp
internal BattleAiDecision EvaluateEntry(
    BattleAiContext context,
    BattleAiRuntimeActionEntry entry
) =>
    entry.Action.Kind switch
    {
        EnemyAiActionKind.UseUnitSkill => _unitSkill.Evaluate(context, entry),
        EnemyAiActionKind.UseGroundSkill => _groundSkill.Evaluate(context, entry),
        EnemyAiActionKind.UseMultiUnitSkill => _multiUnit.Evaluate(context, entry),
        EnemyAiActionKind.MoveToMultiUnitSkillPosition => _multiUnitMove.Evaluate(context, entry),
        EnemyAiActionKind.UseRandomChainSkill => _randomChain.Evaluate(context, entry),
        EnemyAiActionKind.UseCharge => _charge.Evaluate(context, entry),
        EnemyAiActionKind.UseChargePathAoe => _chargePathAoe.Evaluate(context, entry),
        EnemyAiActionKind.MoveToRange => _moveToRange.Evaluate(context, entry),
        EnemyAiActionKind.MoveToAdvantagePosition => _advantage.Evaluate(context, entry),
        EnemyAiActionKind.UseGroundRepositionSkill => _groundReposition.Evaluate(context, entry),
        EnemyAiActionKind.Retreat => _retreat.Evaluate(context, entry),
        EnemyAiActionKind.Wait => _wait.Evaluate(context, entry),
        _ => throw new InvalidOperationException($"Unsupported action kind {entry.Action.Kind}"),
    };
```

Convert `BattleAiScoreService`, `BattleAiScoreProjection`, and `BattleAiContext.active_score_profile` to `BattleAiScoreProfileDefinition`. Delete Resource-action mutation checkpoints, authored fallback entries, `allow_authored_action_fallback_for_tests`, and `DecideWithActionFallback`. Remove `Decide`, `BuildCandidateRequest`, candidate-building helpers, trace/runtime state, and other battle-runtime behavior from `EnemyAiAction`/concrete authoring Resources; they retain exported fields and schema/projection validation only. Add a source gate rejecting `Decide(` and `BuildCandidateRequest(` under `scripts/enemies`. Keep the Phase 2 CLR battle-state mutation guard.

- [ ] **Step 4: Run AI green and commit**

```powershell
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_score_selection_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_move_to_range_progress_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_wait_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_retreat_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_advantage_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs
rg -n "\b(Decide|BuildCandidateRequest)\s*\(" scripts/enemies --glob "*.cs"
python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
# Stage only the exact Task 3 Files manifest; patch-stage protected user hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: evaluate typed enemy AI actions"
```

---

### Task 4: Convert every template/roster runtime consumer

**Files:**
- Modify: `scripts/systems/world/EncounterRosterBuilder.cs`
- Modify: `scripts/systems/world/WildEncounterGrowthSystem.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs`
- Modify: `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs`
- Modify: `scripts/player/progression/QuestContentValidator.cs`
- Modify: `scripts/systems/battle/sim/BattleSimContentProvider.cs`
- Modify: `scripts/systems/battle/sim/BattleSimRunner.cs`
- Modify: `scripts/systems/battle/sim/BattleSimProfileDef.cs`
- Create: `scripts/systems/battle/sim/BattleSimProfileDefinition.cs`
- Create: `scripts/systems/battle/sim/BattleSimOverridePatchDefinition.cs`
- Modify: `scripts/systems/battle/sim/BattleSimOverrideApplier.cs`
- Modify: `scripts/systems/battle/sim/BattleSimOverrideApplyResult.cs`
- Modify: `scripts/systems/content/ProcessContentHost.cs`
- Modify: `scripts/systems/content/ContentSnapshotBuilder.cs`
- Modify: `scripts/systems/content/ContentSnapshot.cs`
- Test: `tests/battle_runtime/runtime/run_encounter_roster_builder_typed_boundary_regression.cs`
- Test: `tests/battle_runtime/runtime/run_enemy_template_attribute_projection_regression.cs`
- Test: `tests/battle_runtime/runtime/run_encounter_roster_loot_preview_regression.cs`
- Test: `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`
- Create: `tests/runtime/validation/run_battle_sim_definition_patch_regression.cs`

**Interfaces:**
- Consumes: Task 1 definitions and typed GameContentCatalog.
- Produces: world/runtime/sim signatures with no Enemy/roster Resources.

- [ ] **Step 1: Add compile/static boundary failures**

Change tests to require `EnemyTemplateDefinition`/`EnemyAiBrainDefinition`/`WildEncounterRosterDefinition`/`BattleAiScoreProfileDefinition`. Add a source guard with exact type-word boundaries that rejects raw Def signatures in runtime consumers.

- [ ] **Step 2: Run red**

```powershell
dotnet build magic.csproj
godot --headless -s res://tests/battle_runtime/runtime/run_encounter_roster_builder_typed_boundary_regression.cs
```

- [ ] **Step 3: Convert consumers and sim overrides**

Project `BattleSimProfileDef` once to:

```csharp
internal sealed record BattleSimOverridePatchDefinition(
    string TargetType,
    StringName TargetId,
    StringName StateId,
    StringName ActionId,
    string Path,
    object Value
);

internal sealed record BattleSimProfileDefinition(
    StringName ProfileId,
    string DisplayName,
    string Description,
    BattleAiScoreProfileDefinition AiScoreProfile,
    IReadOnlyList<BattleSimOverridePatchDefinition> OverridePatches
);

internal BattleSimOverrideApplyResult ApplyProfileTyped(
    IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
    IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
    BattleSimProfileDefinition profile
);
```

`ContentSnapshotBuilder` projects the legacy simulation profiles during the same process build and publishes `IReadOnlyDictionary<StringName, BattleSimProfileDefinition> BattleSimProfiles` on `ContentSnapshot`. `BattleSimOverrideApplyResult` exposes typed skill/brain/score/faction-score definitions only. Apply patches with immutable copy-with functions for skill, brain, action, and score definitions; prohibit Resource `Duplicate()`, `SetIndexed`, Variant Object, and GDictionary intermediate graphs. `run_battle_sim_definition_patch_regression.cs` is a pure definition test, not a numeric battle simulation: it proves every target kind patches the expected field/fingerprint, source definitions stay unchanged, and error ordering is stable. Preserve ordering, loot/drop behavior, creature tags, skill levels, equipment projection, roster stage growth, and headless commands.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_encounter_roster_builder_typed_boundary_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_enemy_template_attribute_projection_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_encounter_roster_loot_preview_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs
godot --headless -s res://tests/runtime/validation/run_battle_sim_definition_patch_regression.cs
dotnet build magic.csproj
# Stage only the exact Task 4 Files manifest; patch-stage protected user hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: consume typed enemy content"
```

---

### Task 5: Delete Enemy/AI LegacyDebt and enforce zero raw runtime references

**Files:**
- Delete: `scripts/systems/content/ILegacyEnemyContentCatalog.cs`
- Modify: `scripts/systems/content/ProcessContentHost.cs`
- Modify: `scripts/systems/content/ContentSnapshot.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Modify: `scripts/systems/persistence/GameSession.cs` and `GameSession.ContentValidation.cs`
- Modify: `tests/shared/GameSessionTestFactory.cs`
- Modify: `tests/shared/SyntheticContentSnapshotFactory.cs`
- Modify: `scripts/utils/GodotObjectOwnership.cs` debt registry
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`
- Modify: `docs/design/project_context_units.md` CU-02/CU-16/CU-20

**Interfaces:**
- Consumes: Tasks 1-4.
- Produces: no Enemy/AI content debt and complete typed ContentSnapshot runtime surface.

- [ ] **Step 1: Add final static gates**

Reject these in runtime/world/AI signatures:

```text
EnemyTemplateDef
EnemyAiBrainDef
EnemyAiAction
WildEncounterRosterDef
BattleAiScoreProfile
BattleSimProfileDef
ResourceAction
BuildAuthoredFallbackEntries / DecideWithActionFallback
GetInstanceId metadata
RuntimeEnemyAiResourceFactory action creation
ProcessContentHost.LegacyEnemyContentRegistry
```

Authoring loader/projector files may reference Def/Resource; runtime consumer files may not.

- [ ] **Step 2: Delete the legacy interface/debt and raw getters**

Remove the separate legacy catalog from session/catalog/host, delete raw runtime getters and debt metadata, and ensure `ContentSnapshot` is the sole enemy catalog source.

The post-Phase-4 binding signatures are:

```csharp
// GameSession
internal void BindContent(ContentSnapshot snapshot);

// GameContentCatalog
internal void BindSnapshot(
    GameSession session,
    ContentSnapshot snapshot
);

// GameSessionTestFactory
internal static GameSession CreateSynthetic(ContentSnapshot snapshot);
```

- [ ] **Step 3: Run complete phase gate**

```powershell
rg -n "\b(EnemyTemplateDef|EnemyAiBrainDef|EnemyAiAction|WildEncounterRosterDef|BattleAiScoreProfile|BattleSimProfileDef)\b" scripts/systems scripts/ui --glob "*.cs" --glob "!scripts/systems/battle/ai/BattleAiScoreProfile.cs" --glob "!scripts/systems/battle/sim/BattleSimProfileDef.cs"
rg -n "ResourceAction|BuildAuthoredFallbackEntries|DecideWithActionFallback|GetInstanceId\(|allow_authored_action_fallback_for_tests|RuntimeEnemyAiResourceFactory" scripts/systems/battle/ai --glob "*.cs"
rg -n "\b(Decide|BuildCandidateRequest)\s*\(" scripts/enemies --glob "*.cs"
python tests/run_regression_suite.py --pattern runtime/validation --jobs 16 --finalizer-crash-retries 0
python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --finalizer-crash-retries 0
python tests/run_regression_suite.py --pattern battle_runtime/runtime --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
git diff --check
```

Expected: all three rg commands have no output; authoring/projector references are outside the scanned runtime method/signature sets. All regressions/build PASS with retries 0.

- [ ] **Step 4: Update context map and commit**

```powershell
# Stage only the exact Task 5 Files manifest; keep unrelated worktree edits unstaged.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: remove legacy enemy content ownership"
```
