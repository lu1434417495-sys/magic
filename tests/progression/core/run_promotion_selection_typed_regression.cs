using System;
using System.Collections.Generic;
using Godot;

public partial class run_promotion_selection_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        TestStrictRequest();
        TestPromptIdentity();
        TestAtomicPromotionAndReplay();
        TestOverlappingTagSelection();
        RequestTestExit(_test.Finish("Promotion selection typed regression"));
    }

    private void TestStrictRequest()
    {
        StringName[] input = { "slash" };
        var request = new PromotionCommitRequest("slash", 1, input, Array.Empty<StringName>());
        input[0] = "changed";
        _test.Eq(request.AssignedCoreSkillIds[0], new StringName("slash"), "Request owns immutable copied selections.");
        _test.True(PromotionCommitRequest.FromPlainPayload(request.ToPlainPayload()).SelectionEquals(request), "Complete plain payload round trips.");
        using var encoded = request.ToDictionary();
        _test.True(PromotionCommitRequest.FromPayload(encoded).SelectionEquals(request), "UI dictionary preserves explicit empty qualifiers.");
        var missing = request.ToPlainPayload();
        missing.Remove("selected_qualifier_skill_ids");
        _test.True(PromotionCommitRequest.FromPlainPayload(missing) == null, "Missing selection is rejected instead of auto choosing.");
        var extra = request.ToPlainPayload();
        extra["hp_roll_override"] = 99;
        _test.True(PromotionCommitRequest.FromPlainPayload(extra) == null, "Unknown fields are rejected.");
        _test.False(new PromotionCommitRequest("slash", 1, new StringName[] { "slash", "slash" }, Array.Empty<StringName>()).IsWellFormed, "Duplicate ids are rejected.");
        _test.False(new PromotionCommitRequest("slash", 1, null, Array.Empty<StringName>()).IsWellFormed, "Missing assigned list is invalid.");
    }

    private void TestPromptIdentity()
    {
        var request = new PromotionCommitRequest("slash", 1, new StringName[] { "slash" }, Array.Empty<StringName>());
        var choice = new GameRuntimePromotionChoiceContext("warrior", "Warrior", "", "", Array.Empty<StringName>(), "", request);
        var first = new GameRuntimePromotionPromptContext("hero", "Hero", new[] { choice });
        var second = new GameRuntimePromotionPromptContext("hero", "Hero", new[] { choice });
        first.TryGetChoice("warrior", out var issued);
        _test.True(first.ContainsChoice("hero", "warrior", issued.Selection), "Issued exact selection is accepted by prompt.");
        _test.False(second.ContainsChoice("hero", "warrior", issued.Selection), "Reopening invalidates stale prompt tokens.");
        _test.False(first.ContainsChoice("hero", "warrior", request), "Domain draft without prompt token cannot be submitted to UI.");
        var other = new GameRuntimePromotionChoiceContext("warrior", "Warrior", "", "", Array.Empty<StringName>(), "",
            new PromotionCommitRequest("guard", 1, new StringName[] { "guard" }, Array.Empty<StringName>()));
        var ambiguous = new GameRuntimePromotionPromptContext("hero", "Hero", new[] { choice, other });
        _test.False(ambiguous.TryGetChoice("warrior", out _), "Profession alone cannot choose between two growth skills.");
        _test.True(ambiguous.TryGetChoice("warrior", out _, "guard"), "Explicit growth skill resolves ambiguity.");
    }

    private void TestAtomicPromotionAndReplay()
    {
        UnitProgress progress = new() { unit_id = "hero", display_name = "Hero" };
        progress.unit_base_attributes.SetAttributeValue("hp_max", 20);
        progress.unit_base_attributes.SetAttributeValue("constitution", 10);
        progress.SetSkillProgress(new UnitSkillProgress { skill_id = "slash", is_learned = true, skill_level = 1 });
        var skill = TestSkillDefinitionProjection.BuildSkill("slash", maxLevel: 2, nonCoreMaxLevel: 1,
            attributeGrowthProgress: new Dictionary<StringName, int> { ["strength"] = 60 });
        var profession = new ProfessionDefinition("warrior", "Warrior", "", 2, 1, "full", true, "", null,
            Array.Empty<ProfessionRankRequirementDefinition>(), Array.Empty<ProfessionGrantedSkillDefinition>(),
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<ProfessionActiveConditionDefinition>(), "auto", "count_when_hidden");
        var skills = new Dictionary<StringName, SkillDefinition> { ["slash"] = skill };
        var professions = new Dictionary<StringName, ProfessionDefinition> { ["warrior"] = profession };
        var service = new ProgressionService();
        service.SetupDefinitions(progress, skills, professions);
        string before = Json.Stringify(progress.ToDictionary());
        var offers = service.GetProfessionUpgradeCandidates();
        _test.Eq(offers.Count, 1, "A non-core milestone creates a first-promotion offer without an active trigger.");
        _test.Eq(Json.Stringify(progress.ToDictionary()), before, "Eligibility query does not mutate state.");
        if (offers.Count == 0) return;
        var request = offers[0].DefaultSelection;
        _test.False(service.PreparePromotion("warrior", null).Ok, "Missing commit request is rejected.");
        var invalid = new PromotionCommitRequest("slash", 1, Array.Empty<StringName>(), Array.Empty<StringName>());
        _test.False(service.PreparePromotion("warrior", invalid).Ok, "Explicit empty assigned selection cannot promote.");
        var prepared = service.PreparePromotion("warrior", request);
        _test.True(prepared.Ok, "Complete promotion is prepared successfully.");
        _test.Eq(Json.Stringify(progress.ToDictionary()), before, "Preparation leaves original state untouched, including HP and core state.");
        if (!prepared.Ok) return;
        var candidate = prepared.Candidate;
        _test.Eq(candidate.character_level, 1, "Prepared character gains exactly one level.");
        _test.Eq(candidate.unit_base_attributes.GetAttributeValue("hp_max"), 21, "Profession HP is applied exactly once.");
        _test.True(candidate.HasUsedGrowthTrigger("slash"), "Unique growth consumption is stored in history.");
        _test.Eq(SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(skill, candidate.GetSkillProgress("slash"), candidate), 2, "History unlocks the full training cap.");
        _test.True(PromotionEligibilityRules.HasCoreQualification(skill, candidate.GetSkillProgress("slash"), candidate), "Completed 1/2 skill still qualifies for profession advancement.");
        _test.False(SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(skill, candidate.GetSkillProgress("slash"), candidate), "CoreQualified is distinct from actual CoreMax.");
        _test.True(UnitProgress.FromDictionary(candidate.ToDictionary()) != null, "New progression schema round trips.");
        using var oldVersion = candidate.ToDictionary();
        oldVersion["version"] = 1;
        _test.True(UnitProgress.FromDictionary(oldVersion) == null, "Old progression versions are rejected.");
        oldVersion["version"] = (1L << 32) + 2;
        _test.True(UnitProgress.FromDictionary(oldVersion) == null, "A wide integer cannot truncate to the current version.");
        using var wideLevel = candidate.ToDictionary();
        wideLevel["character_level"] = (1L << 32) + 1;
        _test.True(UnitProgress.FromDictionary(wideLevel) == null, "A wide integer cannot truncate to a valid character level.");
        using var wideRank = candidate.GetProfessionProgress("warrior").ToDictionary();
        wideRank["rank"] = (1L << 32) + 1;
        _test.True(UnitProfessionProgress.FromDictionary(wideRank) == null, "Profession rank must fit its runtime integer type.");
        using var oldField = candidate.ToDictionary();
        oldField["pending_profession_choices"] = new Godot.Collections.Array();
        _test.True(UnitProgress.FromDictionary(oldField) == null, "Removed persisted prompt fields are rejected.");
        var malformed = candidate.DuplicateState();
        malformed.GetProfessionProgress("warrior").AddPromotionRecord(
            malformed.GetProfessionProgress("warrior").promotion_history[0]);
        malformed.GetProfessionProgress("warrior").rank = 2;
        malformed.character_level = 2;
        _test.True(UnitProgress.FromDictionary(malformed.ToDictionary()) == null, "Duplicate history or non-sequential rank cannot load.");
        using var recordPayload = candidate.GetProfessionProgress("warrior").promotion_history[0].ToDictionary();
        recordPayload["growth_trigger_level"] = (1L << 32) + 1;
        _test.True(ProfessionPromotionRecord.FromDictionary(recordPayload) == null, "Historical milestone level cannot truncate a wide integer.");
        recordPayload["growth_trigger_level"] = 1;
        recordPayload.Remove("growth_trigger_skill_id");
        _test.True(ProfessionPromotionRecord.FromDictionary(recordPayload) == null, "A history record must identify its unique growth skill.");
        service.SetupDefinitions(candidate, skills, professions);
        _test.Eq(service.PreparePromotion("warrior", request).Failure, PromotionFailureKind.AlreadyUsed, "Replaying consumed skill cannot produce another level or growth reward.");
        _test.Eq(service.GetProfessionUpgradeCandidates().Count, 0, "Consumed skill no longer offers a promotion.");
        candidate.GetSkillProgress("slash").is_core = false;
        _test.True(candidate.HasUsedGrowthTrigger("slash"), "Leaving core slots cannot reset growth history.");
        candidate.RemoveSkillProgress("slash");
        _test.True(UnitProgress.FromDictionary(candidate.ToDictionary())?.HasUsedGrowthTrigger("slash") == true, "Forgetting a skill preserves consumed history through save/load.");
    }

    private void TestOverlappingTagSelection()
    {
        UnitProgress progress = new() { unit_id = "overlap" };
        var skills = new Dictionary<StringName, SkillDefinition>();
        void Add(string id, params StringName[] tags)
        {
            skills[id] = TestSkillDefinitionProjection.BuildSkill(id, maxLevel: 1, tags: tags);
            progress.SetSkillProgress(new UnitSkillProgress { skill_id = id, is_learned = true, skill_level = 1 });
        }
        Add("trigger", "core");
        Add("a_greedy", "a", "b", "c", "d");
        Add("b_optimal", "a", "b", "e");
        Add("c_optimal", "c", "d", "f");
        var rules = new List<TagRequirementDefinition> { new("core", 1, "core_qualified", "any", "assigned_core") };
        foreach (StringName tag in new StringName[] { "a", "b", "c", "d", "e", "f" })
            rules.Add(new(tag, 1, "learned", "any", "qualifier"));
        var requirement = new ProfessionPromotionRequirementDefinition(Array.Empty<StringName>(), rules,
            Array.Empty<ProfessionRankGateDefinition>(), Array.Empty<AttributeRequirementDefinition>(),
            Array.Empty<ReputationRequirementDefinition>(), false);
        var profession = new ProfessionDefinition("overlap", "Overlap", "", 1, 1, "full", true, "", requirement,
            Array.Empty<ProfessionRankRequirementDefinition>(), Array.Empty<ProfessionGrantedSkillDefinition>(),
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<ProfessionActiveConditionDefinition>(), "auto", "count_when_hidden");
        var service = new ProgressionService();
        service.SetupDefinitions(progress, skills, new Dictionary<StringName, ProfessionDefinition> { ["overlap"] = profession });
        var offers = service.GetProfessionUpgradeCandidates();
        _test.Eq(offers.Count, 1, "Only the selected core-tag milestone may trigger this profession.");
        if (offers.Count == 0) return;
        _test.Eq(offers[0].DefaultSelection.QualifierSkillIds.Count, 2, "Overlapping tags choose the two-skill solution, not the three-skill greedy result.");
        _test.True(service.PreparePromotion("overlap", offers[0].DefaultSelection).Ok, "The exact default selection passes the same commit validation.");
    }
}
