using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_score_input_metrics_regression : SceneTree
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
            TestGroundSkillEffectiveTargetsExcludeFriendlyFire();
            TestEmptyGroundControlCellsStaySeparateFromUnitTargets();
            TestGroundSkillScoreInputExposesMetrics();
            TestRepeatAttackScoreUsesStageSuccessRate();
            TestChainSkillScoresFriendlyBounceRisk();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI score input metrics regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI score input metrics regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestGroundSkillEffectiveTargetsExcludeFriendlyFire()
    {
        Fixture fixture = BuildFixture("score_input_ground_effective_targets", new Vector2I(8, 6));
        SkillDef skill = BuildSkill(
            "friendly_fire_fireball_probe",
            "Friendly Fire Fireball Probe",
            BuildDamageEffect(10, "any")
        );
        fixture.AddSkill(skill);

        BattleUnitState caster = BuildUnit("friendly_fire_fireball_mage", "hostile", new Vector2I(1, 2));
        BattleUnitState target = BuildUnit("friendly_fire_target", "player", new Vector2I(5, 2));
        BattleUnitState ally = BuildUnit("friendly_fire_ally", "hostile", new Vector2I(5, 3));
        fixture.AddUnit(caster);
        fixture.AddUnit(target);
        fixture.AddUnit(ally);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.skill_id, target.coord),
            BuildPreview(target, ally),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(null, 4, 5)
        );

        AssertTrue(score != null, "友伤火球评分应可生成。");
        if (score == null)
        {
            return;
        }
        AssertEq(score.enemy_target_count, 1, "minimum_hit_count 应只把敌方有效目标计入收益。");
        AssertEq(score.effective_target_count, 1, "友军被火球覆盖不能贡献有效命中数。");
        AssertTrue(score.estimated_friendly_fire_target_count >= 1, "评分应识别火球友伤目标。");
    }

    private void TestEmptyGroundControlCellsStaySeparateFromUnitTargets()
    {
        Fixture fixture = BuildFixture("score_input_empty_ground_control", new Vector2I(6, 5));
        SkillDef skill = BuildSkill(
            "ai_empty_ground_control_score_probe",
            "Empty Ground Control Probe",
            BuildTerrainEffect("mist_pool")
        );
        fixture.AddSkill(skill);

        BattleUnitState caster = BuildUnit("empty_ground_control_scorer", "hostile", new Vector2I(1, 2));
        fixture.AddUnit(caster);
        Vector2I targetCoord = new(3, 2);
        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.skill_id, targetCoord),
            BuildGroundPreview(targetCoord),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(null, 0, 5)
        );

        AssertTrue(score != null, "空地控场评分应可生成。");
        if (score == null)
        {
            return;
        }
        AssertEq(score.target_count, 0, "空地控场不应把地格计入 target_count。");
        AssertEq(score.enemy_target_count, 0, "空地控场不应伪造敌方目标。");
        AssertEq(score.effective_target_count, 0, "空地控场不应伪造有效命中数。");
        AssertEq(score.estimated_ground_control_cell_count, 1, "空地控场应按 preview target_coords 暴露受控地格数。");
        AssertTrue(score.ground_control_score > 0, "空地控场应产生独立地格控制评分。");
    }

    private void TestGroundSkillScoreInputExposesMetrics()
    {
        Fixture fixture = BuildFixture("score_input_ground_metrics", new Vector2I(7, 5));
        SkillDef skill = BuildSkill(
            "archer_suppressive_fire_probe",
            "Suppressive Fire Probe",
            BuildDamageEffect(8, "enemy")
        );
        skill.combat_profile.ap_cost = 2;
        skill.combat_profile.stamina_cost = 2;
        skill.combat_profile.cooldown_tu = 15;
        fixture.AddSkill(skill);

        BattleUnitState harrier = BuildUnit("mist_harrier_score", "hostile", new Vector2I(1, 2));
        BattleUnitState playerA = BuildUnit("player_a", "player", new Vector2I(4, 2));
        BattleUnitState playerB = BuildUnit("player_b", "player", new Vector2I(5, 2));
        fixture.AddUnit(harrier);
        fixture.AddUnit(playerA);
        fixture.AddUnit(playerB);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(harrier),
            skill,
            BuildCommand(harrier, skill.skill_id, playerA.coord),
            BuildPreview(playerA, playerB),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(null, 0, 6)
        );

        AssertTrue(score != null, "AI skill score input 应由 BattleAiScoreService 正式构造。");
        if (score == null)
        {
            return;
        }
        AssertTrue(score.hit_payoff_score > 0, "ground skill score input 应暴露正向命中收益。");
        AssertTrue(score.target_count >= 2, "ground skill score input 应暴露目标数量。");
        AssertEq(score.ap_cost, 2, "ground skill score input 应暴露 AP 消耗。");
        AssertEq(score.stamina_cost, 2, "ground skill score input 应暴露 ST 消耗。");
        AssertEq(score.cooldown_tu, 15, "ground skill score input 应暴露 cooldown_tu。");
        AssertTrue(score.resource_cost_score > 0, "ground skill score input 应暴露资源消耗评分。");
        AssertEq(score.position_objective_kind, new StringName("cast_distance"), "ground skill score input 应记录默认站位目标类型。");
        AssertTrue(score.distance_to_primary_coord >= 0, "ground skill score input 应记录站位目标距离。");
        AssertTrue(score.position_objective_score >= 0, "ground skill score input 应暴露站位目标评分。");
    }

    private void TestRepeatAttackScoreUsesStageSuccessRate()
    {
        Fixture fixture = BuildFixture("score_input_fate_aware_hit_rate", new Vector2I(5, 3));
        SkillDef skill = BuildSkill(
            "ai_fate_preview_combo_probe",
            "Fate Preview Combo Probe",
            BuildDamageEffect(10, "enemy")
        );
        fixture.AddSkill(skill);

        BattleUnitState scorer = BuildUnit("fate_score_user", "hostile", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("fate_score_target", "player", new Vector2I(2, 1));
        fixture.AddUnit(scorer);
        fixture.AddUnit(target);
        BattlePreview preview = BuildPreview(target);
        preview.hit_preview = new AttackPreviewData
        {
            Stages = new List<AttackPreviewStage>
            {
                new(10, 15, 10, 19, 19, "15%"),
            },
            HitRatePercent = 10,
            SuccessRatePercent = 15,
            BaseHitRatePercent = 10,
        };

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(scorer),
            skill,
            BuildCommand(scorer, skill.skill_id, target.coord, target),
            preview,
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(target, 1, 1)
        );

        AssertTrue(score != null, "AI fate-aware 命中率回归应构造出合法 score input。");
        if (score == null)
        {
            return;
        }
        AssertEq(preview.hit_preview.StageBaseHitRates[0], 10, "AI 回归前置：preview 应保留 raw 命中率。");
        AssertEq(preview.hit_preview.StageSuccessRates[0], 15, "AI 回归前置：preview 应保留正式成功率。");
        AssertEq(score.estimated_hit_rate_percent, 15, "AI 评分应消费 fate-aware repeat_attack 成功率，而不是 raw hit rate。");
    }

    private void TestChainSkillScoresFriendlyBounceRisk()
    {
        Fixture fixture = BuildFixture("score_input_chain_friendly_bounce", new Vector2I(8, 6));
        SkillDef skill = BuildSkill(
            "mage_chain_lightning_probe",
            "Chain Lightning Probe",
            BuildChainDamageEffect(1),
            BuildDamageEffect(7, "any")
        );
        fixture.AddSkill(skill);

        BattleUnitState mage = BuildUnit("friendly_chain_mage", "hostile", new Vector2I(1, 2));
        BattleUnitState target = BuildUnit("friendly_chain_target", "player", new Vector2I(5, 2));
        BattleUnitState ally = BuildUnit("friendly_chain_ally", "hostile", new Vector2I(5, 3));
        fixture.AddUnit(mage);
        fixture.AddUnit(target);
        fixture.AddUnit(ally);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(mage),
            skill,
            BuildCommand(mage, skill.skill_id, target.coord, target),
            BuildPreview(target),
            new[] { skill.combat_profile.effect_defs[0], skill.combat_profile.effect_defs[1] },
            BuildPositionMetadata(target, 4, 5)
        );

        AssertTrue(score != null, "友伤链闪评分应可生成。");
        if (score == null)
        {
            return;
        }
        AssertTrue(score.estimated_chain_ally_target_count >= 1, "链闪评分应预估会弹射到友军。");
        AssertTrue(score.estimated_friendly_fire_target_count >= 1, "链闪评分应把友军弹射计为友伤风险。");
    }

    private static Fixture BuildFixture(string battleId, Vector2I mapSize) =>
        new(battleId, mapSize);

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

    private static SkillDef BuildSkill(StringName skillId, string displayName, params CombatEffectDef[] effects)
    {
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            range_value = 5,
            ap_cost = 0,
            mp_cost = 0,
            stamina_cost = 0,
            cooldown_tu = 0,
        };
        foreach (CombatEffectDef effect in effects ?? Array.Empty<CombatEffectDef>())
        {
            if (effect != null)
            {
                combatProfile.effect_defs.Add(effect);
            }
        }
        return new SkillDef
        {
            skill_id = skillId,
            display_name = displayName,
            combat_profile = combatProfile,
        };
    }

    private static CombatEffectDef BuildDamageEffect(int power, StringName targetFilter) =>
        new()
        {
            effect_type = "damage",
            effect_target_team_filter = targetFilter,
            power = power,
        };

    private static CombatEffectDef BuildTerrainEffect(StringName terrainEffectId) =>
        new()
        {
            effect_type = "terrain_effect",
            terrain_effect_id = terrainEffectId,
        };

    private static CombatEffectDef BuildChainDamageEffect(int radius) =>
        new()
        {
            effect_type = "chain_damage",
            effect_target_team_filter = "any",
            prevent_repeat_target = true,
            @params = new GDictionary { ["base_chain_radius"] = radius },
        };

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

    private static BattlePreview BuildPreview(params BattleUnitState[] targets)
    {
        var preview = new BattlePreview
        {
            allowed = true,
        };
        foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
        {
            if (target == null)
            {
                continue;
            }
            preview.target_unit_ids.Add(target.unit_id);
            preview.target_coords.Add(target.coord);
        }
        return preview;
    }

    private static BattlePreview BuildGroundPreview(params Vector2I[] targetCoords)
    {
        var preview = new BattlePreview
        {
            allowed = true,
        };
        foreach (Vector2I coord in targetCoords ?? Array.Empty<Vector2I>())
        {
            preview.target_coords.Add(coord);
        }
        return preview;
    }

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
