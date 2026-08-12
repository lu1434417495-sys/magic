using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_bonus_damage_replacement_schema_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        try
        {
            TestReplacementGroupValidation();
            TestStatusMitigationValidation();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(
            _test.Finish("Equipment bonus-damage replacement schema regression")
        );
    }

    private void TestReplacementGroupValidation()
    {
        using DiceExpressionTermDef term = new()
        {
            dice_count = 1,
            dice_sides = 6,
        };
        using DiceExpressionDef dice = new();
        dice.terms.Add(term);
        using AddDamageDiceActionPayloadDef payload = new()
        {
            target_selector = "target",
            dice = dice,
            damage_type = "fire",
            replacement_group_id = "test.attack_append",
            replacement_priority = 100,
        };

        var validErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(
            payload,
            BuildValidationContext(),
            "test.valid_replacement",
            validErrors
        );
        _test.Eq(
            validErrors.Count,
            0,
            $"合法 replacement group/priority 应通过校验。errors={string.Join(" | ", validErrors)}"
        );
        using EquipmentAbilityActionDef action = new()
        {
            action_id = "action.test.replacement",
            kind = "add_damage_dice",
            payload = payload,
        };
        using EquipmentAbilityReactionDef reaction = new()
        {
            reaction_id = "reaction.test.replacement",
            trigger = "on_hit",
            timing = "after_hit",
        };
        reaction.actions.Add(action);
        using EquipmentAbilityBindingDef binding = new()
        {
            binding_id = "binding.test.replacement",
        };
        binding.reactions.Add(reaction);
        EquipmentAbilityBindingDefinition projected =
            EquipmentAbilityDefinitionProjection.ProjectBinding(binding);
        AddDamageDiceActionPayloadDefinition projectedPayload =
            projected.Reactions[0].Actions[0].PayloadDefinition
                as AddDamageDiceActionPayloadDefinition;
        _test.Eq(
            projectedPayload?.ReplacementGroupId ?? new StringName(""),
            new StringName("test.attack_append"),
            "definition projection 必须保留 replacement_group_id。"
        );
        _test.Eq(
            projectedPayload?.ReplacementPriority ?? -1,
            100,
            "definition projection 必须保留 replacement_priority。"
        );

        payload.replacement_group_id = "";
        var missingGroupErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(
            payload,
            BuildValidationContext(),
            "test.missing_group",
            missingGroupErrors
        );
        _test.True(
            ContainsCode(missingGroupErrors, "EQA_DAMAGE_REPLACEMENT_GROUP_REQUIRED"),
            "非零 replacement_priority 没有 group 时必须 fail closed。"
        );

        payload.replacement_group_id = "test.attack_append";
        payload.replacement_priority = -1;
        var negativePriorityErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(
            payload,
            BuildValidationContext(),
            "test.negative_priority",
            negativePriorityErrors
        );
        _test.True(
            ContainsCode(
                negativePriorityErrors,
                "EQA_DAMAGE_REPLACEMENT_PRIORITY_INVALID"
            ),
            "负 replacement_priority 必须 fail closed。"
        );
    }

    private void TestStatusMitigationValidation()
    {
        using ApplyStatusActionPayloadDef payload = new()
        {
            target_selector = "self",
            status_id = "test_status",
            damage_tag = "fire",
            mitigation_tier = "immune",
        };
        var validErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            payload,
            BuildValidationContext(),
            "test.valid_mitigation",
            validErrors
        );
        _test.Eq(
            validErrors.Count,
            0,
            $"合法 status mitigation tag/tier 应通过校验。errors={string.Join(" | ", validErrors)}"
        );

        payload.damage_tag = "";
        var missingTagErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            payload,
            BuildValidationContext(),
            "test.missing_mitigation_tag",
            missingTagErrors
        );
        _test.True(
            ContainsCode(
                missingTagErrors,
                "EQA_STATUS_MITIGATION_DAMAGE_TAG_REQUIRED"
            ),
            "mitigation_tier 没有 damage tag 时必须 fail closed。"
        );

        payload.damage_tag = "fire";
        payload.mitigation_tier = "quarter";
        var invalidTierErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            payload,
            BuildValidationContext(),
            "test.invalid_mitigation_tier",
            invalidTierErrors
        );
        _test.True(
            ContainsCode(invalidTierErrors, "EQA_STATUS_MITIGATION_TIER_INVALID"),
            "未知 mitigation tier 必须 fail closed。"
        );
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext() =>
        new()
        {
            KnownTraitIds = new HashSet<StringName>(),
            KnownSkillIds = new HashSet<StringName>(),
            WindupSkillIds = new HashSet<StringName>(),
            KnownStatusIds = new HashSet<StringName> { "test_status" },
        };

    private static bool ContainsCode(IEnumerable<string> errors, string code)
    {
        foreach (string error in errors ?? Array.Empty<string>())
        {
            if (error?.StartsWith(code + " ", StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }
}
