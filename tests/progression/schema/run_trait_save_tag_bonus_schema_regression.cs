using System.Collections.Generic;
using System.IO;
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
        using TraitDef def = MakeTraitDef("valid_save_tag_bonus_trait");
        def.save_tag_bonus_entries.Add(MakeEntry("frightened", 3, "add"));
        def.save_tag_bonus_entries.Add(MakeEntry("dragon_frightful_presence", 3, "add"));
        def.save_tag_bonus_entries.Add(MakeEntry("frightened", 5, "highest"));
        List<string> errors = Validate(def);
        _test.Eq(
            errors.Count,
            0,
            $"valid save tag bonus entries should pass validation: {Format(errors)}"
        );
    }

    private void TestInvalidEntriesAreRejected()
    {
        using TraitDef zeroBonus = MakeTraitDef("zero_bonus_trait");
        zeroBonus.save_tag_bonus_entries.Add(MakeEntry("frightened", 0, "add"));
        _test.True(
            Contains(Validate(zeroBonus), "bonus must be positive"),
            "zero bonus should be rejected."
        );

        using TraitDef negativeBonus = MakeTraitDef("negative_bonus_trait");
        negativeBonus.save_tag_bonus_entries.Add(MakeEntry("frightened", -2, "add"));
        _test.True(
            Contains(Validate(negativeBonus), "bonus must be positive"),
            "negative bonus should be rejected."
        );

        using TraitDef duplicate = MakeTraitDef("duplicate_save_tag_bonus_trait");
        duplicate.save_tag_bonus_entries.Add(MakeEntry("frightened", 3, "add"));
        duplicate.save_tag_bonus_entries.Add(MakeEntry("frightened", 4, "add"));
        _test.True(
            Contains(Validate(duplicate), "duplicates save tag bonus"),
            "duplicate (save_tag, stack_mode) should be rejected."
        );

        using TraitDef invalidTag = MakeTraitDef("invalid_save_tag_bonus_trait");
        invalidTag.save_tag_bonus_entries.Add(MakeEntry("not_a_save_tag", 3, "add"));
        _test.True(
            Contains(Validate(invalidTag), "unsupported save tag"),
            "unsupported save tag should be rejected."
        );

        using TraitDef emptyTag = MakeTraitDef("empty_save_tag_bonus_trait");
        emptyTag.save_tag_bonus_entries.Add(MakeEntry("", 3, "add"));
        _test.True(
            Contains(Validate(emptyTag), "save_tag must be a non-empty"),
            "empty save tag should be rejected."
        );
    }

    private void TestUnknownStackModeFailsProjection()
    {
        using TraitDef def = MakeTraitDef("unknown_stack_mode_trait");
        def.save_tag_bonus_entries.Add(MakeEntry("frightened", 3, "max"));
        try
        {
            TestProgressionDefinitionProjection.Trait(def);
            _test.Fail("unknown stack_mode should fail projection closed.");
        }
        catch (InvalidDataException)
        {
            _test.True(true, "unknown stack_mode should fail projection closed.");
        }
    }

    private static TraitDef MakeTraitDef(StringName traitId)
    {
        TraitDef def = new()
        {
            trait_id = traitId,
            display_name = traitId.ToString(),
            description = "Save tag bonus schema fixture.",
            effect_type = "save_advantage",
        };
        def.allowed_source_kinds.Add("equipment_fixed");
        return def;
    }

    private static TraitSaveTagBonusEntryDef MakeEntry(
        StringName saveTag,
        int bonus,
        StringName stackMode
    ) =>
        new()
        {
            save_tag = saveTag,
            bonus = bonus,
            stack_mode = stackMode,
        };

    private static List<string> Validate(TraitDef def)
    {
        TraitDefinition definition = TestProgressionDefinitionProjection.Trait(def);
        var definitions = new Dictionary<StringName, TraitDefinition>
        {
            [definition.TraitId] = definition,
        };
        return new List<string>(
            TraitContentRegistry.ValidateDefinitions(definitions)
        );
    }

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
