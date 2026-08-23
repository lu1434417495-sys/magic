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
        AddDamageDiceActionPayloadImportModel payload = BuildDamagePayload(
            "test.attack_append",
            100
        );

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
        EquipmentAbilityBindingImportModel binding = new()
        {
            binding_id = "binding.test.replacement",
            reactions = new[]
            {
                new EquipmentAbilityReactionImportModel
                {
                    reaction_id = "reaction.test.replacement",
                    trigger = "on_hit",
                    timing = "after_hit",
                    actions = new[]
                    {
                        new EquipmentAbilityActionImportModel
                        {
                            action_id = "action.test.replacement",
                            kind = "add_damage_dice",
                            payload = payload,
                        },
                    },
                },
            },
        };
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

        var missingGroupErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(
            BuildDamagePayload("", 100),
            BuildValidationContext(),
            "test.missing_group",
            missingGroupErrors
        );
        _test.True(
            ContainsCode(missingGroupErrors, "EQA_DAMAGE_REPLACEMENT_GROUP_REQUIRED"),
            "非零 replacement_priority 没有 group 时必须 fail closed。"
        );

        var negativePriorityErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(
            BuildDamagePayload("test.attack_append", -1),
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
        ApplyStatusActionPayloadImportModel payload = BuildStatusPayload("fire", "immune");
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

        var missingTagErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            BuildStatusPayload("", "immune"),
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

        var invalidTierErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            BuildStatusPayload("fire", "quarter"),
            BuildValidationContext(),
            "test.invalid_mitigation_tier",
            invalidTierErrors
        );
        _test.True(
            ContainsCode(invalidTierErrors, "EQA_STATUS_MITIGATION_TIER_INVALID"),
            "未知 mitigation tier 必须 fail closed。"
        );
    }

    private static AddDamageDiceActionPayloadImportModel BuildDamagePayload(
        string replacementGroupId,
        int replacementPriority
    ) => new()
    {
        target_selector = "target",
        dice = new DiceExpressionImportModel
        {
            terms = new[]
            {
                new DiceExpressionTermImportModel
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
            },
        },
        damage_type = "fire",
        replacement_group_id = replacementGroupId,
        replacement_priority = replacementPriority,
    };

    private static ApplyStatusActionPayloadImportModel BuildStatusPayload(
        string damageTag,
        string mitigationTier
    ) => new()
    {
        target_selector = "self",
        status_id = "test_status",
        damage_tag = damageTag,
        mitigation_tier = mitigationTier,
        source_bound_attack_roll_penalty_min_stacks = 1,
        source_bound_incoming_attack_roll_bonus_min_stacks = 1,
    };

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
