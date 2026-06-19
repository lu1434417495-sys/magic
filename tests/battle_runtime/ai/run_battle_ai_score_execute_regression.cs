using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_score_execute_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestInvalidHighHpExecuteProducesNoSaveEstimateOrValue();
            TestKillProbabilityUsesSaveFailureProbability();
            TestExecuteImmunityZeroesKillProbabilityButKeepsSoulFractureValue();
            TestDeathProtectionReducesKillProbability();
            TestAiIgnoresPreviewPresentationText();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle AI score execute regression"));
    }

    private void TestInvalidHighHpExecuteProducesNoSaveEstimateOrValue()
    {
        Fixture fixture = BuildFixture("ai_execute_high_hp");
        SkillDef skill = BuildExecuteSkill();
        BattleUnitState source = BuildUnit("execute_source", "hostile", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = BuildUnit("execute_high_hp_target", "player", new Vector2I(1, 0), 100, 21);
        fixture.AddUnit(source);
        fixture.AddUnit(target);

        BattleAiScoreInput score = BuildScore(fixture, source, target, skill);

        _test.Eq(score.estimated_damage, 0, "高 HP execute 不应产生 AI 伤害估值。");
        _test.Eq(score.estimated_control_count, 0, "高 HP execute 不应被 AI 当作控制收益。");
        _test.Eq(score.effective_target_count, 0, "高 HP execute 不应贡献有效目标收益。");
        _test.Eq(score.execute_kill_probability_basis_points, 0, "高 HP execute kill bps 应为 0。");
        _test.False(
            score.save_estimates_by_target_id.ContainsKey(target.unit_id),
            "高 HP execute 不应解析豁免估算。"
        );
    }

    private void TestKillProbabilityUsesSaveFailureProbability()
    {
        Fixture fixture = BuildFixture("ai_execute_kill_probability");
        SkillDef skill = BuildExecuteSkill();
        BattleUnitState source = BuildUnit("execute_source", "hostile", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = BuildUnit("execute_low_hp_target", "player", new Vector2I(1, 0), 100, 20);
        fixture.AddUnit(source);
        fixture.AddUnit(target);

        BattleAiScoreInput score = BuildScore(fixture, source, target, skill);

        _test.Eq(score.execute_kill_probability_basis_points, 5000, "DC11/WILL0 execute kill bps 应等于 50% 豁免失败率。");
        _test.True(score.execute_soul_fracture_applied, "低血 execute 应记录 soul fracture payoff。");
        _test.Eq(score.estimated_damage, 10, "AI execute 期望伤害应按 fatal damage * save failure 概率估算。");
        _test.True(
            score.save_estimates_by_target_id.ContainsKey(target.unit_id),
            "低血 execute 应暴露目标豁免估算。"
        );
    }

    private void TestExecuteImmunityZeroesKillProbabilityButKeepsSoulFractureValue()
    {
        Fixture fixture = BuildFixture("ai_execute_immunity");
        SkillDef skill = BuildExecuteSkill();
        BattleUnitState source = BuildUnit("execute_source", "hostile", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = BuildUnit("execute_immune_target", "player", new Vector2I(1, 0), 100, 20);
        target.save_advantage_tags.Add("execute_immunity");
        fixture.AddUnit(source);
        fixture.AddUnit(target);

        BattleAiScoreInput score = BuildScore(fixture, source, target, skill);

        _test.Eq(score.execute_kill_probability_basis_points, 0, "execute_immunity 应把 AI kill bps 归零。");
        _test.True(score.execute_soul_fracture_applied, "execute_immunity 仍会留下 soul fracture 收益。");
        _test.Eq(score.estimated_damage, 0, "execute_immunity 不应贡献 HP 伤害期望。");
        _test.True(score.estimated_control_count > 0, "soul fracture 应贡献受限控制收益。");
    }

    private void TestDeathProtectionReducesKillProbability()
    {
        Fixture fixture = BuildFixture("ai_execute_death_protection");
        SkillDef skill = BuildExecuteSkill();
        BattleUnitState source = BuildUnit("execute_source", "hostile", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = BuildUnit("execute_protected_target", "player", new Vector2I(1, 0), 100, 20);
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "divine_refusal",
                duration = 60,
                death_prevention_priority = 900,
            }
        );
        fixture.AddUnit(source);
        fixture.AddUnit(target);

        BattleAiScoreInput score = BuildScore(fixture, source, target, skill);

        _test.Eq(score.execute_kill_probability_basis_points, 0, "足够高权威的死亡保护应把 AI kill bps 降为 0。");
        _test.True(score.execute_soul_fracture_applied, "死亡保护拦截 fatal 后仍应计入 survivor soul fracture 收益。");
    }

    private void TestAiIgnoresPreviewPresentationText()
    {
        Fixture fixture = BuildFixture("ai_execute_ignores_presentation");
        SkillDef skill = BuildExecuteSkill();
        BattleUnitState source = BuildUnit("execute_source", "hostile", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = BuildUnit("execute_text_poison_target", "player", new Vector2I(1, 0), 100, 20);
        fixture.AddUnit(source);
        fixture.AddUnit(target);

        BattlePreview preview = BuildPreview(target);
        preview.AddLogLine("POISON_LOG kill 100%");
        preview.SetSaveBranchPreview(new GDictionary
        {
            ["summary_text"] = "POISON_BRANCH 命中率 100%",
            ["hit_chance_basis_points"] = 10000,
        });
        preview.SetDamagePreview(
            new BattleDamagePreviewRangeService.SkillDamagePreview(
                true,
                999,
                999,
                new List<BattleDamagePreviewRangeService.DamageEffectRange>()
            )
        );
        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(source),
            skill,
            BuildCommand(source, skill.skill_id, target),
            preview,
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(target)
        );

        _test.Eq(score.execute_kill_probability_basis_points, 5000, "AI kill bps 不应读取 preview 展示文案。");
        _test.Eq(score.estimated_damage, 10, "AI 伤害估值不应读取 preview damage 999。");
    }

    private static BattleAiScoreInput BuildScore(
        Fixture fixture,
        BattleUnitState source,
        BattleUnitState target,
        SkillDef skill
    )
    {
        return fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(source),
            skill,
            BuildCommand(source, skill.skill_id, target),
            BuildPreview(target),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(target)
        );
    }

    private static Fixture BuildFixture(string battleId) => new(battleId, new Vector2I(4, 2));

    private static SkillDef BuildExecuteSkill()
    {
        var skill = new SkillDef
        {
            skill_id = "test_ai_power_word_kill",
            display_name = "AI 测试律令死亡",
            combat_profile = new CombatSkillDef
            {
                skill_id = "test_ai_power_word_kill",
                target_mode = "unit",
                target_team_filter = "enemy",
                target_selection_mode = "single_unit",
                range_value = 5,
            },
        };
        skill.combat_profile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "execute",
                effect_target_team_filter = "enemy",
                save_dc_mode = "static",
                save_dc = 11,
                save_ability = "willpower",
                save_tag = "execute",
                damage_tag = "negative_energy",
                threshold_max_hp_ratio_percent = 20,
                threshold_level_anchor = 17,
                threshold_level_bonus_per_delta = 5,
                threshold_cap_max_hp_ratio_percent = 50,
                soul_fracture_duration_tu = 60,
                heal_multiplier_percent = 50,
                shield_gain_multiplier_percent = 50,
            }
        );
        return skill;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int maxHp,
        int currentHp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            current_hp = currentHp,
            current_ap = 2,
            current_mp = 100,
            current_stamina = 100,
            is_alive = currentHp > 0,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue("strength", 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("perception", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        unit.known_active_skill_ids.Add("test_ai_power_word_kill");
        unit.known_skill_level_map[new StringName("test_ai_power_word_kill")] = 17;
        unit.RefreshFootprint();
        return unit;
    }

    private static BattleCommand BuildCommand(
        BattleUnitState actor,
        StringName skillId,
        BattleUnitState target
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = actor.unit_id,
            skill_id = skillId,
            target_coord = target.coord,
            target_unit_id = target.unit_id,
        };
        command.AddTargetCoord(target.coord);
        command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private static BattlePreview BuildPreview(BattleUnitState target)
    {
        var preview = new BattlePreview { allowed = true };
        preview.AddTargetUnitId(target.unit_id);
        preview.AddTargetCoord(target.coord);
        return preview;
    }

    private static Dictionary<string, object> BuildPositionMetadata(BattleUnitState target) =>
        new(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = 0,
            ["desired_max_distance"] = 5,
            ["position_target_unit_id"] = target.unit_id,
        };

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
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private sealed class Fixture
    {
        public readonly BattleState State;
        public readonly BattleGridService GridService = new();
        public readonly BattleAiScoreService ScoreService = new();
        private readonly Dictionary<StringName, SkillDef> _skillDefs = new();

        public Fixture(string battleId, Vector2I mapSize)
        {
            State = BuildFlatState(battleId, mapSize);
        }

        public void AddUnit(BattleUnitState unit)
        {
            State.SetUnit(unit);
        }

        public BattleAiContext BuildContext(BattleUnitState actor)
        {
            var context = new BattleAiContext
            {
                state = State,
                grid_service = GridService,
                unit_state = actor,
            };
            context.SetSkillDefs(_skillDefs);
            return context;
        }
    }
}
