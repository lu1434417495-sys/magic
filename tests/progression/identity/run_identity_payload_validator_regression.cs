using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_identity_payload_validator_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestValidatorNoLongerRequiresGodotRegistration();
        TestValidIdentityPasses();
        TestRegistrySourcePasses();
        TestRejectsMissingRace();
        TestRejectsMissingSubrace();
        TestRejectsSubraceParentMismatch();
        TestRejectsRaceThatDoesNotListSubrace();
        TestRejectsHalfSetBloodlinePair();
        TestRejectsBloodlineStageThatDoesNotBelong();
        TestRejectsHalfSetAscensionPair();
        TestRejectsAscensionStageThatDoesNotBelong();
        TestRejectsAscensionDisallowedRace();
        TestRejectsAscensionDisallowedSubrace();
        TestRejectsAscensionDisallowedBloodline();
        TestBodySizeCacheMismatchIsNotIdentityError();

        RequestTestExit(_test.Finish("Identity payload validator regression"));
    }

    private void TestValidatorNoLongerRequiresGodotRegistration()
    {
        Type validatorType = typeof(IdentityPayloadValidator);
        _test.True(
            validatorType.IsAbstract && validatorType.IsSealed,
            "IdentityPayloadValidator 应是 static helper。"
        );
    }

    private void TestValidIdentityPasses()
    {
        PartyMemberState member = MakeMember();
        member.bloodline_id = "titan";
        member.bloodline_stage_id = "titan_awakened";
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        _test.True(errors.Count == 0, "valid identity payload should pass validation");
    }

    private void TestRegistrySourcePasses()
    {
        PartyMemberState member = MakeMember();
        GDictionary bundle = MakeIdentityBundle();
        ProgressionContentRegistry registry = MakeRegistry(bundle);

        IReadOnlyList<string> errors =
            IdentityPayloadValidator.ValidateMemberIdentityForContentSource(member, registry);

        _test.True(errors.Count == 0, "registry content source should validate the same typed identity data");
        registry.Dispose();
    }

    private void TestRejectsMissingRace()
    {
        PartyMemberState member = MakeMember();
        member.race_id = "missing_race";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "missing race should be rejected");
    }

    private void TestRejectsMissingSubrace()
    {
        PartyMemberState member = MakeMember();
        member.subrace_id = "missing_subrace";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "missing subrace should be rejected");
    }

    private void TestRejectsSubraceParentMismatch()
    {
        PartyMemberState member = MakeMember();
        GDictionary bundle = MakeIdentityBundle();
        ReadObject<SubraceDef>(ReadDictionary(bundle, "subrace_defs"), "high_human").parent_race_id =
            "elf";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog(bundle)
        );
        AssertHasAnyError(errors, "subrace parent mismatch should be rejected");
    }

    private void TestRejectsRaceThatDoesNotListSubrace()
    {
        PartyMemberState member = MakeMember();
        GDictionary bundle = MakeIdentityBundle();
        ReadObject<RaceDef>(ReadDictionary(bundle, "race_defs"), "human").subrace_ids =
            MakeStringNames(Array.Empty<StringName>());

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog(bundle)
        );
        AssertHasAnyError(errors, "race missing selected subrace should be rejected");
    }

    private void TestRejectsHalfSetBloodlinePair()
    {
        PartyMemberState member = MakeMember();
        member.bloodline_id = "titan";
        member.bloodline_stage_id = "";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "half-set bloodline pair should be rejected");
    }

    private void TestRejectsBloodlineStageThatDoesNotBelong()
    {
        PartyMemberState member = MakeMember();
        member.bloodline_id = "titan";
        member.bloodline_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "bloodline stage from another bloodline should be rejected");
    }

    private void TestRejectsHalfSetAscensionPair()
    {
        PartyMemberState member = MakeMember();
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "half-set ascension pair should be rejected");
    }

    private void TestRejectsAscensionStageThatDoesNotBelong()
    {
        PartyMemberState member = MakeMember();
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "elf_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "ascension stage from another ascension should be rejected");
    }

    private void TestRejectsAscensionDisallowedRace()
    {
        PartyMemberState member = MakeMember();
        member.race_id = "elf";
        member.subrace_id = "moon_elf";
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "ascension allowed race gate should be enforced");
    }

    private void TestRejectsAscensionDisallowedSubrace()
    {
        PartyMemberState member = MakeMember();
        member.subrace_id = "low_human";
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "ascension allowed subrace gate should be enforced");
    }

    private void TestRejectsAscensionDisallowedBloodline()
    {
        PartyMemberState member = MakeMember();
        member.ascension_id = "bloodline_locked_ascension";
        member.ascension_stage_id = "bloodline_locked_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        AssertHasAnyError(errors, "ascension allowed bloodline gate should be enforced");
    }

    private void TestBodySizeCacheMismatchIsNotIdentityError()
    {
        PartyMemberState member = MakeMember();
        member.body_size = 99;
        member.body_size_category = "boss";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(
            member,
            MakeIdentityCatalog()
        );
        _test.True(
            errors.Count == 0,
            "stale body size cache should be repairable data, not identity-invalid data"
        );
    }

    private static PartyMemberState MakeMember()
    {
        return new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            race_id = "human",
            subrace_id = "high_human",
            body_size = 2,
            body_size_category = "medium",
        };
    }

    private static GDictionary MakeIdentityBundle()
    {
        return new GDictionary
        {
            ["race_defs"] = new GDictionary
            {
                [new StringName("human")] = MakeRace(
                    "human",
                    new[] { new StringName("high_human"), new StringName("low_human") },
                    "medium"
                ),
                [new StringName("elf")] = MakeRace(
                    "elf",
                    new[] { new StringName("moon_elf") },
                    "medium"
                ),
            },
            ["subrace_defs"] = new GDictionary
            {
                [new StringName("high_human")] = MakeSubrace("high_human", "human", ""),
                [new StringName("low_human")] = MakeSubrace("low_human", "human", ""),
                [new StringName("moon_elf")] = MakeSubrace("moon_elf", "elf", ""),
            },
            ["bloodline_defs"] = new GDictionary
            {
                [new StringName("titan")] = MakeBloodline(
                    "titan",
                    new[] { new StringName("titan_awakened") }
                ),
                [new StringName("dragon")] = MakeBloodline(
                    "dragon",
                    new[] { new StringName("dragon_awakened") }
                ),
            },
            ["bloodline_stage_defs"] = new GDictionary
            {
                [new StringName("titan_awakened")] = MakeBloodlineStage("titan_awakened", "titan"),
                [new StringName("dragon_awakened")] = MakeBloodlineStage("dragon_awakened", "dragon"),
            },
            ["ascension_defs"] = new GDictionary
            {
                [new StringName("dragon_ascension")] = MakeAscension(
                    "dragon_ascension",
                    new[] { new StringName("dragon_awakened") },
                    new[] { new StringName("human") },
                    new[] { new StringName("high_human") },
                    Array.Empty<StringName>()
                ),
                [new StringName("elf_ascension")] = MakeAscension(
                    "elf_ascension",
                    new[] { new StringName("elf_awakened") },
                    new[] { new StringName("elf") },
                    new[] { new StringName("moon_elf") },
                    Array.Empty<StringName>()
                ),
                [new StringName("bloodline_locked_ascension")] = MakeAscension(
                    "bloodline_locked_ascension",
                    new[] { new StringName("bloodline_locked_awakened") },
                    Array.Empty<StringName>(),
                    Array.Empty<StringName>(),
                    new[] { new StringName("titan") }
                ),
            },
            ["ascension_stage_defs"] = new GDictionary
            {
                [new StringName("dragon_awakened")] = MakeAscensionStage(
                    "dragon_awakened",
                    "dragon_ascension",
                    "large"
                ),
                [new StringName("elf_awakened")] = MakeAscensionStage("elf_awakened", "elf_ascension", ""),
                [new StringName("bloodline_locked_awakened")] = MakeAscensionStage(
                    "bloodline_locked_awakened",
                    "bloodline_locked_ascension",
                    ""
                ),
            },
        };
    }

    private static RaceDef MakeRace(
        StringName id,
        IEnumerable<StringName> subraceIds,
        StringName bodySizeCategory
    )
    {
        RaceDef race = new()
        {
            race_id = id,
            body_size_category = bodySizeCategory,
        };
        AddStringNames(race.subrace_ids, subraceIds);
        return race;
    }

    private static SubraceDef MakeSubrace(
        StringName id,
        StringName parentRaceId,
        StringName bodySizeCategory
    )
    {
        return new SubraceDef
        {
            subrace_id = id,
            parent_race_id = parentRaceId,
            body_size_category_override = bodySizeCategory,
        };
    }

    private static BloodlineDef MakeBloodline(StringName id, IEnumerable<StringName> stageIds)
    {
        BloodlineDef bloodline = new()
        {
            bloodline_id = id,
        };
        AddStringNames(bloodline.stage_ids, stageIds);
        return bloodline;
    }

    private static BloodlineStageDef MakeBloodlineStage(
        StringName id,
        StringName bloodlineId
    )
    {
        return new BloodlineStageDef
        {
            stage_id = id,
            bloodline_id = bloodlineId,
        };
    }

    private static AscensionDef MakeAscension(
        StringName id,
        IEnumerable<StringName> stageIds,
        IEnumerable<StringName> allowedRaceIds,
        IEnumerable<StringName> allowedSubraceIds,
        IEnumerable<StringName> allowedBloodlineIds
    )
    {
        AscensionDef ascension = new()
        {
            ascension_id = id,
        };
        AddStringNames(ascension.stage_ids, stageIds);
        AddStringNames(ascension.allowed_race_ids, allowedRaceIds);
        AddStringNames(ascension.allowed_subrace_ids, allowedSubraceIds);
        AddStringNames(ascension.allowed_bloodline_ids, allowedBloodlineIds);
        return ascension;
    }

    private static AscensionStageDef MakeAscensionStage(
        StringName id,
        StringName ascensionId,
        StringName bodySizeCategory
    )
    {
        return new AscensionStageDef
        {
            stage_id = id,
            ascension_id = ascensionId,
            body_size_category_override = bodySizeCategory,
        };
    }

    private static GStringNameArray MakeStringNames(IEnumerable<StringName> values)
    {
        GStringNameArray result = new();
        AddStringNames(result, values);
        return result;
    }

    private static void AddStringNames(GStringNameArray target, IEnumerable<StringName> values)
    {
        foreach (StringName value in values)
            target.Add(value);
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog()
    {
        return MakeIdentityCatalog(MakeIdentityBundle());
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog(GDictionary bundle)
    {
        ProgressionContentRegistry registry = MakeRegistry(bundle);
        ProgressionIdentityCatalogData catalog = registry.GetIdentityCatalogTyped();
        registry.Dispose();
        return catalog;
    }

    private static ProgressionContentRegistry MakeRegistry(GDictionary bundle)
    {
        ProgressionContentRegistry registry = new();
        registry.ReplaceDefinitionsForValidation(
            new ProgressionDefinitionSources
            {
                SkillDefinitions = new Dictionary<StringName, SkillDefinition>(),
                ProfessionDefinitions = new Dictionary<StringName, ProfessionDefinition>(),
                AchievementDefinitions = new Dictionary<StringName, AchievementDefinition>(),
                QuestDefinitions = new Dictionary<StringName, QuestDefinition>(),
                ContingencyDefinitions =
                    new Dictionary<StringName, ContingencySetupTemplateDefinition>(),
                RaceDefinitions = TestProgressionDefinitionProjection.Races(
                    ReadTypedMap<RaceDef>(bundle, "race_defs")
                ),
                SubraceDefinitions = TestProgressionDefinitionProjection.Subraces(
                    ReadTypedMap<SubraceDef>(bundle, "subrace_defs")
                ),
                TraitDefinitions = new Dictionary<StringName, TraitDefinition>(),
                AgeProfileDefinitions = TestProgressionDefinitionProjection.AgeProfiles(
                    ReadTypedMap<AgeProfileDef>(bundle, "age_profile_defs")
                ),
                BloodlineDefinitions = TestProgressionDefinitionProjection.Bloodlines(
                    ReadTypedMap<BloodlineDef>(bundle, "bloodline_defs")
                ),
                BloodlineStageDefinitions =
                    TestProgressionDefinitionProjection.BloodlineStages(
                        ReadTypedMap<BloodlineStageDef>(bundle, "bloodline_stage_defs")
                    ),
                AscensionDefinitions = TestProgressionDefinitionProjection.Ascensions(
                    ReadTypedMap<AscensionDef>(bundle, "ascension_defs")
                ),
                AscensionStageDefinitions =
                    TestProgressionDefinitionProjection.AscensionStages(
                        ReadTypedMap<AscensionStageDef>(bundle, "ascension_stage_defs")
                    ),
                StageAdvancementDefinitions =
                    TestProgressionDefinitionProjection.StageAdvancements(
                        ReadTypedMap<StageAdvancementModifier>(
                            bundle,
                            "stage_advancement_defs"
                        )
                    ),
            }
        );
        return registry;
    }

    private static Dictionary<StringName, T> ReadTypedMap<T>(
        GDictionary source,
        string key
    )
        where T : class
    {
        var result = new Dictionary<StringName, T>();
        GDictionary values = ReadDictionary(source, key);
        foreach (Variant rawKey in values.Keys)
        {
            StringName id = rawKey.VariantType switch
            {
                Variant.Type.StringName => rawKey.AsStringName(),
                Variant.Type.String => new StringName(rawKey.AsString()),
                _ => new StringName(""),
            };
            if (id == "")
                continue;
            Variant rawValue = values[rawKey];
            if (rawValue.VariantType == Variant.Type.Object && rawValue.AsGodotObject() is T typed)
                result[id] = typed;
        }
        return result;
    }

    private static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return new GDictionary();
        Variant value = source[key];
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static T ReadObject<T>(GDictionary source, StringName key)
        where T : class
    {
        if (source == null || !source.ContainsKey(key))
            return null;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
    }

    private void AssertHasAnyError(IReadOnlyList<string> errors, string message)
    {
        _test.True(errors != null && errors.Count > 0, message);
    }
}
