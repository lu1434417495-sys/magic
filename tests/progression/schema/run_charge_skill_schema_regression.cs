using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_charge_skill_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTypedTrapImmunityProjects();
        TestLegacyChargeParametersAreRejected();
        TestInvalidTrapImmunityLevelIsRejected();
        TestTypedPathStepAoeProjects();
        TestLegacyPathStepAoeParametersAreRejected();

        RequestTestExit(_test.Finish("Charge skill schema regression"));
    }

    private void TestTypedTrapImmunityProjects()
    {
        using CombatEffectDef effect = new()
        {
            effect_type = "charge",
            charge_trap_immunity_min_skill_level = 7,
        };
        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            effect,
            "test.charge.typed_trap_immunity"
        );

        _test.Eq(
            definition.ChargeTrapImmunityMinSkillLevel,
            7,
            "charge trap immunity level should project through the typed definition boundary."
        );
    }

    private void TestLegacyChargeParametersAreRejected()
    {
        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        using CombatEffectDef effect = new()
        {
            effect_type = "charge",
            @params = new GDictionary
            {
                ["skill_id"] = "charge",
                ["base_distance"] = 3,
                ["distance_by_level"] = new GDictionary { ["1"] = 4 },
                ["trap_immunity_level"] = 7,
                ["collision_base_damage"] = 10,
                ["collision_size_gap_damage"] = 10,
            },
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(errors, "legacy_charge", effect, "test_effect");
        string formattedErrors = string.Join(" | ", errors);

        foreach (string legacyKey in effect.@params.Keys)
        {
            _test.True(
                formattedErrors.Contains($"params.{legacyKey} is unsupported"),
                $"legacy charge parameter {legacyKey} should be rejected. errors={formattedErrors}"
            );
        }
    }

    private void TestInvalidTrapImmunityLevelIsRejected()
    {
        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        using CombatEffectDef effect = new()
        {
            effect_type = "charge",
            charge_trap_immunity_min_skill_level = -2,
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(errors, "invalid_charge", effect, "test_effect");

        _test.True(
            string.Join(" | ", errors).Contains(
                "charge_trap_immunity_min_skill_level must be -1 or a non-negative skill level"
            ),
            $"invalid typed charge trap immunity level should be rejected. errors={string.Join(" | ", errors)}"
        );
    }

    private void TestTypedPathStepAoeProjects()
    {
        using CombatEffectDef effect = new()
        {
            effect_type = "path_step_aoe",
            path_step_area_pattern = "diamond",
            path_step_radius = 2,
            path_step_log_label = "旋斩",
            repeat_hit_status_id = "staggered",
            repeat_hit_status_threshold = 4,
            repeat_hit_status_min_skill_level = 9,
            repeat_hit_status_power = 1,
            repeat_hit_status_duration_tu = 60,
            repeat_hit_status_log_template = "{target} 连续命中 {hit_count} 次。",
        };
        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            effect,
            "test.path_step_aoe.typed"
        );

        _test.Eq(definition.PathStepAreaPattern, (StringName)"diamond", "路径范围形状应投影为 typed definition。");
        _test.Eq(definition.PathStepRadius, 2, "路径范围半径应投影为 typed definition。");
        _test.Eq(definition.PathStepLogLabel, "旋斩", "路径攻击日志标签应投影为 typed definition。");
        _test.Eq(definition.RepeatHitStatusId, (StringName)"staggered", "连续命中状态应投影为 typed definition。");
        _test.Eq(definition.RepeatHitStatusThreshold, 4, "连续命中阈值应投影为 typed definition。");
        _test.Eq(definition.RepeatHitStatusMinSkillLevel, 9, "连续命中状态等级门槛应投影为 typed definition。");
        _test.Eq(definition.RepeatHitStatusDurationTu, 60, "连续命中状态持续时间应投影为 typed definition。");
    }

    private void TestLegacyPathStepAoeParametersAreRejected()
    {
        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        using CombatEffectDef effect = new()
        {
            effect_type = "path_step_aoe",
            @params = new GDictionary
            {
                ["apply_on_successful_step_only"] = true,
                ["path_step_log_label"] = "旧路径攻击",
                ["repeat_hit_status_id"] = "staggered",
                ["repeat_hit_status_threshold"] = 4,
                ["step_radius"] = 1,
                ["step_shape"] = "diamond",
            },
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_path_step_aoe",
            effect,
            "test_effect"
        );
        string formattedErrors = string.Join(" | ", errors);

        foreach (string legacyKey in effect.@params.Keys)
        {
            _test.True(
                formattedErrors.Contains($"params.{legacyKey} is unsupported"),
                $"legacy path-step parameter {legacyKey} should be rejected. errors={formattedErrors}"
            );
        }
    }
}
