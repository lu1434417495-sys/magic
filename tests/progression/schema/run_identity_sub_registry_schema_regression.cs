using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_identity_sub_registry_schema_regression : LifecycleTestSceneTree
{
    private const string TempRoot = "user://identity_save_tag_schema_regression";
    private const string TempRaceDirectory =
        "user://identity_save_tag_schema_regression/races";
    private const string TempRacePath =
        "user://identity_save_tag_schema_regression/races/invalid_save_tags_race.tres";
    private const string TempSubraceDirectory =
        "user://identity_save_tag_schema_regression/subraces";
    private const string TempSubracePath =
        "user://identity_save_tag_schema_regression/subraces/invalid_save_tags_subrace.tres";

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
            catalog.RaceDefs.Count > 0,
            "identity catalog 应从正式配置加载 race definitions。"
        );
        _test.True(
            catalog.SubraceDefs.Count > 0,
            "identity catalog 应从正式配置加载 subrace definitions。"
        );
        _test.True(
            catalog.AgeProfileDefs.Count > 0,
            "identity catalog 应从正式配置加载 age profile definitions。"
        );
        _test.True(
            catalog.StageAdvancementDefs.Count > 0,
            "identity catalog 应从正式配置加载 stage advancement definitions。"
        );
    }

    private void TestRaceAndSubraceRegistriesRejectInvalidSaveTagLists()
    {
        CleanupTempContent();
        try
        {
            _test.Eq(
                DirAccess.MakeDirRecursiveAbsolute(
                    ProjectSettings.GlobalizePath(TempRaceDirectory)
                ),
                Error.Ok,
                "应能创建 race save-tag schema 临时目录。"
            );
            _test.Eq(
                DirAccess.MakeDirRecursiveAbsolute(
                    ProjectSettings.GlobalizePath(TempSubraceDirectory)
                ),
                Error.Ok,
                "应能创建 subrace save-tag schema 临时目录。"
            );

            using RaceDef raceDef = new()
            {
                race_id = "invalid_save_tags_race",
                display_name = "Invalid Save Tags Race",
                description = "Save tag schema fixture.",
                age_profile_id = "human_age_profile",
                default_subrace_id = "common_human",
                subrace_ids = new GStringNameArray { "common_human" },
                body_size_category = "medium",
                base_speed = 6,
                save_advantage_tags = new GStringNameArray { "poison", "poison" },
                save_disadvantage_tags = new GStringNameArray { "not_a_save_tag" },
                save_immunity_tags = new GStringNameArray { "sleep_immunity" },
            };
            _test.Eq(
                ResourceSaver.Save(raceDef, TempRacePath),
                Error.Ok,
                "应能写入 race save-tag schema fixture。"
            );

            using SubraceDef subraceDef = new()
            {
                subrace_id = "invalid_save_tags_subrace",
                parent_race_id = "human",
                display_name = "Invalid Save Tags Subrace",
                description = "Save tag schema fixture.",
                save_advantage_tags = new GStringNameArray { "magic", "magic" },
                save_disadvantage_tags = new GStringNameArray { "unknown_save_tag" },
                save_immunity_tags = new GStringNameArray { "poison_advantage" },
            };
            _test.Eq(
                ResourceSaver.Save(subraceDef, TempSubracePath),
                Error.Ok,
                "应能写入 subrace save-tag schema fixture。"
            );

            using TestContentResourceLoader raceLoader = new();
            using RaceContentRegistry raceRegistry = new(
                raceLoader,
                loadDefaultContent: false
            );
            raceRegistry.LoadFromDirectory(TempRaceDirectory);
            AssertInvalidSaveTagListErrors(
                raceRegistry.Validate(),
                "Race invalid_save_tags_race"
            );

            using TestContentResourceLoader subraceLoader = new();
            using SubraceContentRegistry subraceRegistry = new(
                subraceLoader,
                loadDefaultContent: false
            );
            subraceRegistry.LoadFromDirectory(TempSubraceDirectory);
            AssertInvalidSaveTagListErrors(
                subraceRegistry.Validate(),
                "Subrace invalid_save_tags_subrace"
            );
        }
        finally
        {
            CleanupTempContent();
        }
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

    private static void CleanupTempContent()
    {
        RemoveFile(TempRacePath);
        RemoveFile(TempSubracePath);
        RemoveDirectory(TempRaceDirectory);
        RemoveDirectory(TempSubraceDirectory);
        RemoveDirectory(TempRoot);
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
