using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_runtime_borrower_teardown_regression : LifecycleTestSceneTree
{
    private const int EquipmentMarkInitialDurationTu = 100;
    private const int EquipmentMarkElapsedTu = 10;

    private sealed class ProbeSpecialProfileView : IBattleSpecialProfileView
    {
        public bool TryGetMeteorSwarmProfile(
            StringName profileId,
            out MeteorSwarmProfileData profile
        )
        {
            profile = null;
            return false;
        }
    }

    private sealed class ThrowingTerrainGenerator : BattleTerrainGenerator
    {
        internal int DisposeAttemptCount { get; private set; }

        public override void Dispose()
        {
            DisposeAttemptCount++;
            throw new InvalidOperationException("expected terrain teardown failure");
        }
    }

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        TestResult result;
        try
        {
            ContentFixture content = LoadContentFixture();
            TestContentRebindClearsAiBorrowers(content);
            TestStateRebindClearsAiPlanAndDecisionContext(content);
            TestEquipmentAbilityServiceDisposeRequiresExplicitRebind();
            TestSuccessfulBorrowerFirstTeardownAndDoubleDispose(content);
            TestExceptionalFinalLeaseCloseStillClearsBorrowers(content);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        result = _test.Finish("Battle runtime borrower teardown regression");

        RequestTestExit(result);
    }

    private void TestContentRebindClearsAiBorrowers(ContentFixture content)
    {
        var runtime = new BattleRuntimeModule();
        try
        {
            SetupRuntime(runtime, content);
            BattleState state = BuildState(out BattleUnitState actor);
            runtime.SetupStateForTests(state);
            BindDecisionBorrowers(runtime, actor);

            _test.True(runtime.HasAiRuntimeBorrowers, "precondition: AI helper borrowers are bound");

            runtime.SyncContentCatalogsTyped(
                new Dictionary<StringName, ItemDefinition>(),
                new Dictionary<StringName, SkillDefinition>(),
                new Dictionary<StringName, TraitDefinition>(),
                new Dictionary<StringName, EquipmentAbilityBindingDefinition>(),
                new Dictionary<StringName, BarrierProfileDefinition>()
            );

            _test.True(!runtime.HasAiRuntimeBorrowers, "content sync clears decision/helper borrowers");
            _test.Eq(runtime.GetSkillDefinitionIndexTyped().Count, 0, "skill rebind drops old definitions");
            _test.Eq(runtime.GetItemDefIndexTyped().Count, 0, "item rebind drops old definitions");
            _test.Eq(runtime.GetTraitDefIndexTyped().Count, 0, "trait rebind drops old definitions");
            _test.Eq(
                runtime.GetEquipmentAbilityBindingIndexTyped().Count,
                0,
                "equipment binding rebind drops old definitions"
            );

            runtime.ReplaceEnemyTemplatesTyped(new Dictionary<StringName, EnemyTemplateDefinition>());
            runtime.ReplaceEnemyAiBrainsTyped(new Dictionary<StringName, EnemyAiBrainDefinition>());
            _test.Eq(runtime.GetEnemyTemplateIndexTyped().Count, 0, "enemy template rebind drops old definitions");
            _test.Eq(runtime.GetEnemyAiBrainIndexTyped().Count, 0, "enemy brain rebind drops old definitions");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestStateRebindClearsAiPlanAndDecisionContext(ContentFixture content)
    {
        var runtime = new BattleRuntimeModule();
        try
        {
            SetupRuntime(runtime, content);
            BattleState originalState = BuildState(out BattleUnitState originalActor);
            runtime.SetupStateForTests(originalState);
            DecisionBorrowerFixture borrower = BindDecisionBorrowers(runtime, originalActor);

            _test.True(
                borrower.Plan.HasRuntimeBorrowers,
                "state rebind precondition: action plan holds runtime borrowers"
            );
            _test.True(
                borrower.Context.HasRuntimeBindings,
                "state rebind precondition: decision context holds runtime bindings"
            );
            _test.True(
                runtime.HasAiRuntimeBorrowers,
                "state rebind precondition: runtime reports AI borrowers"
            );

            BattleState replacementState = BuildState(out _);
            runtime.SetupStateForTests(replacementState);

            _test.True(
                !borrower.Plan.HasRuntimeBorrowers,
                "state rebind disposes the old action plan"
            );
            _test.True(
                !borrower.Context.HasRuntimeBindings,
                "state rebind clears the old decision context"
            );
            _test.True(
                !runtime.TryGetAiActionPlanForUnit(originalActor.unit_id, out _),
                "state rebind removes the old action plan from its owner"
            );
            _test.True(
                !runtime.HasAiRuntimeBorrowers,
                "state rebind leaves no stale AI plan or helper borrower"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestEquipmentAbilityServiceDisposeRequiresExplicitRebind()
    {
        var runtime = new BattleRuntimeModule();
        try
        {
            BattleState state = BuildState(out BattleUnitState targetUnit);
            runtime.SetupStateForTests(state);
            runtime.ConfigureDamageResolverForTests(runtime.GetDamageResolver());
            BattleEquipmentAbilityRuntimeService service =
                runtime.GetEquipmentAbilityRuntimeService();
            BattleDamageResolver damageResolver = runtime.GetDamageResolver();

            _test.True(
                state.SetEquipmentTargetMark(
                    BuildEquipmentLifecycleMark(targetUnit),
                    uniquePerSource: true,
                    out _
                ),
                "precondition: equipment target mark is registered"
            );
            service.Dispose();

            using (var disposedBatch = new BattleEventBatch())
            {
                _test.False(
                    service.AdvanceTargetMarkDurations(
                        targetUnit,
                        EquipmentMarkElapsedTu,
                        disposedBatch
                    ),
                    "disposed equipment service cannot advance target marks"
                );
                _test.Eq(
                    ReadOnlyEquipmentMarkDuration(state),
                    EquipmentMarkInitialDurationTu,
                    "disposed equipment service leaves target mark state unchanged"
                );
                _test.Eq(
                    disposedBatch.ChangeFlags,
                    BattleChangeFlags.None,
                    "disposed equipment service emits no change flags"
                );
                _test.Eq(
                    disposedBatch.ChangedUnitIdsTyped.Count,
                    0,
                    "disposed equipment service emits no changed units"
                );
            }
            _test.True(service.GetBattleState() == null, "disposed equipment service releases battle state");
            _test.True(service.DamageResolver == null, "disposed equipment service releases damage resolver");

            service.Dispose();
            BattleEquipmentAbilityRuntimeService reboundService =
                runtime.GetEquipmentAbilityRuntimeService();
            runtime.ConfigureDamageResolverForTests(damageResolver);

            using var reboundBatch = new BattleEventBatch();
            _test.True(
                reboundService.AdvanceTargetMarkDurations(
                    targetUnit,
                    EquipmentMarkElapsedTu,
                    reboundBatch
                ),
                "rebound equipment service resumes target mark updates"
            );
            _test.Eq(
                ReadOnlyEquipmentMarkDuration(state),
                EquipmentMarkInitialDurationTu - EquipmentMarkElapsedTu,
                "rebound equipment service updates only the current runtime state"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestSuccessfulBorrowerFirstTeardownAndDoubleDispose(ContentFixture content)
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var runtime = new BattleRuntimeModule();
        BattleTerrainGenerator ownedTerrainGenerator = runtime.GetTerrainGenerator();
        BattleState state = BuildState(out BattleUnitState actor);
        BattleRuntimeModuleBorrowerTopologySnapshot initialBorrowerTopology =
            runtime._moduleBorrowers.CaptureTopology(runtime);
        AssertModuleBorrowersBound(initialBorrowerTopology, "constructor binding");
        SetupRuntime(runtime, content);
        BattleRuntimeModuleBorrowerTopologySnapshot reboundBorrowerTopology =
            runtime._moduleBorrowers.CaptureTopology(runtime);
        AssertModuleBorrowerTopologyStable(
            initialBorrowerTopology,
            reboundBorrowerTopology,
            "setup rebind"
        );
        runtime.SetupStateForTests(state);
        BindDecisionBorrowers(runtime, actor);

        _test.True(runtime.HasContentCatalogBorrowers, "precondition: all content borrowers are populated");
        _test.True(runtime.HasAiRuntimeBorrowers, "precondition: decision/plan borrowers are populated");
        _test.True(runtime.HasRuntimeSidecarBindings, "precondition: runtime sidecars are bound");
        _test.True(
            runtime._contingency_system.HasRuntimeCapabilityBinding,
            "precondition: contingency system borrows the runtime capability port"
        );
        AssertModuleBorrowersBound(
            runtime._moduleBorrowers.CaptureTopology(runtime),
            "battle-active binding"
        );
        _test.True(
            runtime._ground_effect_service.ActiveDependencyCount > 0,
            "precondition: ground-effect child borrowers are bound"
        );
        runtime.Dispose();
        AssertRuntimeCleared(runtime, state, "successful teardown");
        _test.True(ownedTerrainGenerator.IsDisposed, "owned terrain resource closes after borrowers/state");
        AssertAuditBaseline(baseline, "successful teardown");

        LifecycleAuditSnapshot afterFirstDispose = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        runtime.Dispose();
        AssertAuditEqual(afterFirstDispose, LifecycleAuditRegistry.Shared.CaptureSnapshot(), "double Dispose");
    }

    private void TestExceptionalFinalLeaseCloseStillClearsBorrowers(ContentFixture content)
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var runtime = new BattleRuntimeModule();
        var throwingTerrainGenerator = new ThrowingTerrainGenerator();
        runtime.ConfigureOwnedTerrainGeneratorForTests(throwingTerrainGenerator);
        BattleState state = BuildState(out BattleUnitState actor);
        SetupRuntime(runtime, content);
        runtime.SetupStateForTests(state);
        BindDecisionBorrowers(runtime, actor);
        AssertModuleBorrowersBound(
            runtime._moduleBorrowers.CaptureTopology(runtime),
            "exceptional teardown binding"
        );
        _test.True(
            runtime._ground_effect_service.ActiveDependencyCount > 0,
            "exceptional teardown precondition: ground-effect child borrowers are bound"
        );
        bool threwExpectedFailure = false;
        try
        {
            runtime.Dispose();
        }
        catch (InvalidOperationException exception)
        {
            threwExpectedFailure = exception.Message.Contains(
                "expected terrain teardown failure",
                StringComparison.Ordinal
            );
        }

        _test.True(threwExpectedFailure, "owned resource close failure remains visible to the caller");
        _test.Eq(throwingTerrainGenerator.DisposeAttemptCount, 1, "failing owner closes exactly once");
        AssertRuntimeCleared(runtime, state, "exceptional teardown");
        AssertAuditBaseline(baseline, "exceptional teardown");

        LifecycleAuditSnapshot afterFailure = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        runtime.Dispose();
        _test.Eq(throwingTerrainGenerator.DisposeAttemptCount, 1, "double Dispose does not retry failed owner");
        AssertAuditEqual(afterFailure, LifecycleAuditRegistry.Shared.CaptureSnapshot(), "exception double Dispose");
    }

    private static void SetupRuntime(BattleRuntimeModule runtime, ContentFixture content)
    {
        runtime.setup(
            skill_definitions: content.SkillDefinitions,
            enemy_templates: content.EnemyTemplates,
            enemy_ai_brains: content.EnemyBrains,
            item_defs: content.ItemDefs,
            equipment_instance_id_allocator: () => "teardown-equipment-instance",
            skill_catalog: new SkillCatalog(null),
            battle_special_profile_view: new ProbeSpecialProfileView(),
            trait_defs: content.TraitDefs,
            equipment_ability_bindings: content.EquipmentBindings
        );
    }

    private static DecisionBorrowerFixture BindDecisionBorrowers(
        BattleRuntimeModule runtime,
        BattleUnitState actor
    )
    {
        const string formalBrainId = "melee_aggressor";
        actor.control_mode = "ai";
        actor.ai_brain_id = formalBrainId;
        try
        {
            EnemyAiBrainDefinition brain = runtime.GetEnemyAiBrainTyped(formalBrainId);
            if (brain == null)
            {
                throw new InvalidOperationException(
                    $"Missing teardown fixture AI brain: {formalBrainId}"
                );
            }
            actor.ai_state_id = brain.DefaultStateId;
            runtime._ensure_ai_action_plan_for_unit(actor);
            if (
                !runtime.TryGetAiActionPlanForUnit(
                    actor.unit_id,
                    out BattleAiRuntimeActionPlan actionPlan
                )
                || actionPlan == null
            )
            {
                throw new InvalidOperationException(
                    $"Failed to build teardown fixture AI action plan for {actor.unit_id}."
                );
            }

            BattleAiContext context = runtime._prepare_ai_context_for_decision(actor);
            runtime._bind_ai_helper_services_for_decision(actor, context);
            return new DecisionBorrowerFixture(actionPlan, context);
        }
        finally
        {
            // These lifecycle fixtures intentionally remain manual so a content rebind clears
            // the old plan without immediately rebuilding another AI plan for the same actor.
            actor.control_mode = "manual";
        }
    }

    private static BattleState BuildState(out BattleUnitState actor)
    {
        var state = new BattleState
        {
            battle_id = "borrower_teardown",
            map_size = Vector2I.One,
            phase = "unit_acting",
            timeline = new BattleTimelineState(),
        };
        actor = new BattleUnitState
        {
            unit_id = "borrower_actor",
            display_name = "Borrower Actor",
            faction_id = "player",
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 10,
            ap: 1,
            isAlive: true
        );
        actor.SetAnchorCoord(Vector2I.Zero);
        state.SetUnit(actor);
        state.ally_unit_ids.Add(actor.unit_id);
        state.active_unit_id = actor.unit_id;
        state.timeline.ready_unit_ids.Add(actor.unit_id);
        return state;
    }

    private static BattleEquipmentTargetMarkState BuildEquipmentLifecycleMark(
        BattleUnitState targetUnit
    ) =>
        new()
        {
            SourceUnitId = targetUnit.unit_id,
            TargetUnitId = targetUnit.unit_id,
            SourceEquipmentInstanceId = "equipment_lifecycle_instance",
            BindingId = "equipment_lifecycle_binding",
            StateKey = "equipment_lifecycle_mark",
            Stacks = 1,
            RemainingDurationTu = EquipmentMarkInitialDurationTu,
        };

    private static int ReadOnlyEquipmentMarkDuration(BattleState state)
    {
        IReadOnlyList<BattleEquipmentTargetMarkState> marks =
            state.GetEquipmentTargetMarksTyped();
        return marks.Count == 1 ? marks[0].RemainingDurationTu : int.MinValue;
    }

    private void AssertModuleBorrowersBound(
        BattleRuntimeModuleBorrowerTopologySnapshot snapshot,
        string label
    )
    {
        _test.True(snapshot.RegisteredCount > 0, $"{label}: borrower topology is non-empty");
        string[] borrowerTypes = snapshot.Signature.Split(
            '>',
            StringSplitOptions.RemoveEmptyEntries
        );
        _test.Eq(
            borrowerTypes.Length,
            snapshot.RegisteredCount,
            $"{label}: topology signature must enumerate every borrower exactly once"
        );
        _test.Eq(
            Array.FindAll(
                borrowerTypes,
                value =>
                    value
                        == nameof(
                            BattleCounterattackPreviewService
                        )
            ).Length,
            1,
            $"{label}: P1B topology must contain exactly one counterattack preview borrower"
        );
        int queryIndex = Array.IndexOf(
            borrowerTypes,
            nameof(BattleCounterattackQueryService)
        );
        int previewIndex = Array.IndexOf(
            borrowerTypes,
            nameof(BattleCounterattackPreviewService)
        );
        int commandPreviewIndex = Array.IndexOf(
            borrowerTypes,
            nameof(BattleCommandPreviewService)
        );
        _test.True(
            queryIndex >= 0
                && previewIndex > queryIndex
                && commandPreviewIndex > previewIndex,
            $"{label}: dependency order must be CounterattackQuery -> CounterattackPreview -> CommandPreview"
        );
        _test.Eq(
            snapshot.BoundCount,
            snapshot.RegisteredCount,
            $"{label}: every registered module borrower is bound"
        );
        _test.Eq(
            snapshot.ActiveDependencyCount,
            snapshot.RegisteredCount,
            $"{label}: every registered module borrower has one active runtime dependency"
        );
    }

    private void AssertModuleBorrowerTopologyStable(
        BattleRuntimeModuleBorrowerTopologySnapshot expected,
        BattleRuntimeModuleBorrowerTopologySnapshot actual,
        string label
    )
    {
        _test.Eq(actual.Signature, expected.Signature, $"{label}: topology signature");
        _test.Eq(
            actual.RegisteredCount,
            expected.RegisteredCount,
            $"{label}: registered borrower count"
        );
        AssertModuleBorrowersBound(actual, label);
    }

    private ContentFixture LoadContentFixture()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.LoadSkillDefinition(
            "mage_arcane_aegis"
        );
        ItemDefinition itemResource = TestItemDefinitionLookup.GetProductionItem("whetstone");
        ItemDefinition itemDefinition = itemResource;
        using TraitContentRegistry traitRegistry = new();
        TraitDefinition traitDefinition = traitRegistry.GetTraitDef("brave")
            ?? throw new InvalidOperationException(
                "Production trait JSON does not define brave."
            );
        EnemyTemplateDefinition enemyTemplate = snapshot.EnemyTemplates["zombie_shambler"];
        EnemyAiBrainDefinition enemyBrain = snapshot.EnemyBrains["melee_aggressor"];
        var equipmentBinding = new EquipmentAbilityBindingDefinition
        {
            BindingId = "borrower_teardown_binding",
            TraitId = traitDefinition.TraitId,
        };

        return new ContentFixture(
            new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            },
            new Dictionary<StringName, ItemDefinition>
            {
                [itemDefinition.ItemId] = itemDefinition,
            },
            new Dictionary<StringName, TraitDefinition>
            {
                [traitDefinition.TraitId] = traitDefinition,
            },
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [equipmentBinding.BindingId] = equipmentBinding,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [enemyTemplate.TemplateId] = enemyTemplate,
            },
            new Dictionary<StringName, EnemyAiBrainDefinition>
            {
                [enemyBrain.BrainId] = enemyBrain,
            }
        );
    }

    private void AssertRuntimeCleared(BattleRuntimeModule runtime, BattleState state, string label)
    {
        _test.True(runtime.IsDisposed, $"{label}: runtime reports disposed");
        _test.True(!runtime.HasAiRuntimeBorrowers, $"{label}: decision/plan borrowers clear");
        _test.True(!runtime.HasRuntimeSidecarBindings, $"{label}: sidecar runtime borrowers clear");
        _test.True(
            !runtime._contingency_system.HasRuntimeCapabilityBinding,
            $"{label}: contingency runtime capability clears"
        );
        BattleRuntimeModuleBorrowerTopologySnapshot borrowerTopology =
            runtime._moduleBorrowers.CaptureTopology(runtime);
        _test.Eq(borrowerTopology.BoundCount, 0, $"{label}: module borrowers unbound");
        _test.Eq(
            borrowerTopology.ActiveDependencyCount,
            0,
            $"{label}: module borrower dependencies clear"
        );
        _test.Eq(
            runtime._ground_effect_service.ActiveDependencyCount,
            0,
            $"{label}: ground-effect child dependencies clear"
        );
        _test.True(!runtime.HasContentCatalogBorrowers, $"{label}: content borrowers clear");
        _test.Eq(runtime.GetSkillDefinitionIndexTyped().Count, 0, $"{label}: skill index zero");
        _test.Eq(runtime.GetTraitDefIndexTyped().Count, 0, $"{label}: trait index zero");
        _test.Eq(
            runtime.GetEquipmentAbilityBindingIndexTyped().Count,
            0,
            $"{label}: equipment binding index zero"
        );
        _test.Eq(runtime.GetItemDefIndexTyped().Count, 0, $"{label}: item index zero");
        _test.Eq(runtime.GetEnemyTemplateIndexTyped().Count, 0, $"{label}: enemy template index zero");
        _test.Eq(runtime.GetEnemyAiBrainIndexTyped().Count, 0, $"{label}: enemy brain index zero");
        _test.True(runtime.GetState() == null, $"{label}: runtime state reference clear");
        _test.Eq(state.GetUnitsTyped().Count, 0, $"{label}: state unit topology clear");
        _test.Eq(state.ally_unit_ids.Count, 0, $"{label}: ally index clear");
        _test.Eq(state.enemy_unit_ids.Count, 0, $"{label}: enemy index clear");
        _test.Eq(state.timeline.ready_unit_ids.Count, 0, $"{label}: ready-unit index clear");
    }

    private void AssertAuditBaseline(LifecycleAuditSnapshot expected, string label) =>
        AssertAuditEqual(expected, LifecycleAuditRegistry.Shared.CaptureSnapshot(), label);

    private void AssertAuditEqual(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owner count");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: lease count");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label}: scope count");
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            expected.ActiveContentBorrowerCount,
            $"{label}: content borrower count"
        );
    }

    private sealed record ContentFixture(
        IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions,
        IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs,
        IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentBindings,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyBrains
    );

    private sealed record DecisionBorrowerFixture(
        BattleAiRuntimeActionPlan Plan,
        BattleAiContext Context
    );
}
