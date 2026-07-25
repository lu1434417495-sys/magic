using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_identity_required_text_schema_regression : LifecycleTestSceneTree
{
    private const string TempRoot = "user://identity_required_text_schema_regression";
    private const string RaceDirectory =
        "user://identity_required_text_schema_regression/races";
    private const string RacePath =
        "user://identity_required_text_schema_regression/races/blank_text_race.tres";
    private const string SubraceDirectory =
        "user://identity_required_text_schema_regression/subraces";
    private const string SubracePath =
        "user://identity_required_text_schema_regression/subraces/blank_text_subrace.tres";
    private const string AgeDirectory =
        "user://identity_required_text_schema_regression/age_profiles";
    private const string AgePath =
        "user://identity_required_text_schema_regression/age_profiles/blank_text_age.tres";
    private const string BloodlineDirectory =
        "user://identity_required_text_schema_regression/bloodlines";
    private const string BloodlinePath =
        "user://identity_required_text_schema_regression/bloodlines/blank_text_bloodline.tres";
    private const string BloodlineStagePath =
        "user://identity_required_text_schema_regression/bloodlines/blank_text_bloodline_stage.tres";
    private const string AscensionDirectory =
        "user://identity_required_text_schema_regression/ascensions";
    private const string AscensionPath =
        "user://identity_required_text_schema_regression/ascensions/blank_text_ascension.tres";
    private const string AscensionStagePath =
        "user://identity_required_text_schema_regression/ascensions/blank_text_ascension_stage.tres";
    private const string StageAdvancementDirectory =
        "user://identity_required_text_schema_regression/stage_advancements";
    private const string StageAdvancementPath =
        "user://identity_required_text_schema_regression/stage_advancements/blank_text_stage_advancement.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestIdentityRegistriesRejectBlankRequiredText();
        RequestTestExit(_test.Finish("Identity required text schema regression"));
    }

    private void TestIdentityRegistriesRejectBlankRequiredText()
    {
        CleanupTempContent();
        try
        {
            CreateTempDirectories();
            SaveFixtures();

            AssertRaceErrors();
            AssertSubraceErrors();
            AssertAgeErrors();
            AssertBloodlineErrors();
            AssertAscensionErrors();
            AssertStageAdvancementErrors();
        }
        finally
        {
            CleanupTempContent();
        }
    }

    private void CreateTempDirectories()
    {
        foreach (
            string directoryPath in new[]
            {
                RaceDirectory,
                SubraceDirectory,
                AgeDirectory,
                BloodlineDirectory,
                AscensionDirectory,
                StageAdvancementDirectory,
            }
        )
        {
            _test.Eq(
                DirAccess.MakeDirRecursiveAbsolute(
                    ProjectSettings.GlobalizePath(directoryPath)
                ),
                Error.Ok,
                $"应能创建必填文本校验临时目录 {directoryPath}。"
            );
        }
    }

    private void SaveFixtures()
    {
        using RaceDef raceDef = new()
        {
            race_id = "blank_text_race",
            display_name = "   ",
            description = "",
            age_profile_id = "probe_age",
            default_subrace_id = "probe_subrace",
            subrace_ids = new GStringNameArray { "probe_subrace" },
            body_size_category = "medium",
            base_speed = 6,
        };
        SaveFixture(raceDef, RacePath);

        using SubraceDef subraceDef = new()
        {
            subrace_id = "blank_text_subrace",
            parent_race_id = "probe_race",
            display_name = "   ",
            description = "",
        };
        SaveFixture(subraceDef, SubracePath);

        using AgeStageRule ageStage = new()
        {
            stage_id = "adult",
            display_name = "   ",
            description = "",
        };
        using AgeProfileDef ageProfile = new()
        {
            profile_id = "blank_text_age",
            race_id = "probe_race",
            stage_rules = new Godot.Collections.Array<AgeStageRule> { ageStage },
        };
        SaveFixture(ageProfile, AgePath);

        using BloodlineDef bloodlineDef = new()
        {
            bloodline_id = "blank_text_bloodline",
            display_name = "   ",
            description = "",
            stage_ids = new GStringNameArray { "blank_text_bloodline_stage" },
        };
        SaveFixture(bloodlineDef, BloodlinePath);

        using BloodlineStageDef bloodlineStage = new()
        {
            stage_id = "blank_text_bloodline_stage",
            bloodline_id = "blank_text_bloodline",
            display_name = "   ",
            description = "",
        };
        SaveFixture(bloodlineStage, BloodlineStagePath);

        using AscensionDef ascensionDef = new()
        {
            ascension_id = "blank_text_ascension",
            display_name = "   ",
            description = "",
            stage_ids = new GStringNameArray { "blank_text_ascension_stage" },
        };
        SaveFixture(ascensionDef, AscensionPath);

        using AscensionStageDef ascensionStage = new()
        {
            stage_id = "blank_text_ascension_stage",
            ascension_id = "blank_text_ascension",
            display_name = "   ",
            description = "",
        };
        SaveFixture(ascensionStage, AscensionStagePath);

        using StageAdvancementModifier stageAdvancement = new()
        {
            modifier_id = "blank_text_stage_advancement",
            display_name = "   ",
            target_axis = "full",
            stage_offset = 1,
        };
        SaveFixture(stageAdvancement, StageAdvancementPath);
    }

    private void SaveFixture(Resource resource, string path)
    {
        _test.Eq(
            ResourceSaver.Save(resource, path),
            Error.Ok,
            $"应能写入必填文本校验 fixture {path}。"
        );
    }

    private void AssertRaceErrors()
    {
        using TestContentResourceLoader loader = new();
        using RaceContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.LoadFromDirectory(RaceDirectory);
        AssertRequiredTextErrors(
            registry.Validate(),
            "Race blank_text_race.display_name",
            "Race blank_text_race.description"
        );
    }

    private void AssertSubraceErrors()
    {
        using TestContentResourceLoader loader = new();
        using SubraceContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.LoadFromDirectory(SubraceDirectory);
        AssertRequiredTextErrors(
            registry.Validate(),
            "Subrace blank_text_subrace.display_name",
            "Subrace blank_text_subrace.description"
        );
    }

    private void AssertAgeErrors()
    {
        using TestContentResourceLoader loader = new();
        using AgeContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.LoadFromDirectory(AgeDirectory);
        AssertRequiredTextErrors(
            registry.Validate(),
            "AgeProfile blank_text_age.stage_rules[0].display_name",
            "AgeProfile blank_text_age.stage_rules[0].description"
        );
    }

    private void AssertBloodlineErrors()
    {
        using TestContentResourceLoader loader = new();
        using BloodlineContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.LoadFromDirectory(BloodlineDirectory);
        AssertRequiredTextErrors(
            registry.Validate(),
            "Bloodline blank_text_bloodline.display_name",
            "Bloodline blank_text_bloodline.description",
            "BloodlineStage blank_text_bloodline_stage.display_name",
            "BloodlineStage blank_text_bloodline_stage.description"
        );
    }

    private void AssertAscensionErrors()
    {
        using TestContentResourceLoader loader = new();
        using AscensionContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.LoadFromDirectory(AscensionDirectory);
        AssertRequiredTextErrors(
            registry.Validate(),
            "Ascension blank_text_ascension.display_name",
            "Ascension blank_text_ascension.description",
            "AscensionStage blank_text_ascension_stage.display_name",
            "AscensionStage blank_text_ascension_stage.description"
        );
    }

    private void AssertStageAdvancementErrors()
    {
        using TestContentResourceLoader loader = new();
        using StageAdvancementContentRegistry registry = new(
            loader,
            loadDefaultContent: false
        );
        registry.LoadFromDirectory(StageAdvancementDirectory);
        AssertRequiredTextErrors(
            registry.Validate(),
            "StageAdvancement blank_text_stage_advancement.display_name"
        );
    }

    private void AssertRequiredTextErrors(
        GStringArray errors,
        params string[] expectedFieldLabels
    )
    {
        string formattedErrors = string.Join(" | ", errors);
        foreach (string fieldLabel in expectedFieldLabels)
        {
            _test.True(
                formattedErrors.Contains(
                    $"{fieldLabel} must be a non-empty String."
                ),
                $"{fieldLabel} 应拒绝空串或纯空白文本。 errors={formattedErrors}"
            );
        }
    }

    private static void CleanupTempContent()
    {
        foreach (
            string filePath in new[]
            {
                RacePath,
                SubracePath,
                AgePath,
                BloodlinePath,
                BloodlineStagePath,
                AscensionPath,
                AscensionStagePath,
                StageAdvancementPath,
            }
        )
        {
            RemoveFile(filePath);
        }

        foreach (
            string directoryPath in new[]
            {
                RaceDirectory,
                SubraceDirectory,
                AgeDirectory,
                BloodlineDirectory,
                AscensionDirectory,
                StageAdvancementDirectory,
                TempRoot,
            }
        )
        {
            RemoveDirectory(directoryPath);
        }
    }

    private static void RemoveFile(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (FileAccess.FileExists(absolutePath))
            DirAccess.RemoveAbsolute(absolutePath);
    }

    private static void RemoveDirectory(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (DirAccess.DirExistsAbsolute(absolutePath))
            DirAccess.RemoveAbsolute(absolutePath);
    }
}
