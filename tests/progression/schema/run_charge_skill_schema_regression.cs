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
}
