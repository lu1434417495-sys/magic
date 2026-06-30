using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_progression_content_registry_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialProgressionRegistryTypedBoundaryMatchesPublicBoundary();
        TestCustomProgressionValidationSourcesTypedBoundaryMatchesPublicBoundary();
        TestDefinitionBucketsSyncIntoTypedIndexes();
        TestTraitDefinitionBucketsSyncIntoTypedIndexes();
        TestIdentityCatalogTypedBoundaryMatchesPublicBuckets();
        TestEquipmentAbilityRegistryStartsEmptyAndValid();

        Quit(_test.Finish("Progression content registry typed regression"));
    }

    private void TestOfficialProgressionRegistryTypedBoundaryMatchesPublicBoundary()
    {
        using ProgressionContentRegistry registry = new();

        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GStringArray projectedErrors = registry.Validate();

        _test.Eq(
            typedErrors.Count,
            projectedErrors.Count,
            "progression registry typed/public validation error 数量应保持一致。"
        );
        _test.Eq(
            projectedErrors.Count,
            0,
            $"正式 progression registry 不应报错: {FormatErrors(projectedErrors)}"
        );
    }

    private void TestCustomProgressionValidationSourcesTypedBoundaryMatchesPublicBoundary()
    {
        using ProgressionContentRegistry registry = new();
        registry.ReplaceValidationSources(BuildCustomValidationSources());

        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GStringArray projectedErrors = registry.Validate();

        _test.Eq(
            typedErrors.Count,
            projectedErrors.Count,
            "custom progression validation sources 的 typed/public validation error 数量应保持一致。"
        );
        _test.True(
            typedErrors.Count >= 4,
            $"custom progression validation sources 应保持非法。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestDefinitionBucketsSyncIntoTypedIndexes()
    {
        using ProgressionContentRegistry registry = new();
        GDictionary sources = BuildCustomValidationSources();
        GDictionary skillDefs = (GDictionary)sources["skill_defs"];
        GDictionary achievementDefs = new()
        {
            [new StringName("broken_achievement")] = ((GDictionary)sources["achievement_defs"])[
                new StringName("broken_achievement")
            ],
        };
        GDictionary raceDefs = new()
        {
            [new StringName("human")] = ((GDictionary)sources["race_defs"])[new StringName("human")],
            ["string_key_race"] = new RaceDef
            {
                race_id = "string_key_race",
                display_name = "String Key Race",
                description = "",
                age_profile_id = "human_profile",
            },
        };
        GDictionary ageProfileDefs = new()
        {
            [new StringName("human_profile")] = ((GDictionary)sources["age_profile_defs"])[
                new StringName("human_profile")
            ],
        };
        GDictionary stageAdvancementDefs = new()
        {
            [new StringName("broken_stage_cap")] = ((GDictionary)
                sources["stage_advancement_defs"])[new StringName("broken_stage_cap")],
        };

        registry.ReplaceDefinitionBuckets(
            new GDictionary
            {
                ["skill_defs"] = skillDefs,
                ["profession_defs"] = new GDictionary(),
                ["achievement_defs"] = achievementDefs,
                ["quest_defs"] = new GDictionary(),
                ["race_defs"] = raceDefs,
                ["subrace_defs"] = new GDictionary(),
                ["trait_defs"] = new GDictionary(),
                ["age_profile_defs"] = ageProfileDefs,
                ["bloodline_defs"] = new GDictionary(),
                ["bloodline_stage_defs"] = new GDictionary(),
                ["ascension_defs"] = new GDictionary(),
                ["ascension_stage_defs"] = new GDictionary(),
                ["stage_advancement_defs"] = stageAdvancementDefs,
            }
        );

        _test.True(
            registry.GetSkillDefinitionsTyped().ContainsKey("known_skill"),
            "SkillDefinition getter 应能看到 definition bucket 的替换内容。"
        );
        _test.True(
            registry.GetAchievementDefsTyped().ContainsKey("broken_achievement"),
            "typed achievement getter 应能看到 definition bucket 的替换内容。"
        );
        _test.True(
            registry.GetRaceDefsTyped().ContainsKey("human"),
            "typed race getter 应能看到 definition bucket 的替换内容。"
        );
        _test.True(
            !registry.GetRaceDefsTyped().ContainsKey("string_key_race"),
            "typed race getter 不应把 definition bucket 里的 string key 恢复成正式 StringName 内容。"
        );
        _test.True(
            registry.GetAgeProfileDefsTyped().ContainsKey("human_profile"),
            "typed age profile getter 应能看到 definition bucket 的替换内容。"
        );
        _test.True(
            registry.GetStageAdvancementDefsTyped().ContainsKey("broken_stage_cap"),
            "typed stage advancement getter 应能看到 definition bucket 的替换内容。"
        );

        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        _test.True(
            typedErrors.Count >= 2,
            $"definition bucket 替换后，typed validation 仍应读取 typed index。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestTraitDefinitionBucketsSyncIntoTypedIndexes()
    {
        using ProgressionContentRegistry registry = new();
        _test.True(
            registry.GetTraitDefsTyped().ContainsKey("human_versatility"),
            "official progression registry should expose generic trait_defs."
        );

        TraitDef customTrait = new()
        {
            trait_id = "custom_identity_trait",
            display_name = "Custom Identity Trait",
            description = "Custom test trait.",
            effect_type = "brave",
            trigger_type = "passive",
            stack_policy = "unique_by_trait",
            charge_scope = "none",
            charge_reset_timing = "none",
        };
        customTrait.allowed_source_kinds.Add("identity");

        RaceDef race = new()
        {
            race_id = "custom_race",
            display_name = "Custom Race",
            description = "Custom test race.",
        };
        race.trait_ids.Add("custom_identity_trait");

        registry.ReplaceDefinitionBuckets(
            new GDictionary
            {
                ["skill_defs"] = new GDictionary(),
                ["profession_defs"] = new GDictionary(),
                ["achievement_defs"] = new GDictionary(),
                ["quest_defs"] = new GDictionary(),
                ["race_defs"] = new GDictionary { [new StringName("custom_race")] = race },
                ["subrace_defs"] = new GDictionary(),
                ["trait_defs"] = new GDictionary
                {
                    [new StringName("custom_identity_trait")] = customTrait,
                },
                ["age_profile_defs"] = new GDictionary(),
                ["bloodline_defs"] = new GDictionary(),
                ["bloodline_stage_defs"] = new GDictionary(),
                ["ascension_defs"] = new GDictionary(),
                ["ascension_stage_defs"] = new GDictionary(),
                ["stage_advancement_defs"] = new GDictionary(),
            }
        );

        _test.True(
            registry.GetTraitDefsTyped().ContainsKey("custom_identity_trait"),
            "typed trait getter should see replacement bucket content."
        );
        _test.Eq(
            CountErrorsContaining(registry.ValidateTyped(), "custom_identity_trait"),
            0,
            "identity trait_ids should validate against generic trait_defs."
        );
    }

    private void TestIdentityCatalogTypedBoundaryMatchesPublicBuckets()
    {
        using ProgressionContentRegistry registry = new();
        registry.ReplaceValidationSources(BuildCustomValidationSources());

        ProgressionIdentityCatalogData catalog = registry.GetIdentityCatalogTyped();

        _test.True(
            catalog.RaceDefs.ContainsKey("human"),
            "identity catalog typed getter 应暴露 race defs。"
        );
        _test.True(
            catalog.AgeProfileDefs.ContainsKey("human_profile"),
            "identity catalog typed getter 应暴露 age profile defs。"
        );
        _test.True(
            catalog.StageAdvancementDefs.ContainsKey("broken_stage_cap"),
            "identity catalog typed getter 应暴露 stage advancement defs。"
        );
    }

    private void TestEquipmentAbilityRegistryStartsEmptyAndValid()
    {
        using ProgressionContentRegistry registry = new();

        EquipmentAbilityRegistryBuildResult result =
            registry.GetEquipmentAbilityLastBuildResultTyped();

        _test.True(
            result.Success,
            $"official progression registry should accept an empty equipment ability pack list: {FormatErrors(result.Errors)}"
        );
        _test.Eq(
            registry.GetEquipmentAbilityContentRevision(),
            result.Revision,
            "progression registry should expose the equipment ability registry revision."
        );
        _test.Eq(
            registry.GetEquipmentAbilityPackDefinitionsTyped().Count,
            0,
            "official progression registry should not require placeholder equipment ability packs."
        );
        _test.Eq(
            registry.GetEquipmentAbilityBindingDefinitionsTyped().Count,
            0,
            "official progression registry should expose an empty equipment ability binding snapshot."
        );
    }

    private static GDictionary BuildCustomValidationSources()
    {
        SkillDef knownSkill = new() { skill_type = "active", learn_source = "book" };
        AchievementDef brokenAchievement = new()
        {
            achievement_id = "broken_achievement",
            display_name = "Broken Achievement",
            description = "",
            event_type = "battle_won",
            threshold = 1,
        };
        brokenAchievement.rewards.Add(
            new AchievementRewardDef
            {
                RewardKind = PendingCharacterRewardEntryKind.SkillUnlock,
                target_id = "missing_skill",
                target_label = "Missing Skill",
                amount = 1,
                reason_text = "Validation probe.",
            }
        );

        RaceDef race = new()
        {
            race_id = "human",
            display_name = "Human",
            description = "",
            age_profile_id = "human_profile",
        };
        AgeProfileDef ageProfile = new() { profile_id = "human_profile", race_id = "human" };
        ageProfile.stage_rules.Add(new AgeStageRule { stage_id = "adult" });
        ageProfile.creation_stage_ids.Add("elder_missing");
        ageProfile.default_age_by_stage["elder_missing"] = 80;

        StageAdvancementModifier brokenStageCap = new()
        {
            modifier_id = "broken_stage_cap",
            display_name = "Broken Stage Cap",
            target_axis = StageAdvancementModifier.ToStringName(StageAdvancementTargetAxis.Full),
            max_stage_id = "transcendence_missing",
        };

        return new GDictionary
        {
            ["skill_defs"] = new GDictionary { [new StringName("known_skill")] = knownSkill },
            ["profession_defs"] = new GDictionary(),
            ["achievement_defs"] = new GDictionary
            {
                [new StringName("broken_achievement")] = brokenAchievement.ToDictionary(),
            },
            ["quest_defs"] = new GDictionary(),
            ["race_defs"] = new GDictionary { [new StringName("human")] = race },
            ["subrace_defs"] = new GDictionary(),
            ["trait_defs"] = new GDictionary(),
            ["age_profile_defs"] = new GDictionary { [new StringName("human_profile")] = ageProfile },
            ["bloodline_defs"] = new GDictionary(),
            ["bloodline_stage_defs"] = new GDictionary(),
            ["ascension_defs"] = new GDictionary(),
            ["ascension_stage_defs"] = new GDictionary(),
            ["stage_advancement_defs"] = new GDictionary
            {
                [new StringName("broken_stage_cap")] = brokenStageCap,
            },
        };
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static int CountErrorsContaining(IEnumerable<string> errors, string needle)
    {
        int count = 0;
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(needle))
            {
                count++;
            }
        }
        return count;
    }
}
