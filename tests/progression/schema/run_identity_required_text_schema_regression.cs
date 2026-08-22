using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_identity_required_text_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        AssertRaceErrors();
        AssertSubraceErrors();
        AssertAgeErrors();
        AssertBloodlineErrors();
        AssertAscensionErrors();
        AssertStageAdvancementErrors();
        RequestTestExit(_test.Finish("Identity required text schema regression"));
    }

    private void AssertRaceErrors()
    {
        RaceJsonDto dto = new()
        {
            RaceId = "blank_text_race", DisplayName = "   ", Description = "",
            AgeProfileId = "probe_age", DefaultSubraceId = "probe_subrace",
            SubraceIds = new[] { "probe_subrace" }, BodySizeCategory = "medium", BaseSpeed = 6,
        };
        using RaceContentRegistry registry = new(
            new IdentityJsonTestSourceReader(
                ProfessionIdentityJsonDomains.RaceDomain,
                IdentityJsonTestDocuments.Entry(dto, ProfessionIdentityJsonSerializerContext.Default.RaceJsonDto)
            ), false
        );
        registry.LoadFromDirectory("memory://races");
        AssertJsonRequiredTextErrors(registry.Validate(), "/display_name", "/description");
    }

    private void AssertSubraceErrors()
    {
        SubraceJsonDto dto = new()
        {
            SubraceId = "blank_text_subrace", ParentRaceId = "probe_race",
            DisplayName = "   ", Description = "",
        };
        using SubraceContentRegistry registry = new(
            new IdentityJsonTestSourceReader(
                ProfessionIdentityJsonDomains.SubraceDomain,
                IdentityJsonTestDocuments.Entry(dto, ProfessionIdentityJsonSerializerContext.Default.SubraceJsonDto)
            ), false
        );
        registry.LoadFromDirectory("memory://subraces");
        AssertJsonRequiredTextErrors(registry.Validate(), "/display_name", "/description");
    }

    private void AssertAgeErrors()
    {
        AgeProfileJsonDto dto = new()
        {
            ProfileId = "blank_text_age", RaceId = "probe_race",
            StageRules = new[]
            {
                new AgeStageRuleJsonDto { StageId = "adult", DisplayName = "   ", Description = "" },
            },
        };
        using AgeContentRegistry registry = new(
            new IdentityJsonTestSourceReader(
                ProfessionIdentityJsonDomains.AgeProfileDomain,
                IdentityJsonTestDocuments.Entry(dto, ProfessionIdentityJsonSerializerContext.Default.AgeProfileJsonDto)
            ), false
        );
        registry.LoadFromDirectory("memory://age_profiles");
        AssertRegistryRequiredTextErrors(
            registry.Validate(),
            "AgeProfile blank_text_age.stage_rules[0].display_name",
            "AgeProfile blank_text_age.stage_rules[0].description"
        );
    }

    private void AssertBloodlineErrors()
    {
        string root = IdentityJsonTestDocuments.Bloodline(
            "blank_text_bloodline", "bloodline",
            new BloodlineJsonDto
            {
                BloodlineId = "blank_text_bloodline", DisplayName = "   ", Description = "",
                StageIds = new[] { "blank_text_bloodline_stage" },
            }
        );
        string stage = IdentityJsonTestDocuments.BloodlineStage(
            "blank_text_bloodline_stage",
            new BloodlineStageJsonDto
            {
                StageId = "blank_text_bloodline_stage", BloodlineId = "blank_text_bloodline",
                DisplayName = "   ", Description = "",
            }
        );
        using BloodlineContentRegistry registry = new(
            new IdentityJsonTestSourceReader(ProfessionIdentityJsonDomains.BloodlineDomain, root, stage), false
        );
        registry.LoadFromDirectory("memory://bloodlines");
        AssertJsonRequiredTextErrors(registry.Validate(), "/payload/display_name", "/payload/description");
    }

    private void AssertAscensionErrors()
    {
        string root = IdentityJsonTestDocuments.Ascension(
            "blank_text_ascension", "ascension",
            new AscensionJsonDto
            {
                AscensionId = "blank_text_ascension", DisplayName = "   ", Description = "",
                StageIds = new[] { "blank_text_ascension_stage" },
            }
        );
        string stage = IdentityJsonTestDocuments.AscensionStage(
            "blank_text_ascension_stage",
            new AscensionStageJsonDto
            {
                StageId = "blank_text_ascension_stage", AscensionId = "blank_text_ascension",
                DisplayName = "   ", Description = "",
            }
        );
        using AscensionContentRegistry registry = new(
            new IdentityJsonTestSourceReader(ProfessionIdentityJsonDomains.AscensionDomain, root, stage), false
        );
        registry.LoadFromDirectory("memory://ascensions");
        AssertJsonRequiredTextErrors(registry.Validate(), "/payload/display_name", "/payload/description");
    }

    private void AssertStageAdvancementErrors()
    {
        StageAdvancementJsonDto dto = new()
        {
            ModifierId = "blank_text_stage_advancement", DisplayName = "   ",
            TargetAxis = "full", StageOffset = 1,
        };
        using StageAdvancementContentRegistry registry = new(
            new IdentityJsonTestSourceReader(
                ProfessionIdentityJsonDomains.StageAdvancementDomain,
                IdentityJsonTestDocuments.Entry(dto, ProfessionIdentityJsonSerializerContext.Default.StageAdvancementJsonDto)
            ), false
        );
        registry.LoadFromDirectory("memory://stage_advancements");
        AssertJsonRequiredTextErrors(registry.Validate(), "/display_name");
    }

    private void AssertJsonRequiredTextErrors(GStringArray errors, params string[] pointers)
    {
        string formatted = string.Join(" | ", errors);
        foreach (string pointer in pointers)
        {
            _test.True(
                formatted.Contains(pointer) && formatted.Contains("Value must be a non-empty string."),
                $"strict JSON required-text validation 应定位 {pointer}。 errors={formatted}"
            );
        }
    }

    private void AssertRegistryRequiredTextErrors(GStringArray errors, params string[] labels)
    {
        string formatted = string.Join(" | ", errors);
        foreach (string label in labels)
        {
            _test.True(
                formatted.Contains($"{label} must be a non-empty String."),
                $"Definition validation 应拒绝空白文本 {label}。 errors={formatted}"
            );
        }
    }
}
