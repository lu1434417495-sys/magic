using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_role_threat_scoring_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestMultiUnitSkillScoresRoleThreatTargetGroups();
            TestGroundSkillScoresRoleThreatAreaTargets();
            TestSkillScorePrioritizesLethalThreatTargets();
            TestLowHpBonusUsesFormalParamOnly();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI role threat scoring regression"));
    }

    private void TestMultiUnitSkillScoresRoleThreatTargetGroups()
    {
        using Fixture fixture = BuildFixture("multi_unit_role_threat_scoring");
        SkillDefinition attackSkill = BuildSkill(
            "archer_multishot_probe",
            "Multishot Probe",
            BuildDamageEffect(10)
        );
        SkillDefinition healerRoleSkill = BuildHealSkill();
        SkillDefinition normalRoleSkill = BuildSkill(
            "warrior_heavy_strike_probe",
            "Heavy Strike Probe",
            BuildDamageEffect(3)
        );

        fixture.AddSkill(attackSkill);
        fixture.AddSkill(healerRoleSkill);
        fixture.AddSkill(normalRoleSkill);

        BattleUnitState actor = BuildUnit("role_threat_multishot_archer", "hostile", new Vector2I(1, 2));
        BattleUnitState normalA = BuildUnit("multi_role_normal_a", "player", new Vector2I(4, 1));
        BattleUnitState normalB = BuildUnit("multi_role_normal_b", "player", new Vector2I(4, 2));
        BattleUnitState healer = BuildUnit("multi_role_healer", "player", new Vector2I(4, 3));
        normalA.AddKnownActiveSkill(normalRoleSkill.SkillId);
        normalB.AddKnownActiveSkill(normalRoleSkill.SkillId);
        healer.AddKnownActiveSkill(healerRoleSkill.SkillId);
        fixture.AddUnit(actor);
        fixture.AddUnit(normalA);
        fixture.AddUnit(normalB);
        fixture.AddUnit(healer);

        BattleAiContext context = fixture.BuildContext(actor);
        BattleAiScoreInput normalScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            attackSkill,
            BuildCommand(actor, attackSkill.SkillId, normalA.GetAnchorCoord()),
            BuildPreview(normalA, normalB),
            new[] { attackSkill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(normalA, 3, 6)
        );
        BattleAiScoreInput threatScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            attackSkill,
            BuildCommand(actor, attackSkill.SkillId, normalA.GetAnchorCoord()),
            BuildPreview(normalA, healer),
            new[] { attackSkill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(normalA, 3, 6)
        );

        _test.True(normalScore != null && threatScore != null, "multi-unit 威胁评分应生成两个合法 score input。");
        if (normalScore == null || threatScore == null)
        {
            return;
        }
        _test.Eq(normalScore.target_count, threatScore.target_count, "两个 multi-unit 候选应命中相同目标数。");
        _test.True(
            threatScore.target_priority_score > normalScore.target_priority_score,
            "multi-unit 技能应对包含治疗威胁目标的组合产生更高 target_priority_score。"
        );
        _test.True(
            threatScore.total_score > normalScore.total_score,
            "multi-unit 技能在命中数相同时，应因目标威胁优先包含治疗单位的组合。"
        );
    }

    private void TestGroundSkillScoresRoleThreatAreaTargets()
    {
        using Fixture fixture = BuildFixture("ground_role_threat_scoring");
        SkillDefinition fireballSkill = BuildSkill(
            "mage_fireball_probe",
            "Fireball Probe",
            BuildDamageEffect(10)
        );
        SkillDefinition healerRoleSkill = BuildHealSkill();
        SkillDefinition normalRoleSkill = BuildSkill(
            "warrior_heavy_strike_probe",
            "Heavy Strike Probe",
            BuildDamageEffect(3)
        );

        fixture.AddSkill(fireballSkill);
        fixture.AddSkill(healerRoleSkill);
        fixture.AddSkill(normalRoleSkill);

        BattleUnitState actor = BuildUnit("role_threat_fireballer", "hostile", new Vector2I(1, 3));
        BattleUnitState normalA = BuildUnit("ground_role_normal_a", "player", new Vector2I(4, 1));
        BattleUnitState normalB = BuildUnit("ground_role_normal_b", "player", new Vector2I(4, 2));
        BattleUnitState healer = BuildUnit("ground_role_healer", "player", new Vector2I(4, 4));
        normalA.AddKnownActiveSkill(normalRoleSkill.SkillId);
        normalB.AddKnownActiveSkill(normalRoleSkill.SkillId);
        healer.AddKnownActiveSkill(healerRoleSkill.SkillId);
        fixture.AddUnit(actor);
        fixture.AddUnit(normalA);
        fixture.AddUnit(normalB);
        fixture.AddUnit(healer);

        BattleAiContext context = fixture.BuildContext(actor);
        BattleAiScoreInput normalScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            fireballSkill,
            BuildCommand(actor, fireballSkill.SkillId, normalB.GetAnchorCoord()),
            BuildPreview(normalA, normalB),
            new[] { fireballSkill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(null, 3, 4)
        );
        BattleAiScoreInput threatScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            fireballSkill,
            BuildCommand(actor, fireballSkill.SkillId, healer.GetAnchorCoord()),
            BuildPreview(normalA, healer),
            new[] { fireballSkill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(null, 3, 4)
        );

        _test.True(normalScore != null && threatScore != null, "范围威胁评分应生成两个合法 score input。");
        if (normalScore == null || threatScore == null)
        {
            return;
        }
        _test.Eq(normalScore.target_count, threatScore.target_count, "两个范围候选应命中相同目标数。");
        _test.True(
            threatScore.target_priority_score > normalScore.target_priority_score,
            "范围技能应对覆盖治疗威胁目标的地格产生更高 target_priority_score。"
        );
        _test.True(
            threatScore.total_score > normalScore.total_score,
            "范围技能在命中数相同时，应因目标威胁优先覆盖治疗单位。"
        );
    }

    private void TestSkillScorePrioritizesLethalThreatTargets()
    {
        using Fixture fixture = BuildFixture("lethal_threat_scoring");
        SkillDefinition fireballSkill = BuildSkill(
            "mage_fireball_probe",
            "Fireball Probe",
            BuildDamageEffect(15)
        );
        SkillDefinition chainSkill = BuildSkill(
            "mage_chain_lightning_probe",
            "Chain Probe",
            BuildDamageEffect(15)
        );
        SkillDefinition rangedThreatSkill = BuildRangedThreatSkill();
        fixture.AddSkill(fireballSkill);
        fixture.AddSkill(chainSkill);
        fixture.AddSkill(rangedThreatSkill);

        BattleUnitState actor = BuildUnit("lethal_threat_mage", "hostile", new Vector2I(1, 2));
        BattleUnitState archerA = BuildUnit("lethal_threat_archer_a", "player", new Vector2I(5, 2), hp: 10);
        BattleUnitState archerB = BuildUnit("lethal_threat_archer_b", "player", new Vector2I(5, 4), hp: 10);
        archerA.AddKnownActiveSkill(rangedThreatSkill.SkillId);
        archerB.AddKnownActiveSkill(rangedThreatSkill.SkillId);
        fixture.AddUnit(actor);
        fixture.AddUnit(archerA);
        fixture.AddUnit(archerB);

        BattleAiContext context = fixture.BuildContext(actor);
        BattleAiScoreInput fireballScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            fireballSkill,
            BuildCommand(actor, fireballSkill.SkillId, archerA.GetAnchorCoord()),
            BuildPreview(archerA, archerB),
            new[] { fireballSkill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(null, 4, 5)
        );
        BattleAiScoreInput chainScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            chainSkill,
            BuildCommand(actor, chainSkill.SkillId, archerA.GetAnchorCoord(), archerA),
            BuildPreview(archerA),
            new[] { chainSkill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(archerA, 4, 5)
        );

        _test.True(fireballScore != null && chainScore != null, "击杀威胁评分应生成两个合法 score input。");
        if (fireballScore == null || chainScore == null)
        {
            return;
        }
        _test.True(
            fireballScore.estimated_lethal_threat_target_count >= 2,
            "范围技能应识别会死亡的多个威胁目标。"
        );
        _test.Eq(
            chainScore.estimated_lethal_threat_target_count,
            1,
            "单体技能只应识别一个会死亡的威胁目标。"
        );
        _test.True(
            fireballScore.total_score > chainScore.total_score,
            "当范围技能能击杀更多威胁单位时，应优先杀人而不是先打单体。"
        );
    }

    private void TestLowHpBonusUsesFormalParamOnly()
    {
        using Fixture fixture = BuildFixture("low_hp_bonus_scoring");
        BattleUnitState actor = BuildUnit("low_hp_bonus_actor", "hostile", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("ai_score_low_hp_target", "player", new Vector2I(2, 1), hp: 30);
        target.SetCurrentHp(18);
        fixture.AddUnit(actor);
        fixture.AddUnit(target);

        SkillDefinition formalSkill = BuildSkill(
            "formal_low_hp_bonus_probe",
            "Formal Low HP Bonus",
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                power: 10,
                bonusCondition: "target_low_hp",
                hpRatioThresholdPercent: 70,
                bonusDamageDiceCount: 2,
                bonusDamageDiceSides: 1
            )
        );
        SkillDefinition legacySkill = BuildSkill(
            "legacy_low_hp_bonus_probe",
            "Legacy Low HP Bonus",
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                power: 10,
                bonusCondition: "target_low_hp",
                parameters: new Dictionary<string, object> { ["low_hp_ratio"] = 0.7 },
                bonusDamageDiceCount: 2,
                bonusDamageDiceSides: 1
            )
        );
        fixture.AddSkill(formalSkill);
        fixture.AddSkill(legacySkill);

        BattleAiContext context = fixture.BuildContext(actor);
        BattleAiScoreInput formalScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            formalSkill,
            BuildCommand(actor, formalSkill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { formalSkill.CombatProfile.EffectDefinitions[0] },
            new Dictionary<string, object>(StringComparer.Ordinal)
        );
        BattleAiScoreInput legacyScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            legacySkill,
            BuildCommand(actor, legacySkill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { legacySkill.CombatProfile.EffectDefinitions[0] },
            new Dictionary<string, object>(StringComparer.Ordinal)
        );

        _test.True(formalScore != null && legacyScore != null, "低血追加骰评分应生成两个合法 score input。");
        if (formalScore == null || legacyScore == null)
        {
            return;
        }
        _test.True(
            formalScore.estimated_damage > legacyScore.estimated_damage,
            "AI 评分应读取正式 hp_ratio_threshold_percent 判定低血追加骰。"
        );
        _test.Eq(
            legacyScore.estimated_damage,
            10,
            "AI 评分不应再读取旧 low_hp_ratio alias。"
        );
    }

    private static BattleState BuildState(string battleId)
    {
        return new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = new Vector2I(8, 6),
            timeline = new BattleTimelineState(),
        };
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
        }.WithCombatResourcesForTest(
            hp: hp,
            mp: 100,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue("strength", 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("perception", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        string displayName,
        params CombatEffectDefinition[] effects
    ) =>
        BuildSkill(skillId, displayName, targetTeamFilter: default, rangeValue: 4, effects: effects);

    private static SkillDefinition BuildSkill(
        StringName skillId,
        string displayName,
        StringName targetTeamFilter,
        int rangeValue,
        params CombatEffectDefinition[] effects
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName,
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects ?? Array.Empty<CombatEffectDefinition>(),
                targetTeamFilter: targetTeamFilter,
                rangeValue: rangeValue
            )
        );
    }

    private static SkillDefinition BuildHealSkill() =>
        BuildSkill(
            "mage_temporal_rewind_probe",
            "Temporal Rewind Probe",
            targetTeamFilter: "ally",
            rangeValue: 4,
            effects: new[] { TestSkillDefinitionProjection.BuildEffect("heal", power: 8) }
        );

    private static SkillDefinition BuildRangedThreatSkill() =>
        BuildSkill(
            "archer_aimed_shot_probe",
            "Aimed Shot Probe",
            targetTeamFilter: default,
            rangeValue: 5,
            effects: new[] { BuildDamageEffect(8) }
        );

    private static CombatEffectDefinition BuildDamageEffect(int power) =>
        TestSkillDefinitionProjection.BuildEffect("damage", power: power);

    private static BattleCommand BuildCommand(
        BattleUnitState actor,
        StringName skillId,
        Vector2I targetCoord,
        BattleUnitState targetUnit = null
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = actor.unit_id,
            skill_id = skillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        if (targetUnit != null)
        {
            command.target_unit_id = targetUnit.unit_id;
            command.AddTargetUnitId(targetUnit.unit_id);
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
            preview.AddTargetUnitId(target.unit_id);
            preview.AddTargetCoord(target.GetAnchorCoord());
        }
        return preview;
    }

    private static Dictionary<string, object> BuildPositionMetadata(
        BattleUnitState positionTarget,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
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

    private sealed class Fixture : IDisposable
    {
        public readonly BattleState State;
        public readonly BattleAiScoreService ScoreService = new();
        private readonly Dictionary<StringName, SkillDefinition> _skillDefinitions = new();

        public Fixture(string battleId)
        {
            State = BuildState(battleId);
        }

        public void Dispose()
        {
            ScoreService.Dispose();
        }

        public void AddSkill(SkillDefinition skillDefinition)
        {
            if (skillDefinition != null && skillDefinition.SkillId != "")
            {
                _skillDefinitions[skillDefinition.SkillId] = skillDefinition;
            }
        }

        public void AddUnit(BattleUnitState unit)
        {
            if (unit == null || unit.unit_id == "")
            {
                return;
            }
            State.SetUnit(unit);
        }

        public BattleAiContext BuildContext(BattleUnitState actor)
        {
            var context = new BattleAiContext
            {
                state = State,
                unit_state = actor,
            };
            context.SetSkillDefinitions(_skillDefinitions);
            return context;
        }
    }

    private static Fixture BuildFixture(string battleId) => new(battleId);
}
