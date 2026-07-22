using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_ground_reposition_behavior_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestBlinkRepositionChoosesSaferGroundCoord();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI ground reposition behavior regression"));
    }

    private void TestBlinkRepositionChoosesSaferGroundCoord()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(9, 5));
        BattleUnitState mage = BuildUnit(
            "blink_reposition_mage",
            "闪现法师",
            "hostile",
            new Vector2I(2, 2),
            controlMode: "ai",
            skillIds: new[] { "mage_blink" }
        );
        BattleUnitState threat = BuildUnit(
            "blink_reposition_threat",
            "近身威胁",
            "player",
            new Vector2I(3, 2),
            controlMode: "manual",
            skillIds: Array.Empty<string>()
        );
        AddUnitToState(runtime, state, mage, isEnemy: true);
        AddUnitToState(runtime, state, threat, isEnemy: false);
        runtime.SetupStateForTests(state);

        var action = TestResourceOwnership.Own(
            new UseGroundRepositionSkillAction
            {
                action_id = "blink_reposition_probe",
                target_selector = "nearest_enemy",
                minimum_safe_distance = 3,
                safe_distance_margin = 0,
                desired_max_distance_bonus = 2,
                min_survival_margin_gain_to_escape = -1,
            },
            "battle_ai_ground_reposition.action"
        );
        action.skill_ids.Add("mage_blink");

        BattleAiDecision decision = new BattleAiGroundRepositionActionEvaluator().Evaluate(
            (UseGroundRepositionSkillActionDefinition)action.ToDefinition(),
            BuildAiContext(runtime, mage)
        );
        _test.True(decision?.command != null, "blink reposition should produce a skill command.");
        _test.Eq(
            decision?.command?.skill_id ?? new StringName(""),
            new StringName("mage_blink"),
            "blink reposition should use mage_blink."
        );
        _test.Eq(
            decision?.command?.CommandKind ?? BattleCommandKind.Unknown,
            BattleCommandKind.Skill,
            "blink reposition should remain a skill command."
        );
        _test.Eq(
            decision?.command?.skill_entry_id ?? new StringName(""),
            BattleSkillEntryIds.KnownSkill("mage_blink"),
            "blink reposition should stamp the known skill entry id for runtime access."
        );
        BattlePreview preview = runtime.PreviewCommand(decision?.command);
        _test.True(
            preview?.allowed == true,
            $"blink reposition command should pass formal preview. logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
        );

        int currentDistance = runtime._grid_service.GetDistanceBetweenUnits(mage, threat);
        int landingDistance = runtime._grid_service.GetDistanceFromUnitToCoord(
            threat,
            decision.command.target_coord
        );
        _test.True(
            landingDistance > currentDistance,
            "blink reposition should choose a landing coord farther from the focus threat."
        );
    }

    private static BattleRuntimeScope BuildRuntimeWithContent()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            null
        );
        return new BattleRuntimeScope(runtime, gameSession);
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_ground_reposition_behavior_regression",
            phase = "timeline_running",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
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
        state.RebuildCellColumns();
        return state;
    }

    private static BattleAiContext BuildAiContext(BattleRuntimeModule runtime, BattleUnitState unitState)
    {
        runtime._ensure_ai_action_plan_for_unit(unitState);
        runtime.TryGetAiActionPlanForUnit(
            unitState.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        var context = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unitState,
            grid_service = runtime._grid_service,
            move_cost_callback = (unit, targetCoord) =>
                runtime._get_ai_move_query_cost(unit.unit_id, unit.coord, targetCoord),
            runtime_action_plan = actionPlan,
        };
        context.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime._bind_ai_helper_services_for_decision(unitState, context);
        return context;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        string controlMode,
        IReadOnlyList<string> skillIds
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = controlMode,
            current_hp = 30,
            current_mp = 120,
            current_stamina = 30,
            current_ap = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina));
        SeedBaseAttributesAndArmorClass(unit);
        unit.attribute_snapshot.SetValue("mp_max", 120);
        unit.attribute_snapshot.SetValue("stamina_max", 30);
        unit.attribute_snapshot.SetValue("action_points", 2);
        foreach (string rawSkillId in skillIds ?? Array.Empty<string>())
        {
            StringName skillId = rawSkillId;
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 7 : 1;
        }
        return unit;
    }

    private void AddUnitToState(
        BattleRuntimeModule runtime,
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
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.coord, true),
            $"测试单位 {unit.unit_id} 应能放入测试战场。"
        );
    }

    private static void SeedBaseAttributesAndArmorClass(BattleUnitState unit)
    {
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            if (!unit.attribute_snapshot.HasValue(attributeId))
            {
                unit.attribute_snapshot.SetValue(attributeId, 10);
            }
        }
        unit.attribute_snapshot.SetValue("hp_max", 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        private readonly GameSession _gameSession;

        internal BattleRuntimeScope(BattleRuntimeModule runtime, GameSession gameSession)
        {
            Runtime = runtime;
            _gameSession = gameSession;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?._state);
            _gameSession?.Dispose();
        }
    }
}
