using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_score_selection_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestMeleeActionPrefersLaterHigherScoreSkillAction();
            TestRangedScorePrefersUnitNukeOverSingleTargetAreaBlast();
            TestUnitSkillActionSelectsHigherHitPayoffTarget();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI score selection regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI score selection regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestMeleeActionPrefersLaterHigherScoreSkillAction()
    {
        Fixture fixture = BuildFixture("score_selection_melee_action", new Vector2I(5, 3));
        SkillDef heavySkill = BuildUnitSkill(
            "warrior_heavy_strike",
            "Heavy Strike",
            5,
            range: 1
        );
        SkillDef executeSkill = BuildUnitSkill(
            "warrior_execution_cleave",
            "Execution Cleave",
            18,
            range: 1
        );
        fixture.AddSkill(heavySkill);
        fixture.AddSkill(executeSkill);

        BattleUnitState wolf = BuildUnit("wolf_score_melee", "hostile", new Vector2I(1, 1));
        wolf.ai_brain_id = "melee_score_probe";
        wolf.known_active_skill_ids.Add(heavySkill.skill_id);
        wolf.known_active_skill_ids.Add(executeSkill.skill_id);
        BattleUnitState player = BuildUnit("low_hp_target", "player", new Vector2I(2, 1), hp: 20);
        player.current_hp = 5;
        fixture.AddUnit(wolf);
        fixture.AddUnit(player);

        BattleAiContext context = fixture.BuildContext(wolf);
        context.allow_authored_action_fallback_for_tests = true;
        BattleAiScoreInput heavyScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            heavySkill,
            BuildCommand(wolf, heavySkill.skill_id, player.coord, player),
            BuildPreview(player),
            new[] { heavySkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(player, 1, 1)
        );
        BattleAiScoreInput executeScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            executeSkill,
            BuildCommand(wolf, executeSkill.skill_id, player.coord, player),
            BuildPreview(player),
            new[] { executeSkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(player, 1, 1)
        );

        AssertTrue(heavyScore != null && executeScore != null, "melee 评分回归应拿到两个合法技能候选的评分。");
        if (heavyScore == null || executeScore == null)
        {
            return;
        }
        AssertTrue(
            executeScore.total_score > heavyScore.total_score,
            "残血目标场景下，warrior_execution_cleave 的评分应高于 warrior_heavy_strike。"
        );

        BattleAiService aiService = new() { EnableMutationGuard = false };
        aiService.Setup(BuildBrainMap(BuildTwoUnitSkillActionBrain(wolf.ai_brain_id, heavySkill.skill_id, executeSkill.skill_id)));
        BattleAiDecision decision = aiService.ChooseCommand(context);
        AssertTrue(decision != null && decision.state_id == new StringName("pressure"), "melee 评分选技回归应保持 pressure 状态。");
        AssertEq(
            decision?.command?.skill_id ?? new StringName(""),
            executeSkill.skill_id,
            "melee AI 不应只按 action 顺序选择先声明的 warrior_heavy_strike。"
        );
        AssertEq(
            decision?.action_id ?? new StringName(""),
            new StringName("score_probe_higher"),
            "melee AI 应能选中后声明但评分更高的技能 action。"
        );
    }

    private void TestRangedScorePrefersUnitNukeOverSingleTargetAreaBlast()
    {
        Fixture fixture = BuildFixture("score_selection_ranged_skill_compare", new Vector2I(7, 5));
        SkillDef fireballSkill = BuildGroundSkill("mage_fireball", "Fireball", 6, range: 4);
        SkillDef iceLanceSkill = BuildUnitSkill("mage_ice_lance", "Ice Lance", 16, range: 4);
        fixture.AddSkill(fireballSkill);
        fixture.AddSkill(iceLanceSkill);

        BattleUnitState caster = BuildUnit("mist_score_caster", "hostile", new Vector2I(1, 2));
        caster.known_active_skill_ids.Add(fireballSkill.skill_id);
        caster.known_active_skill_ids.Add(iceLanceSkill.skill_id);
        BattleUnitState player = BuildUnit("single_target", "player", new Vector2I(4, 2));
        fixture.AddUnit(caster);
        fixture.AddUnit(player);

        BattleAiContext context = fixture.BuildContext(caster);
        BattleAiScoreInput fireballScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            fireballSkill,
            BuildCommand(caster, fireballSkill.skill_id, player.coord),
            BuildPreview(player),
            new[] { fireballSkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(null, 3, 4)
        );
        BattleAiScoreInput iceLanceScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            iceLanceSkill,
            BuildCommand(caster, iceLanceSkill.skill_id, player.coord, player),
            BuildPreview(player),
            new[] { iceLanceSkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(player, 3, 4)
        );

        AssertTrue(fireballScore != null && iceLanceScore != null, "ranged_controller 评分回归应拿到两个合法技能候选的评分。");
        if (fireballScore == null || iceLanceScore == null)
        {
            return;
        }
        AssertTrue(
            iceLanceScore.total_score > fireballScore.total_score,
            "单体目标场景下，mage_ice_lance 的评分应高于 mage_fireball。"
        );
    }

    private void TestUnitSkillActionSelectsHigherHitPayoffTarget()
    {
        Fixture fixture = BuildFixture("score_selection_unit_target_payoff", new Vector2I(7, 5));
        SkillDef skill = BuildUnitSkill("archer_pinning_shot", "Pinning Shot", 10, range: 6);
        fixture.AddSkill(skill);

        BattleUnitState archer = BuildUnit("score_archer", "hostile", new Vector2I(1, 2));
        archer.known_active_skill_ids.Add(skill.skill_id);
        BattleUnitState closeTank = BuildUnit("close_tank", "player", new Vector2I(2, 2));
        BattleUnitState farScout = BuildUnit("far_scout", "player", new Vector2I(4, 2));
        fixture.AddUnit(archer);
        fixture.AddUnit(closeTank);
        fixture.AddUnit(farScout);

        BattleAiContext context = fixture.BuildContext(archer);
        context.skill_score_input_callback = (aiContext, skillDef, command, preview, effects, metadata) =>
        {
            if (command != null && preview != null)
            {
                preview.hit_preview =
                    command.target_unit_id == closeTank.unit_id
                        ? BuildHitPreview(20)
                        : BuildHitPreview(90);
            }
            return fixture.ScoreService.BuildSkillScoreInput(
                aiContext,
                skillDef,
                command,
                preview,
                effects ?? new GArray(),
                metadata ?? new GDictionary()
            );
        };

        BattleAiScoreInput closeScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            skill,
            BuildCommand(archer, skill.skill_id, closeTank.coord, closeTank),
            BuildPreview(closeTank, BuildHitPreview(20)),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(closeTank, 0, 6)
        );
        BattleAiScoreInput farScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            skill,
            BuildCommand(archer, skill.skill_id, farScout.coord, farScout),
            BuildPreview(farScout, BuildHitPreview(90)),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(farScout, 0, 6)
        );

        AssertTrue(closeScore != null && farScore != null, "unit skill score input 应能为多个候选目标生成评分上下文。");
        if (closeScore == null || farScore == null)
        {
            return;
        }
        AssertTrue(
            farScore.hit_payoff_score > closeScore.hit_payoff_score,
            "更脆弱的远处目标应提供更高的命中收益评分。"
        );
        AssertTrue(
            farScore.total_score > closeScore.total_score,
            "共享评分上下文应允许高收益目标压过默认最近目标。"
        );

        var action = new UseUnitSkillAction
        {
            action_id = "score_pick_best_target",
            target_selector = "nearest_enemy",
            desired_min_distance = 0,
            desired_max_distance = 6,
            distance_reference = UseUnitSkillAction.DISTANCE_REF_TARGET_UNIT(),
        };
        action.skill_ids.Add(skill.skill_id);
        BattleAiDecision decision = action.decide(context);
        AssertTrue(decision != null && decision.command != null, "共享 unit score input 后应仍能生成合法指令。");
        AssertEq(
            decision?.command?.target_unit_id ?? new StringName(""),
            farScout.unit_id,
            "UseUnitSkillAction 应根据共享评分上下文选择更高命中收益的目标。"
        );
    }

    private static Fixture BuildFixture(string battleId, Vector2I mapSize) =>
        new(battleId, mapSize);

    private static Dictionary<StringName, EnemyAiBrainDef> BuildBrainMap(EnemyAiBrainDef brain) =>
        new()
        {
            [brain.brain_id] = brain,
        };

    private static EnemyAiBrainDef BuildTwoUnitSkillActionBrain(
        StringName brainId,
        StringName lowerSkillId,
        StringName higherSkillId
    )
    {
        var lowerAction = new UseUnitSkillAction
        {
            action_id = "score_probe_lower",
            target_selector = "nearest_enemy",
            desired_min_distance = 1,
            desired_max_distance = 1,
            distance_reference = UseUnitSkillAction.DISTANCE_REF_TARGET_UNIT(),
        };
        lowerAction.skill_ids.Add(lowerSkillId);
        var higherAction = new UseUnitSkillAction
        {
            action_id = "score_probe_higher",
            target_selector = "nearest_enemy",
            desired_min_distance = 1,
            desired_max_distance = 1,
            distance_reference = UseUnitSkillAction.DISTANCE_REF_TARGET_UNIT(),
        };
        higherAction.skill_ids.Add(higherSkillId);

        var state = new EnemyAiStateDef
        {
            state_id = "pressure",
        };
        state.actions.Add(lowerAction);
        state.actions.Add(higherAction);
        var brain = new EnemyAiBrainDef
        {
            brain_id = brainId,
            default_state_id = state.state_id,
        };
        brain.states.Add(state);
        return brain;
    }

    private static BattleState BuildFlatState(string battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y += 1)
        {
            for (int x = 0; x < mapSize.X; x += 1)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.recalculate_runtime_values();
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp = 30
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            current_hp = hp,
            current_ap = 2,
            current_mp = 100,
            current_stamina = 100,
            is_alive = true,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), hp);
        unit.attribute_snapshot.set_value("strength", 10);
        unit.attribute_snapshot.set_value("agility", 10);
        unit.attribute_snapshot.set_value("constitution", 10);
        unit.attribute_snapshot.set_value("perception", 10);
        unit.attribute_snapshot.set_value("intelligence", 10);
        unit.attribute_snapshot.set_value("willpower", 10);
        unit.refresh_footprint();
        return unit;
    }

    private static SkillDef BuildUnitSkill(StringName skillId, string displayName, int power, int range)
    {
        SkillDef skill = BuildSkill(skillId, displayName, power, range);
        skill.combat_profile.target_mode = "unit";
        return skill;
    }

    private static SkillDef BuildGroundSkill(StringName skillId, string displayName, int power, int range)
    {
        SkillDef skill = BuildSkill(skillId, displayName, power, range);
        skill.combat_profile.target_mode = "ground";
        return skill;
    }

    private static SkillDef BuildSkill(StringName skillId, string displayName, int power, int range)
    {
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            target_team_filter = "enemy",
            range_value = range,
            ap_cost = 0,
            mp_cost = 0,
            stamina_cost = 0,
            cooldown_tu = 0,
        };
        combatProfile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "damage",
                effect_target_team_filter = "enemy",
                power = power,
            }
        );
        return new SkillDef
        {
            skill_id = skillId,
            display_name = displayName,
            combat_profile = combatProfile,
        };
    }

    private static BattleCommand BuildCommand(
        BattleUnitState actor,
        StringName skillId,
        Vector2I targetCoord,
        BattleUnitState targetUnit = null
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = actor.unit_id,
            skill_id = skillId,
            target_coord = targetCoord,
        };
        command.target_coords.Add(targetCoord);
        if (targetUnit != null)
        {
            command.target_unit_id = targetUnit.unit_id;
            command.target_unit_ids.Add(targetUnit.unit_id);
        }
        return command;
    }

    private static BattlePreview BuildPreview(BattleUnitState target, AttackPreviewData hitPreview = null)
    {
        var preview = new BattlePreview
        {
            allowed = true,
            hit_preview = hitPreview,
        };
        if (target != null)
        {
            preview.target_unit_ids.Add(target.unit_id);
            preview.target_coords.Add(target.coord);
        }
        return preview;
    }

    private static AttackPreviewData BuildHitPreview(int successRate) =>
        new()
        {
            Stages = new List<AttackPreviewStage>
            {
                new(successRate, successRate, successRate, 0, 0, ""),
            },
            HitRatePercent = successRate,
            SuccessRatePercent = successRate,
            BaseHitRatePercent = successRate,
        };

    private static GDictionary BuildPositionMetadata(
        BattleUnitState positionTarget,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        var metadata = new GDictionary
        {
            ["desired_min_distance"] = desiredMinDistance,
            ["desired_max_distance"] = desiredMaxDistance,
        };
        if (positionTarget != null)
        {
            metadata["position_target_unit_id"] = positionTarget.unit_id;
        }
        return metadata;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private sealed class Fixture
    {
        public readonly BattleState State;
        public readonly BattleGridService GridService = new();
        public readonly BattleAiScoreService ScoreService = new();
        private readonly GDictionary _skillDefs = new();

        public Fixture(string battleId, Vector2I mapSize)
        {
            State = BuildFlatState(battleId, mapSize);
        }

        public void AddSkill(SkillDef skillDef)
        {
            if (skillDef == null || skillDef.skill_id == "")
            {
                return;
            }
            _skillDefs[skillDef.skill_id] = skillDef;
        }

        public void AddUnit(BattleUnitState unit)
        {
            if (unit == null || unit.unit_id == "")
            {
                return;
            }
            State.units[unit.unit_id] = unit;
            if (unit.faction_id == "hostile")
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            bool placed = GridService.place_unit(State, unit, unit.coord, true);
            if (!placed)
            {
                throw new InvalidOperationException($"Failed to place test unit {unit.unit_id} at {unit.coord}.");
            }
        }

        public BattleAiContext BuildContext(BattleUnitState actor) =>
            new()
            {
                state = State,
                unit_state = actor,
                grid_service = GridService,
                skill_defs = _skillDefs,
            };
    }
}
