using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

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

            TestTurnTraceProjectsTypedDecisionTransition();
            TestBenignAiBookkeepingIsAllowed();
            TestActiveUnitHpMutationIsBlockedAndRestored();
            TestOtherUnitCoordMutationIsBlockedAndRestored();
            TestUnknownBlackboardKeyWriteIsIgnored();
            TestCellOccupantMutationIsBlockedAndRestored();
            TestCellHeightMutationIsBlockedAndRestored();
            TestEffectiveTraitPayloadMutationIsBlockedAndRestored();
            TestEffectiveTraitIdsMutationIsBlockedAndRestored();
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
                BattleAiStateResolver.TransitionConditionTrace.FromCondition(condition),
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

        GDictionary turnTrace = TraceDictionaryProjection.ToDictionary(
            context.BuildTurnTraceTyped(decision).ToTraceDictionary()
        );
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
        Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

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

    private void TestActiveUnitHpMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("active_hp"));
        int beforeHp = fixture.Actor.current_hp;

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => fixture.Service.ChooseCommand(fixture.Context),
            "active unit HP mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "active unit HP mutation 应触发 fail-fast guard。",
            "current_hp",
            "BattleAiMutationGuardTestAction.Decide"
        );
        _test.Eq(fixture.Actor.current_hp, beforeHp, "active unit HP mutation 应被恢复。");
    }

    private void TestOtherUnitCoordMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("other_coord"));
        Vector2I beforeCoord = fixture.Hero.coord;
        GVector2IArray beforeOccupied = DuplicateVector2IArray(fixture.Hero.occupied_coords);

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => fixture.Service.ChooseCommand(fixture.Context),
            "其他单位坐标 mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "其他单位坐标 mutation 应触发 fail-fast guard。",
            "coord",
            "BattleAiMutationGuardTestAction.Decide"
        );
        _test.Eq(fixture.Hero.coord, beforeCoord, "其他单位坐标 mutation 应被恢复。");
        AssertVector2IArrayEq(
            fixture.Hero.occupied_coords,
            beforeOccupied,
            "其他单位 footprint cache mutation 应被恢复。"
        );
    }

    private void TestUnknownBlackboardKeyWriteIsIgnored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("blackboard"));

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

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

    private void TestCellOccupantMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("cell_occupant"));
        BattleCellState cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(3, 1));
        StringName beforeOccupant = cell?.occupant_unit_id ?? "";

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => fixture.Service.ChooseCommand(fixture.Context),
            "cell occupant mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "cell occupant mutation 应触发 fail-fast guard。",
            "occupant_unit_id",
            "BattleAiMutationGuardTestAction.Decide"
        );
        cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(3, 1));
        _test.Eq(cell?.occupant_unit_id ?? "", beforeOccupant, "cell occupant mutation 应被恢复。");
    }

    private void TestCellHeightMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("cell_height"));
        BattleCellState cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(0, 0));
        int beforeHeight = cell?.current_height ?? int.MinValue;
        int beforeOffset = cell?.height_offset ?? int.MinValue;

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => fixture.Service.ChooseCommand(fixture.Context),
            "cell height mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "cell height mutation 应触发 fail-fast guard。",
            "height_offset",
            "BattleAiMutationGuardTestAction.Decide"
        );
        cell = fixture.GridService.GetCellState(fixture.State, new Vector2I(0, 0));
        _test.Eq(cell?.current_height ?? int.MinValue, beforeHeight, "cell current_height mutation 应被恢复。");
        _test.Eq(cell?.height_offset ?? int.MinValue, beforeOffset, "cell height_offset mutation 应被恢复。");
    }

    private void TestEffectiveTraitPayloadMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("effective_trait"));
        fixture.Actor.effective_trait_instances = MakeEffectiveTraitPayload();
        fixture.Actor.effective_trait_ids = new GStringNameArray { "halfling_luck" };

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => fixture.Service.ChooseCommand(fixture.Context),
            "effective trait payload mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "effective trait payload mutation 应触发 fail-fast guard。",
            "effective_trait_instances",
            "BattleAiMutationGuardTestAction.Decide"
        );

        _test.Eq(
            fixture.Actor.effective_trait_instances.Count,
            1,
            "effective trait payload mutation 应恢复 payload 数量。"
        );
        BattleEffectiveTraitInstanceState restored = fixture.Actor.effective_trait_instances.Count > 0
            ? fixture.Actor.effective_trait_instances[0]
            : null;
        _test.Eq(
            restored?.effect_type ?? "",
            new StringName("halfling_luck"),
            "effective trait payload mutation 应恢复 effect_type。"
        );
        _test.True(
            fixture.Actor.effective_trait_ids.Count == 1
                && fixture.Actor.effective_trait_ids.Contains("halfling_luck")
                && !fixture.Actor.effective_trait_ids.Contains("rogue_trait"),
            "effective_trait_ids mutation 应被恢复。"
        );
    }

    private void TestEffectiveTraitIdsMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("effective_trait_ids"));
        fixture.Actor.effective_trait_instances = MakeEffectiveTraitPayload();
        fixture.Actor.effective_trait_ids = new GStringNameArray { "halfling_luck" };

        BattleAiMutationViolationException exception = CaptureMutationViolation(
            () => fixture.Service.ChooseCommand(fixture.Context),
            "effective trait id mutation 应让 mutation guard 立即停止。"
        );

        AssertGuardAborted(
            fixture.Context,
            exception,
            "effective trait id mutation 应触发 fail-fast guard。",
            "effective_trait_ids",
            "BattleAiMutationGuardTestAction.Decide"
        );
        _test.True(
            fixture.Actor.effective_trait_ids.Count == 1
                && fixture.Actor.effective_trait_ids.Contains("halfling_luck")
                && !fixture.Actor.effective_trait_ids.Contains("rogue_trait"),
            "effective_trait_ids mutation 应被恢复。"
        );
    }

    private void TestMissingBrainWaitPathIsAllowed()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("none"), includeBrain: false);
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertNoGuardViolation(fixture.Context, "missing brain fallback 不应触发 mutation guard。");
        _test.True(
            decision != null && decision.action_id == new StringName("wait_missing_brain"),
            "missing brain 应正常回落到 wait。"
        );
    }

    private void TestMissingStateWaitPathIsAllowed()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("none"), includeBrain: true, includeState: false);
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertNoGuardViolation(fixture.Context, "missing state fallback 不应触发 mutation guard。");
        _test.True(
            decision != null && decision.action_id == new StringName("wait_missing_state"),
            "missing state 应正常回落到 wait。"
        );
    }

    private static BattleAiMutationGuardTestAction MakeMutationAction(StringName kind)
    {
        var action = new BattleAiMutationGuardTestAction();
        action.setup(kind);
        return action;
    }

    private Fixture BuildFixture(
        EnemyAiAction action,
        bool includeBrain = true,
        bool includeState = true
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

        Dictionary<StringName, EnemyAiBrainDef> brainMap = new();
        if (includeBrain)
        {
            var brain = TestResourceOwnership.Own(
                new EnemyAiBrainDef
                {
                    brain_id = "guard_brain",
                    default_state_id = "engage",
                    states = new Godot.Collections.Array<EnemyAiStateDef>(),
                },
                "BattleAiMutationGuard.BuildFixture.brain"
            );
            if (includeState)
            {
                brain.states.Add(
                    new EnemyAiStateDef
                    {
                        state_id = "engage",
                        actions = new Godot.Collections.Array<EnemyAiAction> { action },
                    }
                );
            }
            brainMap[brain.brain_id] = TestResourceOwnership.Own(
                brain,
                "BattleAiMutationGuard.BuildFixture.brain"
            );
        }

        var service = new BattleAiService();
        service.Setup(brainMap, null);
        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = gridService,
            allow_authored_action_fallback_for_tests = true,
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
        GStringArray violations = GetGuardViolations(context);
        _test.True(violations.Count > 0, $"{message} violations={FormatStringArray(violations)}");
        _test.True(exception != null, $"{message} guard 应抛出 fail-fast exception。");
        if (exception == null)
        {
            return;
        }
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

    private static GVector2IArray DuplicateVector2IArray(GVector2IArray source)
    {
        var result = new GVector2IArray();
        foreach (Vector2I value in source ?? new GVector2IArray())
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
        GVector2IArray actual,
        GVector2IArray expected,
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

    private static string FormatVector2IArray(GVector2IArray values) =>
        "[" + string.Join(", ", values ?? new GVector2IArray()) + "]";

    private sealed class Fixture
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public BattleUnitState Hero;
        public BattleAiService Service;
        public BattleAiContext Context;
    }
}
