using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_advantage_behavior_regression : LifecycleTestSceneTree
{
    private const string BrainId = "mage_controller";
    private const string StateId = "retreat";
    private const string ActionId = "mage_survival_position";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestFormalSurvivalPositionEscapesBeyondThreatMargin();
            TestFormalSurvivalPositionStopsWhenAlreadySafe();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI advantage behavior regression"));
    }

    private void TestFormalSurvivalPositionEscapesBeyondThreatMargin()
    {
        using RuntimeScope scope = RuntimeScope.Create(
            "battle_ai_advantage_survival_regression",
            new Vector2I(10, 5)
        );
        BattleUnitState actor = BuildUnit(
            "survival_position_actor",
            "求生法师",
            "hostile",
            new Vector2I(3, 2),
            controlMode: "ai",
            brainId: BrainId,
            stateId: StateId
        );
        actor.current_move_points = 4;
        BattleUnitState threat = BuildUnit(
            "survival_position_threat",
            "远程威胁",
            "player",
            new Vector2I(2, 2),
            controlMode: "manual",
            brainId: "",
            stateId: ""
        );
        AddBasicAttack(threat, attackRange: 4);

        scope.AddUnit(actor, isEnemy: true);
        scope.AddUnit(threat, isEnemy: false);
        scope.ActivateState();

        BattleAiContext context = scope.BuildAiContext(actor, traceEnabled: true);
        BattleAiRuntimeActionEntry entry = FindFormalActionEntry(
            scope,
            context,
            StateId,
            ActionId
        );
        var action = entry?.Action as MoveToAdvantagePositionActionDefinition;
        AssertFormalTypedEntry(entry, action);
        _test.Eq(
            action?.MinimumSafeDistance ?? -1,
            4,
            "formal survival action 应保留 minimum_safe_distance。"
        );
        _test.Eq(
            action?.SafeDistanceMargin ?? -1,
            1,
            "formal survival action 应保留 safe_distance_margin。"
        );
        _test.Eq(
            action?.MinSurvivalMarginGainToEscape ?? -1,
            1,
            "formal survival action 应保留 minimum survival margin gain。"
        );

        BattleAiMutationSnapshot before = BattleAiMutationSnapshot.Capture(context);
        BattleAiDecision decision = null;
        try
        {
            decision = EvaluateThroughDecisionEngine(context, entry);

            _test.True(
                decision?.command?.IsMove() == true,
                "unsafe survival position 应经 DecisionEngine 产出 plain move command。"
            );
            _test.Eq(
                decision?.action_id ?? new StringName(""),
                new StringName(ActionId),
                "DecisionEngine 应按 formal action kind 分派 mage_survival_position。"
            );
            int landingDistance = decision?.score_input?.distance_to_primary_coord ?? -1;
            _test.True(
                landingDistance >= 5,
                "threat range 4 + safety margin 1 时，survival position 应移动到至少 5 格外。"
            );
            _test.Eq(
                decision?.score_input?.position_safe_distance ?? -1,
                5,
                "survival score 应公开解析后的 threat safety margin。"
            );
            _test.True(
                decision?.score_input?.has_post_action_threat_projection == true,
                "survival evaluator 应使用正式 pre/post threat projection。"
            );
            _test.True(
                decision?.score_input != null
                    && decision.score_input.post_action_survival_margin
                        - decision.score_input.pre_action_survival_margin
                        >= action.MinSurvivalMarginGainToEscape,
                "被接受的 survival move 应达到 formal min survival margin gain。"
            );
            _test.True(
                decision?.action_trace_id != new StringName(""),
                "survival decision 应关联 plain action trace。"
            );
            AssertPlainDecisionBoundary(decision);

            AiActionTrace trace = FindTrace(context, ActionId);
            _test.True(
                trace != null && trace.CandidateCount > 0,
                "survival trace 应保留通过 min-gain gate 的候选。"
            );
            _test.Eq(
                ReadTraceInt(trace?.Metadata, "minimum_safe_distance", -1),
                4,
                "survival trace 应公开 formal minimum safe distance。"
            );
            _test.Eq(
                ReadTraceInt(trace?.Metadata, "safe_distance_margin", -1),
                1,
                "survival trace 应公开 formal safety margin。"
            );
            _test.True(
                before.MatchesCurrentState(context),
                $"survival evaluator 不应改写 battle state：{string.Join(" | ", before.CompareCurrentState(context))}"
            );
        }
        finally
        {
            decision?.ClearOwnedRuntimeReferences();
        }
    }

    private void TestFormalSurvivalPositionStopsWhenAlreadySafe()
    {
        using RuntimeScope scope = RuntimeScope.Create(
            "battle_ai_advantage_already_safe_regression",
            new Vector2I(10, 5)
        );
        BattleUnitState actor = BuildUnit(
            "already_safe_actor",
            "安全法师",
            "hostile",
            new Vector2I(7, 2),
            controlMode: "ai",
            brainId: BrainId,
            stateId: StateId
        );
        actor.current_move_points = 4;
        BattleUnitState threat = BuildUnit(
            "already_safe_threat",
            "远程威胁",
            "player",
            new Vector2I(2, 2),
            controlMode: "manual",
            brainId: "",
            stateId: ""
        );
        AddBasicAttack(threat, attackRange: 4);

        scope.AddUnit(actor, isEnemy: true);
        scope.AddUnit(threat, isEnemy: false);
        scope.ActivateState();

        BattleAiContext context = scope.BuildAiContext(actor, traceEnabled: true);
        BattleAiRuntimeActionEntry entry = FindFormalActionEntry(
            scope,
            context,
            StateId,
            ActionId
        );
        AssertFormalTypedEntry(
            entry,
            entry?.Action as MoveToAdvantagePositionActionDefinition
        );

        BattleAiMutationSnapshot before = BattleAiMutationSnapshot.Capture(context);
        BattleAiDecision decision = EvaluateThroughDecisionEngine(context, entry);
        _test.True(
            decision == null,
            "距离等于 threat range + safety margin 时，survival action 应以 already_safe 收束。"
        );
        AiActionTrace trace = FindTrace(context, ActionId);
        _test.True(trace != null, "already-safe survival path 应保留 plain trace。" );
        _test.True(
            trace?.BlockReasons.GetValueOrDefault("already_safe", 0) == 1,
            "already-safe survival trace 应记录稳定 block reason。"
        );
        _test.Eq(
            ReadTraceInt(trace?.Metadata, "safe_distance_margin", -1),
            1,
            "already-safe gate 应使用 formal safety margin。"
        );
        _test.True(
            before.MatchesCurrentState(context),
            $"already-safe evaluator 不应改写 battle state：{string.Join(" | ", before.CompareCurrentState(context))}"
        );
    }

    private void AssertFormalTypedEntry(
        BattleAiRuntimeActionEntry entry,
        MoveToAdvantagePositionActionDefinition action
    )
    {
        _test.True(
            action != null,
            "正式 mage survival action 应从 process snapshot/runtime plan 暴露 typed MoveToAdvantagePositionActionDefinition。"
        );
        _test.Eq(
            action?.PositioningMode ?? new StringName(""),
            new StringName("survival"),
            "formal advantage entry 应保持 survival positioning mode。"
        );
        _test.False(
            entry?.Action != null
                && typeof(Resource).IsAssignableFrom(entry.Action.GetType()),
            "advantage runtime entry 不应保留 authored Resource fallback。"
        );
    }

    private BattleAiRuntimeActionEntry FindFormalActionEntry(
        RuntimeScope scope,
        BattleAiContext context,
        StringName stateId,
        StringName actionId
    )
    {
        EnemyAiBrainDefinition brain = scope.GameSession
            .GetEnemyAiBrainDefinitions()
            .GetValueOrDefault(BrainId);
        EnemyAiActionDefinition snapshotAction = null;
        foreach (EnemyAiActionDefinition action in brain?.GetState(stateId)?.Actions ?? Array.Empty<EnemyAiActionDefinition>())
        {
            if (action?.ActionId == actionId)
            {
                snapshotAction = action;
                break;
            }
        }

        BattleAiRuntimeActionEntry planEntry = null;
        foreach (BattleAiRuntimeActionEntry entry in context.GetRuntimeActionEntriesTyped(stateId))
        {
            if (entry?.ActionId == actionId)
            {
                planEntry = entry;
                break;
            }
        }
        _test.True(
            snapshotAction != null,
            "process snapshot 应包含 formal mage_survival_position definition。"
        );
        _test.True(
            planEntry != null && ReferenceEquals(planEntry.Action, snapshotAction),
            "runtime plan 应直接借用 process snapshot 的 immutable survival definition。"
        );
        return planEntry;
    }

    private static BattleAiDecision EvaluateThroughDecisionEngine(
        BattleAiContext context,
        BattleAiRuntimeActionEntry entry
    )
    {
        context.PushActionMetadata(entry?.Metadata);
        try
        {
            return new BattleAiDecisionEngine().EvaluateEntry(context, entry);
        }
        finally
        {
            context.PopActionMetadata();
        }
    }

    private void AssertPlainDecisionBoundary(BattleAiDecision decision)
    {
        _test.False(
            decision != null && typeof(GodotObject).IsAssignableFrom(decision.GetType()),
            "advantage decision 应是 plain CLR value。"
        );
        _test.False(
            decision?.command != null
                && typeof(GodotObject).IsAssignableFrom(decision.command.GetType()),
            "advantage command 应是 plain CLR value。"
        );
        _test.False(
            decision?.score_input != null
                && typeof(GodotObject).IsAssignableFrom(decision.score_input.GetType()),
            "advantage score input 应是 plain CLR value。"
        );
    }

    private static AiActionTrace FindTrace(BattleAiContext context, string actionId)
    {
        IReadOnlyList<AiActionTrace> traces = context?.GetActionTracesTyped();
        if (traces == null)
            return null;
        for (int index = traces.Count - 1; index >= 0; index--)
        {
            if (traces[index]?.ActionId == actionId)
                return traces[index];
        }
        return null;
    }

    private static int ReadTraceInt(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        int fallback
    ) =>
        metadata != null && metadata.TryGetValue(key, out object value)
            ? value switch
            {
                int intValue => intValue,
                long longValue => checked((int)longValue),
                _ => fallback,
            }
            : fallback;

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        string controlMode,
        string brainId = "",
        string stateId = ""
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = controlMode,
            ai_brain_id = new StringName(brainId ?? ""),
            ai_state_id = new StringName(stateId ?? ""),
            current_hp = 30,
            current_ap = 2,
            current_move_points = 2,
            current_stamina = 30,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            unit.attribute_snapshot.SetValue(attributeId, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 8);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        return unit;
    }

    private static void AddBasicAttack(BattleUnitState unit, int attackRange)
    {
        unit.known_active_skill_ids.Add("basic_attack");
        unit.known_skill_level_map["basic_attack"] = 1;
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "advantage_threat_weapon",
                weapon_profile_type_id = "advantage_threat_weapon",
                weapon_family = "test",
                weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private sealed class RuntimeScope : IDisposable
    {
        private RuntimeScope(GameSession gameSession, BattleRuntimeModule runtime, BattleState state)
        {
            GameSession = gameSession;
            Runtime = runtime;
            State = state;
        }

        internal GameSession GameSession { get; }
        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }

        internal static RuntimeScope Create(StringName battleId, Vector2I mapSize)
        {
            GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                null,
                gameSession.GetSkillDefinitionsTyped(),
                gameSession.GetEnemyTemplateDefinitions(),
                gameSession.GetEnemyAiBrainDefinitions(),
                null
            );
            return new RuntimeScope(
                gameSession,
                runtime,
                BattleTestFixture.BuildFlatState(battleId, mapSize)
            );
        }

        internal void AddUnit(BattleUnitState unit, bool isEnemy)
        {
            State.SetUnit(unit);
            if (isEnemy)
                State.enemy_unit_ids.Add(unit.unit_id);
            else
                State.ally_unit_ids.Add(unit.unit_id);
            if (!Runtime._grid_service.PlaceUnit(State, unit, unit.coord, true))
                throw new InvalidOperationException($"Failed to place {unit.unit_id} at {unit.coord}.");
        }

        internal void ActivateState() => Runtime.SetupStateForTests(State);

        internal BattleAiContext BuildAiContext(BattleUnitState actor, bool traceEnabled)
        {
            Runtime._ensure_ai_action_plan_for_unit(actor);
            Runtime._ai_action_plans_by_unit_id.TryGetValue(
                actor.unit_id,
                out BattleAiRuntimeActionPlan actionPlan
            );
            var context = new BattleAiContext
            {
                state = State,
                unit_state = actor,
                grid_service = Runtime._grid_service,
                move_cost_callback = (unit, targetCoord) =>
                    Runtime._get_ai_move_query_cost(unit.unit_id, unit.coord, targetCoord),
                runtime_action_plan = actionPlan,
                trace_enabled = traceEnabled,
            };
            context.SetSkillDefinitions(Runtime.GetSkillDefinitionIndexTyped());
            Runtime._bind_ai_helper_services_for_decision(actor, context);
            return context;
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
            GameSession?.Dispose();
        }
    }
}
