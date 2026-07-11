using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_combat_effect_equipment_durability_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestProjectsTypedEquipmentDurabilitySlotWeights();
        TestLegacyParamsSlotWeightMapIsNotProjected();
        TestSkillContentValidationUsesTypedSlotWeights();

        RequestTestExit(_test.Finish("Combat effect equipment durability schema regression"));
    }

    private void TestProjectsTypedEquipmentDurabilitySlotWeights()
    {
        CombatEffectDef resource = BuildDurabilityEffectResource();
        resource.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "main_hand", weight = 30 },
                new() { slot_id = "off_hand", weight = 20 },
            };

        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            resource,
            "test.combat_effect_durability.typed_slot_weights"
        );

        _test.Eq(
            definition.EquipmentDurabilitySlotWeights.Count,
            2,
            "CombatEffectDefinition should project typed equipment durability slot weights."
        );
        _test.Eq(
            definition.EquipmentDurabilitySlotWeights[0].SlotId,
            new StringName("main_hand"),
            "first projected slot weight should preserve slot id."
        );
        _test.Eq(
            definition.EquipmentDurabilitySlotWeights[0].Weight,
            30,
            "first projected slot weight should preserve positive weight."
        );
        _test.Eq(
            definition.EquipmentDurabilitySlotWeights[1].SlotId,
            new StringName("off_hand"),
            "second projected slot weight should preserve slot id."
        );
        _test.Eq(
            definition.EquipmentDurabilitySlotWeights[1].Weight,
            20,
            "second projected slot weight should preserve positive weight."
        );
    }

    private void TestLegacyParamsSlotWeightMapIsNotProjected()
    {
        CombatEffectDef resource = BuildDurabilityEffectResource();
        resource.@params["slot_weight_map"] = new GDictionary
        {
            [new StringName("main_hand")] = 99,
        };

        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            resource,
            "test.combat_effect_durability.legacy_param"
        );

        _test.Eq(
            definition.EquipmentDurabilitySlotWeights.Count,
            0,
            "legacy params.slot_weight_map should not project into typed durability slot weights."
        );
    }

    private void TestSkillContentValidationUsesTypedSlotWeights()
    {
        using SkillContentRegistry registry = new(new TestContentResourceLoader());
        using CombatEffectDef valid = BuildDurabilityEffectResource();
        valid.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "main_hand", weight = 30 },
                new() { slot_id = "off_hand", weight = 20 },
            };
        GStringArray validErrors = new();
        registry.AppendEffectValidationErrors(
            validErrors,
            "typed_durability_weights",
            valid,
            "test_effect"
        );
        _test.Eq(
            validErrors.Count,
            0,
            $"valid typed durability slot weights should pass. errors={string.Join(" | ", validErrors)}"
        );

        using CombatEffectDef legacy = BuildDurabilityEffectResource();
        legacy.@params["slot_weight_map"] = new GDictionary
        {
            [new StringName("main_hand")] = 30,
        };
        GStringArray legacyErrors = new();
        registry.AppendEffectValidationErrors(
            legacyErrors,
            "legacy_durability_weight_map",
            legacy,
            "test_effect"
        );
        _test.True(
            string.Join(" | ", legacyErrors).Contains(
                "params.slot_weight_map is unsupported; use equipment_durability_slot_weights"
            ),
            $"legacy params.slot_weight_map should be rejected. errors={string.Join(" | ", legacyErrors)}"
        );

        using CombatEffectDef invalid = BuildDurabilityEffectResource();
        invalid.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "main_hand", weight = 30 },
                new() { slot_id = "main_hand", weight = 20 },
                new() { slot_id = "unknown_slot", weight = 10 },
                new() { slot_id = "off_hand", weight = 0 },
            };
        GStringArray invalidErrors = new();
        registry.AppendEffectValidationErrors(
            invalidErrors,
            "invalid_durability_weights",
            invalid,
            "test_effect"
        );
        string formattedInvalidErrors = string.Join(" | ", invalidErrors);
        _test.True(
            formattedInvalidErrors.Contains("equipment_durability_slot_weights repeats slot main_hand"),
            $"duplicate typed slot weights should be rejected. errors={formattedInvalidErrors}"
        );
        _test.True(
            formattedInvalidErrors.Contains(
                "equipment_durability_slot_weights uses unsupported slot unknown_slot"
            ),
            $"unknown typed slot weights should be rejected. errors={formattedInvalidErrors}"
        );
        _test.True(
            formattedInvalidErrors.Contains("equipment_durability_slot_weights[off_hand] must be a positive int"),
            $"non-positive typed slot weights should be rejected. errors={formattedInvalidErrors}"
        );
    }

    private static CombatEffectDef BuildDurabilityEffectResource() =>
        new()
        {
            effect_type = "equipment_durability_damage",
            power = 7,
            effect_target_team_filter = "enemy",
            save_dc_mode = "caster_spell",
            save_ability = "willpower",
            save_dc_source_ability = "intelligence",
            save_tag = "equipment_disjunction",
            require_damage_applied = true,
            @params = new GDictionary
            {
                ["max_damaged_items"] = 1,
                ["target_slots"] = new Godot.Collections.Array<StringName> { "main_hand" },
            },
        };
}
