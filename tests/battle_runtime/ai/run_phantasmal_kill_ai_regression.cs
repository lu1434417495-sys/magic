using System;
using System.Collections.Generic;
using Godot;

public partial class run_phantasmal_kill_ai_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestLowHpEnemyExecuteScoresAboveHighHpEnemy();
            TestIllusionImmuneTargetContributesNoValue();
            TestSaveAdvantageAndDisadvantageChangeExpectedValue();
            TestAffectedAllyUpdatesFriendlyFireAndLethalRiskCounts();
            TestGroundSkillFriendlyFireLimitsAreSoftConfigured();
            TestGradedSaveExecuteClassifiesAsHostileGroundAoeAndOffense();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Phantasmal Kill AI regression"));
    }

    private void TestLowHpEnemyExecuteScoresAboveHighHpEnemy()
    {
        using Fixture fixture = BuildFixture("pk_ai_execute_value");
        SkillDefinition skill = BuildPhantasmalKillSkill(targetMode: "unit");
        BattleUnitState source = BuildUnit("pk_source", "hostile", new Vector2I(0, 0), 200, 200);
        BattleUnitState highHpEnemy = BuildUnit(
            "high_hp_enemy",
            "player",
            new Vector2I(1, 0),
            200,
            120
        );
        BattleUnitState lowHpEnemy = BuildUnit(
            "low_hp_enemy",
            "player",
            new Vector2I(2, 0),
            200,
            40
        );
        fixture.AddUnit(source);
        fixture.AddUnit(highHpEnemy);
        fixture.AddUnit(lowHpEnemy);

        BattleAiScoreInput highHpScore = BuildScore(fixture, source, skill, highHpEnemy);
        BattleAiScoreInput lowHpScore = BuildScore(fixture, source, skill, lowHpEnemy);

        _test.True(
            lowHpScore.execute_kill_probability_basis_points
                > highHpScore.execute_kill_probability_basis_points,
            "低 HP Phantasmal Kill 目标应获得更高 execute kill probability。"
        );
        _test.True(
            lowHpScore.total_score > highHpScore.total_score,
            "低 HP enemy execute 候选评分应高于高 HP enemy。"
        );
    }

    private void TestIllusionImmuneTargetContributesNoValue()
    {
        using Fixture fixture = BuildFixture("pk_ai_immune_noop");
        SkillDefinition skill = BuildPhantasmalKillSkill(targetMode: "unit");
        BattleUnitState source = BuildUnit("pk_source", "hostile", new Vector2I(0, 0), 200, 200);
        BattleUnitState immuneTarget = BuildUnit(
            "illusion_immune_enemy",
            "player",
            new Vector2I(1, 0),
            200,
            40
        );
        immuneTarget.save_advantage_tags.Add("illusion_immunity");
        fixture.AddUnit(source);
        fixture.AddUnit(immuneTarget);

        BattleAiScoreInput score = BuildScore(fixture, source, skill, immuneTarget);

        _test.Eq(score.estimated_damage, 0, "Illusion-immune target should add no damage value.");
        _test.Eq(
            score.execute_kill_probability_basis_points,
            0,
            "Illusion-immune target should add no execute value."
        );
        _test.Eq(
            score.estimated_control_count,
            0,
            "Illusion-immune target should add no control value."
        );
        _test.Eq(score.effective_target_count, 0, "Illusion-immune target should be a no-op.");
    }

    private void TestSaveAdvantageAndDisadvantageChangeExpectedValue()
    {
        using Fixture fixture = BuildFixture("pk_ai_save_advantage");
        SkillDefinition skill = BuildPhantasmalKillSkill(targetMode: "unit");
        BattleUnitState source = BuildUnit("pk_source", "hostile", new Vector2I(0, 0), 200, 200);
        BattleUnitState normalTarget = BuildUnit("normal_enemy", "player", new Vector2I(1, 0), 200, 120);
        BattleUnitState advantageTarget = BuildUnit(
            "advantage_enemy",
            "player",
            new Vector2I(2, 0),
            200,
            120
        );
        advantageTarget.save_advantage_tags.Add("illusion");
        BattleUnitState disadvantageTarget = BuildUnit(
            "disadvantage_enemy",
            "player",
            new Vector2I(3, 0),
            200,
            120
        );
        disadvantageTarget.save_advantage_tags.Add("illusion_disadvantage");
        fixture.AddUnit(source);
        fixture.AddUnit(normalTarget);
        fixture.AddUnit(advantageTarget);
        fixture.AddUnit(disadvantageTarget);

        BattleAiScoreInput normalScore = BuildScore(fixture, source, skill, normalTarget);
        BattleAiScoreInput advantageScore = BuildScore(fixture, source, skill, advantageTarget);
        BattleAiScoreInput disadvantageScore = BuildScore(
            fixture,
            source,
            skill,
            disadvantageTarget
        );

        _test.True(
            advantageScore.estimated_damage < normalScore.estimated_damage,
            "Save advantage should lower Phantasmal Kill expected damage."
        );
        _test.True(
            disadvantageScore.estimated_damage > normalScore.estimated_damage,
            "Save disadvantage should raise Phantasmal Kill expected damage."
        );
    }

    private void TestAffectedAllyUpdatesFriendlyFireAndLethalRiskCounts()
    {
        using Fixture fixture = BuildFixture("pk_ai_friendly_counts");
        SkillDefinition skill = BuildPhantasmalKillSkill(targetMode: "ground");
        BattleUnitState source = BuildUnit("pk_source", "hostile", new Vector2I(0, 0), 200, 200);
        BattleUnitState exposedAlly = BuildUnit("exposed_ally", "hostile", new Vector2I(1, 0), 200, 120);
        BattleUnitState lethalAlly = BuildUnit("lethal_ally", "hostile", new Vector2I(2, 0), 200, 40);
        fixture.AddUnit(source);
        fixture.AddUnit(exposedAlly);
        fixture.AddUnit(lethalAlly);

        BattleAiScoreInput exposedScore = BuildGroundScore(
            fixture,
            source,
            skill,
            new[] { exposedAlly }
        );
        BattleAiScoreInput lethalScore = BuildGroundScore(
            fixture,
            source,
            skill,
            new[] { lethalAlly }
        );

        _test.Eq(
            exposedScore.estimated_friendly_fire_target_count,
            1,
            "Affected non-immune ally should increment friendly-fire target count."
        );
        _test.Eq(
            exposedScore.estimated_friendly_lethal_target_count,
            0,
            "Non-lethal ally exposure should not increment friendly lethal count."
        );
        _test.Eq(
            exposedScore.friendly_fire_reject_reason,
            "",
            "Ordinary Phantasmal Kill ally exposure should not set hard reject reason."
        );
        _test.Eq(
            lethalScore.estimated_friendly_fire_target_count,
            1,
            "Lethal-risk ally should still count as friendly-fire exposure."
        );
        _test.Eq(
            lethalScore.estimated_friendly_lethal_target_count,
            1,
            "Ally inside failure/critical-failure execute threshold should increment friendly lethal count."
        );
        _test.Eq(
            lethalScore.friendly_fire_reject_reason,
            "",
            "Friendly lethal risk should remain soft-configurable, not a hard reject reason."
        );
    }

    private void TestGroundSkillFriendlyFireLimitsAreSoftConfigured()
    {
        UseGroundSkillActionDefinition defaultDefinition = BuildGroundSkillActionDefinition();
        UseGroundSkillActionDefinition softConfiguredDefinition =
            BuildGroundSkillActionDefinition(maximumFriendlyFireTargetCount: 1);
        UseGroundSkillActionDefinition lethalConfiguredDefinition =
            BuildGroundSkillActionDefinition(
                maximumFriendlyFireTargetCount: 1,
                allowFriendlyLethal: true
            );

        BattleAiScoreInput exposedAlly = new()
        {
            effective_target_count = 1,
            estimated_friendly_fire_target_count = 1,
        };
        BattleAiScoreInput lethalAlly = new()
        {
            effective_target_count = 1,
            estimated_friendly_fire_target_count = 1,
            estimated_friendly_lethal_target_count = 1,
        };
        BattleAiScoreInput enemyOnly = new()
        {
            effective_target_count = 1,
            enemy_target_count = 1,
        };
        var evaluator = new BattleAiGroundSkillActionEvaluator();

        _test.False(
            evaluator.PassesFriendlyFireLimits(defaultDefinition, exposedAlly),
            "Default UseGroundSkillAction should reject locations exposing any ally."
        );
        _test.False(
            evaluator.PassesFriendlyFireLimits(defaultDefinition, lethalAlly),
            "Default UseGroundSkillAction should reject locations with friendly lethal risk."
        );
        _test.True(
            evaluator.PassesFriendlyFireLimits(softConfiguredDefinition, exposedAlly),
            "maximum_friendly_fire_target_count should allow configured non-lethal ally exposure."
        );
        _test.True(
            evaluator.PassesFriendlyFireLimits(lethalConfiguredDefinition, lethalAlly),
            "allow_friendly_lethal should allow friendly lethal risk when count limits pass."
        );
        _test.True(
            evaluator.PassesFriendlyFireLimits(defaultDefinition, enemyOnly),
            "Enemy-only Phantasmal Kill location should remain selectable."
        );
        _test.True(
            evaluator.PassesMinimumEffectiveTargetOrGroundControl(
                defaultDefinition,
                enemyOnly
            ),
            "Enemy-only Phantasmal Kill location should satisfy minimum effective target limits."
        );
    }

    private static UseGroundSkillActionDefinition BuildGroundSkillActionDefinition(
        int maximumFriendlyFireTargetCount = 0,
        bool allowFriendlyLethal = false
    ) =>
        new(
            "",
            "",
            "positioning",
            Array.Empty<StringName>(),
            1,
            false,
            false,
            1,
            0,
            maximumFriendlyFireTargetCount,
            allowFriendlyLethal,
            0,
            0,
            -1,
            -1,
            ""
        );

    private void TestGradedSaveExecuteClassifiesAsHostileGroundAoeAndOffense()
    {
        SkillDefinition groundSkill = BuildPhantasmalKillSkill(targetMode: "ground");
        BattleAiSkillAffordanceRecord record =
            new BattleAiSkillAffordanceClassifier()
                .ClassifySkill(groundSkill, 1);

        AssertListHas(record.effect_roles, "damage", "GradedSaveExecute should classify as damage.");
        AssertListHas(record.effect_roles, "control", "GradedSaveExecute should classify as control.");
        AssertListHas(record.effect_roles, "execute", "GradedSaveExecute should classify as execute.");
        AssertListHas(
            record.affordances,
            "ground_hostile.aoe",
            "Ground GradedSaveExecute should classify as hostile ground AOE."
        );
        AssertListHas(
            record.action_families,
            "use_ground_skill",
            "Ground GradedSaveExecute should generate ground skill action family."
        );
        _test.Eq(
            BattleAiActionIntent.InferForSkill(groundSkill),
            BattleAiActionIntent.Offense,
            "GradedSaveExecute should infer offensive intent even with target filter any."
        );
    }

    private static BattleAiScoreInput BuildScore(
        Fixture fixture,
        BattleUnitState source,
        SkillDefinition skill,
        BattleUnitState target
    )
    {
        return fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(source),
            skill,
            BuildCommand(source, skill.SkillId, target.coord, new[] { target.unit_id }),
            BuildPreview(target.coord, new[] { target.unit_id }),
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(target.coord)
        );
    }

    private static BattleAiScoreInput BuildGroundScore(
        Fixture fixture,
        BattleUnitState source,
        SkillDefinition skill,
        IReadOnlyList<BattleUnitState> targets
    )
    {
        var targetIds = new List<StringName>();
        Vector2I anchor = targets.Count > 0 ? targets[0].coord : source.coord;
        foreach (BattleUnitState target in targets)
        {
            targetIds.Add(target.unit_id);
        }
        return fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(source),
            skill,
            BuildCommand(source, skill.SkillId, anchor, targetIds),
            BuildPreview(anchor, targetIds),
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(anchor)
        );
    }

    private static Fixture BuildFixture(string battleId) => new(battleId, new Vector2I(6, 2));

    private static SkillDefinition BuildPhantasmalKillSkill(StringName targetMode) =>
        TestSkillDefinitionProjection.BuildSkill(
            "test_phantasmal_kill_ai",
            "Test Phantasmal Kill AI",
            TestSkillDefinitionProjection.BuildCombatProfile(
                "test_phantasmal_kill_ai",
                effects: new[] { BuildPhantasmalKillEffect() },
                targetMode: targetMode,
                targetTeamFilter: "any",
                targetSelectionMode: targetMode == "ground"
                    ? BattleTypedNames.ToStringName(BattleTargetSelectionMode.SingleCoord)
                    : BattleTypedNames.ToStringName(BattleTargetSelectionMode.SingleUnit),
                rangePattern: "fixed",
                rangeValue: 5,
                areaPattern: targetMode == "ground" ? (StringName)"square" : default,
                areaValue: targetMode == "ground" ? 1 : 0
            )
        );

    private static CombatEffectDefinition BuildPhantasmalKillEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "graded_save_execute",
            effectTargetTeamFilter: "any",
            damageTag: "psychic",
            saveDcMode: "static",
            saveDc: 15,
            saveDcSourceAbility: "intelligence",
            saveAbility: "willpower",
            saveTag: "illusion",
            savePartialOnSuccess: false,
            parameters: new Dictionary<string, object>
            {
                ["profile_id"] = "phantasmal_kill",
                ["failure_execute_threshold_fixed"] = 50,
                ["failure_execute_threshold_max_hp_percent"] = 25,
                ["failure_damage_dice_count"] = 6,
                ["failure_damage_dice_sides"] = 6,
                ["failure_frightened_duration_tu"] = 60,
                ["failure_reaction_lock_duration_tu"] = 30,
                ["critical_failure_execute_threshold_max_hp_percent"] = 35,
                ["critical_failure_damage_dice_count"] = 10,
                ["critical_failure_damage_dice_sides"] = 6,
                ["critical_failure_frightened_duration_tu"] = 90,
                ["critical_failure_stunned_duration_tu"] = 30,
                ["success_aftershock_duration_tu"] = 30,
            }
        );

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
        unit.known_active_skill_ids.Add("test_phantasmal_kill_ai");
        unit.known_skill_level_map[new StringName("test_phantasmal_kill_ai")] = 1;
        unit.RefreshFootprint();
        return unit;
    }

    private static BattleCommand BuildCommand(
        BattleUnitState actor,
        StringName skillId,
        Vector2I targetCoord,
        IEnumerable<StringName> targetUnitIds
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = actor.unit_id,
            skill_id = skillId,
            target_coord = targetCoord,
            target_unit_id = "",
        };
        command.AddTargetCoord(targetCoord);
        foreach (StringName targetUnitId in targetUnitIds ?? Array.Empty<StringName>())
        {
            command.AddTargetUnitId(targetUnitId);
            if (command.target_unit_id == "")
            {
                command.target_unit_id = targetUnitId;
            }
        }
        return command;
    }

    private static BattlePreview BuildPreview(
        Vector2I targetCoord,
        IEnumerable<StringName> targetUnitIds
    )
    {
        var preview = new BattlePreview
        {
            allowed = true,
            resolved_anchor_coord = targetCoord,
        };
        preview.AddTargetCoord(targetCoord);
        foreach (StringName targetUnitId in targetUnitIds ?? Array.Empty<StringName>())
        {
            preview.AddTargetUnitId(targetUnitId);
        }
        return preview;
    }

    private static Dictionary<string, object> BuildPositionMetadata(Vector2I targetCoord) =>
        new(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = 0,
            ["desired_max_distance"] = 5,
            ["position_target_coord"] = targetCoord,
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

    private void AssertListHas(IEnumerable<StringName> values, StringName expected, string message)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
            {
                return;
            }
        }
        _test.Fail(message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BattleState State;
        public readonly BattleGridService GridService = new();
        public readonly BattleAiScoreService ScoreService = new();

        public Fixture(string battleId, Vector2I mapSize)
        {
            State = BuildFlatState(battleId, mapSize);
        }

        public void Dispose()
        {
            ScoreService.Dispose();
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
            context.SetSkillDefinitions(new Dictionary<StringName, SkillDefinition>());
            return context;
        }
    }
}
