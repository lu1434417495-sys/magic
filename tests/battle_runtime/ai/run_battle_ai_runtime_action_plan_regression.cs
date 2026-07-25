using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_runtime_action_plan_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPlanAndEntryOwnNoResourceOrInstanceIdState();
            TestEntryDefensivelyCopiesMetadata();
            TestPlanFingerprintTracksSkillsAndImmutableBrainShape();
            TestClearAndDisposeReleasePlainBorrowers();
            TestAssemblerExceptionDisposesPartialPlanAndBalancesTrace();
            TestPlanReportsEmptyRuntimeState();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI runtime action plan regression"));
    }

    private void TestPlanAndEntryOwnNoResourceOrInstanceIdState()
    {
        foreach (
            FieldInfo field in typeof(BattleAiRuntimeActionPlan).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            _test.True(
                !typeof(Resource).IsAssignableFrom(field.FieldType)
                    && field.FieldType != typeof(NativeLeaseScope)
                    && !field.Name.Contains("instanceId", StringComparison.OrdinalIgnoreCase),
                $"Runtime action plan field {field.Name} must not own Resource/native scope/instance-id metadata."
            );
        }

        PropertyInfo actionProperty = typeof(BattleAiRuntimeActionEntry).GetProperty(
            "Action",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.Eq(
            actionProperty?.PropertyType,
            typeof(EnemyAiActionDefinition),
            "Runtime entries should expose one typed immutable action definition."
        );
        _test.True(
            typeof(BattleAiRuntimeActionEntry).GetProperty(
                "ResourceAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            ) == null,
            "Runtime entries must not expose a ResourceAction fallback."
        );
    }

    private void TestEntryDefensivelyCopiesMetadata()
    {
        WaitActionDefinition action = Wait("metadata_wait");
        var source = new BattleAiRuntimeActionPlan.RuntimeActionMetadata
        {
            state_id = "engage",
            action_id = action.ActionId,
            score_bucket_id = action.ScoreBucketId,
            identity_key = "engage/metadata_wait",
        };
        var entry = new BattleAiRuntimeActionEntry(action, source);
        source.identity_key = "mutated";
        source.action_id = "mutated";

        _test.Eq(entry.Action, action, "Entry should borrow the immutable action definition.");
        _test.Eq(
            entry.Metadata.identity_key,
            "engage/metadata_wait",
            "Entry should retain its own metadata copy."
        );
        _test.Eq(
            entry.ActionId,
            new StringName("metadata_wait"),
            "Entry action identity should come from the definition."
        );

        using var plan = new BattleAiRuntimeActionPlan();
        plan.AddAction("engage", action, source);
        BattleAiRuntimeActionPlan.RuntimeActionMetadata stored = plan.GetActionMetadata(action);
        _test.Eq(
            stored.action_id,
            new StringName("mutated"),
            "Plan metadata should be keyed by the typed definition entry."
        );
        stored.action_id = "external_mutation";
        _test.Eq(
            plan.GetActionMetadata(action).action_id,
            new StringName("mutated"),
            "Metadata queries should return defensive copies."
        );
    }

    private void TestPlanFingerprintTracksSkillsAndImmutableBrainShape()
    {
        EnemyAiBrainDefinition brain = BuildBrain();
        BattleUnitState unit = BuildUnit("actor", "plan_brain", "engage");
        unit.AddKnownActiveSkill("bolt");
        unit.SetKnownSkillLevelsTyped(new Dictionary<StringName, int> { ["bolt"] = 1 });
        unit.SetCurrentAp(1);

        using var plan = new BattleAiRuntimeActionPlan();
        plan.SetSource(unit, brain);
        _test.True(!plan.IsStaleFor(unit, brain), "Same unit/brain/skill signature should not be stale.");

        unit.SetCurrentAp(0);
        _test.True(!plan.IsStaleFor(unit, brain), "Turn resources should not affect plan staleness.");

        unit.SetKnownSkillLevelTyped("bolt", 2);
        _test.True(plan.IsStaleFor(unit, brain), "Skill level changes should make the plan stale.");

        unit.SetKnownSkillLevelTyped("bolt", 1);
        EnemyAiStateDefinition supportState = new(
            "support",
            new EnemyAiActionDefinition[] { Wait("support_wait") },
            Array.Empty<EnemyAiGenerationSlotDefinition>()
        );
        EnemyAiBrainDefinition expandedBrain = new(
            brain.BrainId,
            brain.DefaultStateId,
            brain.ScoreProfile,
            new[] { brain.GetState("engage"), supportState },
            brain.TransitionRules
        );
        _test.True(
            plan.IsStaleFor(unit, expandedBrain),
            "A different immutable brain state/action shape should make the plan stale."
        );

        using var transitionPlan = new BattleAiRuntimeActionPlan();
        transitionPlan.SetSource(unit, expandedBrain);
        EnemyAiBrainDefinition transitionedBrain = new(
            expandedBrain.BrainId,
            expandedBrain.DefaultStateId,
            expandedBrain.ScoreProfile,
            expandedBrain.StateOrder,
            new[]
            {
                new EnemyAiTransitionRuleDefinition(
                    "support_when_low",
                    10,
                    Array.Empty<StringName>(),
                    "support",
                    new[]
                    {
                        new EnemyAiTransitionConditionDefinition(
                            "self_hp_at_or_below_basis_points",
                            5000,
                            -1,
                            Array.Empty<StringName>(),
                            Array.Empty<StringName>()
                        ),
                    },
                    ""
                ),
            }
        );
        _test.True(
            transitionPlan.IsStaleFor(unit, transitionedBrain),
            "A different immutable transition shape should make the plan stale."
        );
    }

    private void TestClearAndDisposeReleasePlainBorrowers()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var plan = new BattleAiRuntimeActionPlan();
        plan.AddStateActions(
            "engage",
            new EnemyAiActionDefinition[] { Wait("first_generation") }
        );
        _test.True(plan.HasRuntimeBorrowers, "Plan should report active plain borrowers.");

        plan.Clear();
        _test.True(!plan.HasRuntimeBorrowers, "Clear should release plain plan borrowers.");
        plan.AddStateActions(
            "engage",
            new EnemyAiActionDefinition[] { Wait("second_generation") }
        );
        plan.Dispose();
        _test.True(!plan.HasRuntimeBorrowers, "Dispose should release plain plan borrowers.");

        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, "action-plan owner baseline");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, "action-plan lease baseline");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, "action-plan scope baseline");
    }

    private void TestAssemblerExceptionDisposesPartialPlanAndBalancesTrace()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        EnemyAiBrainDefinition brain = BuildBrain();
        BattleUnitState unit = BuildUnit("throwing_actor", brain.BrainId, "engage");
        unit.AddKnownActiveSkill("bolt");
        var recorder = new AiTraceRecorder();
        AiTraceRecorder.SetInstance(recorder);
        BattleAiRuntimeActionPlan unexpectedPlan = null;
        bool threw = false;
        try
        {
            unexpectedPlan = new BattleAiActionAssembler().BuildUnitActionPlan(
                unit,
                brain,
                new ThrowingSkillDictionary()
            );
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(
                "action plan classification probe",
                StringComparison.Ordinal
            );
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
            unexpectedPlan?.Dispose();
        }

        _test.True(threw, "Assembler should surface the classification failure.");
        _test.True(recorder.AssertBalanced(), "Assembler failure should close its trace span.");
        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, "assembler owner baseline");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, "assembler lease baseline");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, "assembler scope baseline");
    }

    private void TestPlanReportsEmptyRuntimeState()
    {
        using var plan = new BattleAiRuntimeActionPlan();
        plan.SetSource(BuildUnit("actor", "plan_brain", "engage"), BuildBrain());
        plan.AddStateActions("engage", Array.Empty<EnemyAiActionDefinition>());
        _test.True(plan.HasState("engage"), "Explicit empty states should remain present.");
        _test.True(plan.IsEmptyState("engage"), "Explicit empty states should report empty.");
        _test.True(plan.Validate().Count == 0, "A sourced empty state should remain structurally valid.");
    }

    private static EnemyAiBrainDefinition BuildBrain()
    {
        EnemyAiStateDefinition state = new(
            "engage",
            new EnemyAiActionDefinition[] { Wait("authored_wait") },
            Array.Empty<EnemyAiGenerationSlotDefinition>()
        );
        return new EnemyAiBrainDefinition(
            "plan_brain",
            "engage",
            BattleAiScoreProfileDefinition.Default,
            new[] { state },
            Array.Empty<EnemyAiTransitionRuleDefinition>()
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName brainId, StringName stateId) =>
        new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            ai_brain_id = brainId,
            ai_state_id = stateId,
            control_mode = "ai",
        }.WithCombatResourcesForTest(
            hp: 20,
            mp: 2,
            stamina: 2,
            ap: 2
        );

    private static WaitActionDefinition Wait(StringName actionId) =>
        new(actionId, "", BattleAiActionIntent.Wait, 0, 0);

    private sealed class ThrowingSkillDictionary
        : IReadOnlyDictionary<StringName, SkillDefinition>
    {
        public int Count => throw BuildException();
        public IEnumerable<StringName> Keys => throw BuildException();
        public IEnumerable<SkillDefinition> Values => throw BuildException();
        public SkillDefinition this[StringName key] => throw BuildException();
        public bool ContainsKey(StringName key) => throw BuildException();

        public bool TryGetValue(StringName key, out SkillDefinition value)
        {
            value = null;
            throw BuildException();
        }

        public IEnumerator<KeyValuePair<StringName, SkillDefinition>> GetEnumerator() =>
            throw BuildException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static InvalidOperationException BuildException() =>
            new("action plan classification probe");
    }
}
