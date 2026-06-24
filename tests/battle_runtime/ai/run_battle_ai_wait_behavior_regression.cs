using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_wait_behavior_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestWaitActionMarksActiveRestWhenStaminaStarved();
            TestWaitActionReportsRestWhenNoActionIsAvailable();
            TestActiveRestDoesNotOutrankMeleeScreeningMove();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI wait behavior regression"));
    }

    private void TestWaitActionMarksActiveRestWhenStaminaStarved()
    {
        EnemyAiBrainDef brain = BuildActiveRestProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(3, 1));
        BattleUnitState wolf = BuildAiUnit(
            "active_rest_wolf",
            "体力耗尽战士",
            "hostile",
            new Vector2I(0, 0),
            brain.brain_id,
            "pressure",
            new[] { "basic_attack" },
            28,
            1
        );
        wolf.current_stamina = 0;
        wolf.action_threshold = 30;
        wolf.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        wolf.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 3);
        BattleUnitState player = BuildManualUnit(
            "active_rest_target",
            "贴身目标",
            "player",
            new Vector2I(1, 0),
            new[] { "basic_attack" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);
        runtime.SetupStateForTests(state);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, wolf));
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"active_rest_wait",
            "体力不足且无法支付基础攻击时，默认 wait action 应表达主动休息。"
        );
        _test.True(
            decision?.score_input?.runtime_action_metadata?.HasActiveRest == true
                && decision.score_input.runtime_action_metadata.active_rest,
            "主动休息应通过 score metadata 暴露 active_rest。"
        );
        _test.True(
            decision?.score_input != null && decision.score_input.total_score > -40,
            "主动休息应抬高 wait 评分，但仍保持低于有效移动/守线动作。"
        );
    }

    private void TestWaitActionReportsRestWhenNoActionIsAvailable()
    {
        EnemyAiBrainDef brain = BuildFallbackRestProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(3, 1));
        BattleUnitState wolf = BuildAiUnit(
            "fallback_rest_wolf",
            "无动作战士",
            "hostile",
            new Vector2I(0, 0),
            brain.brain_id,
            "engage",
            Array.Empty<string>(),
            28,
            1
        );
        wolf.current_stamina = 20;
        wolf.action_threshold = 30;
        wolf.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        BattleUnitState player = BuildManualUnit(
            "fallback_rest_target",
            "目标",
            "player",
            new Vector2I(2, 0),
            new[] { "basic_attack" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);
        runtime.SetupStateForTests(state);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, wolf));
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"fallback_rest_wait",
            "没有有效动作时仍应由默认 wait action 收束。"
        );
        _test.Eq(
            decision?.score_input?.total_score ?? 999,
            -40,
            "普通无动作休息只改变语义说明，不应提高 wait 评分去抢移动或卡位。"
        );
    }

    private void TestActiveRestDoesNotOutrankMeleeScreeningMove()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(7, 8));
        BattleUnitState wolf = BuildAiUnit(
            "rest_screening_wolf",
            "体力不足占位战士",
            "hostile",
            new Vector2I(1, 4),
            "melee_aggressor",
            "engage",
            new[] { "basic_attack" },
            28,
            1
        );
        wolf.current_stamina = 0;
        wolf.current_move_points = 2;
        wolf.action_threshold = 30;
        wolf.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        wolf.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 3);
        BattleUnitState archer = BuildAiUnit(
            "rest_screening_archer",
            "被保护弓手",
            "hostile",
            new Vector2I(3, 6),
            "ranged_archer",
            "pressure",
            new[] { "archer_aimed_shot" },
            28,
            2
        );
        ApplyTestBowWeapon(archer, 6);
        BattleUnitState player = BuildManualUnit(
            "rest_screening_threat",
            "近战威胁",
            "player",
            new Vector2I(3, 3),
            new[] { "basic_attack" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, archer, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);
        runtime.SetupStateForTests(state);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, wolf));
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"wolf_close_in",
            "体力不足时，主动休息不能抢掉近战战士仍可执行的守线/接敌移动。"
        );
        _test.Eq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(3, 4),
            "体力不足的近战仍应先走到实际增加敌方路径成本的守线格，之后再等待休息。"
        );
    }

    private static EnemyAiBrainDef BuildActiveRestProbeBrain()
    {
        var basicAction = new UseUnitSkillAction
        {
            action_id = "active_rest_basic",
            target_selector = "nearest_enemy",
            desired_min_distance = 1,
            desired_max_distance = 1,
            DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
        };
        basicAction.skill_ids.Add("basic_attack");
        var waitAction = new WaitAction { action_id = "active_rest_wait" };
        var pressureState = new EnemyAiStateDef { state_id = "pressure" };
        pressureState.actions.Add(basicAction);
        pressureState.actions.Add(waitAction);
        var brain = new EnemyAiBrainDef
        {
            brain_id = "active_rest_probe_brain",
            default_state_id = "pressure",
        };
        brain.states.Add(pressureState);
        return TestResourceOwnership.Own(brain, "BattleAiWaitBehavior.BuildActiveRestProbeBrain");
    }

    private static EnemyAiBrainDef BuildFallbackRestProbeBrain()
    {
        var engageState = new EnemyAiStateDef { state_id = "engage" };
        engageState.actions.Add(new WaitAction { action_id = "fallback_rest_wait" });
        var brain = new EnemyAiBrainDef
        {
            brain_id = "fallback_rest_probe_brain",
            default_state_id = "engage",
        };
        brain.states.Add(engageState);
        return TestResourceOwnership.Own(brain, "BattleAiWaitBehavior.BuildFallbackRestProbeBrain");
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent(params EnemyAiBrainDef[] extraBrains)
    {
        var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        var enemyAiBrains = new Dictionary<StringName, EnemyAiBrainDef>(
            gameSession.GetEnemyAiBrainsTyped()
        );
        foreach (EnemyAiBrainDef brain in extraBrains ?? Array.Empty<EnemyAiBrainDef>())
        {
            if (brain != null && brain.brain_id != (StringName)"")
            {
                enemyAiBrains[brain.brain_id] = brain;
            }
        }
        runtime.setup(
            null,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            enemyAiBrains,
            null
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.SetSkillDefs(runtime.GetSkillDefIndexTyped());
        runtime.ConfigureDamageResolverForTests(damageResolver);
        return new BattleRuntimeScope(runtime, gameSession);
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_wait_behavior_regression",
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

    private static BattleAiContext BuildAiContext(BattleRuntimeModule runtime, BattleUnitState unitState)
    {
        runtime._ensure_ai_action_plan_for_unit(unitState);
        runtime._ai_action_plans_by_unit_id.TryGetValue(
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
        context.SetSkillDefs(runtime.GetSkillDefIndexTyped());
        runtime._bind_ai_helper_services_for_decision(unitState, context);
        return context;
    }

    private static BattleUnitState BuildAiUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        StringName brainId,
        StringName stateId,
        IReadOnlyList<string> skillIds,
        int currentHp,
        int currentAp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "ai",
            ai_brain_id = brainId,
            ai_state_id = stateId,
            current_hp = currentHp,
            current_mp = 120,
            current_stamina = 8,
            current_ap = currentAp,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        SeedBaseAttributesAndArmorClass(unit, Math.Max(currentHp, 24), 8, 12);
        unit.attribute_snapshot.SetValue("mp_max", 120);
        unit.attribute_snapshot.SetValue("action_points", Math.Max(currentAp, 2));
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 3 : 1;
        }
        return unit;
    }

    private static BattleUnitState BuildManualUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        IReadOnlyList<string> skillIds
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 30,
            current_ap = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        SeedBaseAttributesAndArmorClass(unit, 30, 8, 6);
        unit.attribute_snapshot.SetValue("action_points", 2);
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 3 : 1;
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

    private static void SeedBaseAttributesAndArmorClass(
        BattleUnitState unit,
        int hpMax,
        int staminaMax,
        int attackBonus
    )
    {
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            if (!unit.attribute_snapshot.HasValue(attributeId))
            {
                unit.attribute_snapshot.SetValue(attributeId, 10);
            }
        }
        unit.attribute_snapshot.SetValue("hp_max", hpMax);
        unit.attribute_snapshot.SetValue("stamina_max", staminaMax);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), attackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
    }

    private static void ApplyTestBowWeapon(BattleUnitState unit, int attackRange)
    {
        unit?.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "ai_test_longbow",
                weapon_profile_type_id = "longbow",
                weapon_family = "bow",
                weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
                weapon_attack_range = attackRange,
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
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
