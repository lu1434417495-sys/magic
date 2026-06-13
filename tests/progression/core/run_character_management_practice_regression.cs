using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_practice_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPracticeReplacementRequiresConfirmation();
        TestPracticeReplacementUsesFormalLearningValidation();
        TestPracticeReplacementSucceedsAfterFormalLearningValidation();
        TestPracticeReplacementRejectsAmbiguousExistingTrack();
        TestPracticeTrackTagsFailClosedAtRuntime();
        TestPracticeReplacementServiceRequiresVerifiedLearning();

        Quit(_test.Finish("Character management practice regression"));
    }

    private void TestPracticeReplacementRequiresConfirmation()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_confirm_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_confirm_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate"
        );
        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        LearnSkillProgress(progression, oldSkill.skill_id, 3);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill);

        GDictionary status = manager.GetPracticeSkillLearnStatusTyped(
            "hero",
            newSkill.skill_id
        ).ToLearnedStatusDictionary();
        _test.True(ReadBool(status, "is_practice_skill"), "replacement status should mark practice skills.");
        _test.True(ReadBool(status, "needs_replacement"), "replacement status should require replacement.");
        _test.Eq(
            ReadString(status, "existing_skill_id"),
            "practice_confirm_old",
            "replacement status should expose the existing skill."
        );
        _test.Eq(
            ReadInt(status, "predicted_level"),
            2,
            "replacement status should expose the predicted replacement level."
        );

        _test.True(
            !manager.LearnSkill("hero", newSkill.skill_id),
            "practice replacement should require explicit confirmation."
        );
        AssertSkillLearnedLevel(progression, oldSkill.skill_id, 3, "old practice skill should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.skill_id) == null,
            "new practice skill should not be learned without confirmation."
        );
    }

    private void TestPracticeReplacementUsesFormalLearningValidation()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_validation_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_validation_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate"
        );
        newSkill.knowledge_requirements = new Godot.Collections.Array<StringName>
        {
            "missing_practice_lore",
        };

        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        LearnSkillProgress(progression, oldSkill.skill_id, 3);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill);

        _test.True(
            !manager.LearnSkillTyped(
                "hero",
                newSkill.skill_id,
                ConfirmedPracticeReplacementOptions()
            ),
            "practice replacement should not bypass formal learning requirements."
        );
        AssertSkillLearnedLevel(progression, oldSkill.skill_id, 3, "old practice skill should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.skill_id) == null,
            "new practice skill should not be written when requirements fail."
        );
    }

    private void TestPracticeReplacementSucceedsAfterFormalLearningValidation()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_success_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_success_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate"
        );
        SkillDef prerequisite = MakeBookSkill("practice_success_prerequisite");
        prerequisite.max_level = 3;
        prerequisite.mastery_curve = new[] { 10, 20, 30 };
        newSkill.knowledge_requirements = new Godot.Collections.Array<StringName>
        {
            "practice_lore",
        };
        newSkill.SetSkillLevelRequirements(
            new Dictionary<StringName, int> { [prerequisite.skill_id] = 2 }
        );
        newSkill.SetAttributeRequirements(new Dictionary<StringName, int> { ["strength"] = 3 });

        PartyState party = BuildPartyWithMember("hero");
        PartyMemberState member = party.GetMemberState("hero");
        UnitProgress progression = member.progression;
        progression.LearnKnowledge("practice_lore");
        progression.unit_base_attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 3);
        LearnSkillProgress(progression, oldSkill.skill_id, 3);
        LearnSkillProgress(progression, prerequisite.skill_id, 2);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill, prerequisite);

        _test.True(
            manager.LearnSkillTyped(
                "hero",
                newSkill.skill_id,
                ConfirmedPracticeReplacementOptions()
            ),
            "practice replacement should succeed after formal validation passes."
        );
        _test.True(
            progression.GetSkillProgress(oldSkill.skill_id) == null,
            "old practice skill should be removed after replacement."
        );
        AssertSkillLearnedLevel(
            progression,
            newSkill.skill_id,
            2,
            "basic level 3 should become intermediate level 2."
        );
    }

    private void TestPracticeReplacementRejectsAmbiguousExistingTrack()
    {
        SkillDef firstOld = MakePracticeSkill(
            "practice_ambiguous_old_a",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDef secondOld = MakePracticeSkill(
            "practice_ambiguous_old_b",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_ambiguous_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate"
        );
        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        LearnSkillProgress(progression, firstOld.skill_id, 2);
        LearnSkillProgress(progression, secondOld.skill_id, 4);
        CharacterManagementModule manager = BuildManager(party, firstOld, secondOld, newSkill);

        GDictionary status = manager.GetPracticeSkillLearnStatusTyped(
            "hero",
            newSkill.skill_id
        ).ToLearnedStatusDictionary();
        _test.Eq(
            ReadString(status, "error_code"),
            "ambiguous_existing_practice_track",
            "ambiguous replacement status should preserve the boundary error code."
        );
        _test.True(
            !manager.LearnSkillTyped(
                "hero",
                newSkill.skill_id,
                ConfirmedPracticeReplacementOptions()
            ),
            "ambiguous practice replacement should fail."
        );
        AssertSkillLearnedLevel(progression, firstOld.skill_id, 2, "first old practice should remain.");
        AssertSkillLearnedLevel(progression, secondOld.skill_id, 4, "second old practice should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.skill_id) == null,
            "ambiguous replacement should not write the new skill."
        );
    }

    private void TestPracticeTrackTagsFailClosedAtRuntime()
    {
        SkillDef dualTrack = MakePracticeSkill(
            "practice_dual_track_runtime",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        dualTrack.SetTags(
            new[] { PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation), PracticeGrowthService.ToStringName(PracticeTrackKind.Cultivation) }
        );
        SkillDef extraTag = MakePracticeSkill(
            "practice_extra_tag_runtime",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        extraTag.SetTags(new[] { PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation), new StringName("passive") });

        foreach (SkillDef skill in new[] { dualTrack, extraTag })
        {
            PartyState party = BuildPartyWithMember("hero");
            UnitProgress progression = party.GetMemberState("hero").progression;
            CharacterManagementModule manager = BuildManager(party, skill);
            _test.True(
                !manager.LearnSkillTyped(
                    "hero",
                    skill.skill_id,
                    ConfirmedPracticeReplacementOptions()
                ),
                "invalid practice tag configuration should fail closed."
            );
            _test.True(
                progression.GetSkillProgress(skill.skill_id) == null,
                "invalid practice tag configuration should not write learned progress."
            );
        }
    }

    private void TestPracticeReplacementServiceRequiresVerifiedLearning()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_service_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Cultivation),
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_service_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Cultivation),
            "intermediate"
        );
        UnitProgress progression = new();
        LearnSkillProgress(progression, oldSkill.skill_id, 2);
        PracticeGrowthService practiceService = new();
        practiceService.Setup(
            new Dictionary<StringName, SkillDef>
            {
                [oldSkill.skill_id] = oldSkill,
                [newSkill.skill_id] = newSkill,
            },
            null
        );

        _test.True(
            !practiceService.ApplyReplacement(newSkill.skill_id, progression),
            "PracticeGrowthService.ApplyReplacement should require formal learning verification."
        );
        AssertSkillLearnedLevel(progression, oldSkill.skill_id, 2, "old practice skill should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.skill_id) == null,
            "unverified replacement should not write the new skill."
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        params SkillDef[] skillDefs
    )
    {
        GDictionary indexedSkillDefs = new();
        foreach (SkillDef skillDef in skillDefs)
        {
            if (skillDef != null)
                indexedSkillDefs[skillDef.skill_id] = skillDef;
        }
        CharacterManagementModule manager = new();
        manager.setup(party, indexedSkillDefs, new GDictionary(), new GDictionary());
        return manager;
    }

    private static PartyState BuildPartyWithMember(string memberId)
    {
        PartyState party = new();
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId,
        };
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static SkillDef MakePracticeSkill(
        StringName skillId,
        StringName trackType,
        StringName practiceTier
    )
    {
        SkillDef skill = MakeBookSkill(skillId);
        skill.max_level = 5;
        skill.mastery_curve = new[] { 10, 20, 30, 40, 50 };
        skill.SetTags(new[] { trackType });
        skill.practice_tier = practiceTier;
        return skill;
    }

    private static SkillDef MakeBookSkill(StringName skillId) =>
        new()
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            learn_source = "book",
            max_level = 1,
        };

    private static CharacterManagementModule.LearnSkillOptionsData ConfirmedPracticeReplacementOptions() =>
        new(true);

    private static void LearnSkillProgress(
        UnitProgress progression,
        StringName skillId,
        int skillLevel
    )
    {
        progression?.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = skillId,
                is_learned = true,
                skill_level = skillLevel,
            }
        );
    }

    private void AssertSkillLearnedLevel(
        UnitProgress progression,
        StringName skillId,
        int expectedLevel,
        string message
    )
    {
        UnitSkillProgress skillProgress = progression?.GetSkillProgress(skillId);
        _test.True(skillProgress != null && skillProgress.is_learned, message);
        if (skillProgress == null)
            return;
        _test.Eq(skillProgress.skill_level, expectedLevel, $"{message} Level should match.");
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

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
