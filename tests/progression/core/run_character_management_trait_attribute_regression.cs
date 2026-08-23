using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_management_trait_attribute_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestCharacterManagementDisposeIsIdempotent();
        TestCharacterManagementInjectsTraitAttributeModifiers();
        RequestTestExit(_test.Finish("Character management trait attribute regression"));
    }

    private void TestCharacterManagementDisposeIsIdempotent()
    {
        CharacterManagementModule manager = new();
        manager.Dispose();
        manager.Dispose();
    }

    private void TestCharacterManagementInjectsTraitAttributeModifiers()
    {
        PartyState partyState = new();
        UnitProgress progress = MakeProgress("hero");
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            progression = progress,
        };
        member.trait_instances.Add(
            TraitInstanceState.Create(
                "hero_trait_001",
                "character_boost",
                TraitSourceKind.Character,
                "hero"
            )
        );
        partyState.SetMemberState(member);
        partyState.active_member_ids = new Godot.Collections.Array<StringName> { "hero" };
        partyState.leader_member_id = "hero";
        partyState.main_character_member_id = "hero";

        CharacterManagementModule manager = new();
        try
        {
            Dictionary<StringName, TraitDefinition> traitDefs = BuildTraitDefs();
            manager.setup(
                partyState,
                new Dictionary<StringName, SkillDefinition>(),
                new Dictionary<StringName, ProfessionDefinition>(),
                new Dictionary<StringName, AchievementDefinition>(),
                new Dictionary<StringName, ItemDefinition>(),
                new Dictionary<StringName, QuestDefinition>(),
                traitDefs,
                null,
                new ProgressionIdentityCatalogData()
            );

            AttributeSourceContext context = manager.build_attribute_source_context("hero");
            _test.Eq(
                context.trait_attribute_modifiers.Count,
                1,
                "CharacterManagementModule should inject trait-derived attribute modifiers into context."
            );
            _test.Eq(
                context.trait_attribute_modifiers[0].SourceType,
                new StringName("trait_character"),
                "CharacterManagementModule should preserve trait modifier source type."
            );
            _test.Eq(
                context.trait_attribute_modifiers[0].SourceId,
                new StringName("character_boost"),
                "Collapsed character trait modifier source id should use effective trait key."
            );

            AttributeSnapshot snapshot = manager.GetMemberAttributeSnapshot("hero");
            _test.Eq(snapshot.GetValue("strength"), 13, "Member snapshot should include trait strength modifier.");
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static Dictionary<StringName, TraitDefinition> BuildTraitDefs() =>
        new()
        {
            [
                "character_boost"
            ] = TraitTestData.Definition(
                "character_boost",
                new[] { "character" },
                attributeModifiers: new[]
                {
                    new TraitAttributeModifierImportModel(
                        "strength",
                        "flat",
                        3,
                        0,
                        "",
                        ""
                    ),
                }
            ),
        };

    private static UnitProgress MakeProgress(StringName unitId)
    {
        UnitProgress progress = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString().Capitalize(),
        };
        progress.unit_base_attributes.SetAttributeValue("strength", 10);
        return progress;
    }

}
