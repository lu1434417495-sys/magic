using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_random_chain_behavior_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRandomChainActionUsesCandidatePoolNotTargetIds();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI random-chain behavior regression"));
    }

    private void TestRandomChainActionUsesCandidatePoolNotTargetIds()
    {
        SkillDef randomChainSkill = BuildTestRandomChainSkill("ai_random_chain_score_test");
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(randomChainSkill);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(6, 3));
        runtime._state = state;
        BattleUnitState chainUser = BuildAiUnit(
            "random_chain_action_user",
            "Random chain actor",
            "hostile",
            new Vector2I(1, 1),
            "melee_aggressor",
            "engage",
            new[] { "ai_random_chain_score_test" },
            30,
            2
        );
        BattleUnitState targetA = BuildManualUnit(
            "random_chain_candidate_a",
            "Candidate A",
            "player",
            new Vector2I(2, 1),
            new[] { "warrior_heavy_strike" }
        );
        BattleUnitState targetB = BuildManualUnit(
            "random_chain_candidate_b",
            "Candidate B",
            "player",
            new Vector2I(3, 1),
            new[] { "warrior_heavy_strike" }
        );
        AddUnitToState(runtime, state, chainUser, isEnemy: true);
        AddUnitToState(runtime, state, targetA, isEnemy: false);
        AddUnitToState(runtime, state, targetB, isEnemy: false);

        BattleAiContext aiContext = BuildAiContext(runtime, chainUser);
        aiContext.trace_enabled = true;
        var action = new UseRandomChainSkillAction
        {
            action_id = "random_chain_probe",
            target_selector = "nearest_enemy",
            desired_min_distance = 1,
            desired_max_distance = 3,
            distance_reference = "candidate_pool",
        };
        action.skill_ids.Add(randomChainSkill.skill_id);

        BattleAiDecision decision = action.Decide(aiContext);
        _test.True(decision?.command != null, "Random-chain action should produce a legal command.");
        if (decision?.command == null)
        {
            return;
        }

        _test.Eq(
            decision.command.target_unit_ids.Count,
            0,
            "Random-chain commands should not carry deterministic target_unit_ids."
        );
        BattlePreview preview = runtime.PreviewCommand(decision.command);
        _test.True(preview?.allowed == true, "Random-chain command should pass preview_command.");
        _test.Eq(
            preview?.target_unit_ids.Count ?? -1,
            0,
            "Random-chain preview should not fabricate deterministic target ids."
        );
        AssertArrayContains(
            preview?.random_chain_candidate_unit_ids,
            targetA.unit_id,
            "Random-chain preview should expose candidate A in the candidate pool."
        );
        AssertArrayContains(
            preview?.random_chain_candidate_unit_ids,
            targetB.unit_id,
            "Random-chain preview should expose candidate B in the candidate pool."
        );

        BattleAiScoreInput scoreInput = decision.score_input;
        _test.True(scoreInput != null, "Random-chain action should attach a score input.");
        if (scoreInput == null)
        {
            return;
        }
        _test.Eq(
            scoreInput.action_kind,
            (StringName)"random_chain_skill",
            "Random-chain scoring should use the dedicated action kind."
        );
        _test.Eq(
            scoreInput.target_unit_ids.Count,
            0,
            "Random-chain scoring should not treat the candidate pool as target_unit_ids."
        );
        _test.Eq(scoreInput.target_count, 0, "Random-chain target_count should stay deterministic.");
        _test.Eq(
            scoreInput.random_chain_candidate_pool_count,
            2,
            "Random-chain scoring should record the candidate pool size."
        );
        AssertArrayContains(
            scoreInput.random_chain_candidate_unit_ids,
            targetA.unit_id,
            "Random-chain scoring should record candidate A."
        );
        AssertArrayContains(
            scoreInput.random_chain_candidate_unit_ids,
            targetB.unit_id,
            "Random-chain scoring should record candidate B."
        );
        _test.True(
            scoreInput.effective_target_count > 0,
            "Random-chain scoring should produce positive expected effective target count."
        );
        _test.True(
            scoreInput.total_score > 0,
            "Random-chain scoring should produce a positive score."
        );
        _test.Eq(
            scoreInput.random_chain_selection_policy,
            (StringName)"random_from_living_pool",
            "Random-chain scoring should identify the runtime random selection policy."
        );
        _test.Eq(
            scoreInput.random_chain_score_estimate_policy,
            (StringName)"expected_value",
            "Random-chain scoring should identify the expected-value estimate policy."
        );

        IReadOnlyList<AiActionTrace> actionTraces = aiContext.GetActionTracesTyped();
        _test.True(actionTraces.Count > 0, "Random-chain action should write an AI trace.");
        if (actionTraces.Count == 0)
        {
            return;
        }
        IReadOnlyDictionary<string, object> metadata = actionTraces[0].Metadata;
        _test.Eq(
            ReadString(metadata, "action_kind"),
            "random_chain_skill",
            "Random-chain trace should mark the dedicated action kind."
        );
        IReadOnlyList<string> candidatePoolUnitIds = ReadStringList(
            metadata,
            "candidate_pool_unit_ids"
        );
        _test.True(
            System.Linq.Enumerable.Contains(candidatePoolUnitIds, targetA.unit_id.ToString())
                && System.Linq.Enumerable.Contains(candidatePoolUnitIds, targetB.unit_id.ToString()),
            "Random-chain trace metadata should record candidate pool ids."
        );
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent(params SkillDef[] extraSkills)
    {
        var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        var skillDefs = new Dictionary<StringName, SkillDef>(gameSession.GetSkillDefsTyped());
        foreach (SkillDef skill in extraSkills ?? Array.Empty<SkillDef>())
        {
            if (skill != null && skill.skill_id != (StringName)"")
            {
                skillDefs[skill.skill_id] = skill;
            }
        }
        runtime.setup(
            null,
            skillDefs,
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            null
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.SetSkillDefs(runtime.GetSkillDefIndexTyped());
        runtime.ConfigureDamageResolverForTests(damageResolver);
        gameSession.Free();
        return new BattleRuntimeScope(runtime);
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_random_chain_behavior_regression",
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
            unit.known_skill_level_map[skillId] = 1;
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
            unit.known_skill_level_map[skillId] = 1;
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
            $"Test unit {unit.unit_id} should be placeable."
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

    private static SkillDef BuildTestRandomChainSkill(StringName skillId)
    {
        var skill = new SkillDef
        {
            skill_id = skillId,
            display_name = "Test random chain",
            skill_type = "active",
        };
        var combat = new CombatSkillDef
        {
            skill_id = skillId,
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "random_chain",
            range_pattern = "single",
            range_value = 3,
            ap_cost = 1,
            max_hits_per_target = 1,
        };
        combat.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "damage",
                power = 12,
                damage_tag = "physical_slash",
            }
        );
        skill.combat_profile = combat;
        return skill;
    }

    private static string ReadString(IReadOnlyDictionary<string, object> source, string key)
    {
        return source != null
            && source.TryGetValue(key, out object value)
            && value is string text
            ? text
            : "";
    }

    private static IReadOnlyList<string> ReadStringList(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null
            && source.TryGetValue(key, out object value)
            && value is IReadOnlyList<string> values
            ? values
            : Array.Empty<string>();
    }

    private void AssertArrayContains(
        IEnumerable<StringName> values,
        StringName expected,
        string message
    )
    {
        _test.True(values != null && System.Linq.Enumerable.Contains(values, expected), message);
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        internal BattleRuntimeScope(BattleRuntimeModule runtime)
        {
            Runtime = runtime;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            Runtime?.dispose();
        }
    }
}
