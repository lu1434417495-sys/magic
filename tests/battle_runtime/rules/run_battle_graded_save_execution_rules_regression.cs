using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_graded_save_execution_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestImmuneSaveBecomesImmuneGrade();
        TestNaturalOneDowngradesFailureToCriticalFailure();
        TestNaturalTwentyUpgradesSuccessToCriticalSuccess();
        TestFailureThresholdUsesMaxOfFixedAndPercent();
        TestCriticalFailureThresholdUsesPercentOnly();
        TestAverageDiceDamageUsesDiceMean();
        TestGradeDistributionNormalAdvantageDisadvantage();
        TestGradeDistributionImmuneIsAllImmune();
        TestRollOverridesProduceDeterministicDistribution();

        Quit(_test.Finish("Battle graded save execution rules regression"));
    }

    private void TestImmuneSaveBecomesImmuneGrade()
    {
        BattleSaveResult immuneCriticalFailure = MakeSaveResult(
            immune: true,
            naturalRoll: 0,
            rollTotal: 0,
            dc: 15,
            degree: BattleSaveDegreeKind.CriticalFailure
        );

        _test.Eq(
            BattleGradedSaveExecutionRules.ResolveGrade(immuneCriticalFailure),
            GradedSaveExecutionGrade.Immune,
            "Immune save results must map to Immune before natural-roll or degree logic."
        );
    }

    private void TestNaturalOneDowngradesFailureToCriticalFailure()
    {
        BattleSaveDegreeKind degree = BattleSaveResolver.ResolveSaveDegree(
            naturalRoll: 1,
            rollTotal: 14,
            dc: 15
        );
        BattleSaveResult result = MakeSaveResult(
            immune: false,
            naturalRoll: 1,
            rollTotal: 14,
            dc: 15,
            degree: degree
        );

        _test.Eq(
            BattleGradedSaveExecutionRules.ResolveGrade(result),
            GradedSaveExecutionGrade.CriticalFailure,
            "Natural 1 should downgrade a failed save into the critical-failure grade."
        );
    }

    private void TestNaturalTwentyUpgradesSuccessToCriticalSuccess()
    {
        BattleSaveDegreeKind degree = BattleSaveResolver.ResolveSaveDegree(
            naturalRoll: 20,
            rollTotal: 20,
            dc: 15
        );
        BattleSaveResult result = MakeSaveResult(
            immune: false,
            naturalRoll: 20,
            rollTotal: 20,
            dc: 15,
            degree: degree
        );

        _test.Eq(
            BattleGradedSaveExecutionRules.ResolveGrade(result),
            GradedSaveExecutionGrade.CriticalSuccess,
            "Natural 20 should upgrade a successful save into the critical-success grade."
        );
    }

    private void TestFailureThresholdUsesMaxOfFixedAndPercent()
    {
        BattleGradedSaveExecutionProfile profile = ReadProfile();

        _test.Eq(
            BattleGradedSaveExecutionRules.ResolveFailureExecuteThreshold(profile, targetMaxHp: 120),
            50,
            "Failure execute threshold should use the fixed floor when it exceeds the HP percent."
        );
        _test.Eq(
            BattleGradedSaveExecutionRules.ResolveFailureExecuteThreshold(profile, targetMaxHp: 400),
            100,
            "Failure execute threshold should use the HP percent when it exceeds the fixed floor."
        );
    }

    private void TestCriticalFailureThresholdUsesPercentOnly()
    {
        BattleGradedSaveExecutionProfile profile = ReadProfile();

        _test.Eq(
            BattleGradedSaveExecutionRules.ResolveCriticalFailureExecuteThreshold(
                profile,
                targetMaxHp: 200
            ),
            70,
            "Critical-failure execute threshold should use only its max-HP percent."
        );
    }

    private void TestAverageDiceDamageUsesDiceMean()
    {
        _test.Eq(
            BattleGradedSaveExecutionRules.EstimateAverageDiceDamage(diceCount: 6, diceSides: 6),
            21,
            "Average damage for 6d6 should be six times the d6 mean."
        );
        _test.Eq(
            BattleGradedSaveExecutionRules.EstimateAverageDiceDamage(diceCount: 10, diceSides: 6),
            35,
            "Average damage for 10d6 should be ten times the d6 mean."
        );
    }

    private void TestGradeDistributionNormalAdvantageDisadvantage()
    {
        BattleUnitState source = MakeUnit("grade_distribution_source");
        BattleUnitState target = MakeUnit("grade_distribution_target");
        CombatEffectDef effect = MakeStaticSaveEffect(dc: 15);

        BattleGradedSaveGradeDistribution normal =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(source, target, effect);
        AssertDistribution(
            normal,
            immune: 0,
            criticalSuccess: 500,
            success: 2500,
            failure: 6500,
            criticalFailure: 500,
            "normal DC15"
        );

        target.save_advantage_tags.Add("illusion");
        BattleGradedSaveGradeDistribution advantage =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(source, target, effect);
        AssertDistribution(
            advantage,
            immune: 0,
            criticalSuccess: 975,
            success: 4125,
            failure: 4875,
            criticalFailure: 25,
            "advantage DC15"
        );

        target.save_advantage_tags.Clear();
        target.save_advantage_tags.Add("illusion_disadvantage");
        BattleGradedSaveGradeDistribution disadvantage =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(source, target, effect);
        AssertDistribution(
            disadvantage,
            immune: 0,
            criticalSuccess: 25,
            success: 875,
            failure: 8125,
            criticalFailure: 975,
            "disadvantage DC15"
        );
    }

    private void TestGradeDistributionImmuneIsAllImmune()
    {
        BattleUnitState source = MakeUnit("immune_distribution_source");
        BattleUnitState target = MakeUnit("immune_distribution_target");
        target.save_advantage_tags.Add("illusion_immunity");

        BattleGradedSaveGradeDistribution distribution =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(
                source,
                target,
                MakeStaticSaveEffect(dc: 40)
            );

        AssertDistribution(
            distribution,
            immune: 10000,
            criticalSuccess: 0,
            success: 0,
            failure: 0,
            criticalFailure: 0,
            "immune target"
        );
    }

    private void TestRollOverridesProduceDeterministicDistribution()
    {
        BattleUnitState source = MakeUnit("override_distribution_source");
        BattleUnitState target = MakeUnit("override_distribution_target");
        CombatEffectDef effect = MakeStaticSaveEffect(dc: 15);

        BattleGradedSaveGradeDistribution naturalOne =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(
                source,
                target,
                effect,
                BattleSaveContext.WithSaveRollOverride(1)
            );
        AssertDistribution(
            naturalOne,
            immune: 0,
            criticalSuccess: 0,
            success: 0,
            failure: 0,
            criticalFailure: 10000,
            "natural-one override"
        );

        target.save_advantage_tags.Add("illusion");
        BattleGradedSaveGradeDistribution selectedAdvantage =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(
                source,
                target,
                effect,
                BattleSaveContext.WithSaveRollOverrides(new[] { 2, 20 })
            );
        AssertDistribution(
            selectedAdvantage,
            immune: 0,
            criticalSuccess: 10000,
            success: 0,
            failure: 0,
            criticalFailure: 0,
            "advantage override should select the higher roll"
        );

        target.save_advantage_tags.Clear();
        target.save_advantage_tags.Add("illusion_disadvantage");
        BattleGradedSaveGradeDistribution selectedDisadvantage =
            BattleGradedSaveExecutionRules.EstimateGradeDistribution(
                source,
                target,
                effect,
                BattleSaveContext.WithSaveRollOverrides(new[] { 2, 20 })
            );
        AssertDistribution(
            selectedDisadvantage,
            immune: 0,
            criticalSuccess: 0,
            success: 0,
            failure: 10000,
            criticalFailure: 0,
            "disadvantage override should select the lower roll"
        );
    }

    private BattleGradedSaveExecutionProfile ReadProfile()
    {
        bool ok = BattleGradedSaveExecutionRules.TryReadPhantasmalKillProfile(
            MakePhantasmalKillEffect(),
            out BattleGradedSaveExecutionProfile profile,
            out string error
        );
        _test.True(ok, $"formal Phantasmal Kill profile should parse. error={error}");
        _test.Eq(profile.ProfileId, "phantasmal_kill", "profile id should be read as StringName.");
        return profile;
    }

    private void AssertDistribution(
        BattleGradedSaveGradeDistribution distribution,
        int immune,
        int criticalSuccess,
        int success,
        int failure,
        int criticalFailure,
        string label
    )
    {
        _test.Eq(distribution.ImmuneBasisPoints, immune, $"{label}: immune bps.");
        _test.Eq(
            distribution.CriticalSuccessBasisPoints,
            criticalSuccess,
            $"{label}: critical-success bps."
        );
        _test.Eq(distribution.SuccessBasisPoints, success, $"{label}: success bps.");
        _test.Eq(distribution.FailureBasisPoints, failure, $"{label}: failure bps.");
        _test.Eq(
            distribution.CriticalFailureBasisPoints,
            criticalFailure,
            $"{label}: critical-failure bps."
        );
        _test.Eq(
            distribution.ImmuneBasisPoints
                + distribution.CriticalSuccessBasisPoints
                + distribution.SuccessBasisPoints
                + distribution.FailureBasisPoints
                + distribution.CriticalFailureBasisPoints,
            10000,
            $"{label}: grade distribution should total 10000 bps."
        );
    }

    private static BattleSaveResult MakeSaveResult(
        bool immune,
        int naturalRoll,
        int rollTotal,
        int dc,
        BattleSaveDegreeKind degree
    )
    {
        return new BattleSaveResult(
            HasSave: true,
            Immune: immune,
            Success: degree is BattleSaveDegreeKind.Success or BattleSaveDegreeKind.CriticalSuccess,
            NaturalRoll: naturalRoll,
            RollTotal: rollTotal,
            Dc: dc,
            Ability: "willpower",
            SaveTag: "illusion",
            AdvantageState: "normal",
            AbilityValue: 10,
            AbilityModifier: 0,
            Bonus: 0,
            Sources: Array.Empty<BattleSaveSource>()
        )
        {
            Degree = degree,
        };
    }

    private static CombatEffectDef MakePhantasmalKillEffect() => new()
    {
        effect_type = "graded_save_execute",
        effect_target_team_filter = "any",
        damage_tag = "psychic",
        save_dc_mode = "caster_spell",
        save_dc = 0,
        save_dc_source_ability = "intelligence",
        save_ability = "willpower",
        save_tag = "illusion",
        save_partial_on_success = false,
        @params = new GDictionary
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
        },
    };

    private static CombatEffectDef MakeStaticSaveEffect(int dc) => new()
    {
        effect_type = "graded_save_execute",
        save_dc_mode = "static",
        save_dc = dc,
        save_ability = "willpower",
        save_tag = "illusion",
    };

    private static BattleUnitState MakeUnit(StringName unitId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            control_mode = "manual",
            current_hp = 100,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        return unit;
    }
}
