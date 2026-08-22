using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_identity_sub_registry_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestIdentityCatalogLoadsTypedContent();
        TestRaceAndSubraceRegistriesRejectInvalidSaveTagLists();
        RequestTestExit(_test.Finish("Identity sub registry schema regression"));
    }

    private void TestIdentityCatalogLoadsTypedContent()
    {
        using var registry = new ProgressionContentRegistry(new TestContentResourceLoader());
        ProgressionIdentityCatalogData catalog = registry.GetIdentityCatalogTyped();

        _test.True(
            catalog.RaceDefs.TryGetValue("human", out RaceDefinition humanRace),
            "identity catalog 应从正式配置加载 human race definition。"
        );
        if (humanRace != null)
        {
            _test.Eq(
                humanRace.DefaultSubraceId,
                new StringName("common_human"),
                "human definition 应保留 canonical default subrace 关系。"
            );
            _test.Eq(
                humanRace.AgeProfileId,
                new StringName("human_age_profile"),
                "human definition 应保留 canonical age profile 关系。"
            );
        }

        _test.True(
            catalog.SubraceDefs.TryGetValue(
                "common_human",
                out SubraceDefinition commonHuman
            ),
            "identity catalog 应从正式配置加载 common_human subrace definition。"
        );
        if (commonHuman != null)
        {
            _test.Eq(
                commonHuman.ParentRaceId,
                new StringName("human"),
                "common_human definition 应保留 canonical parent race 关系。"
            );
        }

        _test.True(
            catalog.AgeProfileDefs.TryGetValue(
                "human_age_profile",
                out AgeProfileDefinition humanAgeProfile
            ),
            "identity catalog 应从正式配置加载 human_age_profile definition。"
        );
        if (humanAgeProfile != null)
        {
            _test.Eq(
                humanAgeProfile.RaceId,
                new StringName("human"),
                "human_age_profile definition 应保留 canonical race 关系。"
            );
        }

        _test.True(
            catalog.StageAdvancementDefs.TryGetValue(
                "empty_stage_advancement",
                out StageAdvancementDefinition emptyStageAdvancement
            ),
            "identity catalog 应从正式配置加载 empty_stage_advancement sentinel。"
        );
        if (emptyStageAdvancement != null)
        {
            _test.Eq(
                emptyStageAdvancement.ModifierId,
                new StringName("empty_stage_advancement"),
                "stage advancement sentinel 应保留 canonical modifier id。"
            );
            _test.Eq(
                emptyStageAdvancement.TargetAxis,
                new StringName("full"),
                "stage advancement sentinel 应保留 typed target axis。"
            );
        }
    }

    private void TestRaceAndSubraceRegistriesRejectInvalidSaveTagLists()
    {
        RaceJsonDto race = new()
        {
            RaceId = "invalid_save_tags_race",
            DisplayName = "Invalid Save Tags Race",
            Description = "Save tag schema fixture.",
            AgeProfileId = "human_age_profile",
            DefaultSubraceId = "common_human",
            SubraceIds = new[] { "common_human" },
            BodySizeCategory = "medium",
            BaseSpeed = 6,
            SaveAdvantageTags = new[] { "poison", "poison" },
            SaveDisadvantageTags = new[] { "not_a_save_tag" },
            SaveImmunityTags = new[] { "sleep_immunity" },
        };
        using RaceContentRegistry raceRegistry = new(
            new IdentityJsonTestSourceReader(
                ProfessionIdentityJsonDomains.RaceDomain,
                IdentityJsonTestDocuments.Entry(race, ProfessionIdentityJsonSerializerContext.Default.RaceJsonDto)
            ), false
        );
        raceRegistry.LoadFromDirectory("memory://races");
        AssertInvalidSaveTagListErrors(raceRegistry.Validate(), "Race invalid_save_tags_race");

        SubraceJsonDto subrace = new()
        {
            SubraceId = "invalid_save_tags_subrace",
            ParentRaceId = "human",
            DisplayName = "Invalid Save Tags Subrace",
            Description = "Save tag schema fixture.",
            SaveAdvantageTags = new[] { "magic", "magic" },
            SaveDisadvantageTags = new[] { "unknown_save_tag" },
            SaveImmunityTags = new[] { "poison_advantage" },
        };
        using SubraceContentRegistry subraceRegistry = new(
            new IdentityJsonTestSourceReader(
                ProfessionIdentityJsonDomains.SubraceDomain,
                IdentityJsonTestDocuments.Entry(subrace, ProfessionIdentityJsonSerializerContext.Default.SubraceJsonDto)
            ), false
        );
        subraceRegistry.LoadFromDirectory("memory://subraces");
        AssertInvalidSaveTagListErrors(subraceRegistry.Validate(), "Subrace invalid_save_tags_subrace");
    }

    private void AssertInvalidSaveTagListErrors(GStringArray errors, string ownerLabel)
    {
        string formattedErrors = string.Join(" | ", errors);
        _test.True(
            formattedErrors.Contains(ownerLabel)
                && formattedErrors.Contains("duplicates save tag"),
            $"{ownerLabel} 应拒绝重复 save tag。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains(ownerLabel)
                && formattedErrors.Contains("not a supported save tag"),
            $"{ownerLabel} 应拒绝未知 save tag。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains(ownerLabel)
                && formattedErrors.Contains("removed suffix syntax"),
            $"{ownerLabel} 应拒绝旧后缀 save tag。 errors={formattedErrors}"
        );
    }

}
