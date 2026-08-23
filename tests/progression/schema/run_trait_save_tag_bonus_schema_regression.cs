using System.Collections.Generic;
using Godot;

public partial class run_trait_save_tag_bonus_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStackModeMapping();
        TestDragonFrightfulPresenceMapping();
        TestValidEntriesPassValidation();
        TestInvalidEntriesAreRejected();
        TestUnknownStackModeFailsProjection();

        RequestTestExit(_test.Finish("Trait save tag bonus schema regression"));
    }

    private void TestStackModeMapping()
    {
        _test.Eq(
            TraitSaveTagBonusStackModeKind.Add,
            TraitContentRules.ToSaveTagBonusStackModeKind("add"),
            "add stack mode should map."
        );
        _test.Eq(
            TraitSaveTagBonusStackModeKind.Highest,
            TraitContentRules.ToSaveTagBonusStackModeKind("highest"),
            "highest stack mode should map."
        );
        _test.Eq(
            "add",
            TraitContentRules.ToStringName(TraitSaveTagBonusStackModeKind.Add),
            "add stack mode should round-trip."
        );
        _test.Eq(
            "highest",
            TraitContentRules.ToStringName(TraitSaveTagBonusStackModeKind.Highest),
            "highest stack mode should round-trip."
        );
        _test.Eq(
            "",
            TraitContentRules.ToStringName(TraitSaveTagBonusStackModeKind.Unknown),
            "unknown stack mode should serialize to empty."
        );
        _test.False(
            TraitContentRules.IsValidSaveTagBonusStackMode("max"),
            "unknown stack mode should be invalid."
        );
    }

    private void TestDragonFrightfulPresenceMapping()
    {
        StringName tag = "dragon_frightful_presence";
        _test.Eq(
            BattleSaveTagKind.DragonFrightfulPresence,
            BattleSaveContentRules.ToSaveTagKind(tag),
            "dragon_frightful_presence should map to its kind."
        );
        _test.Eq(
            tag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.DragonFrightfulPresence),
            "dragon_frightful_presence should round-trip."
        );
        _test.True(
            BattleSaveContentRules.IsValidSaveTag(tag),
            "dragon_frightful_presence should be a valid save tag."
        );
        _test.True(
            BattleSaveContentRules.IsControlSaveTag(tag),
            "dragon_frightful_presence causes frightened and must be a control save tag."
        );
        _test.True(
            BattleSaveContentRules.IsControlSaveTag("frightened"),
            "frightened should stay a control save tag."
        );
        _test.False(
            BattleSaveContentRules.IsControlSaveTag("poison"),
            "poison should stay a non-control save tag."
        );
    }

    private void TestValidEntriesPassValidation()
    {
        TraitImportModel import = MakeTraitImport(
            "valid_save_tag_bonus_trait",
            MakeEntry("frightened", 3, "add"),
            MakeEntry("dragon_frightful_presence", 3, "add"),
            MakeEntry("frightened", 5, "highest")
        );
        List<string> errors = Validate(import);
        _test.Eq(
            errors.Count,
            0,
            $"valid save tag bonus entries should pass validation: {Format(errors)}"
        );
    }

    private void TestInvalidEntriesAreRejected()
    {
        TraitImportModel zeroBonus = MakeTraitImport(
            "zero_bonus_trait",
            MakeEntry("frightened", 0, "add")
        );
        _test.True(
            Contains(Validate(zeroBonus), "bonus must be positive"),
            "zero bonus should be rejected."
        );

        TraitImportModel negativeBonus = MakeTraitImport(
            "negative_bonus_trait",
            MakeEntry("frightened", -2, "add")
        );
        _test.True(
            Contains(Validate(negativeBonus), "bonus must be positive"),
            "negative bonus should be rejected."
        );

        TraitImportModel duplicate = MakeTraitImport(
            "duplicate_save_tag_bonus_trait",
            MakeEntry("frightened", 3, "add"),
            MakeEntry("frightened", 4, "add")
        );
        _test.True(
            Contains(Validate(duplicate), "duplicates save tag bonus"),
            "duplicate (save_tag, stack_mode) should be rejected."
        );

        TraitImportModel invalidTag = MakeTraitImport(
            "invalid_save_tag_bonus_trait",
            MakeEntry("not_a_save_tag", 3, "add")
        );
        _test.True(
            Contains(Validate(invalidTag), "unsupported save tag"),
            "unsupported save tag should be rejected."
        );

        TraitImportModel emptyTag = MakeTraitImport(
            "empty_save_tag_bonus_trait",
            MakeEntry("", 3, "add")
        );
        _test.True(
            Contains(Validate(emptyTag), "save_tag must be a non-empty"),
            "empty save tag should be rejected."
        );
    }

    private void TestUnknownStackModeFailsProjection()
    {
        TraitImportModel import = MakeTraitImport(
            "unknown_stack_mode_trait",
            MakeEntry("frightened", 3, "max")
        );
        _test.True(
            Contains(Validate(import), "stack_mode must be add or highest"),
            "unknown stack_mode should fail the import validator closed."
        );
    }

    private static TraitImportModel MakeTraitImport(
        StringName traitId,
        params TraitSaveTagBonusEntryImportModel[] entries
    ) =>
        new(
            traitId.ToString(),
            traitId.ToString(),
            "Save tag bonus schema fixture.",
            System.Array.Empty<string>(),
            new[] { "equipment_fixed" },
            "save_advantage",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            System.Array.Empty<TraitAttributeModifierImportModel>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<TraitDamageResistanceEntryImportModel>(),
            System.Array.Empty<TraitSaveBonusEntryImportModel>(),
            entries,
            System.Array.Empty<TraitPassiveStatusEffectImportModel>(),
            System.Array.Empty<TraitRollValueSchemaEntryImportModel>()
        );

    private static TraitSaveTagBonusEntryImportModel MakeEntry(
        StringName saveTag,
        int bonus,
        StringName stackMode
    ) => new(saveTag.ToString(), bonus, stackMode.ToString());

    private static List<string> Validate(TraitImportModel import) =>
        new(new TraitImportModelValidator().ValidateMessages(import));

    private static bool Contains(IEnumerable<string> errors, string needle)
    {
        foreach (string error in errors)
        {
            if (error.Contains(needle))
                return true;
        }
        return false;
    }

    private static string Format(IEnumerable<string> errors) =>
        string.Join(" || ", errors);
}
