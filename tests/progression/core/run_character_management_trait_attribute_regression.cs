using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_management_trait_attribute_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCharacterManagementInjectsTraitAttributeModifiers();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Character management trait attribute regression"));
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
            Dictionary<StringName, TraitDef> traitDefs = BuildTraitDefs();
            manager.setup(
                partyState,
                new Dictionary<StringName, SkillDef>(),
                new Dictionary<StringName, ProfessionDef>(),
                new Dictionary<StringName, AchievementDef>(),
                new Dictionary<StringName, ItemDef>(),
                new Dictionary<StringName, QuestDef>(),
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
                context.trait_attribute_modifiers[0].source_type,
                new StringName("trait_character"),
                "CharacterManagementModule should preserve trait modifier source type."
            );
            _test.Eq(
                context.trait_attribute_modifiers[0].source_id,
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

    private static Dictionary<StringName, TraitDef> BuildTraitDefs() =>
        new()
        {
            [
                "character_boost"
            ] = new TraitDef
            {
                trait_id = "character_boost",
                display_name = "Character Boost",
                description = "Fixture character trait.",
                allowed_source_kinds = new Godot.Collections.Array<StringName> { "character" },
                effect_type = "attribute_modifier",
                trigger_type = "passive",
                stack_policy = "unique_by_trait",
                charge_scope = "none",
                charge_reset_timing = "none",
                attribute_modifiers = new Godot.Collections.Array<AttributeModifier>
                {
                    new()
                    {
                        attribute_id = "strength",
                        mode = AttributeModifier.ToStringName(AttributeModifierMode.Flat),
                        value = 3,
                    },
                },
            },
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
