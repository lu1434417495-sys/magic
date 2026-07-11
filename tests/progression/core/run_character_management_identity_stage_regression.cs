using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_identity_stage_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAgeStageResolverNoLongerRequiresGodotRegistration();
        TestStageAdvancementRefreshesEffectiveAgeStage();
        TestAscensionReplacesEffectiveAgeStage();

        RequestTestExit(_test.Finish("Character management identity stage regression"));
    }

    private void TestAgeStageResolverNoLongerRequiresGodotRegistration()
    {
        System.Type resolverType = typeof(AgeStageResolver);
    }

    private void TestStageAdvancementRefreshesEffectiveAgeStage()
    {
        AgeProfileDef ageProfile = MakeAgeProfile();
        StageAdvancementModifier modifier = new()
        {
            modifier_id = "advance_one_stage",
            display_name = "Advance one stage",
            target_axis = StageAdvancementModifier.ToStringName(StageAdvancementTargetAxis.Full),
            stage_offset = 1,
        };
        PartyState party = BuildPartyWithMember("hero");
        CharacterManagementModule manager = BuildManager(
            party,
            MakeIdentityCatalog(
                ageProfileDefs: new Dictionary<StringName, AgeProfileDef>
                {
                    [ageProfile.profile_id] = ageProfile,
                },
                stageAdvancementDefs: new Dictionary<StringName, StageAdvancementModifier>
                {
                    [modifier.modifier_id] = modifier,
                }
            )
        );

        _test.True(
            manager.AddStageAdvancementModifier("hero", modifier.modifier_id),
            "stage advancement modifier should be accepted."
        );
        PartyMemberState member = party.GetMemberState("hero");
        _test.Eq(
            member.effective_age_stage_id,
            new StringName("middle_age"),
            "stage advancement should refresh effective stage through typed resolver result."
        );
        _test.Eq(
            member.effective_age_stage_source_type,
            new StringName("stage_advancement"),
            "stage advancement source type should be preserved."
        );
        _test.Eq(
            member.effective_age_stage_source_id,
            modifier.modifier_id,
            "stage advancement source id should be preserved."
        );
        _test.Eq(
            ReadString(manager.GetIdentitySummaryForMember("hero"), "effective_age_stage_label"),
            "Middle Age",
            "identity summary should still label effective stage from typed age profile lookup."
        );

        _test.True(
            manager.RemoveStageAdvancementModifier("hero", modifier.modifier_id),
            "stage advancement modifier should be removable."
        );
        _test.Eq(
            member.effective_age_stage_id,
            new StringName("adult"),
            "removing modifier should restore natural age stage."
        );
        _test.Eq(
            member.effective_age_stage_source_type,
            new StringName(""),
            "removing modifier should clear stage source type."
        );
    }

    private void TestAscensionReplacesEffectiveAgeStage()
    {
        AgeProfileDef ageProfile = MakeAgeProfile();
        AscensionDef ascension = new()
        {
            ascension_id = "divine_body",
            display_name = "Divine Body",
            replaces_age_growth = true,
        };
        ascension.stage_ids.Add("divine_age");
        AscensionStageDef stage = new()
        {
            stage_id = "divine_age",
            ascension_id = ascension.ascension_id,
            display_name = "Divine Age",
        };
        PartyState party = BuildPartyWithMember("hero");
        CharacterManagementModule manager = BuildManager(
            party,
            MakeIdentityCatalog(
                ageProfileDefs: new Dictionary<StringName, AgeProfileDef>
                {
                    [ageProfile.profile_id] = ageProfile,
                },
                ascensionDefs: new Dictionary<StringName, AscensionDef>
                {
                    [ascension.ascension_id] = ascension,
                },
                ascensionStageDefs: new Dictionary<StringName, AscensionStageDef>
                {
                    [stage.stage_id] = stage,
                }
            )
        );

        _test.True(
            manager.ApplyAscension("hero", ascension.ascension_id, stage.stage_id, 12),
            "ascension should apply with matching typed content definitions."
        );
        PartyMemberState member = party.GetMemberState("hero");
        _test.Eq(
            member.effective_age_stage_id,
            stage.stage_id,
            "ascension that replaces age growth should override effective stage."
        );
        _test.Eq(
            member.effective_age_stage_source_type,
            new StringName("ascension"),
            "ascension source type should be preserved."
        );
        _test.Eq(
            member.effective_age_stage_source_id,
            stage.stage_id,
            "ascension source id should be the ascension stage id."
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        ProgressionIdentityCatalogData identityCatalog
    )
    {
        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>(),
            null,
            identityCatalog
        );
        return manager;
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog(
        IReadOnlyDictionary<StringName, AgeProfileDef> ageProfileDefs = null,
        IReadOnlyDictionary<StringName, AscensionDef> ascensionDefs = null,
        IReadOnlyDictionary<StringName, AscensionStageDef> ascensionStageDefs = null,
        IReadOnlyDictionary<StringName, StageAdvancementModifier> stageAdvancementDefs = null
    )
    {
        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition>(),
            new Dictionary<StringName, SubraceDefinition>(),
            ageProfileDefs != null
                ? TestProgressionDefinitionProjection.AgeProfiles(ageProfileDefs)
                : new Dictionary<StringName, AgeProfileDefinition>(),
            new Dictionary<StringName, BloodlineDefinition>(),
            new Dictionary<StringName, BloodlineStageDefinition>(),
            ascensionDefs != null
                ? TestProgressionDefinitionProjection.Ascensions(ascensionDefs)
                : new Dictionary<StringName, AscensionDefinition>(),
            ascensionStageDefs != null
                ? TestProgressionDefinitionProjection.AscensionStages(ascensionStageDefs)
                : new Dictionary<StringName, AscensionStageDefinition>(),
            stageAdvancementDefs != null
                ? TestProgressionDefinitionProjection.StageAdvancements(stageAdvancementDefs)
                : new Dictionary<StringName, StageAdvancementDefinition>()
        );
    }

    private static PartyState BuildPartyWithMember(string memberId)
    {
        PartyState party = new();
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId,
            age_profile_id = "test_age_profile",
            natural_age_stage_id = "adult",
            effective_age_stage_id = "adult",
        };
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static AgeProfileDef MakeAgeProfile()
    {
        AgeProfileDef ageProfile = new()
        {
            profile_id = "test_age_profile",
            race_id = "human",
        };
        ageProfile.stage_rules.Add(MakeAgeStageRule("adult", "Adult"));
        ageProfile.stage_rules.Add(MakeAgeStageRule("middle_age", "Middle Age"));
        ageProfile.stage_rules.Add(MakeAgeStageRule("old", "Old"));
        return ageProfile;
    }

    private static AgeStageRule MakeAgeStageRule(StringName stageId, string displayName) =>
        new()
        {
            stage_id = stageId,
            display_name = displayName,
            reachable_by_aging = true,
        };

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }


}
