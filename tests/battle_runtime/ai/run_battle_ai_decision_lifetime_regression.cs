using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_decision_lifetime_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSuccessCopiesTraceBeforeClearingBorrowers();
            TestResultDetachesDecisionAndTraceAliases();
            TestWaitClearsBorrowers();
            TestInvalidContextClearsPartialBindings();
            TestExceptionClearsBorrowersAndBalancesTrace();
            TestDoubleDisposeKeepsAuditBaseline();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
        }

        RequestTestExit(_test.Finish("Battle AI decision lifetime regression"));
    }

    private void TestSuccessCopiesTraceBeforeClearingBorrowers()
    {
        using Fixture fixture = BuildFixture(includeBrain: true, throwOnDecide: false);
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();

        BattleAiDecisionResult result = fixture.Service.ChooseCommand(
            fixture.Context,
            captureTrace: true
        );

        _test.True(result?.Decision != null, "success path should return a typed decision result.");
        _test.Eq(
            result?.Decision?.action_id ?? new StringName(""),
            new StringName("lifetime_success"),
            "success result should preserve the chosen action."
        );
        _test.Eq(
            result?.TurnTrace?.UnitId ?? "",
            "lifetime_actor",
            "trace should be copied before the context actor is cleared."
        );
        _test.Eq(
            result?.TurnTrace?.ActionId ?? "",
            "lifetime_success",
            "trace should preserve the chosen action after context cleanup."
        );
        AssertDecisionBorrowersCleared(fixture, "success");
        AssertAuditBaseline(baseline, "success decision");
    }

    private void TestResultDetachesDecisionAndTraceAliases()
    {
        using Fixture fixture = BuildFixture(includeBrain: true, throwOnDecide: false);
        var sourceCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Wait,
            unit_id = fixture.Context.unit_state.unit_id,
            target_unit_id = "source_target",
        };
        sourceCommand.AddTargetUnitId("source_target");
        sourceCommand.AddTargetCoord(new Vector2I(0, 0));
        var sourcePreview = new BattlePreview { allowed = true };
        sourcePreview.AddTargetUnitId("source_target");
        sourcePreview.AddTargetCoord(new Vector2I(0, 0));
        var sourceScore = new BattleAiScoreInput
        {
            command = sourceCommand,
            preview = sourcePreview,
            action_kind = "wait",
            action_label = "alias source",
            target_unit_ids = new List<StringName> { "source_target" },
            target_coords = new List<Vector2I> { new(0, 0) },
            high_priority_reasons = new Dictionary<StringName, List<string>>
            {
                ["source_target"] = new() { "captured" },
            },
            path_step_hit_counts_by_unit_id = new Dictionary<StringName, int>
            {
                ["source_target"] = 2,
            },
            total_score = 37,
        };
        var sourceTransition = new BattleAiStateResolver.TransitionResult(
            "engage",
            "engage",
            "",
            "alias_probe",
            new List<BattleAiStateResolver.TransitionConditionTrace>()
        );
        var sourceTraceNested = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["value"] = "captured",
        };
        var sourceCandidateScoreNested = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["value"] = "captured",
        };
        var sourceCandidateExtraList = new List<object> { "captured" };
        var sourceTrace = new AiActionTrace(
            "alias_trace",
            "alias_action",
            "utility",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["nested"] = sourceTraceNested,
            }
        );
        sourceTrace.EvaluationCount = 1;
        AiCandidateSummary sourceCandidate = AiCandidateSummary.Create(
            "alias candidate",
            sourceCommand,
            sourceScore,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["nested_list"] = sourceCandidateExtraList,
            }
        );
        sourceCandidate.ScoreInput["nested"] = sourceCandidateScoreNested;
        sourceTrace.TopCandidates.Add(sourceCandidate);
        fixture.Context.RecordActionTrace(sourceTrace);

        var sourceDecision = new BattleAiDecision
        {
            command = sourceCommand,
            brain_id = "lifetime_brain",
            state_id = "engage",
            action_id = "alias_action",
            reason_text = "alias probe",
            score_bucket_id = "utility",
            score_input = sourceScore,
            skill_score_input = sourceScore,
            Transition = sourceTransition,
        };
        BattleAiDecisionCommitter.AttachStatePatch(sourceDecision);

        BattleAiDecisionResult result = BattleAiDecisionResult.Capture(
            fixture.Context,
            sourceDecision,
            captureTrace: true
        );

        _test.True(result?.Decision != null, "capture should return a detached decision.");
        _test.True(
            !ReferenceEquals(sourceDecision, result?.Decision),
            "result decision must not alias the engine decision."
        );
        _test.True(sourceDecision.command == null, "capture should clear the source command.");
        _test.True(sourceDecision.score_input == null, "capture should clear the source score input.");
        _test.True(sourceDecision.StatePatch == null, "capture should clear the source state patch.");
        _test.True(
            !ReferenceEquals(sourceCommand, result?.Decision?.command),
            "result command must not alias the source command."
        );
        _test.True(
            !ReferenceEquals(sourceScore, result?.Decision?.score_input),
            "result score input must not alias the source score input."
        );
        _test.True(
            result?.Decision?.score_input?.preview == null,
            "detached score input must not retain the runtime preview borrower."
        );
        _test.True(
            !ReferenceEquals(sourceTransition, result?.Decision?.Transition),
            "result transition must be rebuilt."
        );
        _test.True(result?.Decision?.StatePatch != null, "result state patch must be rebuilt.");
        _test.True(
            result?.TurnTrace?.ScoreInputFacts != null,
            "trace should retain plain score facts after detaching its score input."
        );
        _test.True(
            IsStrictPlainGraph(result?.TurnTrace?.ScoreInputFacts),
            "trace score facts should contain only strict plain dictionaries, lists, and scalars."
        );
        _test.True(
            !ReferenceEquals(sourceScore, result?.TurnTrace?.ScoreInput),
            "trace score input must not alias the source score input."
        );
        _test.True(
            result?.TurnTrace?.ScoreInput?.preview == null,
            "trace score input must not retain the runtime preview borrower."
        );
        _test.True(
            result?.TurnTrace?.ActionTraces?.Count == 1
                && !ReferenceEquals(sourceTrace, result.TurnTrace.ActionTraces[0]),
            "trace action entries must be deep copies."
        );

        sourceCommand.AddTargetUnitId("mutated_target");
        sourceScore.target_unit_ids.Add("mutated_target");
        sourceScore.high_priority_reasons["source_target"].Add("mutated");
        sourceTrace.ActionId = "mutated_action";
        sourceTraceNested["value"] = "mutated";
        sourceCandidateScoreNested["value"] = "mutated";
        sourceCandidateExtraList.Add("mutated");

        _test.Eq(
            result?.Decision?.command?.TargetUnitIdsTyped.Count ?? -1,
            1,
            "source command mutation must not change the result command."
        );
        _test.Eq(
            result?.Decision?.score_input?.target_unit_ids.Count ?? -1,
            1,
            "source score-list mutation must not change the result score input."
        );
        _test.Eq(
            result?.Decision?.score_input?.high_priority_reasons["source_target"].Count ?? -1,
            1,
            "nested score-list mutation must not change the result score input."
        );
        _test.Eq(
            result?.TurnTrace?.ActionTraces[0].ActionId ?? "",
            "alias_action",
            "source action-trace mutation must not change the result trace."
        );
        var copiedTraceNested =
            result?.TurnTrace?.ActionTraces[0].Metadata["nested"]
            as IReadOnlyDictionary<string, object>;
        _test.Eq(
            copiedTraceNested?["value"]?.ToString() ?? "",
            "captured",
            "nested action-trace metadata must be deep copied."
        );
        var copiedCandidate = result?.TurnTrace?.ActionTraces[0].TopCandidates[0];
        var copiedCandidateScoreNested =
            copiedCandidate?.ScoreInput["nested"] as IReadOnlyDictionary<string, object>;
        var copiedCandidateExtraList =
            copiedCandidate?.ExtraFields["nested_list"] as IReadOnlyList<object>;
        var copiedCandidatePathSteps =
            copiedCandidate?.ScoreInput["path_step_hit_counts_by_unit_id"]
            as IReadOnlyDictionary<string, object>;
        _test.Eq(
            copiedCandidateScoreNested?["value"]?.ToString() ?? "",
            "captured",
            "candidate score-input nested maps must be deep copied."
        );
        _test.Eq(
            copiedCandidateExtraList?.Count ?? -1,
            1,
            "candidate extra-field nested lists must be deep copied."
        );
        _test.Eq(
            copiedCandidatePathSteps?["source_target"] as int? ?? -1,
            2,
            "candidate score facts should canonicalize non-empty path-step maps."
        );

        BattleAiDecisionCommitter.Commit(fixture.Context.unit_state, result?.Decision);
        _test.Eq(
            fixture.Context.unit_state.ai_blackboard.last_action_id,
            new StringName("alias_action"),
            "rebuilt state patch should remain committable after source cleanup."
        );
    }

    private static bool IsStrictPlainGraph(object value)
    {
        switch (value)
        {
            case null:
            case bool:
            case byte:
            case short:
            case int:
            case long:
            case float:
            case double:
            case string:
            case StringName:
            case Vector2I:
            case Vector2:
            case Vector3I:
            case Vector3:
            case Color:
                return true;
            case IReadOnlyDictionary<string, object> dictionary:
                foreach ((string key, object child) in dictionary)
                {
                    if (string.IsNullOrEmpty(key) || !IsStrictPlainGraph(child))
                        return false;
                }
                return true;
            case IReadOnlyList<object> list:
                foreach (object child in list)
                {
                    if (!IsStrictPlainGraph(child))
                        return false;
                }
                return true;
            default:
                return false;
        }
    }

    private void TestWaitClearsBorrowers()
    {
        using Fixture fixture = BuildFixture(includeBrain: false, throwOnDecide: false);
        BattleAiDecisionResult result = fixture.Service.ChooseCommand(
            fixture.Context,
            captureTrace: true
        );

        _test.Eq(
            result?.Decision?.action_id ?? new StringName(""),
            new StringName("wait_missing_brain"),
            "missing brain should still return the formal wait result."
        );
        _test.Eq(
            result?.TurnTrace?.UnitId ?? "",
            "lifetime_actor",
            "wait trace should be detached before context cleanup."
        );
        AssertDecisionBorrowersCleared(fixture, "wait");
    }

    private void TestInvalidContextClearsPartialBindings()
    {
        using Fixture fixture = BuildFixture(includeBrain: false, throwOnDecide: false);
        fixture.Context.grid_service = null;

        BattleAiDecisionResult result = fixture.Service.ChooseCommand(
            fixture.Context,
            captureTrace: false
        );

        _test.True(result == null, "invalid context should not produce a decision result.");
        AssertDecisionBorrowersCleared(fixture, "invalid context");
    }

    private void TestExceptionClearsBorrowersAndBalancesTrace()
    {
        using Fixture fixture = BuildFixture(includeBrain: true, throwOnDecide: true);
        var recorder = new AiTraceRecorder();
        AiTraceRecorder.SetInstance(recorder);
        bool threw = false;
        try
        {
            fixture.Service.ChooseCommand(fixture.Context, captureTrace: true);
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(
                "decision lifetime score probe",
                StringComparison.Ordinal
            );
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
        }

        _test.True(threw, "exception path should surface the decision failure.");
        _test.True(
            recorder.AssertBalanced(),
            "score exception path should close the action, score, and service trace spans."
        );
        AssertDecisionBorrowersCleared(fixture, "exception");
    }

    private void TestDoubleDisposeKeepsAuditBaseline()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var service = new BattleAiService();
        var plan = new BattleAiRuntimeActionPlan();

        plan.Dispose();
        service.Dispose();
        AssertAuditBaseline(baseline, "first dispose");

        plan.Dispose();
        service.Dispose();
        AssertAuditBaseline(baseline, "second dispose");
    }

    private Fixture BuildFixture(bool includeBrain, bool throwOnDecide)
    {
        var state = new BattleState
        {
            battle_id = "ai_decision_lifetime",
            phase = "unit_acting",
            map_size = new Vector2I(1, 1),
            timeline = new BattleTimelineState(),
        };
        var actor = new BattleUnitState
        {
            unit_id = "lifetime_actor",
            display_name = "Lifetime Actor",
            faction_id = "hostile",
            control_mode = "ai",
            ai_brain_id = "lifetime_brain",
            ai_state_id = "engage",
        }.WithCombatResourcesForTest(
            hp: 10,
            ap: 1,
            movePoints: 1,
            isAlive: true
        );
        actor.SetAnchorCoord(Vector2I.Zero);
        state.SetUnit(actor);
        state.enemy_unit_ids.Add(actor.unit_id);
        state.active_unit_id = actor.unit_id;

        var grid = new BattleGridService();
        var plan = new BattleAiRuntimeActionPlan();
        var context = new BattleAiContext();
        context.ResetForDecision(
            state,
            actor,
            grid,
            plan,
            new Dictionary<StringName, SkillDefinition>(),
            traceEnabled: true,
            new SkillCatalog(null)
        );
        context.preview_command_callback = _ => new BattlePreview();
        context.move_cost_callback = (_, _) => 1;
        context.skill_score_input_callback = (_, _, _, _, _, _) => null;
        if (throwOnDecide)
        {
            context.action_score_input_callback = (_, _, _, _, _, _, _) =>
                throw new InvalidOperationException("decision lifetime score probe");
        }
        context.skill_cast_block_reason_callback = (_, _) =>
            BattleSkillCastBlockReasonKind.None;

        var brains = new Dictionary<StringName, EnemyAiBrainDefinition>();
        if (includeBrain)
        {
            var action = new WaitActionDefinition(
                "lifetime_success",
                "",
                BattleAiActionIntent.Wait,
                0,
                0
            );
            var brainState = new EnemyAiStateDefinition(
                "engage",
                new EnemyAiActionDefinition[] { action },
                Array.Empty<EnemyAiGenerationSlotDefinition>()
            );
            var brain = new EnemyAiBrainDefinition(
                "lifetime_brain",
                "engage",
                BattleAiScoreProfileDefinition.Default,
                new[] { brainState },
                Array.Empty<EnemyAiTransitionRuleDefinition>()
            );
            brains[brain.BrainId] = brain;
            plan.SetSource(actor, brain);
            plan.AddStateActions(brainState.StateId, brainState.Actions);
        }

        var service = new BattleAiService();
        service.Setup(brains, null);
        return new Fixture(service, context, plan);
    }

    private void AssertDecisionBorrowersCleared(Fixture fixture, string label)
    {
        _test.True(!fixture.Context.HasRuntimeBindings, $"{label}: context borrowers should clear.");
        _test.True(
            !fixture.Service.GetScoreService().DecisionScopeActive,
            $"{label}: score decision scope should close."
        );
    }

    private void AssertAuditBaseline(LifecycleAuditSnapshot baseline, string label)
    {
        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, $"{label}: owner baseline");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, $"{label}: lease baseline");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, $"{label}: scope baseline");
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            baseline.ActiveContentBorrowerCount,
            $"{label}: borrower baseline"
        );
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture(
            BattleAiService service,
            BattleAiContext context,
            BattleAiRuntimeActionPlan plan
        )
        {
            Service = service;
            Context = context;
            Plan = plan;
        }

        internal BattleAiService Service { get; }

        internal BattleAiContext Context { get; }

        internal BattleAiRuntimeActionPlan Plan { get; }

        public void Dispose()
        {
            Context.ClearRuntimeBindings();
            Plan.Dispose();
            Service.Dispose();
        }
    }
}
