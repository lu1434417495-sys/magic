using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_practice_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPracticeGrowthServiceNoLongerRequiresGodotRegistration();
        TestPracticeReplacementRequiresConfirmation();
        TestPracticeReplacementUsesFormalLearningValidation();
        TestPracticeReplacementSucceedsAfterFormalLearningValidation();
        TestPracticeReplacementRejectsAmbiguousExistingTrack();
        TestPracticeTrackTagsFailClosedAtRuntime();
        TestPracticeReplacementServiceRequiresVerifiedLearning();

        if (_failures.Count == 0)
        {
            GD.Print("Character management practice regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Character management practice regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestPracticeGrowthServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(PracticeGrowthService);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(serviceType),
            "PracticeGrowthService should be a plain C# service, not a GodotObject/RefCounted."
        );
        AssertTrue(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                == 0,
            "PracticeGrowthService should not remain registered as a Godot GlobalClass."
        );
        AssertEq(
            serviceType.GetField("PracticeTracks", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "PracticeGrowthService should keep valid tracks in a C# HashSet."
        );
        AssertEq(
            serviceType.GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "PracticeGrowthService should cache skill defs in a typed C# dictionary."
        );
        AssertTrue(
            serviceType.GetMethod("can_learn_practice_skill") == null,
            "PracticeGrowthService should not keep the unused Godot Dictionary can-learn wrapper."
        );
        AssertTrue(
            serviceType.GetMethod("get_skill_learned_status") == null,
            "PracticeGrowthService should not keep the unused Godot Dictionary learned-status wrapper."
        );
    }

    private void TestPracticeReplacementRequiresConfirmation()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_confirm_old",
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_confirm_new",
            PracticeGrowthService.TRACK_MEDITATION,
            "intermediate"
        );
        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.get_member_state("hero").progression;
        LearnSkillProgress(progression, oldSkill.skill_id, 3);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill);

        GDictionary status = manager.get_practice_skill_learn_status("hero", newSkill.skill_id);
        AssertTrue(ReadBool(status, "is_practice_skill"), "replacement status should mark practice skills.");
        AssertTrue(ReadBool(status, "needs_replacement"), "replacement status should require replacement.");
        AssertEq(
            ReadString(status, "existing_skill_id"),
            "practice_confirm_old",
            "replacement status should expose the existing skill."
        );
        AssertEq(
            ReadInt(status, "predicted_level"),
            2,
            "replacement status should expose the predicted replacement level."
        );

        AssertTrue(
            !manager.learn_skill("hero", newSkill.skill_id),
            "practice replacement should require explicit confirmation."
        );
        AssertSkillLearnedLevel(progression, oldSkill.skill_id, 3, "old practice skill should remain.");
        AssertTrue(
            progression.get_skill_progress(newSkill.skill_id) == null,
            "new practice skill should not be learned without confirmation."
        );
    }

    private void TestPracticeReplacementUsesFormalLearningValidation()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_validation_old",
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_validation_new",
            PracticeGrowthService.TRACK_MEDITATION,
            "intermediate"
        );
        newSkill.knowledge_requirements.Add("missing_practice_lore");

        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.get_member_state("hero").progression;
        LearnSkillProgress(progression, oldSkill.skill_id, 3);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill);

        AssertTrue(
            !manager.learn_skill(
                "hero",
                newSkill.skill_id,
                new GDictionary { ["confirm_practice_replacement"] = true }
            ),
            "practice replacement should not bypass formal learning requirements."
        );
        AssertSkillLearnedLevel(progression, oldSkill.skill_id, 3, "old practice skill should remain.");
        AssertTrue(
            progression.get_skill_progress(newSkill.skill_id) == null,
            "new practice skill should not be written when requirements fail."
        );
    }

    private void TestPracticeReplacementSucceedsAfterFormalLearningValidation()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_success_old",
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_success_new",
            PracticeGrowthService.TRACK_MEDITATION,
            "intermediate"
        );
        SkillDef prerequisite = MakeBookSkill("practice_success_prerequisite");
        prerequisite.max_level = 3;
        prerequisite.mastery_curve = new[] { 10, 20, 30 };
        newSkill.knowledge_requirements.Add("practice_lore");
        newSkill.skill_level_requirements[prerequisite.skill_id] = 2;
        newSkill.attribute_requirements["strength"] = 3;

        PartyState party = BuildPartyWithMember("hero");
        PartyMemberState member = party.get_member_state("hero");
        UnitProgress progression = member.progression;
        progression.learn_knowledge("practice_lore");
        progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.STRENGTH(), 3);
        LearnSkillProgress(progression, oldSkill.skill_id, 3);
        LearnSkillProgress(progression, prerequisite.skill_id, 2);
        CharacterManagementModule manager = BuildManager(party, oldSkill, newSkill, prerequisite);

        AssertTrue(
            manager.learn_skill(
                "hero",
                newSkill.skill_id,
                new GDictionary { ["confirm_practice_replacement"] = true }
            ),
            "practice replacement should succeed after formal validation passes."
        );
        AssertTrue(
            progression.get_skill_progress(oldSkill.skill_id) == null,
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
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        SkillDef secondOld = MakePracticeSkill(
            "practice_ambiguous_old_b",
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_ambiguous_new",
            PracticeGrowthService.TRACK_MEDITATION,
            "intermediate"
        );
        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.get_member_state("hero").progression;
        LearnSkillProgress(progression, firstOld.skill_id, 2);
        LearnSkillProgress(progression, secondOld.skill_id, 4);
        CharacterManagementModule manager = BuildManager(party, firstOld, secondOld, newSkill);

        GDictionary status = manager.get_practice_skill_learn_status("hero", newSkill.skill_id);
        AssertEq(
            ReadString(status, "error_code"),
            "ambiguous_existing_practice_track",
            "ambiguous replacement status should preserve the boundary error code."
        );
        AssertTrue(
            !manager.learn_skill(
                "hero",
                newSkill.skill_id,
                new GDictionary { ["confirm_practice_replacement"] = true }
            ),
            "ambiguous practice replacement should fail."
        );
        AssertSkillLearnedLevel(progression, firstOld.skill_id, 2, "first old practice should remain.");
        AssertSkillLearnedLevel(progression, secondOld.skill_id, 4, "second old practice should remain.");
        AssertTrue(
            progression.get_skill_progress(newSkill.skill_id) == null,
            "ambiguous replacement should not write the new skill."
        );
    }

    private void TestPracticeTrackTagsFailClosedAtRuntime()
    {
        SkillDef dualTrack = MakePracticeSkill(
            "practice_dual_track_runtime",
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        dualTrack.tags.Add(PracticeGrowthService.TRACK_CULTIVATION);
        SkillDef extraTag = MakePracticeSkill(
            "practice_extra_tag_runtime",
            PracticeGrowthService.TRACK_MEDITATION,
            "basic"
        );
        extraTag.tags.Add("passive");

        foreach (SkillDef skill in new[] { dualTrack, extraTag })
        {
            PartyState party = BuildPartyWithMember("hero");
            UnitProgress progression = party.get_member_state("hero").progression;
            CharacterManagementModule manager = BuildManager(party, skill);
            AssertTrue(
                !manager.learn_skill(
                    "hero",
                    skill.skill_id,
                    new GDictionary { ["confirm_practice_replacement"] = true }
                ),
                "invalid practice tag configuration should fail closed."
            );
            AssertTrue(
                progression.get_skill_progress(skill.skill_id) == null,
                "invalid practice tag configuration should not write learned progress."
            );
        }
    }

    private void TestPracticeReplacementServiceRequiresVerifiedLearning()
    {
        SkillDef oldSkill = MakePracticeSkill(
            "practice_service_old",
            PracticeGrowthService.TRACK_CULTIVATION,
            "basic"
        );
        SkillDef newSkill = MakePracticeSkill(
            "practice_service_new",
            PracticeGrowthService.TRACK_CULTIVATION,
            "intermediate"
        );
        UnitProgress progression = new();
        LearnSkillProgress(progression, oldSkill.skill_id, 2);
        PracticeGrowthService practiceService = new();
        practiceService.setup(
            new GDictionary
            {
                [oldSkill.skill_id] = oldSkill,
                [newSkill.skill_id] = newSkill,
            },
            new GDictionary()
        );

        AssertTrue(
            !practiceService.apply_replacement(newSkill.skill_id, progression),
            "PracticeGrowthService.apply_replacement should require formal learning verification."
        );
        AssertSkillLearnedLevel(progression, oldSkill.skill_id, 2, "old practice skill should remain.");
        AssertTrue(
            progression.get_skill_progress(newSkill.skill_id) == null,
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
        party.set_member_state(member);
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
        skill.tags.Add(trackType);
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

    private static void LearnSkillProgress(
        UnitProgress progression,
        StringName skillId,
        int skillLevel
    )
    {
        progression?.set_skill_progress(
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
        UnitSkillProgress skillProgress = progression?.get_skill_progress(skillId);
        AssertTrue(skillProgress != null && skillProgress.is_learned, message);
        if (skillProgress == null)
            return;
        AssertEq(skillProgress.skill_level, expectedLevel, $"{message} Level should match.");
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
