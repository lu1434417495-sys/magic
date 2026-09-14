using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_dynamic_move_capacity_skill_range_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestProjectionValidationAndDynamicRange();
            RequestTestExit(_test.Finish("Dynamic move-capacity skill range regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(
                _test.Finish("Dynamic move-capacity skill range regression", 1)
            );
        }
    }

    private void TestProjectionValidationAndDynamicRange()
    {
        using CombatSkillDef invalidProfile = BuildProfile(
            "invalid_dynamic_range",
            rangeValue: 5,
            capacityMultiplier: -1
        );
        string validationErrors = string.Join(" | ", Validate(invalidProfile));
        _test.True(
            validationErrors.Contains(
                "range_move_point_capacity_multiplier must be >= 0",
                StringComparison.Ordinal
            ),
            $"负倍率必须在内容加载边界被拒绝。 errors={validationErrors}"
        );

        SkillDefinition dynamicSkill = ProjectSkill(
            BuildProfile(
                "dynamic_move_capacity_range",
                rangeValue: 99,
                capacityMultiplier: 3
            )
        );
        SkillDefinition ordinarySkill = ProjectSkill(
            BuildProfile(
                "ordinary_static_range",
                rangeValue: 7,
                capacityMultiplier: 0
            )
        );
        _test.Eq(
            dynamicSkill.CombatProfile.RangeMovePointCapacityMultiplier,
            3,
            "移动力容量倍率应投影到 immutable CombatSkillDefinition。"
        );

        BattleUnitState unit = BuildUnit();
        int initialCapacity = unit.GetMovePointCapacity();
        _test.Eq(
            BattleRangeService.ResolveConfiguredSkillRange(unit, dynamicSkill),
            initialCapacity * 3,
            "动态射程应忽略静态 range_value，按有效移动力容量乘倍率计算。"
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(unit, dynamicSkill),
            initialCapacity * 3,
            "正式有效射程入口应消费同一动态射程规则。"
        );
        _test.Eq(
            BattleRangeService.ResolveConfiguredSkillRange(unit, ordinarySkill),
            7,
            "倍率为零的普通技能应继续使用静态 range_value。"
        );

        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "dynamic_range_capacity_boost",
                source_unit_id = unit.unit_id,
                stacks = 1,
                duration = 60,
                move_point_capacity_delta = 2,
            }
        );
        int boostedCapacity = unit.GetMovePointCapacity();
        _test.Eq(
            boostedCapacity,
            initialCapacity + 2,
            "测试状态必须真实修改单位的有效移动力容量。"
        );
        _test.Eq(
            BattleRangeService.ResolveConfiguredSkillRange(unit, dynamicSkill),
            boostedCapacity * 3,
            "状态修改有效移动力容量后，动态射程必须立即随之变化。"
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(unit, ordinarySkill),
            7,
            "移动力容量变化不得改变倍率为零的普通技能射程。"
        );

        BattleTestFixture.DisposeBattleUnit(unit);
    }

    private static CombatSkillDef BuildProfile(
        StringName skillId,
        int rangeValue,
        int capacityMultiplier
    ) => new()
    {
        skill_id = skillId,
        target_mode = "ground",
        target_team_filter = "any",
        range_pattern = "single",
        range_value = rangeValue,
        range_move_point_capacity_multiplier = capacityMultiplier,
        area_pattern = "single",
        projectile_kind = "none",
    };

    private static SkillDefinition ProjectSkill(CombatSkillDef profile)
    {
        using (profile)
        {
            using SkillDef resource = new()
            {
                skill_id = profile.skill_id,
                display_name = profile.skill_id.ToString(),
                combat_profile = profile,
            };
            return SkillDefinition.FromDiagnosticFixture(resource);
        }
    }

    private static GStringArray Validate(CombatSkillDef profile)
    {
        var errors = new GStringArray();
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        validator.AppendCombatProfileValidationErrors(errors, profile.skill_id, profile);
        return errors;
    }

    private static BattleUnitState BuildUnit()
    {
        var unit = new BattleUnitState
        {
            unit_id = "dynamic_range_unit",
            source_member_id = "dynamic_range_unit",
            display_name = "动态射程测试单位",
        }.WithCombatResourcesForTest(hp: 20, ap: 2, isAlive: true);
        unit.SetAnchorCoord(Vector2I.Zero);
        return unit;
    }
}
