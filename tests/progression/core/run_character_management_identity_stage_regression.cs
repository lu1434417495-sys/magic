using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_identity_stage_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<GodotObject> _ownedGodotObjects = new();
    private readonly List<IDisposable> _ownedDisposables = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestAgeStageResolverNoLongerRequiresGodotRegistration();
            TestStageAdvancementRefreshesEffectiveAgeStage();
            TestAscensionReplacesEffectiveAgeStage();
        }
        finally
        {
            DisposeOwned();
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Character management identity stage regression"));
    }

    private void TestAgeStageResolverNoLongerRequiresGodotRegistration()
    {
        System.Type resolverType = typeof(AgeStageResolver);
    }

    private void TestStageAdvancementRefreshesEffectiveAgeStage()
    {
        AgeProfileDef ageProfile = MakeAgeProfile();
        StageAdvancementModifier modifier = TrackOwned(new StageAdvancementModifier
        {
            modifier_id = "advance_one_stage",
            display_name = "Advance one stage",
            target_axis = StageAdvancementModifier.ToStringName(StageAdvancementTargetAxis.Full),
            stage_offset = 1,
        });
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
        AscensionDef ascension = TrackOwned(new AscensionDef
        {
            ascension_id = "divine_body",
            display_name = "Divine Body",
            replaces_age_growth = true,
        });
        ascension.stage_ids.Add("divine_age");
        AscensionStageDef stage = TrackOwned(new AscensionStageDef
        {
            stage_id = "divine_age",
            ascension_id = ascension.ascension_id,
            display_name = "Divine Age",
        });
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

    private CharacterManagementModule BuildManager(
        PartyState party,
        ProgressionIdentityCatalogData identityCatalog
    )
    {
        CharacterManagementModule manager = TrackDisposable(new CharacterManagementModule());
        manager.setup(
            party,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
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
            new Dictionary<StringName, RaceDef>(),
            new Dictionary<StringName, SubraceDef>(),
            ageProfileDefs,
            new Dictionary<StringName, BloodlineDef>(),
            new Dictionary<StringName, BloodlineStageDef>(),
            ascensionDefs,
            ascensionStageDefs,
            stageAdvancementDefs
        );
    }

    private PartyState BuildPartyWithMember(string memberId)
    {
        PartyState party = TrackOwned(new PartyState());
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

    private AgeProfileDef MakeAgeProfile()
    {
        AgeProfileDef ageProfile = TrackOwned(new AgeProfileDef
        {
            profile_id = "test_age_profile",
            race_id = "human",
        });
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

    private T TrackOwned<T>(T value)
        where T : GodotObject
    {
        if (value != null)
            _ownedGodotObjects.Add(value);
        return value;
    }

    private T TrackDisposable<T>(T value)
        where T : IDisposable
    {
        if (value != null)
            _ownedDisposables.Add(value);
        return value;
    }

    private void DisposeOwned()
    {
        for (int index = _ownedDisposables.Count - 1; index >= 0; index--)
            _ownedDisposables[index]?.Dispose();
        _ownedDisposables.Clear();

        for (int index = _ownedGodotObjects.Count - 1; index >= 0; index--)
            DisposeOwnedGodotObject(_ownedGodotObjects[index]);
        _ownedGodotObjects.Clear();
    }

    private static void DisposeOwnedGodotObject(GodotObject ownedObject)
    {
        switch (ownedObject)
        {
            case null:
                return;
            case PartyState party:
                GodotRefCountedDisposer.DisposeIfValid(party);
                return;
            default:
                BattleTestFixture.DisposeFixtureObject(ownedObject);
                return;
        }
    }


}
