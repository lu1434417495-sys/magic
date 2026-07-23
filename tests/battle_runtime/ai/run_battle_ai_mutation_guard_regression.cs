using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_ai_mutation_guard_regression : LifecycleTestSceneTree
{
    private const string AbortProcessSetting = "battle_ai/fail_loud_abort_process";
    private const string FailureModeSetting = "battle_ai/failure_policy_mode";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Variant previousAbortProcess = ProjectSettings.GetSetting(AbortProcessSetting, false);
        Variant previousFailureMode = ProjectSettings.GetSetting(FailureModeSetting, "");

        try
        {
            ConfigureRuntimeFaultMode();

            TestProductionDefaultDisablesFullSnapshotGuard();
            TestMutationThenEvaluatorExceptionPrefersGuardViolation();
            TestEvaluatorExceptionWithoutMutationPropagatesOriginal();
            TestTurnTraceProjectsTypedDecisionTransition();
            TestReportPayloadMutationIsDetectedWithoutRollback();
            TestPromotionPayloadMutationIsDetectedWithoutRollback();
            TestSnapshotIsPlainAndDetectsStateWithoutAuditGrowth();
            TestDeclaredBattleUnitFieldsHaveStableCoverage();
            TestBattleStatePublicFieldsHaveSnapshotSentinelCoverage();
            TestBattleObjectiveAuthorityIsDetected();
            TestObjectiveRuntimeProjectionIsTotal();
            TestNestedAuthoritySchemasRemainExplicit();
            TestDeclaredStatusPropertiesHaveSnapshotCoverage();
            TestUnitAuthorityBlindSpotsAreDetected();
            TestNullableUnitAuthorityFieldsAreDetected();
            TestNullableBattleStateContainersAreDetected();
            TestNullableBattleUnitCoreFieldsAreDetected();
            TestNestedAuthorityStructuresAreDetected();
            TestCanonicalContainerKeysAreDetected();
            TestCellAndTerrainRawAuthorityIsDetected();
            TestBarrierRawAuthorityIsDetected();
            TestBlackboardRawPresenceIsDetected();
            TestPlainPayloadTypeIdentityIsDetected();
            TestRawUnitProjectionMutationIsDetectedWithoutNormalization();
            TestSkillDefinitionGraphAndIndexMutationsAreDetected();
            TestNullableStatusFieldsAreDetected();
            TestStatusSemanticBlindSpotsAreDetected();
            TestBattleStateAuthorityBlindSpotsAreDetected();
            TestStableDoubleComparisonPreservesDoublePrecision();
            TestBenignAiBookkeepingIsAllowed();
            TestActiveUnitHpMutationFailsWithoutRollback();
            TestOtherUnitCoordMutationFailsWithoutRollback();
            TestUnknownBlackboardKeyWriteIsIgnored();
            TestCellOccupantMutationFailsWithoutRollback();
            TestCellHeightMutationFailsWithoutRollback();
            TestEffectiveTraitPayloadMutationFailsWithoutRollback();
            TestEffectiveTraitIdsMutationFailsWithoutRollback();
            TestMissingBrainWaitPathIsAllowed();
            TestMissingStateWaitPathIsAllowed();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            BattleAiFailurePolicy.Reset();
            ProjectSettings.SetSetting(AbortProcessSetting, previousAbortProcess);
            ProjectSettings.SetSetting(FailureModeSetting, previousFailureMode);
        }

        RequestTestExit(_test.Finish("Battle AI mutation guard regression"));
    }

    private static void ConfigureRuntimeFaultMode()
    {
        BattleAiFailurePolicy.Reset();
        ProjectSettings.SetSetting(AbortProcessSetting, false);
        ProjectSettings.SetSetting(FailureModeSetting, BattleAiFailurePolicy.ModeRuntimeFault.ToString());
    }

    private void TestProductionDefaultDisablesFullSnapshotGuard()
    {
        using Fixture fixture = BuildFixture(
            MakeMutationAction("active_hp"),
            enableFullSnapshotGuard: false
        );
        _test.Eq(
            fixture.Service.MutationGuardMode,
            BattleAiMutationGuardMode.Disabled,
            "production AI service 默认不应执行 full-snapshot mutation guard。"
        );
        BattleAiDecision decision = Choose(fixture);
        _test.True(
            decision != null,
            "production 默认关闭 guard 时，AI decision 应正常返回。"
        );
        _test.Eq(
            fixture.Actor.current_hp,
            1,
            "production 默认关闭 guard 时，不应捕获或阻断测试 mutation。"
        );
        AssertNoGuardViolation(
            fixture.Context,
            "production 默认关闭 guard 时不应生成 mutation violation。"
        );
    }

    private void TestMutationThenEvaluatorExceptionPrefersGuardViolation()
    {
        var sentinel = new InvalidOperationException("mutation_then_throw_sentinel");
        using Fixture fixture = BuildFixture(
            MakeMutationAction("active_hp"),
            evaluatorException: sentinel
        );
        int beforeHp = fixture.Actor.current_hp;
        int beforeFailureEventCount = BattleAiFailurePolicy.Events.Count;

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "evaluator mutate-then-throw 应优先抛出 mutation violation。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "evaluator mutate-then-throw 应触发 fail-fast guard。",
            "current_hp",
            "BattleAiService.ChooseCommandImpl"
        );
        AssertDiffContainsAll(
            exception?.Report?.Violations,
            "evaluator mutate-then-throw",
            "current_hp"
        );
        _test.Eq(
            exception?.Report?.Stage ?? "",
            "decision_exception",
            "evaluator 异常路径的 mutation report 应保留稳定 stage。"
        );
        _test.True(
            ReferenceEquals(exception?.InnerException, sentinel),
            "mutation violation 应在 InnerException 中保留原 evaluator sentinel。"
        );
        _test.True(
            fixture.Actor.current_hp == 1 && fixture.Actor.current_hp != beforeHp,
            "evaluator mutate-then-throw 后不应回滚 live HP。"
        );
        _test.True(
            !fixture.Service.GetScoreService().DecisionScopeActive,
            "evaluator mutate-then-throw 后应关闭 score decision scope。"
        );
        _test.Eq(
            BattleAiFailurePolicy.Events.Count,
            beforeFailureEventCount + 1,
            "evaluator mutate-then-throw 应只记录一次 mutation failure event。"
        );
    }

    private void TestEvaluatorExceptionWithoutMutationPropagatesOriginal()
    {
        BattleAiFailurePolicy.Reset();
        var sentinel = new InvalidOperationException("throw_without_mutation_sentinel");
        using Fixture fixture = BuildFixture(
            MakeMutationAction("none"),
            evaluatorException: sentinel
        );
        InvalidOperationException actual = null;

        try
        {
            Choose(fixture);
        }
        catch (InvalidOperationException exception)
        {
            actual = exception;
        }

        _test.True(
            ReferenceEquals(actual, sentinel),
            "evaluator 未 mutation 时应原样传播 sentinel exception。"
        );
        _test.True(
            BattleAiFailurePolicy.LastEvent == null
                && BattleAiFailurePolicy.Events.Count == 0,
            "evaluator 未 mutation 时不应生成 mutation failure event。"
        );
        AssertNoGuardViolation(
            fixture.Context,
            "evaluator 未 mutation 时不应生成 mutation violation。"
        );
        _test.True(
            !fixture.Service.GetScoreService().DecisionScopeActive,
            "evaluator 未 mutation 的异常路径也应关闭 score decision scope。"
        );
    }

    private void TestTurnTraceProjectsTypedDecisionTransition()
    {
        var context = new BattleAiContext
        {
            state = new BattleState { battle_id = "decision_transition_projection" },
            unit_state = new BattleUnitState
            {
                unit_id = "actor",
                display_name = "Actor",
                faction_id = "hostile",
                ai_brain_id = "brain",
                ai_state_id = "idle",
            },
        };
        var condition = new EnemyAiTransitionConditionDef
        {
            predicate = "always",
            state_ids = new Godot.Collections.Array<StringName> { "idle" },
        };
        var transition = new BattleAiStateResolver.TransitionResult(
            "idle",
            "engage",
            "enter_engage",
            "matched",
            new List<BattleAiStateResolver.TransitionConditionTrace>
            {
                BattleAiStateResolver.TransitionConditionTrace.FromCondition(
                    condition.ToDefinition()
                ),
            }
        );
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Wait),
            unit_id = "actor",
        };
        var decision = new BattleAiDecision
        {
            command = command,
            action_id = "wait",
            reason_text = "transition projected",
            Transition = transition,
        };

        using GodotProjectionLease<GDictionary> traceLease =
            BattleAiTurnTracePayloadProjection.BuildLease(
                context.BuildTurnTraceTyped(decision)
            );
        GDictionary turnTrace = traceLease.Value;
        GDictionary transitionPayload = turnTrace["transition"].AsGodotDictionary();
        _test.Eq(
            ProgressionDataUtils.to_string_name(transitionPayload["previous_state_id"]),
            new StringName("idle"),
            "turn trace 应从 typed transition 投影 previous_state_id。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(transitionPayload["state_id"]),
            new StringName("engage"),
            "turn trace 应从 typed transition 投影 state_id。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(transitionPayload["rule_id"]),
            new StringName("enter_engage"),
            "turn trace 应从 typed transition 投影 rule_id。"
        );
        _test.Eq(
            transitionPayload["matched_conditions"].AsGodotArray().Count,
            1,
            "turn trace 应投影 matched condition trace。"
        );
    }

    private void TestBenignAiBookkeepingIsAllowed()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleAiDecision decision = Choose(fixture);

        AssertNoGuardViolation(fixture.Context, "普通 wait 决策不应触发 mutation guard。");
        _test.True(
            decision != null && decision.action_id == new StringName("test_mutation_none"),
            "普通 action 应正常返回原 decision。"
        );
        BattleAiDecisionCommitter.Commit(fixture.Actor, decision);
        _test.Eq(
            fixture.Actor.ai_blackboard.last_action_id,
            new StringName("test_mutation_none"),
            "合法 decision bookkeeping 应保留。"
        );
        _test.Eq(
            fixture.Actor.ai_blackboard.turn_decision_count,
            1,
            "合法 decision commit 应递增 turn_decision_count。"
        );
        _test.True(
            fixture.Actor.ai_blackboard.has("turn_decision_count"),
            "合法 decision commit 应同步 turn_decision_count 的 typed presence 标记。"
        );
    }

    private void TestActiveUnitHpMutationFailsWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("active_hp"));
        int beforeHp = fixture.Actor.current_hp;

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "active unit HP mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "active unit HP mutation 应触发 fail-fast guard。",
            "current_hp",
            "BattleAiService.ChooseCommandImpl"
        );
        _test.True(
            fixture.Actor.current_hp == 1 && fixture.Actor.current_hp != beforeHp,
            "mutation guard 抛错后不应回滚 active unit HP；失败 fixture 应直接废弃。"
        );
    }

    private void TestOtherUnitCoordMutationFailsWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("other_coord"));
        Vector2I beforeCoord = fixture.Hero.coord;

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "其他单位坐标 mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "其他单位坐标 mutation 应触发 fail-fast guard。",
            "coord",
            "BattleAiService.ChooseCommandImpl"
        );
        _test.True(
            fixture.Hero.coord == new Vector2I(4, 2)
                && fixture.Hero.coord != beforeCoord,
            "mutation guard 抛错后不应回滚其他单位坐标。"
        );
    }

    private void TestUnknownBlackboardKeyWriteIsIgnored()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("blackboard"));

        BattleAiDecision decision = Choose(fixture);

        AssertNoGuardViolation(fixture.Context, "未知 blackboard key 写入应被 typed blackboard 忽略。");
        _test.True(
            decision != null && decision.action_id == new StringName("test_mutation_blackboard"),
            "未知 blackboard key 不应阻断 action。"
        );
        _test.True(
            !fixture.Actor.ai_blackboard.has("rogue_key"),
            "未知 blackboard key 不应落入运行时状态。"
        );
    }

    private void TestCellOccupantMutationFailsWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("cell_occupant"));
        BattleCellState cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(3, 1));
        StringName beforeOccupant = cell?.occupant_unit_id ?? "";

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "cell occupant mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "cell occupant mutation 应触发 fail-fast guard。",
            "occupant_unit_id",
            "BattleAiService.ChooseCommandImpl"
        );
        cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(3, 1));
        _test.True(
            (cell?.occupant_unit_id ?? "") == fixture.Actor.unit_id
                && (cell?.occupant_unit_id ?? "") != beforeOccupant,
            "mutation guard 抛错后不应回滚 cell occupant。"
        );
    }

    private void TestCellHeightMutationFailsWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("cell_height"));
        BattleCellState cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(0, 0));
        int beforeOffset = cell?.height_offset ?? int.MinValue;

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "cell height mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "cell height mutation 应触发 fail-fast guard。",
            "height_offset",
            "BattleAiService.ChooseCommandImpl"
        );
        cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(0, 0));
        _test.True(
            (cell?.height_offset ?? int.MinValue) == 2
                && (cell?.height_offset ?? int.MinValue) != beforeOffset,
            "mutation guard 抛错后不应回滚 cell height_offset。"
        );
    }

    private void TestEffectiveTraitPayloadMutationFailsWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("effective_trait"));
        fixture.Actor.effective_trait_instances = MakeEffectiveTraitPayload();
        fixture.Actor.effective_trait_ids = new GStringNameArray { "halfling_luck" };

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "effective trait payload mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "effective trait payload mutation 应触发 fail-fast guard。",
            "effective_trait_instances",
            "BattleAiService.ChooseCommandImpl"
        );

        BattleEffectiveTraitInstanceState mutated = fixture.Actor.effective_trait_instances.Count > 0
            ? fixture.Actor.effective_trait_instances[0]
            : null;
        _test.Eq(
            mutated?.effect_type ?? "",
            new StringName("savage_attacks"),
            "mutation guard 抛错后不应回滚 effective trait payload。"
        );
    }

    private void TestEffectiveTraitIdsMutationFailsWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("effective_trait_ids"));
        fixture.Actor.effective_trait_instances = MakeEffectiveTraitPayload();
        fixture.Actor.effective_trait_ids = new GStringNameArray { "halfling_luck" };

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "effective trait id mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "effective trait id mutation 应触发 fail-fast guard。",
            "effective_trait_ids",
            "BattleAiService.ChooseCommandImpl"
        );
        _test.True(
            fixture.Actor.effective_trait_ids.Count == 2
                && fixture.Actor.effective_trait_ids.Contains("halfling_luck")
                && fixture.Actor.effective_trait_ids.Contains("rogue_trait"),
            "mutation guard 抛错后不应回滚 effective_trait_ids。"
        );
    }

    private void TestReportPayloadMutationIsDetectedWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("report_payload"));
        fixture.State.SetReportEntries(
            new[]
            {
                new Dictionary<string, object>
                {
                    ["entry_type"] = "mutation_probe",
                    ["text"] = "before",
                    ["event_tags"] = new List<object> { "before_tag" },
                },
            }
        );

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "report payload mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "report payload mutation 应触发 fail-fast guard。",
            "report_entries",
            "BattleAiService.ChooseCommandImpl"
        );
        AssertDiffContainsAll(
            exception?.Report?.Violations,
            "report 完整 payload mutation",
            "report_entries",
            "text",
            "event_tags"
        );
        _test.Eq(
            fixture.State.ReportEntriesTyped[0]["text"]?.ToString() ?? "",
            "after",
            "report payload violation 抛错后不应回滚。"
        );
    }

    private void TestPromotionPayloadMutationIsDetectedWithoutRollback()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("promotion_payload"));
        fixture.State.SetPromotionQueue(
            new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = "mutation_probe",
                    ["extension_payload"] = new GDictionary
                    {
                        ["choice"] = "before",
                    },
                },
            }
        );

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => Choose(fixture),
            "promotion payload mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "promotion payload mutation 应触发 fail-fast guard。",
            "promotion_queue",
            "BattleAiService.ChooseCommandImpl"
        );
        AssertDiffContainsAll(
            exception?.Report?.Violations,
            "promotion 完整 payload mutation",
            "promotion_queue",
            "extension_payload",
            "choice"
        );
        IReadOnlyDictionary<string, object> entry =
            fixture.State.PromotionQueueTyped[0];
        var extension = entry["extension_payload"]
            as IReadOnlyDictionary<string, object>;
        _test.Eq(
            extension?["choice"]?.ToString() ?? "",
            "after",
            "promotion payload violation 抛错后不应回滚。"
        );
    }

    private void TestMissingBrainWaitPathIsAllowed()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"), includeBrain: false);
        BattleAiDecision decision = Choose(fixture);

        AssertNoGuardViolation(fixture.Context, "missing brain fallback 不应触发 mutation guard。");
        _test.True(
            decision != null && decision.action_id == new StringName("wait_missing_brain"),
            "missing brain 应正常回落到 wait。"
        );
    }

    private void TestMissingStateWaitPathIsAllowed()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"), includeBrain: true, includeState: false);
        BattleAiDecision decision = Choose(fixture);

        AssertNoGuardViolation(fixture.Context, "missing state fallback 不应触发 mutation guard。");
        _test.True(
            decision != null && decision.action_id == new StringName("wait_missing_state"),
            "missing state 应正常回落到 wait。"
        );
    }

    private static StringName MakeMutationAction(StringName kind) => kind;

    private static void ApplyMutationForTest(StringName mutationKind, BattleAiContext context)
    {
        if (context == null)
        {
            return;
        }

        switch (mutationKind.ToString())
        {
            case "active_hp":
                context.unit_state.current_hp = 1;
                break;
            case "other_coord":
                if (
                    context.state.ContainsUnit(new StringName("hero"))
                    && context.state.GetUnit(new StringName("hero"))
                        is BattleUnitState target
                )
                {
                    target.SetAnchorCoord(new Vector2I(4, 2));
                }
                break;
            case "blackboard":
                context.unit_state.ai_blackboard.SetText("rogue_key", "should_not_persist");
                break;
            case "cell_occupant":
                context.grid_service.SetOccupant(
                    context.state,
                    new Vector2I(3, 1),
                    context.unit_state.unit_id
                );
                break;
            case "cell_height":
                context.grid_service.SetHeightOffset(context.state, new Vector2I(0, 0), 2);
                break;
            case "effective_trait":
                if (context.unit_state.effective_trait_instances.Count > 0)
                {
                    context.unit_state.effective_trait_instances = TraitTestData.EffectiveTraits(
                        TraitTestData.EffectiveTrait(
                            "halfling_luck",
                            "halfling_luck",
                            "on_crit",
                            "per_turn",
                            "turn_start",
                            effectType: "savage_attacks",
                            sourceType: "character",
                            sourceId: "guard_actor"
                        )
                    );
                }
                break;
            case "effective_trait_ids":
                context.unit_state.effective_trait_ids.Add("rogue_trait");
                break;
            case "report_payload":
                context.state.SetReportEntries(
                    new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["entry_type"] = "mutation_probe",
                            ["text"] = "after",
                            ["event_tags"] = new List<object> { "after_tag" },
                        },
                    }
                );
                break;
            case "promotion_payload":
                context.state.SetPromotionQueue(
                    new GArray
                    {
                        new GDictionary
                        {
                            ["entry_type"] = "mutation_probe",
                            ["extension_payload"] = new GDictionary
                            {
                                ["choice"] = "after",
                            },
                        },
                    }
                );
                break;
        }
    }

    private void TestSnapshotIsPlainAndDetectsStateWithoutAuditGrowth()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        SkillDefinition snapshotSkill = TestSkillDefinitionProjection.BuildSkill(
            "snapshot_skill",
            levelDescriptionConfigs: new Dictionary<
                int,
                IReadOnlyDictionary<string, object>
            >
            {
                [1] = new Dictionary<string, object>
                {
                    ["variant_probe"] = "plain-boundary",
                },
            }
        );
        fixture.Context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [snapshotSkill.SkillId] = snapshotSkill,
            }
        );
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        fixture.Actor.encounter_actor_id = "guard_actor";
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);

        AssertSnapshotGraphHasNoGodotDynamicBoundary(snapshot);
        _test.True(
            snapshot.MatchesCurrentState(fixture.Context),
            "fresh mutation snapshot should match the captured stable fingerprint."
        );

        fixture.Actor.encounter_actor_id = "mutated_guard_actor";
        _test.True(
            !snapshot.MatchesCurrentState(fixture.Context),
            "encounter actor identity mutation should change the stable fingerprint."
        );

        fixture.Actor.current_hp = Math.Max(fixture.Actor.current_hp - 3, 0);
        _test.True(
            !snapshot.MatchesCurrentState(fixture.Context),
            "typed mutation should change the stable fingerprint."
        );
        AssertAuditBaseline(baseline, "mutation snapshot capture/detection");
    }

    private void TestDeclaredBattleUnitFieldsHaveStableCoverage()
    {
        StableMap unitStable = BattleAiMutationStableProjection.StableBattleUnitState(
            new BattleUnitState()
        );
        StableMap fieldStable = unitStable.GetMapOrEmpty("fields");
        foreach (
            FieldInfo field in typeof(BattleUnitState).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            if (field.IsStatic)
                continue;
            string fieldName = NormalizeDeclaredFieldName(field.Name);
            bool covered = fieldName switch
            {
                "attribute_snapshot" => unitStable.ContainsKey("attribute_snapshot_values"),
                "equipment_view" => unitStable.ContainsKey("equipment_view"),
                "_statusEffects" => unitStable.ContainsKey("status_effects"),
                "_consumedContingencySetups" =>
                    fieldStable.ContainsKey("consumed_contingency_setup_ids"),
                _ => fieldStable.ContainsKey(fieldName),
            };
            _test.True(
                covered,
                $"BattleUnitState 新增权威字段必须进入 mutation snapshot：{fieldName}"
            );
        }
    }

    private void TestBattleStatePublicFieldsHaveSnapshotSentinelCoverage()
    {
        foreach (
            FieldInfo field in typeof(BattleState).GetFields(
                BindingFlags.Instance | BindingFlags.Public
            )
        )
        {
            if (
                field.IsStatic
                || field.Name == "timeline"
                || field.Name == "party_backpack_view"
                || field.Name == "runtime_edges_dirty"
            )
            {
                continue;
            }
            var state = new BattleState();
            BattleStateFieldsSnapshot snapshot = BattleStateFieldsSnapshot.Capture(state);
            if (!TryBuildStateFieldMutation(field.FieldType, field.Name, out object mutation))
            {
                _test.Fail(
                    $"BattleState 权威字段缺少结构 mutation fixture：{field.Name} ({field.FieldType.FullName})"
                );
                continue;
            }
            field.SetValue(state, mutation);
            StableMap mutatedStable = BattleStateFieldsSnapshot.Capture(state).ToStableMap();
            List<StableDiff> mutationDiffs = new();
            BattleAiMutationGuard.CollectDiffs(
                snapshot.ToStableMap(),
                mutatedStable,
                $"battle_state.{field.Name}",
                mutationDiffs
            );
            _test.True(
                mutationDiffs.Count > 0,
                $"BattleState 字段必须由 stable projection 读取真实值：{field.Name}"
            );
        }
    }

    private void TestBattleObjectiveAuthorityIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));

        BattleAiMutationSnapshot emptyObjectiveSnapshot =
            BattleAiMutationSnapshot.Capture(fixture.Context);
        _test.True(
            fixture.State.InitializeObjective(
                BattleEliminationObjectiveDefinition.Instance
            ),
            "测试前提：elimination objective 应可初始化。"
        );
        AssertDiffContainsAll(
            emptyObjectiveSnapshot.CompareCurrentState(fixture.Context),
            "objective runtime initialization",
            "objective_runtime_state"
        );

        _test.True(
            fixture.State.InitializeObjective(
                BattleEliminationObjectiveDefinition.Instance
            ),
            "测试前提：objective 应可重新初始化。"
        );
        var baselineDecision = new BattleFinalDecision(
            BattleObjectiveMode.Elimination,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.EliminationHostilesDefeated,
            17
        );
        _test.True(
            fixture.State.TryLatchFinalDecision(baselineDecision),
            "测试前提：基线 final decision 应可锁存。"
        );
        BattleAiMutationSnapshot latchedDecisionSnapshot =
            BattleAiMutationSnapshot.Capture(fixture.Context);

        fixture.State.RestoreObjectiveState(
            new BattleEliminationObjectiveRuntimeState(),
            new BattleFinalDecision(
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EliminationAlliesDefeated,
                91
            )
        );
        AssertDiffContainsAll(
            latchedDecisionSnapshot.CompareCurrentState(fixture.Context),
            "final decision authority",
            "final_decision_outcome",
            "final_decision_end_reason",
            "final_decision_tu",
            "winner_faction_id"
        );
    }

    private void TestObjectiveRuntimeProjectionIsTotal()
    {
        foreach (
            FieldInfo field in typeof(BattleObjectiveRuntimeState).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            _test.Eq(
                NormalizeDeclaredFieldName(field.Name),
                "Mode",
                $"objective runtime base 新增字段 {field.Name} 时必须扩展 stable projection。"
            );
        }
        foreach (
            PropertyInfo property in typeof(BattleObjectiveRuntimeState).GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            _test.Eq(
                property.Name,
                "Mode",
                $"objective runtime base 新增属性 {property.Name} 时必须扩展 stable projection。"
            );
        }

        var runtimeStatesByType = new Dictionary<
            Type,
            BattleObjectiveRuntimeState
        >
        {
            [typeof(BattleEliminationObjectiveRuntimeState)] =
                new BattleEliminationObjectiveRuntimeState(),
            [typeof(BattleBossObjectiveRuntimeState)] =
                new BattleBossObjectiveRuntimeState(
                    "boss_actor",
                    "boss_unit",
                    new StringName[] { "party_unit" }
                ),
            [typeof(BattleEscapeObjectiveRuntimeState)] =
                new BattleEscapeObjectiveRuntimeState(
                    "escape_exit",
                    BattleMapEdge.Right,
                    1,
                    new StringName[] { "party_unit" },
                    new Vector2I[] { new(1, 0) }
                ),
            [typeof(BattleRescueObjectiveRuntimeState)] =
                new BattleRescueObjectiveRuntimeState(
                    "rescue_actor",
                    "rescue_unit",
                    new StringName[] { "party_unit" }
                ),
            [typeof(BattleEscortObjectiveRuntimeState)] =
                new BattleEscortObjectiveRuntimeState(
                    "escort_actor",
                    "escort_unit",
                    "escort_exit",
                    BattleMapEdge.Right,
                    1,
                    new StringName[] { "party_unit" },
                    new Vector2I[] { new(1, 0) }
                ),
            [typeof(BattleDefenseObjectiveRuntimeState)] =
                new BattleDefenseObjectiveRuntimeState(
                    "defense_actor",
                    "defense_unit",
                    new StringName[] { "party_unit" },
                    10,
                    110
                ),
            [typeof(BattleInterceptObjectiveRuntimeState)] =
                new BattleInterceptObjectiveRuntimeState(
                    "intercept_actor",
                    "intercept_unit",
                    "intercept_exit",
                    BattleMapEdge.Left,
                    1,
                    new StringName[] { "party_unit" },
                    new Vector2I[] { new(0, 0) }
                ),
            [typeof(BattleNodeOperationObjectiveRuntimeState)] =
                new BattleNodeOperationObjectiveRuntimeState(
                    new StringName[] { "party_unit" },
                    new[]
                    {
                        new BattleOperationNodeRuntimeState(
                            "operation_node",
                            "Operation Node",
                            "operation_zone",
                            BattleMapEdge.Right,
                            1,
                            new Vector2I(1, 0)
                        ),
                    }
                ),
            [typeof(BattleControlObjectiveRuntimeState)] =
                new BattleControlObjectiveRuntimeState(
                    new StringName[] { "party_unit" },
                    new[]
                    {
                        new BattleControlZoneRuntimeState(
                            "control_zone",
                            "Control Zone",
                            BattleMapEdge.Left,
                            1,
                            new Vector2I[] { new(0, 0) }
                        ),
                    },
                    100,
                    15,
                    20
                ),
        };
        foreach (Type type in typeof(BattleObjectiveRuntimeState).Assembly.GetTypes())
        {
            if (
                type.IsAbstract
                || !typeof(BattleObjectiveRuntimeState).IsAssignableFrom(type)
            )
            {
                continue;
            }

            _test.True(
                runtimeStatesByType.ContainsKey(type),
                $"新增 objective runtime subtype {type.FullName} 时必须显式扩展 mutation projection。"
            );
        }
        foreach (BattleObjectiveRuntimeState runtimeState in runtimeStatesByType.Values)
            BattleAiMutationStableProjection.StableObjectiveRuntimeState(runtimeState);

        AssertDeclaredInstanceFields(typeof(BattleEliminationObjectiveRuntimeState));
        AssertDeclaredInstanceFields(
            typeof(BattleBossObjectiveRuntimeState),
            "TargetActorId",
            "TargetUnitId",
            "RequiredPartyUnitIds"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleEscapeObjectiveRuntimeState),
            "_exitCoordSet",
            "ExitZoneId",
            "ExitEdge",
            "ExitDepth",
            "RequiredUnitIds",
            "ExitCoords"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleRescueObjectiveRuntimeState),
            "TargetActorId",
            "TargetUnitId",
            "RequiredPartyUnitIds",
            "TargetSecured"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleEscortObjectiveRuntimeState),
            "_exitCoordSet",
            "TargetActorId",
            "TargetUnitId",
            "ExitZoneId",
            "ExitEdge",
            "ExitDepth",
            "RequiredPartyUnitIds",
            "ExitCoords"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleDefenseObjectiveRuntimeState),
            "TargetActorId",
            "TargetUnitId",
            "RequiredPartyUnitIds",
            "StartTu",
            "DeadlineTu"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleInterceptObjectiveRuntimeState),
            "_exitCoordSet",
            "TargetActorId",
            "TargetUnitId",
            "ExitZoneId",
            "ExitEdge",
            "ExitDepth",
            "RequiredPartyUnitIds",
            "ExitCoords"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleNodeOperationObjectiveRuntimeState),
            "_nodesById",
            "_nodesByCoord",
            "RequiredPartyUnitIds",
            "OperationNodes"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleOperationNodeRuntimeState),
            "NodeId",
            "DisplayName",
            "ZoneId",
            "PlacementEdge",
            "PlacementDepth",
            "Coord",
            "IsCompleted"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleControlObjectiveRuntimeState),
            "RequiredPartyUnitIds",
            "ControlZones",
            "ScoreTarget",
            "PlayerScore",
            "HostileScore"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleControlZoneRuntimeState),
            "_coordSet",
            "ZoneId",
            "DisplayName",
            "PlacementEdge",
            "PlacementDepth",
            "Coords"
        );

        var finalDecisionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "ObjectiveMode",
            "Outcome",
            "EndReason",
            "DecisionTu",
        };
        foreach (
            FieldInfo field in typeof(BattleFinalDecision).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            string fieldName = NormalizeDeclaredFieldName(field.Name);
            _test.True(
                finalDecisionFields.Remove(fieldName),
                $"final decision 新增字段 {field.Name} 时必须扩展 mutation projection。"
            );
        }
        _test.Eq(
            finalDecisionFields.Count,
            0,
            "final decision 结构门禁应覆盖四个 canonical 字段。"
        );
        var finalDecisionProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "ObjectiveMode",
            "Outcome",
            "EndReason",
            "DecisionTu",
            "WinnerFactionId",
        };
        foreach (
            PropertyInfo property in typeof(BattleFinalDecision).GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            _test.True(
                finalDecisionProperties.Remove(property.Name),
                $"final decision 新增属性 {property.Name} 时必须扩展 mutation projection。"
            );
        }
        _test.Eq(
            finalDecisionProperties.Count,
            0,
            "final decision 结构门禁应覆盖 canonical 属性与派生 winner。"
        );
    }

    private void TestNestedAuthoritySchemasRemainExplicit()
    {
        AssertDeclaredWritableProperties(
            typeof(BattleTimelineState),
            "current_tu",
            "tu_per_tick",
            "frozen",
            "ready_unit_ids"
        );
        AssertDeclaredInstanceFields(
            typeof(WarehouseState),
            "stacks",
            "equipment_instances"
        );
        AssertDeclaredInstanceFields(
            typeof(WarehouseStackState),
            "item_id",
            "quantity"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleEffectiveTraitInstanceState),
            "trait_id",
            "effective_instance_key",
            "source_type",
            "source_id",
            "effect_type",
            "trigger_type",
            "charge_scope",
            "charge_reset_timing",
            "rank",
            "stacks",
            "roll_values"
        );
        AssertDeclaredInstanceFields(
            typeof(TraitRollValueState),
            "key",
            "value_type",
            "int_value",
            "string_name_value",
            "bool_value"
        );
        AssertDeclaredInstanceFields(
            typeof(TraitInstanceState),
            "trait_instance_id",
            "trait_id",
            "source_type",
            "source_id",
            "rank",
            "stacks",
            "roll_values"
        );
        AssertDeclaredInstanceFields(
            typeof(EquipmentEntryState),
            "item_id",
            "occupied_slot_ids",
            "instance_id",
            "equipment_instance"
        );
        AssertDeclaredInstanceFields(
            typeof(EquipmentInstanceState),
            "instance_id",
            "item_id",
            "rarity",
            "current_durability",
            "trait_instances",
            "ability_usage_periods",
            "ability_persistent_counters"
        );
        AssertDeclaredInstanceFields(
            typeof(EquipmentState),
            "_equipped_slots",
            "_slot_to_entry_slot"
        );
        AssertDeclaredInstanceFields(
            typeof(BattlePendingCastState),
            "_targetUnitIds",
            "_targetCoords",
            "SourceUnitId",
            "SkillId",
            "VariantId",
            "TargetMode",
            "BindingMode",
            "StartedCoord",
            "StartedTu",
            "BaseCastingTimeTu",
            "RemainingCastProgress",
            "LastMaintenanceCheckpointHp",
            "CastSequence",
            "CostTransaction",
            "SpellControlMetadata"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleConsumedContingencySetupCollection),
            "_setupIds"
        );
        AssertDeclaredWritableProperties(
            typeof(BattlePendingCastState),
            "SourceUnitId",
            "SkillId",
            "VariantId",
            "TargetMode",
            "BindingMode",
            "StartedCoord",
            "StartedTu",
            "BaseCastingTimeTu",
            "RemainingCastProgress",
            "LastMaintenanceCheckpointHp",
            "CastSequence",
            "CostTransaction",
            "SpellControlMetadata"
        );
        AssertDeclaredWritableProperties(
            typeof(SkillCostTransaction),
            "SkillId",
            "SkillLevel",
            "ApCost",
            "MpCost",
            "StaminaCost",
            "AuraCost",
            "CooldownTurns",
            "PrecastDamage"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleSpellControlMetadata),
            "AttackResolution",
            "SpellControlResolution",
            "AttackSuccess",
            "CriticalHit",
            "CriticalFail",
            "OrdinaryMiss",
            "IsDisadvantage",
            "HiddenLuckAtBirth",
            "FaithLuckBonus",
            "EffectiveLuck",
            "CritLocked",
            "CritGateDie",
            "CritGateRoll",
            "HitRoll",
            "FumbleLowEnd",
            "CritThreshold",
            "LockedSkillHitBonus",
            "EffectiveHitRoll",
            "ReverseFateDowngraded"
        );
        AssertDeclaredWritableProperties(
            typeof(EquipmentAbilityUsagePeriodState),
            "AbilityId",
            "PeriodKind",
            "PeriodIndex",
            "UsedCount"
        );
        AssertDeclaredWritableProperties(
            typeof(EquipmentAbilityPersistentCounterState),
            "CounterId",
            "Value"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleCellState),
            "coord",
            "stack_layer",
            "base_terrain",
            "base_height",
            "height_offset",
            "current_height",
            "passable",
            "move_cost",
            "occupant_unit_id",
            "prop_ids",
            "terrain_effect_ids",
            "timed_terrain_effects",
            "flow_direction",
            "edge_feature_east",
            "edge_feature_south"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleTerrainEffectState),
            "field_instance_id",
            "effect_id",
            "effect_type",
            "RuntimeEffectKind",
            "lifetime_policy",
            "move_cost_delta",
            "applied_status_id",
            "applied_status_duration_tu",
            "render_overlay_id",
            "overlay_priority",
            "display_name",
            "accuracy_modifier_spec",
            "does_not_stack_with_status_id",
            "does_not_stack_with_status_ids",
            "contact_status_id",
            "contact_status_duration_tu",
            "contact_stack_behavior",
            "contact_stack_limit",
            "contact_status_display_label",
            "contact_counts_as_debuff_override",
            "contact_counts_as_debuff",
            "contact_undispellable",
            "contact_dispellable_magic",
            "contact_dispellable_harmful_magic",
            "contact_dispellable_beneficial_magic",
            "contact_save_dc",
            "contact_save_ability",
            "contact_save_tag",
            "contact_apply_on_save_failure",
            "contact_tick_interval_tu",
            "contact_timeline_damage_dice_count",
            "contact_timeline_damage_dice_sides",
            "contact_timeline_damage_flat_bonus",
            "contact_blocked_by_trait_id",
            "source_unit_id",
            "source_skill_id",
            "target_team_filter",
            "power",
            "damage_tag",
            "remaining_tu",
            "tick_interval_tu",
            "next_tick_at_tu",
            "stack_behavior",
            "params"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleAttackRollModifierSpec),
            "source_domain",
            "source_id",
            "source_instance_id",
            "label",
            "modifier_delta",
            "stack_key",
            "stack_mode",
            "roll_kind_filter",
            "endpoint_mode",
            "distance_min_exclusive",
            "distance_max_inclusive",
            "target_team_filter",
            "footprint_mode",
            "applies_to",
            "StackModeKind",
            "EndpointModeKind",
            "FootprintModeKind",
            "AppliesToKind"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleEdgeFeatureState),
            "feature_kind",
            "render_kind",
            "render_layers",
            "blocks_move",
            "blocks_occupancy",
            "blocks_los",
            "interaction_kind",
            "state_tag",
            "FeatureKind",
            "RenderKind",
            "InteractionKind"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleBarrierInstanceState),
            "BarrierInstanceId",
            "ProfileId",
            "DisplayName",
            "SourceUnitId",
            "SourceSkillId",
            "AnchorMode",
            "AnchorCoord",
            "RadiusCells",
            "AreaPattern",
            "RemainingTu",
            "CreatedTu",
            "SaveDc",
            "CatchAllProjectedEffects"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleBarrierLayerState),
            "LayerId",
            "DisplayName",
            "Order",
            "Broken",
            "HasSaveRollOverride",
            "SaveRollOverride"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleBarrierOutcomeState),
            "OutcomeType",
            "OutcomeKind",
            "Amount",
            "DamageTag",
            "HalfOnSuccess",
            "SuccessAmount",
            "SuccessDamageTag",
            "FatalDamage",
            "StatusId",
            "SaveAbility",
            "SaveTag",
            "SaveDc"
        );
        AssertDeclaredInstanceFields(
            typeof(BattleAiBlackboard),
            "last_brain_id",
            "last_state_id",
            "last_action_id",
            "last_reason_text",
            "last_transition_previous_state_id",
            "last_transition_state_id",
            "last_transition_rule_id",
            "last_transition_reason",
            "turn_started_tu",
            "turn_decision_count",
            "madness_ai_control",
            "madness_target_any_team",
            "low_luck_reverse_fate_used",
            "low_luck_black_star_wedge_used",
            "meteor_protected_ally",
            "protected_ally",
            "summoned",
            "temporary_unit",
            "summon_source_unit_id",
            "summon_source_equipment_instance_id",
            "summon_binding_id",
            "summon_state_key",
            "summon_expires_at_tu",
            "_hasTurnStartedTu",
            "_hasTurnDecisionCount"
        );
        AssertDeclaredInstanceFields(typeof(AttributeSnapshot), "_values");
        AssertDeclaredWritableProperties(
            typeof(BattleEquipmentTargetMarkState),
            "SourceUnitId",
            "TargetUnitId",
            "SourceEquipmentInstanceId",
            "BindingId",
            "StateKey",
            "Stacks",
            "RemainingDurationTu",
            "RemoveOnSourceMissing"
        );
        AssertDeclaredWritableProperties(
            typeof(BattleTemporaryEdgeFeatureState),
            "OriginCoord",
            "Direction",
            "SourceUnitId",
            "SourceEquipmentInstanceId",
            "BindingId",
            "ActionId",
            "CreatedAtTu",
            "ExpiresAtTu",
            "Sequence",
            "Feature"
        );
    }

    private void TestDeclaredStatusPropertiesHaveSnapshotCoverage()
    {
        BattleStatusEffectState baseline = BuildStatusSchemaSentinel("baseline");
        StableMap baselineStable = BattleAiMutationStableProjection.StableStatusEffect(
            baseline
        );
        BattleStatusEffectState duplicate = baseline.DuplicateState();
        List<StableDiff> duplicateDiffs = new();
        BattleAiMutationGuard.CollectDiffs(
            baselineStable,
            BattleAiMutationStableProjection.StableStatusEffect(duplicate),
            "status_duplicate",
            duplicateDiffs
        );
        _test.True(
            duplicateDiffs.Count == 0,
            "BattleStatusEffectState.DuplicateState 必须覆盖全部 stable 属性："
                + string.Join(
                    " | ",
                    BattleAiMutationGuard.FormatStableDiffs(duplicateDiffs)
                )
        );

        foreach (
            PropertyInfo property in typeof(BattleStatusEffectState).GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            )
        )
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;
            BattleStatusEffectState mutated = baseline.DuplicateState();
            if (
                !TryBuildStatusPropertyValue(
                    property.PropertyType,
                    property.Name,
                    "mutated",
                    out object mutation
                )
            )
            {
                _test.Fail(
                    $"BattleStatusEffectState 属性缺少结构 mutation fixture：{property.Name} ({property.PropertyType.FullName})"
                );
                continue;
            }
            property.SetValue(mutated, mutation);
            List<StableDiff> propertyDiffs = new();
            BattleAiMutationGuard.CollectDiffs(
                baselineStable,
                BattleAiMutationStableProjection.StableStatusEffect(mutated),
                $"status.{property.Name}",
                propertyDiffs
            );
            _test.True(
                propertyDiffs.Count > 0,
                $"BattleStatusEffectState 属性必须进入 stable projection：{property.Name}"
            );
        }

        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        fixture.Actor.SetStatusEffect(baseline.DuplicateState());
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);
        BattleStatusEffectState live = fixture.Actor.GetStatusEffect(baseline.status_id);
        foreach (
            PropertyInfo property in typeof(BattleStatusEffectState).GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            )
        )
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;
            if (
                TryBuildStatusPropertyValue(
                    property.PropertyType,
                    property.Name,
                    "mutated",
                    out object mutation
                )
            )
            {
                property.SetValue(live, mutation);
            }
        }
        live.SetParamsTyped(
            new Dictionary<string, object> { ["residual_schema_probe"] = "mutated" }
        );
        _test.True(
            snapshot.CompareCurrentState(fixture.Context).Count > 0,
            "全属性 status mutation 应触发完整 snapshot diff。"
        );
    }

    private void TestUnitAuthorityBlindSpotsAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleUnitState actor = fixture.Actor;
        actor.battle_sprite_texture_path = "res://tests/original_guard_actor.png";
        actor.equipment_view_initialized = false;
        actor.ReplaceConsumedContingencySetupIdsTyped(
            new StringName[] { "contingency_alpha", "contingency_beta" }
        );
        actor.equipment_ability_sources = new List<BattleEquipmentAbilitySourceState>
        {
            new()
            {
                EffectiveInstanceKey = "source_original",
                EquipmentDefId = "equipment_original",
                SourceEquipmentInstanceId = "instance_original",
                SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                AbilityIds = new List<StringName> { "ability_alpha", "ability_beta" },
            },
        };
        actor.temporal_progress_modifiers = new List<BattleTemporalProgressModifierState>
        {
            new()
            {
                ModifierId = "modifier_original",
                BindingId = "binding_original",
                SourceEquipmentInstanceId = "instance_original",
                AppliesToActionProgress = true,
                AppliesToCastProgress = false,
                SaveDc = 12,
                AttributeModifierId = "wisdom",
                SuccessRatePercent = 75,
                FailureRatePercent = 25,
                Label = "original modifier",
            },
        };
        actor.creature_type_tags = new StringNameList(
            new StringName[] { "humanoid", "guard" }
        );
        actor.weapon_range_type = "melee";

        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);

        actor.battle_sprite_texture_path = "res://tests/rogue_guard_actor.png";
        actor.equipment_view_initialized = true;
        actor.ReplaceConsumedContingencySetupIdsTyped(
            new StringName[] { "contingency_rogue", "contingency_extra" }
        );
        actor.equipment_ability_sources = new List<BattleEquipmentAbilitySourceState>
        {
            new()
            {
                EffectiveInstanceKey = "source_rogue",
                EquipmentDefId = "equipment_rogue",
                SourceEquipmentInstanceId = "instance_rogue",
                SourceKind = EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment,
                AbilityIds = new List<StringName> { "ability_rogue", "ability_extra" },
            },
        };
        actor.temporal_progress_modifiers = new List<BattleTemporalProgressModifierState>
        {
            new()
            {
                ModifierId = "modifier_rogue",
                BindingId = "binding_rogue",
                SourceEquipmentInstanceId = "instance_rogue",
                AppliesToActionProgress = false,
                AppliesToCastProgress = true,
                SaveDc = 19,
                AttributeModifierId = "intelligence",
                SuccessRatePercent = 40,
                FailureRatePercent = 60,
                Label = "rogue modifier",
            },
        };
        actor.creature_type_tags = new StringNameList(
            new StringName[] { "construct", "rogue" }
        );
        actor.weapon_range_type = "ranged";

        List<string> diffs = snapshot.CompareCurrentState(fixture.Context);
        AssertDiffContainsAll(
            diffs,
            "unit authority blind spots",
            "battle_sprite_texture_path",
            "equipment_view_initialized",
            "consumed_contingency_setup_ids",
            "equipment_ability_sources",
            "effective_instance_key",
            "equipment_def_id",
            "source_equipment_instance_id",
            "source_kind",
            "ability_ids",
            "temporal_progress_modifiers",
            "modifier_id",
            "binding_id",
            "applies_to_action_progress",
            "applies_to_cast_progress",
            "save_dc",
            "attribute_modifier_id",
            "success_rate_percent",
            "failure_rate_percent",
            "label",
            "creature_type_tags",
            "weapon_range_type"
        );
    }

    private void TestNullableUnitAuthorityFieldsAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleUnitState actor = fixture.Actor;
        actor.battle_sprite_texture_path = "";
        actor.equipment_ability_sources = new List<BattleEquipmentAbilitySourceState>
        {
            new()
            {
                EffectiveInstanceKey = "nullable_source",
                EquipmentDefId = "nullable_equipment",
                SourceEquipmentInstanceId = "nullable_instance",
                SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                AbilityIds = new List<StringName>(),
            },
        };
        actor.temporal_progress_modifiers = new List<BattleTemporalProgressModifierState>
        {
            BuildNullableModifier(label: ""),
        };
        actor.creature_type_tags = new StringNameList();

        BattleAiMutationSnapshot nestedSnapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );
        actor.equipment_ability_sources[0].AbilityIds = null;
        actor.temporal_progress_modifiers[0] = BuildNullableModifier(label: null);
        AssertDiffContainsAll(
            nestedSnapshot.CompareCurrentState(fixture.Context),
            "nullable nested unit authority fields",
            "ability_ids",
            "label"
        );

        BattleAiMutationSnapshot elementSnapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );
        actor.equipment_ability_sources[0] = null;
        actor.temporal_progress_modifiers[0] = null;
        AssertDiffContainsAll(
            elementSnapshot.CompareCurrentState(fixture.Context),
            "nullable unit authority elements",
            "equipment_ability_sources",
            "temporal_progress_modifiers"
        );

        BattleAiMutationSnapshot outerSnapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );
        actor.battle_sprite_texture_path = null;
        actor.equipment_ability_sources = null;
        actor.temporal_progress_modifiers = null;
        actor.creature_type_tags = null;
        AssertDiffContainsAll(
            outerSnapshot.CompareCurrentState(fixture.Context),
            "nullable unit authority collections",
            "battle_sprite_texture_path",
            "equipment_ability_sources",
            "temporal_progress_modifiers",
            "creature_type_tags"
        );
    }

    private void TestNullableBattleStateContainersAreDetected()
    {
        AssertBattleStateNullMutationDetected(
            "timeline",
            "timeline",
            state => state.timeline = new BattleTimelineState(),
            state => state.timeline = null,
            state => state.timeline != null
        );
        AssertBattleStateNullMutationDetected(
            "party backpack view",
            "party_backpack_view",
            state => state.party_backpack_view = new WarehouseState(),
            state => state.party_backpack_view = null,
            state => state.party_backpack_view != null
        );
        AssertBattleStateNullMutationDetected(
            "attack disadvantage tags",
            "attack_disadvantage_tags",
            state => state.attack_disadvantage_tags = new StringNameList(),
            state => state.attack_disadvantage_tags = null,
            state => state.attack_disadvantage_tags?.Count == 0
        );
        AssertBattleStateNullMutationDetected(
            "ally unit ids",
            "ally_unit_ids",
            state => state.ally_unit_ids = new StringNameList(),
            state => state.ally_unit_ids = null,
            state => state.ally_unit_ids?.Count == 0
        );
        AssertBattleStateNullMutationDetected(
            "enemy unit ids",
            "enemy_unit_ids",
            state => state.enemy_unit_ids = new StringNameList(),
            state => state.enemy_unit_ids = null,
            state => state.enemy_unit_ids?.Count == 0
        );
        AssertBattleStateNullMutationDetected(
            "log entries",
            "log_entries",
            state => state.log_entries = new StringList(),
            state => state.log_entries = null,
            state => state.log_entries?.Count == 0
        );

        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        fixture.State.log_entries = new StringList { null };
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);
        try
        {
            fixture.State.log_entries = new StringList { "" };
            AssertDiffContainsAll(
                snapshot.CompareCurrentState(fixture.Context),
                "log null element versus empty text",
                "log_entries",
                "[0]"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"log null element detection 不应抛异常：{exception}");
        }

        using Fixture timelineFixture = BuildFixture(MakeMutationAction("none"));
        timelineFixture.State.timeline.ready_unit_ids = new StringNameList();
        BattleAiMutationSnapshot timelineSnapshot = BattleAiMutationSnapshot.Capture(
            timelineFixture.Context
        );
        timelineFixture.State.timeline.ready_unit_ids = null;
        AssertDiffContainsAll(
            timelineSnapshot.CompareCurrentState(timelineFixture.Context),
            "timeline ready ids empty-to-null",
            "timeline",
            "ready_unit_ids"
        );
    }

    private void TestNullableBattleUnitCoreFieldsAreDetected()
    {
        AssertBattleUnitNullMutationDetected(
            "display name",
            "display_name",
            unit => unit.display_name = "",
            unit => unit.display_name = null,
            unit => unit.display_name == ""
        );
        AssertBattleUnitNullMutationDetected(
            "AI blackboard",
            "ai_blackboard",
            unit => unit.ai_blackboard = new BattleAiBlackboard(),
            unit => unit.ai_blackboard = null,
            unit => unit.ai_blackboard != null
        );
        AssertBattleUnitNullMutationDetected(
            "occupied coords",
            "occupied_coords",
            unit => unit.occupied_coords = new Vector2IList(),
            unit => unit.occupied_coords = null,
            unit => unit.occupied_coords?.Count == 0
        );
        AssertBattleUnitNullMutationDetected(
            "unlocked combat resource ids",
            "unlocked_combat_resource_ids",
            unit => unit.unlocked_combat_resource_ids = new StringNameList(),
            unit => unit.unlocked_combat_resource_ids = null,
            unit => unit.unlocked_combat_resource_ids?.Count == 0
        );
        AssertBattleUnitNullMutationDetected(
            "known active skill ids",
            "known_active_skill_ids",
            unit => unit.known_active_skill_ids = new StringNameList(),
            unit => unit.known_active_skill_ids = null,
            unit => unit.known_active_skill_ids?.Count == 0
        );
        AssertBattleUnitNullMutationDetected(
            "known skill level map",
            "known_skill_level_map",
            unit => unit.known_skill_level_map = new BattleStringNameIntMap(),
            unit => unit.known_skill_level_map = null,
            unit => unit.known_skill_level_map?.Count == 0
        );
        AssertBattleUnitNullMutationDetected(
            "known skill lock hit bonus map",
            "known_skill_lock_hit_bonus_map",
            unit => unit.known_skill_lock_hit_bonus_map = new BattleStringNameIntMap(),
            unit => unit.known_skill_lock_hit_bonus_map = null,
            unit => unit.known_skill_lock_hit_bonus_map?.Count == 0
        );

        AssertBattleUnitStringNameListNullMutationDetected(
            "movement tags",
            "movement_tags",
            unit => unit.movement_tags = new StringNameList(),
            unit => unit.movement_tags = null,
            unit => unit.movement_tags
        );
        AssertBattleUnitStringNameListNullMutationDetected(
            "vision tags",
            "vision_tags",
            unit => unit.vision_tags = new StringNameList(),
            unit => unit.vision_tags = null,
            unit => unit.vision_tags
        );
        AssertBattleUnitStringNameListNullMutationDetected(
            "proficiency tags",
            "proficiency_tags",
            unit => unit.proficiency_tags = new StringNameList(),
            unit => unit.proficiency_tags = null,
            unit => unit.proficiency_tags
        );
        AssertBattleUnitStringNameListNullMutationDetected(
            "save advantage tags",
            "save_advantage_tags",
            unit => unit.save_advantage_tags = new StringNameList(),
            unit => unit.save_advantage_tags = null,
            unit => unit.save_advantage_tags
        );
        AssertBattleUnitStringNameListNullMutationDetected(
            "save disadvantage tags",
            "save_disadvantage_tags",
            unit => unit.save_disadvantage_tags = new StringNameList(),
            unit => unit.save_disadvantage_tags = null,
            unit => unit.save_disadvantage_tags
        );
        AssertBattleUnitStringNameListNullMutationDetected(
            "save immunity tags",
            "save_immunity_tags",
            unit => unit.save_immunity_tags = new StringNameList(),
            unit => unit.save_immunity_tags = null,
            unit => unit.save_immunity_tags
        );
        AssertBattleUnitStringNameListNullMutationDetected(
            "effective trait ids",
            "effective_trait_ids",
            unit => unit.effective_trait_ids = new StringNameList(),
            unit => unit.effective_trait_ids = null,
            unit => unit.effective_trait_ids
        );

        AssertBattleUnitNullMutationDetected(
            "damage resistances",
            "damage_resistances",
            unit => unit.damage_resistances = new BattleStringNameMap(),
            unit => unit.damage_resistances = null,
            unit => unit.damage_resistances?.Count == 0
        );
        AssertBattleUnitIntMapNullMutationDetected(
            "save bonus by ability",
            "save_bonus_by_ability",
            unit => unit.save_bonus_by_ability = new BattleStringNameIntMap(),
            unit => unit.save_bonus_by_ability = null,
            unit => unit.save_bonus_by_ability
        );
        AssertBattleUnitIntMapNullMutationDetected(
            "cooldowns",
            "cooldowns",
            unit => unit.cooldowns = new BattleStringNameIntMap(),
            unit => unit.cooldowns = null,
            unit => unit.cooldowns
        );
        AssertBattleUnitIntMapNullMutationDetected(
            "per battle charges",
            "per_battle_charges",
            unit => unit.per_battle_charges = new BattleStringNameIntMap(),
            unit => unit.per_battle_charges = null,
            unit => unit.per_battle_charges
        );
        AssertBattleUnitIntMapNullMutationDetected(
            "per turn charges",
            "per_turn_charges",
            unit => unit.per_turn_charges = new BattleStringNameIntMap(),
            unit => unit.per_turn_charges = null,
            unit => unit.per_turn_charges
        );
        AssertBattleUnitIntMapNullMutationDetected(
            "per turn charge limits",
            "per_turn_charge_limits",
            unit => unit.per_turn_charge_limits = new BattleStringNameIntMap(),
            unit => unit.per_turn_charge_limits = null,
            unit => unit.per_turn_charge_limits
        );
        AssertBattleUnitIntMapNullMutationDetected(
            "fumble protection used",
            "fumble_protection_used",
            unit => unit.fumble_protection_used = new BattleStringNameIntMap(),
            unit => unit.fumble_protection_used = null,
            unit => unit.fumble_protection_used
        );

        AssertBattleUnitNullMutationDetected(
            "attribute snapshot",
            "attribute_snapshot_values",
            unit => unit.attribute_snapshot = new AttributeSnapshot(),
            unit => unit.attribute_snapshot = null,
            unit => unit.attribute_snapshot != null
        );
        AssertBattleUnitNullMutationDetected(
            "equipment view",
            "equipment_view",
            unit => unit.equipment_view = new EquipmentState(),
            unit => unit.equipment_view = null,
            unit => unit.equipment_view != null
        );
        AssertBattleUnitNullMutationDetected(
            "one-handed weapon dice",
            "weapon_one_handed_dice",
            unit => unit.weapon_one_handed_dice = new WeaponDice(),
            unit => unit.weapon_one_handed_dice = null,
            unit => unit.weapon_one_handed_dice?.IsEmpty() == true
        );
        AssertBattleUnitNullMutationDetected(
            "two-handed weapon dice",
            "weapon_two_handed_dice",
            unit => unit.weapon_two_handed_dice = new WeaponDice(),
            unit => unit.weapon_two_handed_dice = null,
            unit => unit.weapon_two_handed_dice?.IsEmpty() == true
        );
        AssertBattleUnitNullMutationDetected(
            "pending cast",
            "pending_cast",
            unit => unit.pending_cast = new BattlePendingCastState(),
            unit => unit.pending_cast = null,
            unit => unit.pending_cast != null
        );
    }

    private void TestNestedAuthorityStructuresAreDetected()
    {
        TestPartyBackpackExactStructureIsDetected();
        TestEffectiveTraitExactStructureIsDetected();
        TestPendingCastNestedNullsAreDetected();
        TestEquipmentNestedStructureIsDetected();
    }

    private void TestPartyBackpackExactStructureIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        fixture.State.party_backpack_view = new WarehouseState
        {
            stacks = new List<WarehouseStackState>(),
            equipment_instances = new List<EquipmentInstanceState>(),
        };
        BattleAiMutationSnapshot nullSnapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );
        fixture.State.party_backpack_view.stacks = null;
        fixture.State.party_backpack_view.equipment_instances = null;
        AssertDiffContainsAll(
            nullSnapshot.CompareCurrentState(fixture.Context),
            "party backpack nested empty-to-null",
            "party_backpack_view",
            "stacks",
            "equipment_instances"
        );

        EquipmentInstanceState rawInstance = new()
        {
            instance_id = "",
            item_id = "",
            rarity = -1,
            current_durability = -2,
            trait_instances = null,
            ability_usage_periods = null,
            ability_persistent_counters = null,
        };
        fixture.State.party_backpack_view.stacks =
            new List<WarehouseStackState>
            {
                null,
                new() { item_id = "raw_stack", quantity = -3 },
            };
        fixture.State.party_backpack_view.equipment_instances =
            new List<EquipmentInstanceState> { null, rawInstance };
        BattleAiMutationSnapshot rawSnapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        fixture.State.party_backpack_view.stacks = new List<WarehouseStackState>();
        fixture.State.party_backpack_view.equipment_instances =
            new List<EquipmentInstanceState>();
        AssertDiffContainsAll(
            rawSnapshot.CompareCurrentState(fixture.Context),
            "party backpack raw nested structure",
            "party_backpack_view",
            "stacks",
            "equipment_instances"
        );
    }

    private void TestEffectiveTraitExactStructureIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        var roll = new TraitRollValueState
        {
            key = "raw_roll",
            value_type = "raw_type",
            int_value = -7,
            string_name_value = "raw_value",
            bool_value = true,
        };
        fixture.Actor.effective_trait_instances =
            new List<BattleEffectiveTraitInstanceState>
            {
                null,
                new()
                {
                    trait_id = "raw_trait",
                    effective_instance_key = "raw_instance",
                    source_type = "raw_source",
                    source_id = "raw_source_id",
                    effect_type = "raw_effect",
                    trigger_type = "raw_trigger",
                    charge_scope = "raw_scope",
                    charge_reset_timing = "raw_reset",
                    rank = 0,
                    stacks = -2,
                    roll_values = new List<TraitRollValueState>
                    {
                        null,
                        roll,
                        roll.DuplicateState(),
                    },
                },
            };
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        fixture.Actor.effective_trait_instances = MakeEffectiveTraitPayload();
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "effective trait exact nested structure",
            "effective_trait_instances"
        );
    }

    private void TestPendingCastNestedNullsAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattlePendingCastState pending = new()
        {
            SourceUnitId = fixture.Actor.unit_id,
            SkillId = "raw_pending_skill",
            VariantId = "raw_pending_variant",
            TargetMode = (BattleTargetMode)998,
            BindingMode = (PendingCastBindingModeKind)998,
            RemainingCastProgress = 13,
            CostTransaction = null,
            SpellControlMetadata = null,
        };
        pending.SetTargetUnitIds(new[] { fixture.Hero.unit_id });
        pending.SetTargetCoords(new[] { new Vector2I(1, 0), new Vector2I(1, 0) });
        fixture.Actor.pending_cast = pending;
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        fixture.Actor.pending_cast.CostTransaction = new SkillCostTransaction();
        fixture.Actor.pending_cast.SpellControlMetadata =
            new BattleSpellControlMetadata();
        fixture.Actor.pending_cast.TargetMode = (BattleTargetMode)999;
        fixture.Actor.pending_cast.BindingMode = (PendingCastBindingModeKind)999;
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "pending cast nested null structure",
            "pending_cast",
            "cost_transaction",
            "spell_control_metadata",
            "target_mode",
            "binding_mode"
        );
    }

    private void TestEquipmentNestedStructureIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        EquipmentState equipment = new();
        _test.True(
            equipment.SetEquippedEntry(
                "main_hand",
                "raw_guard_weapon",
                new[] { new StringName("main_hand") },
                EquipmentInstanceState.CreateInstance(
                    "raw_guard_weapon",
                    "eq_raw_guard_weapon"
                )
            ),
            "测试前提：装备应可放入 main_hand。"
        );
        EquipmentEntryState entry = equipment.GetEntry("main_hand");
        EquipmentInstanceState instance = entry?.equipment_instance;
        _test.True(entry != null && instance != null, "测试前提：装备 entry 应存在。");
        if (entry == null || instance == null)
            return;

        entry.item_id = "";
        entry.instance_id = null;
        entry.occupied_slot_ids = null;
        instance.trait_instances =
            new List<TraitInstanceState>
            {
                null,
                new()
                {
                    trait_instance_id = "raw_equipment_trait_instance",
                    trait_id = "raw_equipment_trait",
                    source_type = "raw_source",
                    source_id = "raw_source_id",
                    rank = 0,
                    stacks = -3,
                    roll_values = new List<TraitRollValueState> { null },
                },
            };
        instance.ability_usage_periods =
            new List<EquipmentAbilityUsagePeriodState>
            {
                null,
                new()
                {
                    AbilityId = null,
                    PeriodKind = null,
                    PeriodIndex = -4,
                    UsedCount = -5,
                },
            };
        instance.ability_persistent_counters =
            new List<EquipmentAbilityPersistentCounterState>
            {
                null,
                new() { CounterId = null, Value = -6 },
            };
        fixture.Actor.equipment_view = equipment;
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        entry.item_id = "normalized_weapon";
        entry.instance_id = "normalized_instance";
        entry.occupied_slot_ids = new List<StringName>();
        instance.trait_instances = new List<TraitInstanceState>();
        instance.ability_usage_periods = new List<EquipmentAbilityUsagePeriodState>();
        instance.ability_persistent_counters =
            new List<EquipmentAbilityPersistentCounterState>();
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "equipment exact nested structure",
            "equipment_view",
            "item_id",
            "instance_id",
            "occupied_slot_ids",
            "trait_instances",
            "ability_usage_periods",
            "ability_persistent_counters"
        );
    }

    private void TestCanonicalContainerKeysAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleStatusEffectState firstStatus = BuildStatusSchemaSentinel("canonical_first");
        firstStatus.status_id = "canonical_status_first";
        BattleStatusEffectState secondStatus = BuildStatusSchemaSentinel(
            "canonical_second"
        );
        secondStatus.status_id = "canonical_status_second";
        BattleStatusEffectState emptyKeyStatus = BuildStatusSchemaSentinel(
            "empty_canonical_key"
        );
        emptyKeyStatus.status_id = "embedded_empty_key_status";
        fixture.Actor.ReplaceStatusEffectsForMutationSnapshotExact(
            new[]
            {
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    "canonical_status_first",
                    firstStatus
                ),
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    "canonical_status_second",
                    secondStatus
                ),
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    "raw_null_status",
                    null
                ),
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    "",
                    emptyKeyStatus
                ),
            }
        );

        StringName actorId = fixture.Actor.unit_id;
        StringName heroId = fixture.Hero.unit_id;
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        fixture.Actor.unit_id = heroId;
        fixture.Hero.unit_id = actorId;
        firstStatus.status_id = "canonical_status_second";
        secondStatus.status_id = "canonical_status_first";
        fixture.Actor.ReplaceStatusEffectsForMutationSnapshotExact(
            new[]
            {
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    "canonical_status_first",
                    firstStatus
                ),
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    "canonical_status_second",
                    secondStatus
                ),
            }
        );
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "canonical container keys versus embedded ids",
            "units",
            "unit_id",
            "status_effects",
            "status_id",
            "raw_null_status"
        );
    }

    private void TestCellAndTerrainRawAuthorityIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        Vector2I canonicalCoord = Vector2I.Zero;
        BattleCellState cell = fixture.State.GetCell(canonicalCoord);
        _test.True(cell != null, "测试前提：原点 cell 应存在。");
        if (cell == null)
            return;

        var modifier = new BattleAttackRollModifierSpec
        {
            source_domain = "terrain",
            source_id = "raw_accuracy",
            source_instance_id = "",
            label = "",
            modifier_delta = -2,
            stack_key = "raw_stack",
            stack_mode = "add",
            roll_kind_filter = "attack",
            endpoint_mode = "either",
            distance_min_exclusive = -1,
            distance_max_inclusive = 0,
            target_team_filter = "any",
            footprint_mode = "any_cell",
            applies_to = "attack_roll",
        };
        var terrain = new BattleTerrainEffectState
        {
            field_instance_id = "raw_field",
            effect_id = "raw_effect",
            effect_type = "damage",
            lifetime_policy = "timed",
            move_cost_delta = -3,
            applied_status_id = "raw_status",
            applied_status_duration_tu = -4,
            render_overlay_id = "raw_overlay",
            overlay_priority = -5,
            display_name = "",
            accuracy_modifier_spec = modifier,
            does_not_stack_with_status_id = "",
            does_not_stack_with_status_ids = new List<StringName>(),
            contact_status_id = "raw_contact",
            contact_status_duration_tu = -6,
            contact_stack_behavior = "refresh",
            contact_stack_limit = -7,
            contact_status_display_label = "",
            contact_counts_as_debuff_override = false,
            contact_counts_as_debuff = false,
            contact_undispellable = false,
            contact_dispellable_magic = false,
            contact_dispellable_harmful_magic = false,
            contact_dispellable_beneficial_magic = false,
            contact_save_dc = -8,
            contact_save_ability = "",
            contact_save_tag = "",
            contact_apply_on_save_failure = false,
            contact_tick_interval_tu = -9,
            contact_timeline_damage_dice_count = -10,
            contact_timeline_damage_dice_sides = -11,
            contact_timeline_damage_flat_bonus = -12,
            contact_blocked_by_trait_id = "",
            source_unit_id = null,
            source_skill_id = "raw_skill",
            target_team_filter = "any",
            power = -13,
            damage_tag = "raw_damage",
            remaining_tu = -14,
            tick_interval_tu = -15,
            next_tick_at_tu = -16,
            stack_behavior = "refresh",
        };
        terrain.SetParamsTyped(
            new Dictionary<string, object>
            {
                ["number"] = 1,
                ["name"] = "same",
                ["color"] = new Color(0.1f, 0.2f, 0.3f, 0.4f),
            }
        );
        cell.prop_ids = new List<StringName>();
        cell.terrain_effect_ids = new List<StringName>();
        cell.timed_terrain_effects = new List<BattleTerrainEffectState>
        {
            null,
            terrain,
        };
        cell.edge_feature_east = new BattleEdgeFeatureState
        {
            feature_kind = null,
            render_kind = "raw_render",
            render_layers = -17,
            blocks_move = false,
            blocks_occupancy = true,
            blocks_los = false,
            interaction_kind = null,
            state_tag = "raw_edge_state",
        };
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        cell.SetCoord(new Vector2I(9, 9));
        cell.base_terrain = null;
        cell.occupant_unit_id = null;
        cell.prop_ids = null;
        cell.terrain_effect_ids = null;
        terrain.applied_status_id = null;
        terrain.display_name = null;
        terrain.does_not_stack_with_status_ids = null;
        terrain.contact_counts_as_debuff = true;
        terrain.contact_save_dc = 8;
        terrain.accuracy_modifier_spec.modifier_delta = 2;
        cell.edge_feature_east.feature_kind = "wall";
        cell.edge_feature_east.render_layers = 17;
        terrain.SetParamsTyped(
            new Dictionary<string, object>
            {
                ["number"] = 1L,
                ["name"] = new StringName("same"),
                ["color"] = new Color(0.1f, 0.2f, 0.3f, 0.5f),
            }
        );
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "cell and terrain raw authority",
            "coord",
            "base_terrain",
            "occupant_unit_id",
            "prop_ids",
            "terrain_effect_ids",
            "applied_status_id",
            "display_name",
            "does_not_stack_with_status_ids",
            "contact_counts_as_debuff",
            "contact_save_dc",
            "modifier_delta",
            "edge_feature_east",
            "feature_kind",
            "render_layers",
            "number",
            "name",
            "color"
        );
    }

    private void TestBarrierRawAuthorityIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        var outcome = new BattleBarrierOutcomeState
        {
            OutcomeType = "damage",
            Amount = -2,
            DamageTag = null,
            HalfOnSuccess = false,
            SuccessAmount = -3,
            SuccessDamageTag = "",
            FatalDamage = 0,
            StatusId = "",
            SaveAbility = "",
            SaveTag = "",
            SaveDc = -4,
        };
        var layer = new BattleBarrierLayerState
        {
            LayerId = "",
            DisplayName = null,
            Order = -5,
            Broken = false,
            HasSaveRollOverride = false,
            SaveRollOverride = 17,
        };
        layer.SetBlockedCategoriesForMutationSnapshotExact(
            new StringName[] { null, "" }
        );
        layer.SetBreakerSkillIdsForMutationSnapshotExact(
            new StringName[] { "", null }
        );
        layer.SetPassageOutcomesForMutationSnapshotExact(
            new BattleBarrierOutcomeState[] { null, outcome }
        );
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = null,
            ProfileId = "raw_profile",
            DisplayName = null,
            SourceUnitId = null,
            SourceSkillId = "",
            AnchorMode = (BarrierAnchorMode)998,
            AnchorCoord = new Vector2I(-1, -2),
            RadiusCells = -6,
            AreaPattern = null,
            RemainingTu = -7,
            CreatedTu = -8,
            SaveDc = -9,
            CatchAllProjectedEffects = false,
        };
        barrier.SetLayersForMutationSnapshotExact(
            new BattleBarrierLayerState[] { null, layer }
        );
        var barrierKey = new StringName("canonical_barrier_key");
        fixture.State.ReplaceLayeredBarrierFieldsForMutationSnapshotExact(
            new[]
            {
                new KeyValuePair<StringName, BattleBarrierInstanceState>(
                    barrierKey,
                    barrier
                ),
            }
        );
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        BattleBarrierInstanceState mutated =
            barrier.DuplicateForMutationSnapshotExact();
        mutated.BarrierInstanceId = barrierKey;
        mutated.DisplayName = "";
        mutated.AnchorMode = (BarrierAnchorMode)999;
        BattleBarrierLayerState mutatedLayer = mutated.GetLayersTyped()[1];
        mutatedLayer.LayerId = "normalized_layer";
        mutatedLayer.HasSaveRollOverride = true;
        mutatedLayer.SaveRollOverride = 23;
        BattleBarrierOutcomeState mutatedOutcome =
            mutatedLayer.GetPassageOutcomesTyped()[1];
        mutatedOutcome.FatalDamage = -1;
        fixture.State.ReplaceLayeredBarrierFieldsForMutationSnapshotExact(
            new[]
            {
                new KeyValuePair<StringName, BattleBarrierInstanceState>(
                    barrierKey,
                    mutated
                ),
            }
        );
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "barrier raw authority",
            "barrier_instance_id",
            "display_name",
            "anchor_mode",
            "layer_id",
            "has_save_roll_override",
            "save_roll_override",
            "fatal_damage"
        );
    }

    private void TestBlackboardRawPresenceIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        fixture.Actor.ai_blackboard = new BattleAiBlackboard
        {
            last_brain_id = null,
            turn_started_tu = 37,
            turn_decision_count = 41,
            summon_expires_at_tu = -9,
        };
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        fixture.Actor.ai_blackboard.last_brain_id = "";
        fixture.Actor.ai_blackboard.SetInt("turn_started_tu", 38);
        fixture.Actor.ai_blackboard.SetInt("turn_decision_count", 42);
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "blackboard raw value and presence",
            "last_brain_id",
            "turn_started_tu",
            "turn_decision_count",
            "has_turn_started_tu",
            "has_turn_decision_count"
        );
    }

    private void TestPlainPayloadTypeIdentityIsDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleStatusEffectState status = BuildStatusSchemaSentinel("typed_payload");
        status.status_id = "typed_payload_status";
        status.SetParamsTyped(
            new Dictionary<string, object>
            {
                ["number"] = 1,
                ["name"] = "same",
                ["color"] = new Color(0.25f, 0.5f, 0.75f, 1.0f),
            }
        );
        fixture.Actor.SetStatusEffect(status);
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        status.SetParamsTyped(
            new Dictionary<string, object>
            {
                ["number"] = 1L,
                ["name"] = new StringName("same"),
                ["color"] = new Color(0.25f, 0.5f, 0.75f, 0.5f),
            }
        );
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "plain payload type identity",
            "number",
            "name",
            "color"
        );
    }

    private void TestRawUnitProjectionMutationIsDetectedWithoutNormalization()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        fixture.Actor.RestoreCombatResourceProjectionForMutationSnapshotExact(
            -1,
            -2,
            -3,
            -4,
            -5,
            -6,
            true
        );
        fixture.Actor.RestoreBodyShapeProjectionForMutationSnapshotExact(
            new Vector2I(-7, -8),
            "raw_body_category",
            -9,
            new Vector2I(-10, -11),
            null
        );
        var attributes = new AttributeSnapshot();
        attributes.ReplaceValuesForMutationSnapshotExact(
            new Dictionary<StringName, int>
            {
                ["strength"] = 10,
                ["strength_modifier"] = 99,
            }
        );
        fixture.Actor.attribute_snapshot = attributes;
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );

        fixture.Actor.SetCombatResources(10, 11, 12, 13, 14, 15);
        fixture.Actor.RestoreBodyShapeProjectionForMutationSnapshotExact(
            Vector2I.Zero,
            "medium",
            BattleUnitState.BodySizeMedium,
            Vector2I.One,
            new[] { Vector2I.Zero }
        );
        fixture.Actor.attribute_snapshot.SetValue("strength", 20);
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "raw unit projection",
            "current_hp",
            "current_mp",
            "body_size_category",
            "body_size",
            "footprint_size",
            "occupied_coords",
            "strength_modifier"
        );
    }

    private void TestSkillDefinitionGraphAndIndexMutationsAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        var masteryCurve = new List<int> { 10, 20 };
        var tags = new List<StringName> { "frozen_tag" };
        var skillRequirements = new Dictionary<StringName, int>
        {
            ["required_skill"] = 2,
        };
        var aiTags = new List<StringName> { "frozen_ai_tag" };
        var effectTags = new List<StringName> { "frozen_effect_tag" };
        var accuracy = new BattleAttackRollModifierSpec
        {
            source_domain = "skill_definition",
            source_id = "frozen_accuracy",
            modifier_delta = 7,
        };
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            accuracyModifierSpec: accuracy,
            effectTags: effectTags
        );
        var effects = new List<CombatEffectDefinition> { effect };
        CombatSkillDefinition combat = TestSkillDefinitionProjection.BuildCombatProfile(
            "frozen_skill",
            effects: effects,
            aiTags: aiTags
        );
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "frozen_skill",
            combatProfile: combat,
            maxLevel: 1,
            tags: tags,
            masteryCurve: masteryCurve,
            skillLevelRequirements: skillRequirements
        );

        masteryCurve[0] = 999;
        tags[0] = "mutated_tag";
        skillRequirements["required_skill"] = 999;
        aiTags[0] = "mutated_ai_tag";
        effectTags[0] = "mutated_effect_tag";
        effects.Clear();
        accuracy.modifier_delta = 999;
        BattleAttackRollModifierSpec exposedAccuracy = effect.AccuracyModifierSpec;
        exposedAccuracy.modifier_delta = 888;
        _test.True(
            skill.MasteryCurve.Count == 2
                && skill.MasteryCurve[0] == 10
                && skill.Tags[0] == new StringName("frozen_tag")
                && skill.SkillLevelRequirements["required_skill"] == 2
                && combat.AiTags[0] == new StringName("frozen_ai_tag")
                && combat.EffectDefinitions.Count == 1
                && effect.EffectTags[0] == new StringName("frozen_effect_tag")
                && effect.AccuracyModifierSpec.modifier_delta == 7,
            "skill definition graph 必须防御性冻结构造输入，并以副本暴露可变 spec。"
        );

        fixture.Context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skill.SkillId] = skill,
            }
        );
        IReadOnlyDictionary<StringName, SkillDefinition> readOnlyIndex =
            fixture.Context.GetSkillDefinitionIndexTyped();
        _test.True(
            readOnlyIndex is not Dictionary<StringName, SkillDefinition>,
            "AI context 不得把真实 mutable skill dictionary 暴露为 IReadOnly 接口。"
        );
        bool writeRejected = false;
        try
        {
            ((IDictionary<StringName, SkillDefinition>)readOnlyIndex).Add(
                "illegal_write",
                skill
            );
        }
        catch (NotSupportedException)
        {
            writeRejected = true;
        }
        _test.True(writeRejected, "skill definition read-only view 必须拒绝写入。");

        var barrierProfile = new BarrierProfileDefinition(
            "frozen_barrier_profile",
            "Frozen barrier",
            "fixed",
            "diamond",
            2,
            120,
            false,
            Array.Empty<BarrierLayerDefinition>()
        );
        var barrierSource =
            new Dictionary<StringName, BarrierProfileDefinition>
            {
                [barrierProfile.ProfileId] = barrierProfile,
            };
        fixture.Context.SetBarrierProfileDefinitions(barrierSource);
        barrierSource.Clear();
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> readOnlyBarriers =
            fixture.Context.GetBarrierProfileDefinitionIndexTyped();
        _test.True(
            readOnlyBarriers.Count == 1
                && readOnlyBarriers is not Dictionary<
                    StringName,
                    BarrierProfileDefinition
                >,
            "AI context 必须复制 barrier profile index 并只暴露 read-only view。"
        );
        bool barrierWriteRejected = false;
        try
        {
            ((IDictionary<StringName, BarrierProfileDefinition>)readOnlyBarriers).Clear();
        }
        catch (NotSupportedException)
        {
            barrierWriteRejected = true;
        }
        _test.True(barrierWriteRejected, "barrier profile read-only view 必须拒绝写入。");

        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(
            fixture.Context
        );
        SkillDefinition replacement = TestSkillDefinitionProjection.BuildSkill(
            "frozen_skill",
            maxLevel: 9
        );
        SkillDefinition added = TestSkillDefinitionProjection.BuildSkill(
            "added_skill"
        );
        fixture.Context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [replacement.SkillId] = replacement,
                [added.SkillId] = added,
            }
        );
        var replacementBarrier = new BarrierProfileDefinition(
            "frozen_barrier_profile",
            "Replacement barrier",
            "fixed",
            "diamond",
            9,
            120,
            false,
            Array.Empty<BarrierLayerDefinition>()
        );
        var addedBarrier = new BarrierProfileDefinition(
            "added_barrier_profile",
            "Added barrier",
            "fixed",
            "diamond",
            1,
            60,
            false,
            Array.Empty<BarrierLayerDefinition>()
        );
        fixture.Context.SetBarrierProfileDefinitions(
            new Dictionary<StringName, BarrierProfileDefinition>
            {
                [replacementBarrier.ProfileId] = replacementBarrier,
                [addedBarrier.ProfileId] = addedBarrier,
            }
        );
        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "immutable definition indexes identity",
            "skill_definitions",
            "frozen_skill",
            "added_skill",
            "barrier_profile_definitions",
            "frozen_barrier_profile",
            "added_barrier_profile"
        );
    }

    private void TestNullableStatusFieldsAreDetected()
    {
        AssertStatusNullMutationDetected(
            "display label",
            "display_label",
            status => status.display_label = "",
            status => status.display_label = null,
            status => status.display_label == ""
        );
        AssertStatusNullMutationDetected(
            "damage tags",
            "damage_tags",
            status => status.damage_tags = new List<StringName>(),
            status => status.damage_tags = null,
            status => status.damage_tags?.Count == 0
        );
        AssertStatusNullMutationDetected(
            "save advantage tags",
            "save_advantage_tags",
            status => status.save_advantage_tags = new List<StringName>(),
            status => status.save_advantage_tags = null,
            status => status.save_advantage_tags?.Count == 0
        );
        AssertStatusNullMutationDetected(
            "save disadvantage tags",
            "save_disadvantage_tags",
            status => status.save_disadvantage_tags = new List<StringName>(),
            status => status.save_disadvantage_tags = null,
            status => status.save_disadvantage_tags?.Count == 0
        );
        AssertStatusNullMutationDetected(
            "save immunity tags",
            "save_immunity_tags",
            status => status.save_immunity_tags = new List<StringName>(),
            status => status.save_immunity_tags = null,
            status => status.save_immunity_tags?.Count == 0
        );
        AssertStatusNullMutationDetected(
            "status tags",
            "status_tags",
            status => status.status_tags = new List<StringName>(),
            status => status.status_tags = null,
            status => status.status_tags?.Count == 0
        );
        AssertStatusNullMutationDetected(
            "save bonus by tag",
            "save_bonus_by_tag",
            status => status.save_bonus_by_tag = new Dictionary<StringName, int>(),
            status => status.save_bonus_by_tag = null,
            status => status.save_bonus_by_tag?.Count == 0
        );

        AssertStatusStructuralMutationDetected(
            "damage tags empty key",
            "damage_tags",
            status => status.damage_tags = new List<StringName>(),
            status => status.damage_tags = new List<StringName> { new StringName("") },
            status => status.damage_tags?.Count == 0
        );
        AssertStatusStructuralMutationDetected(
            "save bonus empty key",
            "save_bonus_by_tag",
            status => status.save_bonus_by_tag = new Dictionary<StringName, int>(),
            status =>
                status.save_bonus_by_tag = new Dictionary<StringName, int>
                {
                    [new StringName("")] = 7,
                },
            status => status.save_bonus_by_tag?.Count == 0
        );

        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleStatusEffectState baseline = new() { status_id = "nullable_status_params" };
        baseline.SetParamsTyped(
            new Dictionary<string, object>
            {
                ["nullable_sequence"] = new List<object> { null },
            }
        );
        fixture.Actor.SetStatusEffect(baseline);
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);
        try
        {
            fixture.Actor.GetStatusEffect(baseline.status_id).SetParamsTyped(
                new Dictionary<string, object>
                {
                    ["nullable_sequence"] = new List<object> { "" },
                }
            );
            AssertDiffContainsAll(
                snapshot.CompareCurrentState(fixture.Context),
                "status params null element versus empty text",
                "params",
                "nullable_sequence",
                "[0]"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"status params null element detection 不应抛异常：{exception}");
        }

        using Fixture unitRefFixture = BuildFixture(MakeMutationAction("none"));
        BattleStatusEffectState unitRefBaseline = new()
        {
            status_id = "unit_ref_status_param",
        };
        unitRefBaseline.SetParamsTyped(
            new Dictionary<string, object> { ["unit_ref"] = 17 }
        );
        unitRefFixture.Actor.SetStatusEffect(unitRefBaseline);
        BattleAiMutationSnapshot unitRefSnapshot = BattleAiMutationSnapshot.Capture(
            unitRefFixture.Context
        );
        BattleStatusEffectState unitRefLive = unitRefFixture.Actor.GetStatusEffect(
            unitRefBaseline.status_id
        );
        unitRefLive.SetParamsTyped(
            new Dictionary<string, object> { ["unit_ref"] = 23 }
        );
        AssertDiffContainsAll(
            unitRefSnapshot.CompareCurrentState(unitRefFixture.Context),
            "status unit_ref param",
            "unit_ref"
        );
    }

    private void TestStatusSemanticBlindSpotsAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        fixture.Actor.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "guard_semantics",
                source_unit_id = fixture.Hero.unit_id,
                stacks = 1,
                duration = 3,
                forced_move_immune = true,
                counts_as_debuff_override = true,
                counts_as_debuff = false,
                lock_counterattack = true,
                lock_guard = false,
                lock_dodge_bonus = true,
                lock_crit = false,
                main_skill_lock_other_debuff_count = 2,
            }
        );
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);

        BattleStatusEffectState mutated = fixture.Actor.GetStatusEffect("guard_semantics");
        mutated.forced_move_immune = false;
        mutated.counts_as_debuff_override = false;
        mutated.counts_as_debuff = true;
        mutated.lock_counterattack = false;
        mutated.lock_guard = true;
        mutated.lock_dodge_bonus = false;
        mutated.lock_crit = true;
        mutated.main_skill_lock_other_debuff_count = 9;

        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "status semantic blind spots",
            "forced_move_immune",
            "counts_as_debuff_override",
            "counts_as_debuff",
            "lock_counterattack",
            "lock_guard",
            "lock_dodge_bonus",
            "lock_crit",
            "main_skill_lock_other_debuff_count"
        );
    }

    private void TestBattleStateAuthorityBlindSpotsAreDetected()
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleEquipmentTargetMarkState originalMark = new()
        {
            SourceUnitId = fixture.Actor.unit_id,
            TargetUnitId = fixture.Hero.unit_id,
            SourceEquipmentInstanceId = "mark_instance_original",
            BindingId = "mark_binding_original",
            StateKey = "mark_state_original",
            Stacks = 2,
            RemainingDurationTu = 30,
            RemoveOnSourceMissing = true,
        };
        _test.True(
            fixture.State.SetEquipmentTargetMark(
                originalMark,
                uniquePerSource: false,
                out _
            ),
            "测试前提：原始 equipment target mark 应可写入。"
        );
        _test.True(
            fixture.State.PutTemporaryEdgeFeature(
                BuildTemporaryEdgeFeature("edge_baseline", 0, 100),
                refreshExisting: false,
                maxActiveEdges: 0
            ),
            "测试前提：基线 temporary edge feature 应可写入。"
        );
        var invalidMark = new BattleEquipmentTargetMarkState
        {
            SourceUnitId = null,
            TargetUnitId = "",
            SourceEquipmentInstanceId = null,
            BindingId = "",
            StateKey = null,
            Stacks = -11,
            RemainingDurationTu = -12,
            RemoveOnSourceMissing = false,
        };
        fixture.State.ReplaceEquipmentTargetMarksForMutationSnapshotExact(
            new BattleEquipmentTargetMarkState[] { null, invalidMark, originalMark }
        );
        BattleTemporaryEdgeFeatureState validBaselineEdge = fixture.State
            .CaptureTemporaryEdgeFeaturesForMutationSnapshotExact()[0];
        var invalidEdge = new BattleTemporaryEdgeFeatureState
        {
            OriginCoord = new Vector2I(-1, -2),
            Direction = Vector2I.Zero,
            SourceUnitId = null,
            SourceEquipmentInstanceId = null,
            BindingId = "",
            ActionId = null,
            CreatedAtTu = -13,
            ExpiresAtTu = -14,
            Sequence = -15,
            Feature = null,
        };
        fixture.State.ReplaceTemporaryEdgeFeaturesForMutationSnapshotExact(
            new BattleTemporaryEdgeFeatureState[]
            {
                null,
                invalidEdge,
                validBaselineEdge,
            }
        );
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);

        BattleEquipmentTargetMarkState rogueMark = new()
        {
            SourceUnitId = fixture.Hero.unit_id,
            TargetUnitId = fixture.Actor.unit_id,
            SourceEquipmentInstanceId = "mark_instance_rogue",
            BindingId = "mark_binding_rogue",
            StateKey = "mark_state_rogue",
            Stacks = 7,
            RemainingDurationTu = 90,
            RemoveOnSourceMissing = false,
        };
        var mutatedInvalidMark = new BattleEquipmentTargetMarkState
        {
            SourceUnitId = "mutated_raw_source",
            TargetUnitId = null,
            SourceEquipmentInstanceId = "mutated_raw_instance",
            BindingId = null,
            StateKey = "mutated_raw_state",
            Stacks = -21,
            RemainingDurationTu = -22,
            RemoveOnSourceMissing = true,
        };
        fixture.State.ReplaceEquipmentTargetMarksForMutationSnapshotExact(
            new BattleEquipmentTargetMarkState[]
            {
                mutatedInvalidMark,
                mutatedInvalidMark,
                rogueMark,
            }
        );
        fixture.State.AllocateCastSequence();
        var mutatedInvalidEdge = new BattleTemporaryEdgeFeatureState
        {
            OriginCoord = new Vector2I(-21, -22),
            Direction = Vector2I.Left,
            SourceUnitId = "mutated_raw_source",
            SourceEquipmentInstanceId = "mutated_raw_instance",
            BindingId = null,
            ActionId = "mutated_raw_action",
            CreatedAtTu = -23,
            ExpiresAtTu = -24,
            Sequence = -25,
            Feature = new BattleEdgeFeatureState
            {
                feature_kind = null,
                render_layers = -26,
            },
        };
        fixture.State.ReplaceTemporaryEdgeFeaturesForMutationSnapshotExact(
            new BattleTemporaryEdgeFeatureState[]
            {
                mutatedInvalidEdge,
                mutatedInvalidEdge,
                validBaselineEdge,
            }
        );
        fixture.State.RestoreNextTemporaryEdgeFeatureSequence(3);

        AssertDiffContainsAll(
            snapshot.CompareCurrentState(fixture.Context),
            "battle state authority blind spots",
            "equipment_target_marks",
            "source_unit_id",
            "target_unit_id",
            "source_equipment_instance_id",
            "binding_id",
            "state_key",
            "stacks",
            "remaining_duration_tu",
            "remove_on_source_missing",
            "temporary_edge_features",
            "direction",
            "sequence",
            "next_cast_sequence",
            "next_temporary_edge_feature_sequence"
        );
    }

    private void TestStableDoubleComparisonPreservesDoublePrecision()
    {
        double expected = 1.0d;
        double actual = Math.BitIncrement(expected);
        _test.Eq((float)actual, (float)expected, "测试前提：两个 double 应降为同一 float。");
        StableMap expectedMap = new();
        expectedMap.Set("probe", StableValue.FromFloat(expected));
        StableMap actualMap = new();
        actualMap.Set("probe", StableValue.FromFloat(actual));
        List<StableDiff> diffs = new();
        BattleAiMutationGuard.CollectDiffs(
            expectedMap,
            actualMap,
            "double_precision",
            diffs
        );
        _test.Eq(
            diffs.Count,
            1,
            "mutation guard 不得因 float 截断或近似比较漏报 double 的位级变化。"
        );
    }

    private void AssertSnapshotGraphHasNoGodotDynamicBoundary(object root)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(root, "snapshot", visited);
    }

    private void Visit(object value, string path, HashSet<object> visited)
    {
        if (value == null)
            return;
        Type type = value.GetType();
        if (IsGodotDynamicBoundaryType(type) || value is GodotObject)
        {
            _test.Fail($"mutation snapshot must be plain/typed: {path} type={type.FullName}");
            return;
        }
        if (
            type.IsPrimitive
            || type.IsEnum
            || type.IsValueType
            || value is string
            || value is Delegate
        )
        {
            return;
        }
        if (!visited.Add(value))
            return;
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                Visit(entry.Key, $"{path}.key", visited);
                Visit(entry.Value, $"{path}[{entry.Key}]", visited);
            }
            return;
        }
        if (value is IEnumerable sequence)
        {
            int index = 0;
            foreach (object entry in sequence)
            {
                Visit(entry, $"{path}[{index}]", visited);
                index++;
            }
            return;
        }
        foreach (
            FieldInfo field
            in type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            Visit(field.GetValue(value), $"{path}.{field.Name}", visited);
        }
    }

    private void AssertDeclaredInstanceFields(Type type, params string[] expectedNames)
    {
        var remaining = new HashSet<string>(
            expectedNames ?? Array.Empty<string>(),
            StringComparer.Ordinal
        );
        foreach (
            FieldInfo field in type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            if (field.IsStatic)
                continue;
            string fieldName = NormalizeDeclaredFieldName(field.Name);
            _test.True(
                remaining.Remove(fieldName),
                $"{type.Name} 新增字段 {field.Name} 时必须扩展 exact snapshot、stable projection 与 mutation detection 回归。"
            );
        }
        _test.Eq(
            remaining.Count,
            0,
            $"{type.Name} 结构门禁缺少预期字段：{string.Join(", ", remaining)}"
        );
    }

    private void AssertDeclaredWritableProperties(
        Type type,
        params string[] expectedNames
    )
    {
        var remaining = new HashSet<string>(
            expectedNames ?? Array.Empty<string>(),
            StringComparer.Ordinal
        );
        foreach (
            PropertyInfo property in type.GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            if (property.SetMethod == null || property.GetIndexParameters().Length > 0)
                continue;
            _test.True(
                remaining.Remove(property.Name),
                $"{type.Name} 新增可写属性 {property.Name} 时必须扩展 exact snapshot、stable projection 与 mutation detection 回归。"
            );
        }
        _test.Eq(
            remaining.Count,
            0,
            $"{type.Name} 结构门禁缺少预期属性：{string.Join(", ", remaining)}"
        );
    }

    private static string NormalizeDeclaredFieldName(string fieldName)
    {
        string value = fieldName ?? "";
        if (
            value.StartsWith("<", StringComparison.Ordinal)
            && value.EndsWith(">k__BackingField", StringComparison.Ordinal)
        )
        {
            int closing = value.IndexOf('>');
            if (closing > 1)
                return value.Substring(1, closing - 1);
        }
        return value;
    }

    private static bool TryBuildStateFieldMutation(
        Type fieldType,
        string fieldName,
        out object value
    )
    {
        string marker = $"mutation_{fieldName}";
        if (fieldType == typeof(StringName))
            value = new StringName(marker);
        else if (fieldType == typeof(long))
            value = 101L;
        else if (fieldType == typeof(int))
            value = 101;
        else if (fieldType == typeof(Vector2I))
            value = new Vector2I(7, 9);
        else if (fieldType == typeof(StringNameList))
            value = new StringNameList(new StringName[] { marker });
        else if (fieldType == typeof(StringList))
            value = new StringList(new[] { marker });
        else
        {
            value = null;
            return false;
        }
        return true;
    }

    private static BattleStatusEffectState BuildStatusSchemaSentinel(string suffix)
    {
        var result = new BattleStatusEffectState();
        foreach (
            PropertyInfo property in typeof(BattleStatusEffectState).GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            )
        )
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;
            if (
                TryBuildStatusPropertyValue(
                    property.PropertyType,
                    property.Name,
                    suffix,
                    out object value
                )
            )
            {
                property.SetValue(result, value);
            }
        }
        result.SetParamsTyped(
            new Dictionary<string, object>
            {
                ["residual_schema_probe"] = $"{suffix}_residual",
            }
        );
        return result;
    }

    private static bool TryBuildStatusPropertyValue(
        Type propertyType,
        string propertyName,
        string suffix,
        out object value
    )
    {
        string marker = $"{suffix}_{propertyName}";
        bool mutated = suffix == "mutated";
        if (propertyType == typeof(StringName))
            value = new StringName(marker);
        else if (propertyType == typeof(string))
            value = marker;
        else if (propertyType == typeof(int))
            value = mutated ? 11 : 7;
        else if (propertyType == typeof(bool))
            value = !mutated;
        else if (propertyType == typeof(int?))
            value = (int?)(mutated ? 11 : 7);
        else if (propertyType == typeof(double?))
            value = (double?)(mutated ? 2.5d : 1.25d);
        else if (propertyType == typeof(List<StringName>))
            value = new List<StringName> { marker };
        else if (propertyType == typeof(Dictionary<StringName, int>))
        {
            value = new Dictionary<StringName, int>
            {
                [new StringName(marker)] = mutated ? 11 : 7,
            };
        }
        else
        {
            value = null;
            return false;
        }
        return true;
    }

    private static BattleTemporalProgressModifierState BuildNullableModifier(string label)
    {
        return new BattleTemporalProgressModifierState
        {
            ModifierId = "nullable_modifier",
            BindingId = "nullable_binding",
            SourceEquipmentInstanceId = "nullable_instance",
            AppliesToActionProgress = true,
            AppliesToCastProgress = false,
            SaveDc = 10,
            AttributeModifierId = "dexterity",
            SuccessRatePercent = 50,
            FailureRatePercent = 50,
            Label = label,
        };
    }

    private static BattleTemporaryEdgeFeatureState BuildTemporaryEdgeFeature(
        StringName actionId,
        int createdAtTu,
        int expiresAtTu
    )
    {
        return new BattleTemporaryEdgeFeatureState
        {
            OriginCoord = Vector2I.Zero,
            Direction = Vector2I.Right,
            BindingId = "mutation_guard_edge",
            ActionId = actionId,
            CreatedAtTu = createdAtTu,
            ExpiresAtTu = expiresAtTu,
            Feature = new BattleEdgeFeatureState
            {
                feature_kind = "wall",
                render_kind = "wall",
                render_layers = 1,
                blocks_move = true,
                blocks_occupancy = true,
                blocks_los = true,
                state_tag = actionId,
            },
        };
    }

    private void AssertBattleStateNullMutationDetected(
        string label,
        string expectedDiffFragment,
        Action<BattleState> prepareBaseline,
        Action<BattleState> mutateToNull,
        Func<BattleState, bool> isBaselinePrepared
    )
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        prepareBaseline(fixture.State);
        _test.True(
            isBaselinePrepared(fixture.State),
            $"BattleState {label} detection baseline 应正确建立。"
        );
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);
        try
        {
            mutateToNull(fixture.State);
            AssertDiffContainsAll(
                snapshot.CompareCurrentState(fixture.Context),
                $"BattleState {label} empty-to-null",
                expectedDiffFragment
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"BattleState {label} null detection 不应抛异常：{exception}");
        }
    }

    private void AssertBattleUnitNullMutationDetected(
        string label,
        string expectedDiffFragment,
        Action<BattleUnitState> prepareBaseline,
        Action<BattleUnitState> mutateToNull,
        Func<BattleUnitState, bool> isBaselinePrepared
    )
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        prepareBaseline(fixture.Actor);
        _test.True(
            isBaselinePrepared(fixture.Actor),
            $"BattleUnit {label} detection baseline 应正确建立。"
        );
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);
        try
        {
            mutateToNull(fixture.Actor);
            AssertDiffContainsAll(
                snapshot.CompareCurrentState(fixture.Context),
                $"BattleUnit {label} empty-to-null",
                expectedDiffFragment
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"BattleUnit {label} null detection 不应抛异常：{exception}");
        }
    }

    private void AssertBattleUnitStringNameListNullMutationDetected(
        string label,
        string expectedDiffFragment,
        Action<BattleUnitState> prepareBaseline,
        Action<BattleUnitState> mutateToNull,
        Func<BattleUnitState, StringNameList> readBaseline
    ) =>
        AssertBattleUnitNullMutationDetected(
            label,
            expectedDiffFragment,
            prepareBaseline,
            mutateToNull,
            unit => readBaseline(unit)?.Count == 0
        );

    private void AssertBattleUnitIntMapNullMutationDetected(
        string label,
        string expectedDiffFragment,
        Action<BattleUnitState> prepareBaseline,
        Action<BattleUnitState> mutateToNull,
        Func<BattleUnitState, BattleStringNameIntMap> readBaseline
    ) =>
        AssertBattleUnitNullMutationDetected(
            label,
            expectedDiffFragment,
            prepareBaseline,
            mutateToNull,
            unit => readBaseline(unit)?.Count == 0
        );

    private void AssertStatusNullMutationDetected(
        string label,
        string expectedDiffFragment,
        Action<BattleStatusEffectState> prepareBaseline,
        Action<BattleStatusEffectState> mutateToNull,
        Func<BattleStatusEffectState, bool> isBaselinePrepared
    ) =>
        AssertStatusStructuralMutationDetected(
            label,
            expectedDiffFragment,
            prepareBaseline,
            mutateToNull,
            isBaselinePrepared
        );

    private void AssertStatusStructuralMutationDetected(
        string label,
        string expectedDiffFragment,
        Action<BattleStatusEffectState> prepareBaseline,
        Action<BattleStatusEffectState> mutate,
        Func<BattleStatusEffectState, bool> isBaselinePrepared
    )
    {
        using Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleStatusEffectState baseline = new()
        {
            status_id = new StringName($"nullable_{label.Replace(' ', '_')}"),
        };
        prepareBaseline(baseline);
        _test.True(
            isBaselinePrepared(baseline),
            $"status {label} detection baseline 应正确建立。"
        );
        fixture.Actor.SetStatusEffect(baseline);
        BattleAiMutationSnapshot snapshot = BattleAiMutationSnapshot.Capture(fixture.Context);
        try
        {
            BattleStatusEffectState live = fixture.Actor.GetStatusEffect(baseline.status_id);
            mutate(live);
            AssertDiffContainsAll(
                snapshot.CompareCurrentState(fixture.Context),
                $"status {label} mutation",
                expectedDiffFragment
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"status {label} detection 不应抛异常：{exception}");
        }
    }

    private void AssertDiffContainsAll(
        IReadOnlyList<string> diffs,
        string label,
        params string[] expectedFragments
    )
    {
        string joined = string.Join(" | ", diffs ?? Array.Empty<string>());
        _test.True((diffs?.Count ?? 0) > 0, $"{label} 应产生 mutation diff。");
        foreach (string fragment in expectedFragments ?? Array.Empty<string>())
            AssertContains(joined, fragment, $"{label} 应包含 {fragment} diff。");
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

    private static BattleAiDecision Choose(Fixture fixture) =>
        fixture?.Service.ChooseCommand(fixture.Context, captureTrace: false)?.Decision;

    private Fixture BuildFixture(
        StringName mutationKind,
        bool includeBrain = true,
        bool includeState = true,
        bool enableFullSnapshotGuard = true,
        Exception evaluatorException = null
    )
    {
        BattleState state = BuildFlatState(new Vector2I(6, 4));
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit(
            "guard_actor",
            "守卫",
            "hostile",
            new Vector2I(1, 1),
            "guard_brain",
            "engage",
            20,
            2
        );
        BattleUnitState hero = BuildUnit(
            "hero",
            "玩家",
            "player",
            new Vector2I(3, 1),
            "",
            "",
            30,
            2
        );
        AddUnitToState(gridService, state, actor, isEnemy: true);
        AddUnitToState(gridService, state, hero, isEnemy: false);
        state.phase = "unit_acting";
        state.active_unit_id = actor.unit_id;

        Dictionary<StringName, EnemyAiBrainDefinition> brainMap = new();
        if (includeBrain)
        {
            var states = new List<EnemyAiStateDefinition>();
            if (includeState)
            {
                states.Add(
                    new EnemyAiStateDefinition(
                        "engage",
                        new EnemyAiActionDefinition[]
                        {
                            new WaitActionDefinition(
                                new StringName($"test_mutation_{mutationKind}"),
                                "",
                                BattleAiActionIntent.Wait,
                                0,
                                0
                            ),
                        },
                        Array.Empty<EnemyAiGenerationSlotDefinition>()
                    )
                );
            }
            var brain = new EnemyAiBrainDefinition(
                "guard_brain",
                "engage",
                BattleAiScoreProfileDefinition.Default,
                states,
                Array.Empty<EnemyAiTransitionRuleDefinition>()
            );
            brainMap[brain.BrainId] = brain;
        }

        var actionPlan = new BattleAiRuntimeActionPlan();
        if (brainMap.TryGetValue("guard_brain", out EnemyAiBrainDefinition brainDefinition))
        {
            actionPlan.SetSource(actor, brainDefinition);
            if (brainDefinition.TryGetState("engage", out EnemyAiStateDefinition stateDefinition))
            {
                actionPlan.AddStateActions(stateDefinition.StateId, stateDefinition.Actions);
            }
        }

        var service = new BattleAiService();
        if (enableFullSnapshotGuard)
        {
            service.MutationGuardMode =
                BattleAiMutationGuardMode.FullSnapshotDiagnostic;
        }
        service.Setup(brainMap, null);
        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = gridService,
            runtime_action_plan = actionPlan,
        };
        context.action_score_input_callback = (_, _, _, _, _, _, _) =>
        {
            ApplyMutationForTest(mutationKind, context);
            if (evaluatorException != null)
            {
                throw evaluatorException;
            }
            return null;
        };
        context.SetSkillDefinitions(new Dictionary<StringName, SkillDefinition>());

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Hero = hero,
            Service = service,
            Context = context,
            ActionPlan = actionPlan,
        };
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_mutation_guard_regression",
            phase = "timeline_running",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        StringName brainId,
        StringName stateId,
        int currentHp,
        int currentAp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = brainId != "" ? new StringName("ai") : new StringName("manual"),
            ai_brain_id = brainId,
            ai_state_id = stateId,
            current_hp = currentHp,
            current_mp = 20,
            current_stamina = 10,
            current_ap = currentAp,
            current_move_points = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue("hp_max", Math.Max(currentHp, 1));
        unit.attribute_snapshot.SetValue("mp_max", 20);
        unit.attribute_snapshot.SetValue("stamina_max", 10);
        unit.attribute_snapshot.SetValue("action_points", Math.Max(currentAp, 1));
        return unit;
    }

    private static System.Collections.Generic.List<BattleEffectiveTraitInstanceState> MakeEffectiveTraitPayload()
    {
        return TraitTestData.EffectiveTraits(
            TraitTestData.EffectiveTrait(
                "halfling_luck",
                "halfling_luck",
                "on_natural_one",
                "per_turn",
                "turn_start",
                effectType: "halfling_luck",
                sourceType: "character",
                sourceId: "guard_actor"
            )
        );
    }

    private void AddUnitToState(
        BattleGridService gridService,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }

        bool placed = gridService.PlaceUnit(state, unit, unit.coord, true);
        _test.True(placed, $"测试单位 {unit.unit_id} 应能放入测试战场。");
    }

    private BattleAiMutationViolationException CaptureMutationViolation(
        Action action,
        string message
    )
    {
        try
        {
            action?.Invoke();
        }
        catch (BattleAiMutationViolationException exception)
        {
            return exception;
        }

        _test.Fail(message);
        return null;
    }

    private void AssertGuardAborted(
        BattleAiContext context,
        BattleAiMutationViolationException exception,
        string message,
        string violationFragment,
        string callSiteFragment
    )
    {
        _test.True(exception != null, $"{message} guard 应抛出 fail-fast exception。");
        if (exception == null)
        {
            return;
        }
        _test.True(
            (exception.Report?.Violations.Count ?? 0) > 0,
            $"{message} exception report 应保留 mutation diff。"
        );
        _test.True(
            context != null && !context.HasRuntimeBindings,
            $"{message} exception finally 应清空 AI context borrowers。"
        );
        AssertContains(exception.Message, violationFragment, $"{message} exception 应包含 diff 字段。");
        AssertContains(exception.Message, callSiteFragment, $"{message} exception 应包含 action 调用点。");
        _test.Eq(
            BattleAiFailurePolicy.LastEvent?.Severity ?? new StringName(""),
            BattleAiFailurePolicy.SeverityMutationViolation,
            $"{message} failure policy 应记录 mutation_violation。"
        );
    }

    private void AssertNoGuardViolation(BattleAiContext context, string message)
    {
        GStringArray violations = GetGuardViolations(context);
        _test.True(violations.Count == 0, $"{message} violations={FormatStringArray(violations)}");
        _test.True(
            context != null && !context.HasRuntimeBindings,
            $"{message} decision finally 应清空 AI context borrowers。"
        );
    }

    private void AssertContains(string actual, string expectedFragment, string message)
    {
        if (actual == null || !actual.Contains(expectedFragment ?? "", StringComparison.Ordinal))
        {
            _test.Fail($"{message} expected_fragment={expectedFragment} actual={actual}");
        }
    }

    private static GStringArray GetGuardViolations(BattleAiContext context)
    {
        var result = new GStringArray();
        foreach (string value in context?.GetMutationGuardViolationsTyped() ?? Array.Empty<string>())
        {
            result.Add(value);
        }
        return result;
    }

    private static List<Vector2I> DuplicateVector2IArray(IEnumerable<Vector2I> source)
    {
        var result = new List<Vector2I>();
        foreach (Vector2I value in source ?? Array.Empty<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    private static bool IsGodotDynamicBoundaryType(Type type) =>
        type == typeof(GDictionary)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private void AssertVector2IArrayEq(
        IReadOnlyList<Vector2I> actual,
        IReadOnlyList<Vector2I> expected,
        string message
    )
    {
        if ((actual?.Count ?? 0) != (expected?.Count ?? 0))
        {
            _test.Fail(
                $"{message} expected={FormatVector2IArray(expected)} actual={FormatVector2IArray(actual)}"
            );
            return;
        }
        for (int index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _test.Fail(
                    $"{message} expected={FormatVector2IArray(expected)} actual={FormatVector2IArray(actual)}"
                );
                return;
            }
        }
    }

    private static string FormatStringArray(GStringArray values) =>
        "[" + string.Join(", ", values ?? new GStringArray()) + "]";

    private static string FormatVector2IArray(IEnumerable<Vector2I> values) =>
        "[" + string.Join(", ", values ?? Array.Empty<Vector2I>()) + "]";

    private sealed class Fixture : IDisposable
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public BattleUnitState Hero;
        public BattleAiService Service;
        public BattleAiContext Context;
        public BattleAiRuntimeActionPlan ActionPlan;

        public void Dispose()
        {
            Context?.ClearRuntimeBindings();
            ActionPlan?.Dispose();
            Service?.Dispose();
        }
    }
}
