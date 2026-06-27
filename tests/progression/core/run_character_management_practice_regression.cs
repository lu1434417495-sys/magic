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
        SkillDefinition oldSkill = MakePracticeSkill(
            "practice_confirm_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDefinition newSkill = MakePracticeSkill(
            "practice_confirm_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate"
        );
        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        LearnSkillProgress(progression, oldSkill.SkillId, 3);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill);

        GDictionary status = manager.GetPracticeSkillLearnStatusTyped(
            "hero",
            newSkill.SkillId
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
            !manager.LearnSkill("hero", newSkill.SkillId),
            "practice replacement should require explicit confirmation."
        );
        AssertSkillLearnedLevel(progression, oldSkill.SkillId, 3, "old practice skill should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.SkillId) == null,
            "new practice skill should not be learned without confirmation."
        );
    }

    private void TestPracticeReplacementUsesFormalLearningValidation()
    {
        SkillDefinition oldSkill = MakePracticeSkill(
            "practice_validation_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDefinition newSkill = MakePracticeSkill(
            "practice_validation_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate",
            knowledgeRequirements: new[] { new StringName("missing_practice_lore") }
        );

        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        LearnSkillProgress(progression, oldSkill.SkillId, 3);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill);

        _test.True(
            !manager.LearnSkillTyped(
                "hero",
                newSkill.SkillId,
                ConfirmedPracticeReplacementOptions()
            ),
            "practice replacement should not bypass formal learning requirements."
        );
        AssertSkillLearnedLevel(progression, oldSkill.SkillId, 3, "old practice skill should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.SkillId) == null,
            "new practice skill should not be written when requirements fail."
        );
    }

    private void TestPracticeReplacementSucceedsAfterFormalLearningValidation()
    {
        SkillDefinition oldSkill = MakePracticeSkill(
            "practice_success_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDefinition prerequisite = MakeBookSkill(
            "practice_success_prerequisite",
            maxLevel: 3,
            masteryCurve: new[] { 10, 20, 30 }
        );
        SkillDefinition newSkill = MakePracticeSkill(
            "practice_success_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate",
            knowledgeRequirements: new[] { new StringName("practice_lore") },
            skillLevelRequirements: new Dictionary<StringName, int> { [prerequisite.SkillId] = 2 },
            attributeRequirements: new Dictionary<StringName, int> { ["strength"] = 3 }
        );

        PartyState party = BuildPartyWithMember("hero");
        PartyMemberState member = party.GetMemberState("hero");
        UnitProgress progression = member.progression;
        progression.LearnKnowledge("practice_lore");
        progression.unit_base_attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 3);
        LearnSkillProgress(progression, oldSkill.SkillId, 3);
        LearnSkillProgress(progression, prerequisite.SkillId, 2);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill, prerequisite);

        _test.True(
            manager.LearnSkillTyped(
                "hero",
                newSkill.SkillId,
                ConfirmedPracticeReplacementOptions()
            ),
            "practice replacement should succeed after formal validation passes."
        );
        _test.True(
            progression.GetSkillProgress(oldSkill.SkillId) == null,
            "old practice skill should be removed after replacement."
        );
        AssertSkillLearnedLevel(
            progression,
            newSkill.SkillId,
            2,
            "basic level 3 should become intermediate level 2."
        );
    }

    private void TestPracticeReplacementRejectsAmbiguousExistingTrack()
    {
        SkillDefinition firstOld = MakePracticeSkill(
            "practice_ambiguous_old_a",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDefinition secondOld = MakePracticeSkill(
            "practice_ambiguous_old_b",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic"
        );
        SkillDefinition newSkill = MakePracticeSkill(
            "practice_ambiguous_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "intermediate"
        );
        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        LearnSkillProgress(progression, firstOld.SkillId, 2);
        LearnSkillProgress(progression, secondOld.SkillId, 4);
        CharacterManagementModule manager = BuildManager(party, firstOld, secondOld, newSkill);

        GDictionary status = manager.GetPracticeSkillLearnStatusTyped(
            "hero",
            newSkill.SkillId
        ).ToLearnedStatusDictionary();
        _test.Eq(
            ReadString(status, "error_code"),
            "ambiguous_existing_practice_track",
            "ambiguous replacement status should preserve the boundary error code."
        );
        _test.True(
            !manager.LearnSkillTyped(
                "hero",
                newSkill.SkillId,
                ConfirmedPracticeReplacementOptions()
            ),
            "ambiguous practice replacement should fail."
        );
        AssertSkillLearnedLevel(progression, firstOld.SkillId, 2, "first old practice should remain.");
        AssertSkillLearnedLevel(progression, secondOld.SkillId, 4, "second old practice should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.SkillId) == null,
            "ambiguous replacement should not write the new skill."
        );
    }

    private void TestPracticeTrackTagsFailClosedAtRuntime()
    {
        SkillDefinition dualTrack = MakePracticeSkill(
            "practice_dual_track_runtime",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic",
            tags: new[]
            {
                PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
                PracticeGrowthService.ToStringName(PracticeTrackKind.Cultivation),
            }
        );
        SkillDefinition extraTag = MakePracticeSkill(
            "practice_extra_tag_runtime",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
            "basic",
            tags: new[]
            {
                PracticeGrowthService.ToStringName(PracticeTrackKind.Meditation),
                new StringName("passive"),
            }
        );

        foreach (SkillDefinition skill in new[] { dualTrack, extraTag })
        {
            PartyState party = BuildPartyWithMember("hero");
            UnitProgress progression = party.GetMemberState("hero").progression;
            CharacterManagementModule manager = BuildManager(party, skill);
            _test.True(
                !manager.LearnSkillTyped(
                    "hero",
                    skill.SkillId,
                    ConfirmedPracticeReplacementOptions()
                ),
                "invalid practice tag configuration should fail closed."
            );
            _test.True(
                progression.GetSkillProgress(skill.SkillId) == null,
                "invalid practice tag configuration should not write learned progress."
            );
        }
    }

    private void TestPracticeReplacementServiceRequiresVerifiedLearning()
    {
        SkillDefinition oldSkill = MakePracticeSkill(
            "practice_service_old",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Cultivation),
            "basic"
        );
        SkillDefinition newSkill = MakePracticeSkill(
            "practice_service_new",
            PracticeGrowthService.ToStringName(PracticeTrackKind.Cultivation),
            "intermediate"
        );
        UnitProgress progression = new();
        LearnSkillProgress(progression, oldSkill.SkillId, 2);
        PracticeGrowthService practiceService = new();
        practiceService.Setup(
            new Dictionary<StringName, SkillDefinition>
            {
                [oldSkill.SkillId] = oldSkill,
                [newSkill.SkillId] = newSkill,
            },
            null
        );

        _test.True(
            !practiceService.ApplyReplacement(newSkill.SkillId, progression),
            "PracticeGrowthService.ApplyReplacement should require formal learning verification."
        );
        AssertSkillLearnedLevel(progression, oldSkill.SkillId, 2, "old practice skill should remain.");
        _test.True(
            progression.GetSkillProgress(newSkill.SkillId) == null,
            "unverified replacement should not write the new skill."
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        params SkillDefinition[] skillDefinitions
    )
    {
        Dictionary<StringName, SkillDefinition> indexedSkillDefinitions = new();
        foreach (SkillDefinition skillDefinition in skillDefinitions)
        {
            if (skillDefinition != null && skillDefinition.SkillId != "")
                indexedSkillDefinitions[skillDefinition.SkillId] = skillDefinition;
        }
        CharacterManagementModule manager = new();
        manager.setup(
            party,
            indexedSkillDefinitions,
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>()
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
        };
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static SkillDefinition MakePracticeSkill(
        StringName skillId,
        StringName trackType,
        StringName practiceTier,
        IReadOnlyList<StringName> tags = null,
        IReadOnlyList<StringName> knowledgeRequirements = null,
        IReadOnlyDictionary<StringName, int> skillLevelRequirements = null,
        IReadOnlyDictionary<StringName, int> attributeRequirements = null
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            maxLevel: 5,
            masteryCurve: new[] { 10, 20, 30, 40, 50 },
            tags: tags ?? new[] { trackType },
            practiceTier: practiceTier,
            knowledgeRequirements: knowledgeRequirements,
            skillLevelRequirements: skillLevelRequirements,
            attributeRequirements: attributeRequirements
        );

    private static SkillDefinition MakeBookSkill(
        StringName skillId,
        int maxLevel = 1,
        IReadOnlyList<int> masteryCurve = null
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            maxLevel: maxLevel,
            masteryCurve: masteryCurve
        );

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
