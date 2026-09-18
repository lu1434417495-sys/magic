using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_combat_effect_equipment_durability_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
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

        CombatEffectDefinition definition = CombatEffectDefinition.FromDiagnosticFixture(
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

        bool rejected = false;
        try
        {
            CombatEffectDefinition.FromDiagnosticFixture(
                resource,
                "test.combat_effect_durability.legacy_param"
            );
        }
        catch (System.IO.InvalidDataException exception)
        {
            rejected = exception.Message.Contains("slot_weight_map");
        }
        _test.True(
            rejected,
            "strict Resource import should reject legacy params.slot_weight_map before projection."
        );
    }

    private void TestSkillContentValidationUsesTypedSlotWeights()
    {
        using CombatEffectDef valid = BuildDurabilityEffectResource();
        valid.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "main_hand", weight = 30 },
                new() { slot_id = "off_hand", weight = 20 },
            };
        GStringArray validErrors = new();
        validErrors.AddRange(TestSkillDefinitionProjection.ValidateSyntheticEffectFixture(valid, "typed_durability_weights"));
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
        legacyErrors.AddRange(TestSkillDefinitionProjection.ValidateSyntheticEffectFixture(legacy, "legacy_durability_weight_map"));
        _test.True(
            string.Join(" | ", legacyErrors).Contains(
                "payload/slot_weight_map: Unknown effect payload member."
            ),
            $"legacy params.slot_weight_map should be rejected. errors={string.Join(" | ", legacyErrors)}"
        );

        // 未知槽位在 strict DTO 边界就被闭集拒绝；重复槽位与非正权重只能由 Definition validator 发现。
        // 三种情况拆成独立夹具，避免 DTO 失败让后两条规则根本不执行。
        using CombatEffectDef duplicate = BuildDurabilityEffectResource();
        duplicate.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "main_hand", weight = 30 },
                new() { slot_id = "main_hand", weight = 20 },
            };
        string duplicateErrors = string.Join(
            " | ",
            TestSkillDefinitionProjection.ValidateSyntheticEffectFixture(duplicate, "duplicate_durability_weights")
        );
        _test.True(
            duplicateErrors.Contains("equipment_durability_slot_weights repeats slot main_hand"),
            $"duplicate typed slot weights should be rejected. errors={duplicateErrors}"
        );

        using CombatEffectDef unknown = BuildDurabilityEffectResource();
        unknown.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "unknown_slot", weight = 10 },
            };
        string unknownErrors = string.Join(
            " | ",
            TestSkillDefinitionProjection.ValidateSyntheticEffectFixture(unknown, "unknown_durability_weights")
        );
        _test.True(
            unknownErrors.Contains(
                "effect_defs/0/equipment_durability_slot_weights/0/slot_id: Value is not registered by the closed combat effect contract."
            ),
            $"unknown typed slot weights should be rejected. errors={unknownErrors}"
        );

        using CombatEffectDef nonPositive = BuildDurabilityEffectResource();
        nonPositive.equipment_durability_slot_weights =
            new Godot.Collections.Array<CombatEffectSlotWeightDef>
            {
                new() { slot_id = "off_hand", weight = 0 },
            };
        string nonPositiveErrors = string.Join(
            " | ",
            TestSkillDefinitionProjection.ValidateSyntheticEffectFixture(nonPositive, "non_positive_durability_weights")
        );
        _test.True(
            nonPositiveErrors.Contains("equipment_durability_slot_weights[off_hand] must be a positive int"),
            $"non-positive typed slot weights should be rejected. errors={nonPositiveErrors}"
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
