using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_identity_stage_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAgeStageResolverNoLongerRequiresGodotRegistration();
        TestStageAdvancementRefreshesEffectiveAgeStage();
        TestAscensionReplacesEffectiveAgeStage();

        if (_failures.Count == 0)
        {
            GD.Print("Character management identity stage regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Character management identity stage regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestAgeStageResolverNoLongerRequiresGodotRegistration()
    {
        System.Type resolverType = typeof(AgeStageResolver);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(resolverType),
            "AgeStageResolver should be a plain C# helper, not a GodotObject/RefCounted."
        );
        AssertTrue(
            resolverType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                == 0,
            "AgeStageResolver should not remain registered as a Godot GlobalClass."
        );
        AssertEq(
            resolverType
                .GetMethod(nameof(AgeStageResolver.resolve_effective_stage))
                ?.GetParameters()[2]
                .ParameterType,
            typeof(IEnumerable<StageAdvancementModifier>),
            "AgeStageResolver should consume typed stage modifier sequences instead of Godot Array state."
        );
    }

    private void TestStageAdvancementRefreshesEffectiveAgeStage()
    {
        AgeProfileDef ageProfile = MakeAgeProfile();
        StageAdvancementModifier modifier = new()
        {
            modifier_id = "advance_one_stage",
            display_name = "Advance one stage",
            target_axis = StageAdvancementModifier.TARGET_AXIS_FULL(),
            stage_offset = 1,
        };
        PartyState party = BuildPartyWithMember("hero");
        CharacterManagementModule manager = BuildManager(
            party,
            new GDictionary
            {
                ["age_profile_defs"] = new GDictionary { [ageProfile.profile_id] = ageProfile },
                ["stage_advancement_defs"] = new GDictionary { [modifier.modifier_id] = modifier },
            }
        );

        AssertTrue(
            manager.add_stage_advancement_modifier("hero", modifier.modifier_id),
            "stage advancement modifier should be accepted."
        );
        PartyMemberState member = party.get_member_state("hero");
        AssertEq(
            member.effective_age_stage_id,
            new StringName("middle_age"),
            "stage advancement should refresh effective stage through typed resolver result."
        );
        AssertEq(
            member.effective_age_stage_source_type,
            new StringName("stage_advancement"),
            "stage advancement source type should be preserved."
        );
        AssertEq(
            member.effective_age_stage_source_id,
            modifier.modifier_id,
            "stage advancement source id should be preserved."
        );
        AssertEq(
            ReadString(manager.get_identity_summary_for_member("hero"), "effective_age_stage_label"),
            "Middle Age",
            "identity summary should still label effective stage from typed age profile lookup."
        );

        AssertTrue(
            manager.remove_stage_advancement_modifier("hero", modifier.modifier_id),
            "stage advancement modifier should be removable."
        );
        AssertEq(
            member.effective_age_stage_id,
            new StringName("adult"),
            "removing modifier should restore natural age stage."
        );
        AssertEq(
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
            new GDictionary
            {
                ["age_profile_defs"] = new GDictionary { [ageProfile.profile_id] = ageProfile },
                ["ascension_defs"] = new GDictionary { [ascension.ascension_id] = ascension },
                ["ascension_stage_defs"] = new GDictionary { [stage.stage_id] = stage },
            }
        );

        AssertTrue(
            manager.apply_ascension("hero", ascension.ascension_id, stage.stage_id, 12),
            "ascension should apply with matching typed content definitions."
        );
        PartyMemberState member = party.get_member_state("hero");
        AssertEq(
            member.effective_age_stage_id,
            stage.stage_id,
            "ascension that replaces age growth should override effective stage."
        );
        AssertEq(
            member.effective_age_stage_source_type,
            new StringName("ascension"),
            "ascension source type should be preserved."
        );
        AssertEq(
            member.effective_age_stage_source_id,
            stage.stage_id,
            "ascension source id should be the ascension stage id."
        );
    }

    private static CharacterManagementModule BuildManager(PartyState party, GDictionary bundle)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            bundle
        );
        return manager;
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
        party.set_member_state(member);
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

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }
}
