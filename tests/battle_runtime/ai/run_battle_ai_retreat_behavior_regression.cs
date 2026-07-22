using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_retreat_behavior_regression : LifecycleTestSceneTree
{
    private const string BrainId = "mage_controller";
    private const string StateId = "retreat";
    private const string ActionId = "mage_retreat";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestFormalTypedRetreatUsesMostUnsafeDynamicThreat();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI retreat behavior regression"));
    }

    private void TestFormalTypedRetreatUsesMostUnsafeDynamicThreat()
    {
        using RuntimeScope scope = RuntimeScope.Create(
            "battle_ai_retreat_behavior_regression",
            new Vector2I(10, 7)
        );
        BattleUnitState actor = BuildUnit(
            "retreat_actor",
            "撤退法师",
            "hostile",
            new Vector2I(4, 3),
            controlMode: "ai",
            brainId: BrainId,
            stateId: StateId
        );
        actor.current_move_points = 1;
        BattleUnitState nearbyMelee = BuildUnit(
            "nearby_melee_threat",
            "近身威胁",
            "player",
            new Vector2I(4, 2),
            controlMode: "manual",
            brainId: "",
            stateId: ""
        );
        AddBasicAttack(nearbyMelee, attackRange: 1);
        BattleUnitState rangedThreat = BuildUnit(
            "ranged_unsafe_threat",
            "远程高危威胁",
            "player",
            new Vector2I(0, 3),
            controlMode: "manual",
            brainId: "",
            stateId: ""
        );
        AddBasicAttack(rangedThreat, attackRange: 8);

        scope.AddUnit(actor, isEnemy: true);
        scope.AddUnit(nearbyMelee, isEnemy: false);
        scope.AddUnit(rangedThreat, isEnemy: false);
        scope.ActivateState();

        BattleAiContext context = scope.BuildAiContext(actor, traceEnabled: true);
        BattleAiRuntimeActionEntry entry = FindFormalActionEntry(
            scope,
            context,
            StateId,
            ActionId
        );
        _test.True(
            entry?.Action is RetreatActionDefinition,
            "正式 mage retreat action 应从 process snapshot/runtime plan 暴露 typed RetreatActionDefinition。"
        );
        _test.False(
            entry?.Action != null
                && typeof(Resource).IsAssignableFrom(entry.Action.GetType()),
            "retreat runtime entry 不应保留 authored Resource fallback。"
        );

        int initialRangedDistance = scope.Runtime._grid_service.GetDistanceBetweenUnits(
            actor,
            rangedThreat
        );
        BattleAiMutationSnapshot before = BattleAiMutationSnapshot.Capture(context);
        BattleAiDecision decision = null;
        try
        {
            decision = EvaluateThroughDecisionEngine(context, entry);

            _test.True(decision?.command?.IsMove() == true, "typed retreat 应产出 plain move command。");
            _test.Eq(
                decision?.action_id ?? new StringName(""),
                new StringName(ActionId),
                "DecisionEngine 应按 formal action kind 分派 mage_retreat。"
            );
            _test.Eq(
                decision?.command?.unit_id ?? new StringName(""),
                actor.unit_id,
                "retreat command 应保留行动单位。"
            );
            int resolvedDistance = decision?.score_input?.distance_to_primary_coord ?? -1;
            _test.True(
                resolvedDistance > initialRangedDistance,
                "dynamic retreat 应相对最不安全的远程威胁增加距离。"
            );
            _test.Eq(
                decision?.score_input?.desired_min_distance ?? -1,
                9,
                "dynamic retreat safe distance 应使用 threat range 8 + margin 1，而不是只用固定最小值 4。"
            );
            _test.Eq(
                decision?.score_input?.desired_max_distance ?? -1,
                9,
                "retreat 的 typed distance band 应收束到同一 resolved safe distance。"
            );
            _test.True(
                decision?.action_trace_id != new StringName(""),
                "retreat decision 应关联 plain action trace。"
            );
            AssertPlainDecisionBoundary(decision);

            AiActionTrace trace = FindTrace(context, ActionId);
            _test.True(trace != null && trace.CandidateCount > 0, "retreat trace 应记录正式候选。" );
            _test.Eq(
                ReadTraceText(trace?.Metadata, "focus_target_unit_id"),
                rangedThreat.unit_id.ToString(),
                "dynamic retreat 应在 trace 中标识 unsafe gap 最大的远程目标。"
            );
            _test.Eq(
                ReadTraceInt(trace?.Metadata, "resolved_safe_distance", -1),
                9,
                "retreat trace 应公开动态 threat safe distance。"
            );
            _test.True(
                before.MatchesCurrentState(context),
                $"retreat evaluator 不应改写 battle state：{string.Join(" | ", before.CompareCurrentState(context))}"
            );
        }
        finally
        {
            decision?.ClearOwnedRuntimeReferences();
        }
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
        _test.True(snapshotAction != null, "process snapshot 应包含 formal mage_retreat definition。" );
        _test.True(
            planEntry != null && ReferenceEquals(planEntry.Action, snapshotAction),
            "runtime plan 应直接借用 process snapshot 的 immutable retreat definition。"
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
            "retreat decision 应是 plain CLR value。"
        );
        _test.False(
            decision?.command != null
                && typeof(GodotObject).IsAssignableFrom(decision.command.GetType()),
            "retreat command 应是 plain CLR value。"
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

    private static string ReadTraceText(
        IReadOnlyDictionary<string, object> metadata,
        string key
    ) =>
        metadata != null && metadata.TryGetValue(key, out object value)
            ? value?.ToString() ?? ""
            : "";

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
                weapon_item_id = "retreat_threat_weapon",
                weapon_profile_type_id = "retreat_threat_weapon",
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
            Runtime.TryGetAiActionPlanForUnit(
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
