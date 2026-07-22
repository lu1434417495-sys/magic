using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_progression_content_registry_typed_regression : LifecycleTestSceneTree
{
    private static readonly string[] AggregatedRegistryContentPrefixes =
    {
        "res://data/configs/skills/",
        "res://data/configs/professions/",
        "res://data/configs/races/",
        "res://data/configs/subraces/",
        "res://data/configs/traits/",
        "res://data/configs/age_profiles/",
        "res://data/configs/bloodlines/",
        "res://data/configs/ascensions/",
        "res://data/configs/stage_advancements/",
    };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        Run();
    }

    private void Run()
    {
        TestOfficialProgressionRegistryTypedBoundaryMatchesPublicBoundary();
        TestPureDefinitionReplacementFeedsTypedValidation();
        TestDefinitionReplacementProducesDefensiveSnapshots();
        TestTraitDefinitionReplacementFeedsIdentityValidation();
        TestIdentityCatalogUsesDefinitionIndexes();

        RequestTestExit(_test.Finish("Progression content registry typed regression"));
    }

    private void TestOfficialProgressionRegistryTypedBoundaryMatchesPublicBoundary()
    {
        using TestContentResourceLoader loader = new();
        using ProgressionContentRegistry registry = new(loader);

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
        _test.True(
            registry.GetAchievementDefsTyped().ContainsKey("battle_won_first"),
            "代码种子成就应直接作为 AchievementDefinition 暴露。"
        );
        _test.True(
            registry.GetContingencySetupTemplatesTyped().Count > 0,
            "contingency getter 应暴露 Definition snapshot。"
        );

        foreach (string contentPrefix in AggregatedRegistryContentPrefixes)
        {
            _test.True(
                loader.CountLoadedPathsUnder(contentPrefix) > 0,
                $"聚合 registry 应加载 {contentPrefix} 下的正式内容。"
            );
            IReadOnlyList<string> duplicateLoads = loader.GetDuplicateLoadsUnder(contentPrefix);
            _test.Eq(
                duplicateLoads.Count,
                0,
                $"聚合 registry 构造期间每个内容路径只能加载一次: {FormatErrors(duplicateLoads)}"
            );
        }
    }

    private void TestPureDefinitionReplacementFeedsTypedValidation()
    {
        using TestContentResourceLoader loader = new();
        using ProgressionContentRegistry registry = new(
            loader,
            loadDefaultContent: false
        );
        registry.ReplaceDefinitionsForValidation(BuildCustomDefinitionSources());

        foreach (string contentPrefix in AggregatedRegistryContentPrefixes)
        {
            _test.Eq(
                loader.CountLoadedPathsUnder(contentPrefix),
                0,
                $"pure definition validation 不应加载正式内容目录 {contentPrefix}。"
            );
        }

        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GStringArray projectedErrors = registry.Validate();

        _test.Eq(
            typedErrors.Count,
            projectedErrors.Count,
            "pure definition sources 的 typed/public validation error 数量应保持一致。"
        );
        _test.True(
            typedErrors.Count >= 4,
            $"pure definition sources 应保持非法。 errors={FormatErrors(typedErrors)}"
        );
        _test.True(
            CountErrorsContaining(typedErrors, "missing_skill") > 0,
            "achievement definition 的缺失 skill 引用应保留正式诊断。"
        );
        _test.True(
            CountErrorsContaining(typedErrors, "elder_missing") >= 2,
            "age definition 的 creation/default stage 缺失诊断应保留。"
        );
        _test.True(
            CountErrorsContaining(typedErrors, "transcendence_missing") > 0,
            "stage advancement definition 的 max_stage_id 诊断应保留。"
        );
        _test.Eq(
            CountErrorsContaining(
                typedErrors,
                "Profession injected_invalid_profession must have max_rank >= 1"
            ),
            1,
            "replacement profession index 必须由纯 Definition validator 校验。"
        );
        _test.True(
            CountErrorsContaining(
                typedErrors,
                "Trait injected_invalid_trait.effect_type uses unsupported value"
            ) > 0,
            "replacement trait index 必须校验 effect_type 语义。"
        );
        _test.True(
            CountErrorsContaining(
                typedErrors,
                "Trait injected_invalid_trait.allowed_source_kinds[0] uses unsupported"
            ) > 0,
            "replacement trait index 必须校验 allowed_source_kinds 语义。"
        );
        _test.True(
            CountErrorsContaining(
                typedErrors,
                "Trait injected_invalid_trait.roll_value_schema[0]"
            ) > 0,
            "replacement trait index 必须校验 roll_value_schema 语义。"
        );
    }

    private void TestDefinitionReplacementProducesDefensiveSnapshots()
    {
        using TestContentResourceLoader loader = new();
        using ProgressionContentRegistry registry = new(
            loader,
            loadDefaultContent: false
        );
        ProgressionDefinitionSources sources = BuildCustomDefinitionSources();
        registry.ReplaceDefinitionsForValidation(sources);

        IReadOnlyDictionary<StringName, RaceDefinition> firstSnapshot =
            registry.GetRaceDefsTyped();
        var mutableSource = (Dictionary<StringName, RaceDefinition>)sources.RaceDefinitions;
        mutableSource.Clear();

        _test.True(
            registry.GetRaceDefsTyped().ContainsKey("human"),
            "替换入口必须复制 definition index，不能保留调用方字典所有权。"
        );
        _test.True(
            RejectsMutation(firstSnapshot),
            "typed getter 应返回拒绝写入的 defensive snapshot。"
        );
        _test.True(
            registry.GetAchievementDefsTyped()["broken_achievement"]
                is AchievementDefinition,
            "achievement typed getter 只能返回 AchievementDefinition。"
        );
        _test.True(
            registry.GetStageAdvancementDefsTyped()["broken_stage_cap"]
                is StageAdvancementDefinition,
            "stage advancement typed getter 只能返回 StageAdvancementDefinition。"
        );
    }

    private void TestTraitDefinitionReplacementFeedsIdentityValidation()
    {
        using TestContentResourceLoader loader = new();
        using ProgressionContentRegistry registry = new(
            loader,
            loadDefaultContent: false
        );
        TraitDefinition customTrait = BuildIdentityTrait("custom_identity_trait");
        RaceDefinition customRace = BuildRace(
            "custom_race",
            "",
            [new StringName("custom_identity_trait")]
        );
        registry.ReplaceDefinitionsForValidation(
            new ProgressionDefinitionSources
            {
                RaceDefinitions = new Dictionary<StringName, RaceDefinition>
                {
                    [customRace.RaceId] = customRace,
                },
                TraitDefinitions = new Dictionary<StringName, TraitDefinition>
                {
                    [customTrait.TraitId] = customTrait,
                },
            }
        );

        _test.True(
            registry.GetTraitDefsTyped().ContainsKey("custom_identity_trait"),
            "typed trait getter should see pure definition replacement content."
        );
        _test.Eq(
            CountErrorsContaining(registry.ValidateTyped(), "custom_identity_trait"),
            0,
            "identity trait_ids should validate against TraitDefinition indexes."
        );
    }

    private void TestIdentityCatalogUsesDefinitionIndexes()
    {
        using TestContentResourceLoader loader = new();
        using ProgressionContentRegistry registry = new(
            loader,
            loadDefaultContent: false
        );
        registry.ReplaceDefinitionsForValidation(BuildCustomDefinitionSources());

        ProgressionIdentityCatalogData catalog = registry.GetIdentityCatalogTyped();

        _test.True(catalog.RaceDefs.ContainsKey("human"), "identity catalog 应暴露 race definitions。");
        _test.True(
            catalog.AgeProfileDefs.ContainsKey("human_profile"),
            "identity catalog 应暴露 age profile definitions。"
        );
        _test.True(
            catalog.StageAdvancementDefs.ContainsKey("broken_stage_cap"),
            "identity catalog 应暴露 stage advancement definitions。"
        );
    }

    private static ProgressionDefinitionSources BuildCustomDefinitionSources()
    {
        AchievementDefinition achievement = new(
            "broken_achievement",
            "Broken Achievement",
            "",
            "battle_won",
            "",
            1,
            [
                new AchievementRewardDefinition(
                    PendingCharacterRewardContentRules.ToStringName(
                        PendingCharacterRewardEntryKind.SkillUnlock
                    ),
                    "missing_skill",
                    "Missing Skill",
                    1,
                    "Validation probe."
                ),
            ]
        );
        RaceDefinition race = BuildRace("human", "human_profile", Array.Empty<StringName>());
        AgeProfileDefinition ageProfile = new(
            "human_profile",
            "human",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            [
                new AgeStageRuleDefinition(
                    "adult",
                    "Adult",
                    "",
                    Array.Empty<AttributeModifierDefinition>(),
                    Array.Empty<StringName>(),
                    Array.Empty<string>(),
                    true,
                    true
                ),
            ],
            [new StringName("elder_missing")],
            new Dictionary<StringName, int> { ["elder_missing"] = 80 }
        );
        StageAdvancementDefinition stageAdvancement = new(
            "broken_stage_cap",
            "Broken Stage Cap",
            "full",
            1,
            "transcendence_missing",
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            false,
            false,
            false
        );
        ProfessionDefinition profession = new(
            "injected_invalid_profession",
            "Injected Invalid Profession",
            "Validation probe.",
            0,
            8,
            "half",
            true,
            "",
            null,
            Array.Empty<ProfessionRankRequirementDefinition>(),
            Array.Empty<ProfessionGrantedSkillDefinition>(),
            Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<ProfessionActiveConditionDefinition>(),
            "auto",
            "count_when_hidden"
        );
        TraitDefinition trait = new(
            "injected_invalid_trait",
            "Injected Invalid Trait",
            "Validation probe.",
            Array.Empty<StringName>(),
            [new StringName("unsupported_source")],
            "unsupported_effect",
            "unsupported_trigger",
            "unsupported_stack",
            "unsupported_charge_scope",
            "unsupported_reset_timing",
            "",
            0,
            0,
            Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitDamageResistanceEntryDefinition>(),
            Array.Empty<TraitSaveBonusEntryDefinition>(),
            Array.Empty<TraitPassiveStatusEffectDefinition>(),
            [
                new TraitRollValueSchemaEntryDefinition(
                    "",
                    "unsupported_value_type",
                    0,
                    0,
                    Array.Empty<StringName>()
                ),
            ]
        );

        return new ProgressionDefinitionSources
        {
            ProfessionDefinitions = new Dictionary<StringName, ProfessionDefinition>
            {
                [profession.ProfessionId] = profession,
            },
            AchievementDefinitions = new Dictionary<StringName, AchievementDefinition>
            {
                [achievement.AchievementId] = achievement,
            },
            RaceDefinitions = new Dictionary<StringName, RaceDefinition>
            {
                [race.RaceId] = race,
            },
            AgeProfileDefinitions = new Dictionary<StringName, AgeProfileDefinition>
            {
                [ageProfile.ProfileId] = ageProfile,
            },
            StageAdvancementDefinitions = new Dictionary<StringName, StageAdvancementDefinition>
            {
                [stageAdvancement.ModifierId] = stageAdvancement,
            },
            TraitDefinitions = new Dictionary<StringName, TraitDefinition>
            {
                [trait.TraitId] = trait,
            },
        };
    }

    private static RaceDefinition BuildRace(
        StringName raceId,
        StringName ageProfileId,
        IReadOnlyList<StringName> traitIds
    ) =>
        new(
            raceId,
            raceId.ToString(),
            "",
            ageProfileId,
            "",
            Array.Empty<StringName>(),
            "medium",
            6,
            Array.Empty<AttributeModifierDefinition>(),
            traitIds,
            Array.Empty<RacialGrantedSkillDefinition>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<string>()
        );

    private static TraitDefinition BuildIdentityTrait(StringName traitId) =>
        new(
            traitId,
            traitId.ToString(),
            "Custom test trait.",
            Array.Empty<StringName>(),
            [new StringName("identity")],
            "brave",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitDamageResistanceEntryDefinition>(),
            Array.Empty<TraitSaveBonusEntryDefinition>(),
            Array.Empty<TraitPassiveStatusEffectDefinition>(),
            Array.Empty<TraitRollValueSchemaEntryDefinition>()
        );

    private static bool RejectsMutation(IReadOnlyDictionary<StringName, RaceDefinition> snapshot)
    {
        try
        {
            ((IDictionary<StringName, RaceDefinition>)snapshot).Clear();
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
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
                count++;
        }
        return count;
    }

}
