using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_resource_validation_regression : LifecycleTestSceneTree
{
    private const string OFFICIAL_SKILL_DIRECTORY = "res://data/configs/skills";
    private const string OFFICIAL_PROFESSION_DIRECTORY = "res://data/configs/professions";
    private const string OFFICIAL_RECIPE_DIRECTORY = "res://data/configs/recipes";
    private const string OFFICIAL_ENEMY_SEED_PATH = "res://data/configs/enemies/enemy_content_seed.tres";
    private const string SKILL_INVALID_DIRECTORY = "res://tests/progression/fixtures/skill_registry_invalid";
    private const string SKILL_VALID_DIRECTORY = "res://tests/progression/fixtures/skill_registry_valid";
    private const string PROFESSION_INVALID_DIRECTORY =
        "res://tests/progression/fixtures/profession_registry_invalid";
    private const string ITEM_INVALID_DIRECTORY =
        "res://tests/fixtures/resource_validation/item_registry_invalid";
    private const string ITEM_TEMPLATE_INVALID_ITEM_DIRECTORY =
        "res://tests/fixtures/resource_validation/item_registry_template_invalid/items";
    private const string ITEM_TEMPLATE_INVALID_TEMPLATE_DIRECTORY =
        "res://tests/fixtures/resource_validation/item_registry_template_invalid/templates";
    private const string ITEM_TEMPLATE_ISOLATED_ITEM_DIRECTORY =
        "res://tests/fixtures/resource_validation/item_registry_template_isolated/items";
    private const string ITEM_TEMPLATE_ISOLATED_TEMPLATE_DIRECTORY =
        "res://tests/fixtures/resource_validation/item_registry_template_isolated/templates";
    private const string RECIPE_INVALID_DIRECTORY =
        "res://tests/fixtures/resource_validation/recipe_registry_invalid";
    private const string IDENTITY_INVALID_RACE_DIRECTORY =
        "res://tests/progression/fixtures/identity_registry_invalid/races";
    private const string IDENTITY_INVALID_SUBRACE_DIRECTORY =
        "res://tests/progression/fixtures/identity_registry_invalid/subraces";
    private const string TRAIT_INVALID_DIRECTORY =
        "res://tests/progression/fixtures/trait_registry_invalid";
    private const string IDENTITY_INVALID_STAGE_ADVANCEMENT_DIRECTORY =
        "res://tests/progression/fixtures/identity_registry_invalid/stage_advancements";
    private const string ENEMY_MISSING_ID_SEED_PATH =
        "res://tests/fixtures/enemy_content/missing_template_id/enemy_content_seed.tres";
    private const string ENEMY_DUPLICATE_ID_SEED_PATH =
        "res://tests/fixtures/enemy_content/duplicate_template_id/enemy_content_seed.tres";
    private const string ENEMY_INVALID_REFERENCE_SEED_PATH =
        "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres";
    private const string ENEMY_INCOMPLETE_SEED_PATH =
        "res://tests/fixtures/enemy_content/incomplete_seed/enemy_content_seed.tres";
    private const string ENEMY_INCOMPLETE_BRAIN_DIRECTORY =
        "res://tests/fixtures/enemy_content/incomplete_seed/brains";
    private const string ENEMY_INCOMPLETE_TEMPLATE_DIRECTORY =
        "res://tests/fixtures/enemy_content/incomplete_seed/templates";
    private const string ENEMY_INCOMPLETE_ROSTER_DIRECTORY =
        "res://tests/fixtures/enemy_content/incomplete_seed/rosters";
    private const string ENEMY_INVALID_INITIAL_STAGE_SEED_PATH =
        "res://tests/fixtures/enemy_content/invalid_roster_initial_stage/enemy_content_seed.tres";
    private const string ENEMY_INVALID_SKILL_LEVEL_MAP_SEED_PATH =
        "res://tests/fixtures/enemy_content/invalid_skill_level_map/enemy_content_seed.tres";
    private const string BATTLE_SPECIAL_PROFILE_FIXTURE_ROOT =
        "user://resource_validation/battle_special_profiles";

    private readonly TestHarness _test = new();
    private readonly List<string> _reports = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        using TestContentResourceLoader contentLoader = new();
        using ProgressionContentRegistry progressionRegistry = new(contentLoader);
        using ItemContentRegistry itemRegistry = new(contentLoader);

        GDictionary skillDefs = progressionRegistry.DuplicateSkillResourceBucketForValidation();
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs =
            itemRegistry.GetItemDefsTyped();
        EnemyContentDefinitionGraph enemyDefinitions = new(
            snapshot.EnemyTemplates,
            snapshot.EnemyBrains,
            snapshot.EncounterRosters
        );
        HashSet<StringName> battleEncounterIds = new(snapshot.BattleEncounters.Keys);
        IReadOnlyDictionary<StringName, ItemDefinition> typedItemDefs = itemDefs;
        IReadOnlyDictionary<StringName, SkillDefinition> typedSkillDefinitions =
            progressionRegistry.GetSkillDefinitionsTyped();

        TestFormalPhantasmalKillResource(typedSkillDefinitions);

        ValidationDomainResult officialItemResult = ContentValidationRunner.ValidateOfficialItemContent();
        ValidationDomainResult officialEnemyResult = ContentValidationRunner.ValidateEnemySeed(
            OFFICIAL_ENEMY_SEED_PATH,
            typedItemDefs,
            typedSkillDefinitions
        );

        TestItemRegistryDirectoryRebuildClearsTemplateCache();

        ValidationRunReport officialReport = ContentValidationRunner.BuildRunReport(
            "official_content",
            new[]
            {
                ContentValidationRunner.ValidateSkillDirectory(OFFICIAL_SKILL_DIRECTORY),
                ContentValidationRunner.ValidateProfessionDirectory(
                    OFFICIAL_PROFESSION_DIRECTORY,
                    skillDefs
                ),
                ContentValidationRunner.ValidateIdentityContent("official_identity", skillDefs),
                ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                    "official_battle_special_profiles",
                    typedSkillDefinitions
                ),
                officialItemResult,
                ContentValidationRunner.ValidateRecipeDirectory(OFFICIAL_RECIPE_DIRECTORY, itemDefs),
                officialEnemyResult,
                ContentValidationRunner.ValidateWorldPresets(
                    battleEncounterIds
                ),
                ContentValidationRunner.ValidateQuestEntries(
                    "official_quests",
                    BuildQuestEntriesFromTyped(
                        progressionRegistry.GetQuestDefsTyped(),
                        "progression_seed"
                    ),
                    typedItemDefs,
                    typedSkillDefinitions,
                    enemyDefinitions.EnemyTemplates
                ),
            }
        );
        _reports.Add(ContentValidationRunner.FormatReport(officialReport));
        _test.True(officialReport.Ok, "正式内容 validation runner 应通过。");
        _test.True(officialReport.ErrorCount == 0, "正式内容 validation runner 不应报告错误。");
        _test.Eq(
            FormatErrors(officialItemResult.Errors),
            FormatErrors(ToStringList(itemRegistry.Validate())),
            "正式 item validation runner 应与 ItemContentRegistry 默认 runtime 构建路径等价。"
        );
        AssertDomainIs(
            officialEnemyResult,
            "enemy",
            "正式 enemy validation runner 应稳定归入 enemy domain。"
        );

        ValidationDomainResult skillResult = ContentValidationRunner.ValidateSkillDirectory(
            SKILL_INVALID_DIRECTORY,
            true
        );
        ValidationDomainResult validSkillResult = ContentValidationRunner.ValidateSkillDirectory(
            SKILL_VALID_DIRECTORY
        );
        ValidationDomainResult professionResult = ContentValidationRunner.ValidateProfessionDirectory(
            PROFESSION_INVALID_DIRECTORY,
            skillDefs
        );
        ValidationDomainResult identityResult = ContentValidationRunner.ValidateIdentityDirectories(
            "invalid_identity_directories",
            ["res://data/configs/races", IDENTITY_INVALID_RACE_DIRECTORY],
            ["res://data/configs/subraces", IDENTITY_INVALID_SUBRACE_DIRECTORY],
            ["res://data/configs/traits", TRAIT_INVALID_DIRECTORY],
            ["res://data/configs/age_profiles"],
            ["res://data/configs/bloodlines"],
            ["res://data/configs/ascensions"],
            [
                "res://data/configs/stage_advancements",
                IDENTITY_INVALID_STAGE_ADVANCEMENT_DIRECTORY,
            ],
            skillDefs
        );
        ValidationDomainResult itemResult = ContentValidationRunner.ValidateItemDirectories(
            "isolated_invalid_items",
            [ITEM_INVALID_DIRECTORY]
        );
        ValidationDomainResult itemTemplateResult = ContentValidationRunner.ValidateItemDirectories(
            "invalid_item_templates",
            [ITEM_TEMPLATE_INVALID_ITEM_DIRECTORY],
            [ITEM_TEMPLATE_INVALID_TEMPLATE_DIRECTORY]
        );
        ValidationDomainResult recipeResult = ContentValidationRunner.ValidateRecipeDirectory(
            RECIPE_INVALID_DIRECTORY,
            itemDefs
        );
        ValidationDomainResult enemyMissingResult = ContentValidationRunner.ValidateEnemySeed(
            ENEMY_MISSING_ID_SEED_PATH,
            typedItemDefs,
            typedSkillDefinitions
        );
        ValidationDomainResult enemyDuplicateResult = ContentValidationRunner.ValidateEnemySeed(
            ENEMY_DUPLICATE_ID_SEED_PATH,
            typedItemDefs,
            typedSkillDefinitions
        );
        ValidationDomainResult enemyInvalidReferenceResult =
            ContentValidationRunner.ValidateEnemySeed(
                ENEMY_INVALID_REFERENCE_SEED_PATH,
                typedItemDefs,
                typedSkillDefinitions
            );
        ValidationDomainResult enemyIncompleteSeedResult =
            ContentValidationRunner.ValidateEnemySeedWithDirectoryCompleteness(
                ENEMY_INCOMPLETE_SEED_PATH,
                ENEMY_INCOMPLETE_TEMPLATE_DIRECTORY,
                ENEMY_INCOMPLETE_BRAIN_DIRECTORY,
                ENEMY_INCOMPLETE_ROSTER_DIRECTORY,
                typedItemDefs,
                typedSkillDefinitions
            );
        ValidationDomainResult enemyInvalidInitialStageResult =
            ContentValidationRunner.ValidateEnemySeed(
                ENEMY_INVALID_INITIAL_STAGE_SEED_PATH,
                typedItemDefs,
                typedSkillDefinitions
            );
        ValidationDomainResult enemyInvalidSkillLevelMapResult =
            ContentValidationRunner.ValidateEnemySeed(
                ENEMY_INVALID_SKILL_LEVEL_MAP_SEED_PATH,
                typedItemDefs,
                typedSkillDefinitions
            );
        ValidationDomainResult battleSpecialMissingManifestResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_missing_manifest",
                typedSkillDefinitions,
                PrepareEmptyBattleSpecialProfileManifestDir("missing_manifest")
            );
        ValidationDomainResult battleSpecialUnknownProfileResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_unknown_profile_missing_manifest",
                BuildSingleSpecialProfileSkillDefinitions(
                    "phantom_special_skill",
                    "phantom_profile"
                ),
                PrepareEmptyBattleSpecialProfileManifestDir("unknown_profile_missing_manifest")
            );
        ValidationDomainResult battleSpecialDuplicateProfileResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_duplicate_profile",
                typedSkillDefinitions,
                PrepareBattleSpecialProfileManifestDir(
                    "duplicate_profile",
                    new List<GDictionary>
                    {
                        new()
                        {
                            ["file_name"] = "a",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = BuildValidMeteorSwarmProfile(),
                        },
                        new()
                        {
                            ["file_name"] = "b",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = BuildValidMeteorSwarmProfile(),
                        },
                    }
                )
            );
        ValidationDomainResult battleSpecialDuplicateOwnerResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_duplicate_owner",
                typedSkillDefinitions,
                PrepareBattleSpecialProfileManifestDir(
                    "duplicate_owner",
                    new List<GDictionary>
                    {
                        new()
                        {
                            ["file_name"] = "a",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = BuildValidMeteorSwarmProfile(),
                        },
                        new()
                        {
                            ["file_name"] = "b",
                            ["profile_id"] = "other_profile",
                            ["runtime_resolver_id"] = "other_profile",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = new Resource(),
                        },
                    }
                )
            );
        ValidationDomainResult battleSpecialWrongResourceResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_wrong_resource_type",
                typedSkillDefinitions,
                PrepareBattleSpecialProfileManifestDir(
                    "wrong_resource_type",
                    new List<GDictionary>
                    {
                        new()
                        {
                            ["file_name"] = "wrong_resource",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = new Resource(),
                        },
                    }
                )
            );
        ValidationDomainResult battleSpecialMissingOwnerResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_missing_owner",
                typedSkillDefinitions,
                PrepareBattleSpecialProfileManifestDir(
                    "missing_owner",
                    new List<GDictionary>
                    {
                        new()
                        {
                            ["file_name"] = "missing_owner",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "missing_skill" },
                            ["profile_resource"] = BuildValidMeteorSwarmProfile(),
                        },
                    }
                )
            );
        ValidationDomainResult battleSpecialMissingRequiredTestResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_missing_required_test",
                typedSkillDefinitions,
                PrepareBattleSpecialProfileManifestDir(
                    "missing_required_test",
                    new List<GDictionary>
                    {
                        new()
                        {
                            ["file_name"] = "missing_required_test",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = BuildValidMeteorSwarmProfile(),
                            ["required_regression_tests"] = new GArray
                            {
                                "tests/missing/missing_profile_regression.cs",
                            },
                        },
                    }
                )
            );
        ValidationDomainResult battleSpecialBadSchemaResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_bad_schema",
                typedSkillDefinitions,
                PrepareBattleSpecialProfileManifestDir(
                    "bad_schema",
                    new List<GDictionary>
                    {
                        new()
                        {
                            ["file_name"] = "bad_schema",
                            ["profile_id"] = "meteor_swarm",
                            ["owning_skill_ids"] = new GArray { "mage_meteor_swarm" },
                            ["profile_resource"] = BuildBadSchemaMeteorSwarmProfile(),
                        },
                    }
                )
            );
        ValidationDomainResult worldResult = ContentValidationRunner.ValidateWorldGenerationConfig(
            "invalid_world_generation_config",
            BuildInvalidWorldGenerationDefinition(),
            battleEncounterIds
        );
        ValidationDomainResult questResult = ContentValidationRunner.ValidateQuestEntries(
            "invalid_quest_entries",
            BuildInvalidQuestEntries(),
            typedItemDefs,
            typedSkillDefinitions,
            enemyDefinitions.EnemyTemplates
        );
        ValidationRunReport invalidFixtureReport = ContentValidationRunner.BuildRunReport(
            "invalid_fixture_coverage",
            new[]
            {
                skillResult,
                professionResult,
                identityResult,
                itemResult,
                itemTemplateResult,
                recipeResult,
                enemyMissingResult,
                enemyDuplicateResult,
                enemyInvalidReferenceResult,
                enemyIncompleteSeedResult,
                enemyInvalidInitialStageResult,
                enemyInvalidSkillLevelMapResult,
                battleSpecialMissingManifestResult,
                battleSpecialUnknownProfileResult,
                battleSpecialDuplicateProfileResult,
                battleSpecialDuplicateOwnerResult,
                battleSpecialWrongResourceResult,
                battleSpecialMissingOwnerResult,
                battleSpecialMissingRequiredTestResult,
                battleSpecialBadSchemaResult,
                worldResult,
                questResult,
            }
        );
        _reports.Add(ContentValidationRunner.FormatReport(invalidFixtureReport));

        AssertInvalid(skillResult, "非法技能 fixture 应保持非法。");
        AssertContainsError(
            skillResult,
            "skill.invalid_level_description_malformed_skill.level_description_configs",
            "非法技能 fixture 应保留 strict projection 的精确路径错误。"
        );
        _test.True(validSkillResult.ErrorCount == 0, "合法技能 targeting fixture 不应产生 validation 错误。");

        AssertInvalid(professionResult, "非法职业 fixture 应保持非法。");

        AssertInvalid(identityResult, "非法身份 fixture 应保持非法。");
        AssertContainsError(
            identityResult,
            "Trait missing_text_trait.display_name",
            "非法身份 fixture 应包含 generic trait 内容 validation 错误。"
        );

        AssertInvalid(itemResult, "非法物品 fixture 应保持非法。");

        AssertInvalid(itemTemplateResult, "非法物品 template fixture 应保持非法。");

        AssertInvalid(recipeResult, "非法配方 fixture 应保持非法。");

        AssertDomainIs(enemyMissingResult, "enemy", "缺失 template_id 的 enemy fixture 应稳定归入 enemy domain。");
        AssertDomainIs(enemyDuplicateResult, "enemy", "重复 template_id 的 enemy fixture 应稳定归入 enemy domain。");
        AssertDomainIs(enemyInvalidReferenceResult, "enemy", "非法 roster 引用的 enemy fixture 应稳定归入 enemy domain。");
        AssertDomainIs(enemyIncompleteSeedResult, "enemy", "遗漏 seed entry 的 enemy fixture 应稳定归入 enemy domain。");
        AssertDomainIs(enemyInvalidInitialStageResult, "enemy", "initial_stage 不匹配的 roster fixture 应稳定归入 enemy domain。");
        AssertDomainIs(enemyInvalidSkillLevelMapResult, "enemy", "skill_level_map 非法的 template fixture 应稳定归入 enemy domain。");
        AssertInvalid(enemyMissingResult, "缺失 template_id 的 enemy fixture 应保持非法。");
        AssertInvalid(enemyDuplicateResult, "重复 template_id 的 enemy fixture 应保持非法。");
        AssertInvalid(enemyInvalidReferenceResult, "非法 roster 引用的 enemy fixture 应保持非法。");
        AssertInvalid(enemyIncompleteSeedResult, "遗漏 seed entry 的 enemy fixture 应保持非法。");
        AssertInvalid(enemyInvalidInitialStageResult, "initial_stage 不匹配的 roster fixture 应保持非法。");
        AssertInvalid(enemyInvalidSkillLevelMapResult, "skill_level_map 非法的 template fixture 应保持非法。");

        AssertInvalid(battleSpecialMissingManifestResult, "特殊技能 profile 缺失 manifest fixture 应保持非法。");
        AssertInvalid(battleSpecialUnknownProfileResult, "特殊技能 profile 未知 profile fixture 应保持非法。");
        AssertInvalid(battleSpecialDuplicateProfileResult, "特殊技能 profile 重复 profile_id fixture 应保持非法。");
        AssertInvalid(battleSpecialDuplicateOwnerResult, "特殊技能 profile duplicate-owner fixture 应保持非法。");
        AssertInvalid(battleSpecialWrongResourceResult, "特殊技能 profile 错误 resource fixture 应保持非法。");
        AssertInvalid(battleSpecialMissingOwnerResult, "特殊技能 profile 缺失 owning skill fixture 应保持非法。");
        AssertInvalid(battleSpecialMissingRequiredTestResult, "特殊技能 profile 缺失 required test fixture 应保持非法。");
        AssertInvalid(battleSpecialBadSchemaResult, "特殊技能 profile schema typo fixture 应保持非法。");

        AssertInvalid(worldResult, "非法世界配置 fixture 应保持非法。");

        _test.True(questResult.Domain == "quest", "任务 validation runner 应稳定归入 quest domain。");
        AssertInvalid(questResult, "非法任务 fixture 应保持非法。");

        foreach (string reportText in _reports)
            ConsoleProcessOutput.WriteStandard(reportText);

        RequestTestExit(_test.Finish("Resource validation regression"));
    }

    private static List<QuestValidationEntry> BuildQuestEntriesFromTyped(
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs,
        string sourcePrefix
    )
    {
        List<QuestValidationEntry> entries = new();
        foreach (StringName questId in SortedStringNameKeys(questDefs))
        {
            entries.Add(
                new QuestValidationEntry(
                    $"{sourcePrefix}::{questId}",
                    questDefs.TryGetValue(questId, out QuestDefinition questDefinition)
                        ? questDefinition
                        : null
                )
            );
        }
        return entries;
    }

    private static List<StringName> SortedStringNameKeys(
        IReadOnlyDictionary<StringName, QuestDefinition> source
    )
    {
        List<StringName> keys = new();
        if (source == null)
            return keys;
        foreach (StringName key in source.Keys)
            keys.Add(key);
        keys.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return keys;
    }

    private void TestItemRegistryDirectoryRebuildClearsTemplateCache()
    {
        using TestContentResourceLoader loader = new();
        using ItemContentRegistry registry = new(loader);
        registry.RebuildFromDirectories(
            new GArray { ITEM_TEMPLATE_ISOLATED_ITEM_DIRECTORY },
            new GArray { ITEM_TEMPLATE_ISOLATED_TEMPLATE_DIRECTORY }
        );
        _test.True(registry.Validate().Count == 0, "显式传入 fixture template 时 isolated item registry 应可通过。");
        _test.True(
            registry.GetItemDefsTyped().ContainsKey("fixture_inherited_item"),
            "显式传入 fixture template 时应注册继承后的 fixture item。"
        );

        registry.RebuildFromDirectories(
            new GArray { ITEM_TEMPLATE_ISOLATED_ITEM_DIRECTORY },
            new GArray()
        );
        _test.True(
            registry.Validate().Count > 0,
            "同一个 registry 重新构建时不得残留上一次的 fixture template cache。"
        );
    }

    private void TestFormalPhantasmalKillResource(
        IReadOnlyDictionary<StringName, SkillDefinition> typedSkillDefinitions
    )
    {
        StringName skillId = "mage_phantasmal_kill";
        _test.True(
            typedSkillDefinitions != null && typedSkillDefinitions.ContainsKey(skillId),
            "正式 SkillDefinition 目录应包含 mage_phantasmal_kill。"
        );
        if (
            typedSkillDefinitions == null
            || !typedSkillDefinitions.TryGetValue(skillId, out SkillDefinition skill)
        )
            return;

        _test.Eq(skill.SkillId, skillId, "Phantasmal Kill skill_id 应匹配。");
        _test.Eq(skill.DisplayName, "怪影杀戮", "Phantasmal Kill display_name 应匹配。");
        _test.Eq(skill.IconId, skillId, "Phantasmal Kill icon_id 应匹配。");
        _test.Eq(skill.SkillType, new StringName("active"), "Phantasmal Kill 应是 active 技能。");
        _test.Eq(skill.MaxLevel, 9, "Phantasmal Kill max_level 应为 9。");
        _test.Eq(skill.NonCoreMaxLevel, 7, "Phantasmal Kill non_core_max_level 应为 7。");
        AssertIntArray(
            skill.MasteryCurve,
            new[] { 360, 900, 1980, 3600, 5760, 8600, 12000, 16000, 21000 },
            "Phantasmal Kill mastery_curve 应匹配正式 9 级曲线。"
        );
        AssertStringNameListContainsAll(
            skill.Tags,
            new StringName[]
            {
                "mage",
                "magic",
                "illusion",
                "fear",
                "psychic",
                "execute",
                "output",
                "control",
                "ultimate",
            },
            "Phantasmal Kill tags 应包含正式分类。"
        );
        _test.Eq(skill.LearnSource, new StringName("book"), "Phantasmal Kill learn_source 应为 book。");
        _test.Eq(skill.GrowthTier, new StringName("ultimate"), "Phantasmal Kill growth_tier 应为 ultimate。");
        _test.Eq(
            skill.AttributeGrowthProgress.TryGetValue("intelligence", out int intelligence)
                ? intelligence
                : 0,
            160,
            "Phantasmal Kill intelligence growth 应为 160。"
        );
        _test.Eq(
            skill.AttributeGrowthProgress.TryGetValue("willpower", out int willpower)
                ? willpower
                : 0,
            80,
            "Phantasmal Kill willpower growth 应为 80。"
        );

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "Phantasmal Kill 应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("ground"), "Phantasmal Kill target_mode 应为 ground。");
        _test.Eq(combat.TargetTeamFilter, new StringName("any"), "Phantasmal Kill target_team_filter 应为 any。");
        _test.Eq(
            combat.TargetSelectionMode,
            new StringName("single_coord"),
            "Phantasmal Kill target_selection_mode 应为 single_coord。"
        );
        _test.Eq(combat.SelectionOrderMode, new StringName("stable"), "Phantasmal Kill selection_order_mode 应为 stable。");
        _test.Eq(combat.RangeValue, 12, "Phantasmal Kill range_value 应为 12。");
        _test.Eq(combat.AreaPattern, new StringName("square"), "Phantasmal Kill area_pattern 应为 square。");
        _test.Eq(combat.AreaValue, 3, "Phantasmal Kill area_value 应为 3，形成 7x7 区域。");
        _test.Eq(combat.ApCost, 3, "Phantasmal Kill ap_cost 应为 3。");
        _test.Eq(combat.MpCost, 2000, "Phantasmal Kill mp_cost 应为 2000。");
        _test.Eq(combat.AuraCost, 2, "Phantasmal Kill aura_cost 应为 2。");
        _test.Eq(combat.CooldownTu, 600, "Phantasmal Kill cooldown_tu 应为 600。");
        _test.Eq(
            combat.SpecialResolutionProfileId,
            new StringName(""),
            "Phantasmal Kill 不应设置 special_resolution_profile_id。"
        );
        AssertStringNameListContainsAll(
            combat.AiTags,
            new StringName[] { "large_aoe", "ultimate", "execute", "friendly_fire_risk" },
            "Phantasmal Kill ai_tags 应包含友伤与处决提示。"
        );
        AssertStringNameListContainsAll(
            combat.DeliveryCategories,
            new StringName[] { "spell", "illusion", "fear", "psychic" },
            "Phantasmal Kill delivery_categories 应匹配法术/幻象/恐惧/心灵。"
        );
        _test.Eq(
            combat.EffectDefinitions?.Count ?? 0,
            1,
            "Phantasmal Kill 应只有一个正式效果。"
        );
        if (combat.EffectDefinitions == null || combat.EffectDefinitions.Count == 0)
            return;

        CombatEffectDefinition effect = combat.EffectDefinitions[0];
        _test.Eq(effect.EffectType, new StringName("graded_save_execute"), "Phantasmal Kill effect_type 应匹配。");
        _test.Eq(effect.EffectTargetTeamFilter, new StringName("any"), "Phantasmal Kill effect target filter 应为 any。");
        _test.Eq(effect.DamageTag, new StringName("psychic"), "Phantasmal Kill damage_tag 应为 psychic。");
        _test.Eq(effect.SaveDcMode, new StringName("caster_spell"), "Phantasmal Kill save_dc_mode 应为 caster_spell。");
        _test.Eq(effect.SaveDc, 0, "Phantasmal Kill save_dc 应为 0。");
        _test.Eq(effect.SaveDcSourceAbility, new StringName("intelligence"), "Phantasmal Kill save DC 来源应为 intelligence。");
        _test.Eq(effect.SaveAbility, new StringName("willpower"), "Phantasmal Kill save_ability 应为 willpower。");
        _test.Eq(effect.SaveTag, new StringName("illusion"), "Phantasmal Kill save_tag 应为 illusion。");
        _test.False(effect.SavePartialOnSuccess, "Phantasmal Kill 不应启用 save_partial_on_success。");

        IReadOnlyDictionary<string, object> parameters = effect.Parameters;
        _test.Eq(parameters?.Count ?? 0, 13, "Phantasmal Kill profile params 应为精确白名单。");
        AssertParamString(parameters, "profile_id", "phantasmal_kill");
        AssertParamInt(parameters, "failure_execute_threshold_fixed", 50);
        AssertParamInt(parameters, "failure_execute_threshold_max_hp_percent", 25);
        AssertParamInt(parameters, "failure_damage_dice_count", 6);
        AssertParamInt(parameters, "failure_damage_dice_sides", 6);
        AssertParamInt(parameters, "failure_frightened_duration_tu", 60);
        AssertParamInt(parameters, "failure_reaction_lock_duration_tu", 30);
        AssertParamInt(parameters, "critical_failure_execute_threshold_max_hp_percent", 35);
        AssertParamInt(parameters, "critical_failure_damage_dice_count", 10);
        AssertParamInt(parameters, "critical_failure_damage_dice_sides", 6);
        AssertParamInt(parameters, "critical_failure_frightened_duration_tu", 90);
        AssertParamInt(parameters, "critical_failure_stunned_duration_tu", 30);
        AssertParamInt(parameters, "success_aftershock_duration_tu", 30);

        for (int level = 0; level <= 9; level++)
        {
            string description = SkillLevelDescriptionFormatter.BuildLevelDescription(
                skill,
                level,
                new GDictionary()
            );
            AssertContainsText(description, "射程12", $"Phantasmal Kill level {level} 描述应包含射程。");
            AssertContainsText(description, "7x7", $"Phantasmal Kill level {level} 描述应包含 7x7 区域。");
            AssertContainsText(description, "意志幻象豁免", $"Phantasmal Kill level {level} 描述应包含意志幻象豁免。");
            AssertContainsText(description, "max(50, 最大生命25%)", $"Phantasmal Kill level {level} 描述应包含失败阈值。");
            AssertContainsText(description, "最大生命35%", $"Phantasmal Kill level {level} 描述应包含大失败阈值。");
            AssertContainsText(description, "6D6心灵伤害", $"Phantasmal Kill level {level} 描述应包含失败心灵伤害。");
            AssertContainsText(description, "10D6心灵伤害", $"Phantasmal Kill level {level} 描述应包含大失败心灵伤害。");
            AssertContainsText(description, "恐惧60TU", $"Phantasmal Kill level {level} 描述应包含失败状态。");
            AssertContainsText(description, "反应封锁30TU", $"Phantasmal Kill level {level} 描述应包含 reaction_lock。");
            AssertContainsText(description, "恐惧90TU", $"Phantasmal Kill level {level} 描述应包含大失败恐惧。");
            AssertContainsText(description, "震慑30TU", $"Phantasmal Kill level {level} 描述应包含大失败震慑。");
            AssertContainsText(description, "余悸30TU", $"Phantasmal Kill level {level} 描述应包含成功状态。");
            AssertContainsText(description, "友伤风险", $"Phantasmal Kill level {level} 描述应包含友伤风险。");
        }
    }

    private static IReadOnlyDictionary<StringName, SkillDefinition> BuildSingleSpecialProfileSkillDefinitions(
        StringName skillId,
        StringName profileId
    )
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "Special Profile Fixture",
            masteryCurve: new[] { 100 },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                specialResolutionProfileId: profileId
            )
        );
        return new Dictionary<StringName, SkillDefinition>
        {
            [skillId] = skillDefinition,
        };
    }

    private string PrepareEmptyBattleSpecialProfileManifestDir(string fixtureId)
    {
        return PrepareBattleSpecialProfileManifestDir(fixtureId, Array.Empty<GDictionary>());
    }

    private string PrepareBattleSpecialProfileManifestDir(
        string fixtureId,
        IReadOnlyList<GDictionary> manifestSpecs
    )
    {
        string fixtureRoot = $"{BATTLE_SPECIAL_PROFILE_FIXTURE_ROOT}/{fixtureId}";
        RemoveDirRecursive(fixtureRoot);
        string manifestDir = $"{fixtureRoot}/manifests";
        string profileDir = $"{fixtureRoot}/profiles";
        Error manifestError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(manifestDir)
        );
        Error profileError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(profileDir)
        );
        _test.True(manifestError == Error.Ok, "应能创建 battle special profile manifest fixture 目录。");
        _test.True(profileError == Error.Ok, "应能创建 battle special profile profile fixture 目录。");

        for (int specIndex = 0; specIndex < manifestSpecs.Count; specIndex++)
        {
            GDictionary spec = manifestSpecs[specIndex];
            string fileName = DictString(spec, "file_name", $"manifest_{specIndex}");
            Resource profileResource = spec.ContainsKey("profile_resource")
                ? spec["profile_resource"].AsGodotObject() as Resource
                : null;
            Resource savedProfile = null;
            if (profileResource != null)
            {
                string profilePath = $"{profileDir}/{fileName}_profile.tres";
                Error profileSaveError = ResourceSaver.Save(profileResource, profilePath);
                _test.True(profileSaveError == Error.Ok, "应能保存 battle special profile fixture profile。");
                savedProfile = ResourceLoader.Load(profilePath);
            }

            StringName profileId = ToStringName(
                GetDictValueOrDefault(spec, "profile_id", "meteor_swarm")
            );
            BattleSpecialProfileManifest manifest = new()
            {
                profile_id = profileId,
                schema_version = DictInt(spec, "schema_version", 1),
                owning_skill_ids = ToStringNameArray(
                    GetDictValueOrDefault(spec, "owning_skill_ids", new GArray { "mage_meteor_swarm" })
                ),
                runtime_resolver_id = ToStringName(
                    GetDictValueOrDefault(spec, "runtime_resolver_id", profileId)
                ),
                profile_resource = savedProfile,
                runtime_read_policy = ToStringName(
                    GetDictValueOrDefault(spec, "runtime_read_policy", "forbidden")
                ),
                required_regression_tests = ToStringArray(
                    GetDictValueOrDefault(spec, "required_regression_tests", new GArray())
                ),
            };
            string manifestPath = $"{manifestDir}/{fileName}.tres";
            Error manifestSaveError = ResourceSaver.Save(manifest, manifestPath);
            _test.True(manifestSaveError == Error.Ok, "应能保存 battle special profile fixture manifest。");
        }
        return manifestDir;
    }

    private static MeteorSwarmProfile BuildValidMeteorSwarmProfile()
    {
        return new MeteorSwarmProfile
        {
            coverage_shape_id = "square_7x7",
            radius = 3,
            friendly_fire_soft_expected_hp_percent = 10,
            friendly_fire_hard_expected_hp_percent = 25,
            friendly_fire_hard_worst_case_hp_percent = 50,
        };
    }

    private static MeteorSwarmProfile BuildBadSchemaMeteorSwarmProfile()
    {
        MeteorSwarmProfile profile = BuildValidMeteorSwarmProfile();
        profile.terrain_profiles = new GArray
        {
            new GDictionary
            {
                ["terrain_profile_id"] = "meteor_swarm_dust",
                ["ring_min"] = 0,
                ["ring_max"] = 2,
                ["move_cost_delta"] = 0,
                ["lifetime_policy"] = "timed",
                ["duration_tu"] = 50,
                ["tick_interval_tu"] = 5,
                ["tick_effect_type"] = "none",
                ["accuracy_modifer_spec"] = new GDictionary { ["modifier_delta"] = -2 },
                ["render_overlay_id"] = "meteor_dust_cloud",
            },
        };
        return profile;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(object valuesOption)
    {
        Godot.Collections.Array<StringName> result = new();
        GArray values = valuesOption switch
        {
            GArray rawArray => rawArray,
            Variant variant when variant.VariantType == Variant.Type.Array => variant.AsGodotArray(),
            _ => null,
        };
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.StringName)
                result.Add(value.AsStringName());
            else if (value.VariantType == Variant.Type.String)
                result.Add(value.AsString());
        }
        return result;
    }

    private static Godot.Collections.Array<string> ToStringArray(object valuesOption)
    {
        Godot.Collections.Array<string> result = new();
        GArray values = valuesOption switch
        {
            GArray rawArray => rawArray,
            Variant variant when variant.VariantType == Variant.Type.Array => variant.AsGodotArray(),
            _ => null,
        };
        if (values == null)
            return result;
        foreach (Variant value in values)
            result.Add(value.ToString());
        return result;
    }

    private static StringName ToStringName(object value)
    {
        return value switch
        {
            StringName stringName => stringName,
            string text => text,
            Variant variant when variant.VariantType == Variant.Type.StringName => variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String => variant.AsString(),
            _ => "",
        };
    }

    private static void RemoveDirRecursive(string directoryPath)
    {
        string absolutePath = ProjectSettings.GlobalizePath(directoryPath);
        if (!DirAccess.DirExistsAbsolute(absolutePath))
            return;
        using DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
            return;
        directory.ListDirBegin();
        while (true)
        {
            string entryName = directory.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;
            if (entryName == "." || entryName == "..")
                continue;
            string entryPath = $"{directoryPath}/{entryName}";
            if (directory.CurrentIsDir())
                RemoveDirRecursive(entryPath);
            else
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(entryPath));
        }
        directory.ListDirEnd();
        DirAccess.RemoveAbsolute(absolutePath);
    }

    private static List<QuestValidationEntry> BuildInvalidQuestEntries()
    {
        QuestDefinition missingIdQuest = BuildValidationQuest(
            "",
            "Missing Quest Id",
            "service_contract_board"
        );
        QuestDefinition duplicateA = BuildValidationQuest(
            "duplicate_quest",
            "Duplicate Quest A",
            "service_contract_board"
        );
        QuestDefinition duplicateB = BuildValidationQuest(
            "duplicate_quest",
            "Duplicate Quest B",
            "service_contract_board"
        );
        QuestDefinition invalidReferenceQuest = BuildValidationQuest(
            "invalid_reference_quest",
            "Invalid Reference Quest",
            "service_missing",
            objectives:
            [
                new QuestObjectiveDefinition(
                    "submit_missing_item",
                    "submit_item",
                    "missing_item",
                    1
                ),
                new QuestObjectiveDefinition(
                    "defeat_missing_enemy",
                    "defeat_enemy",
                    "missing_enemy",
                    1
                ),
            ],
            rewards:
            [
                new QuestRewardDefinition(
                    "item",
                    0,
                    "missing_item",
                    1,
                    "",
                    Array.Empty<QuestPendingRewardEntryDefinition>()
                ),
                new QuestRewardDefinition(
                    "pending_character_reward",
                    0,
                    "",
                    0,
                    "hero",
                    [
                        new QuestPendingRewardEntryDefinition(
                            "skill_unlock",
                            "missing_skill",
                            1
                        ),
                        new QuestPendingRewardEntryDefinition(
                            "skill_level",
                            "charge",
                            1
                        ),
                    ]
                ),
            ]
        );

        return
        [
            new QuestValidationEntry("fixture::missing_quest_id", missingIdQuest),
            new QuestValidationEntry("fixture::duplicate_quest_a", duplicateA),
            new QuestValidationEntry("fixture::duplicate_quest_b", duplicateB),
            new QuestValidationEntry("fixture::invalid_reference_quest", invalidReferenceQuest),
        ];
    }

    private static QuestDefinition BuildValidationQuest(
        StringName questId,
        string displayName,
        StringName providerInteractionId,
        IReadOnlyList<QuestObjectiveDefinition> objectives = null,
        IReadOnlyList<QuestRewardDefinition> rewards = null
    ) =>
        new(
            questId,
            displayName,
            "Validation fixture.",
            providerInteractionId,
            Array.Empty<StringName>(),
            Array.Empty<QuestAcceptRequirementDefinition>(),
            objectives
                ??
                [
                    new QuestObjectiveDefinition(
                        "report_once",
                        "settlement_action",
                        "service:training",
                        1
                    ),
                ],
            rewards
                ??
                [
                    new QuestRewardDefinition(
                        "gold",
                        10,
                        "",
                        0,
                        "",
                        Array.Empty<QuestPendingRewardEntryDefinition>()
                    ),
                ],
            false,
            "service_contract_board",
            [new StringName("contract_board")],
            "",
            "",
            "",
            ""
        );

    private static WorldGenerationDefinition BuildInvalidWorldGenerationDefinition()
    {
        const string resourcePath =
            "res://synthetic/resource_validation_invalid_world_generation.tres";
        using TestContentResourceLoader loader = new();
        using WorldMapGenerationConfig source = new()
        {
            world_size_in_chunks = Vector2I.Zero,
            chunk_size = Vector2I.Zero,
            starting_wild_spawn_min_distance = 2,
            starting_wild_spawn_max_distance = 1,
        };
        loader.RegisterCanonical(resourcePath, source);
        WorldMapGenerationConfig canonicalSource =
            loader.LoadCanonical<WorldMapGenerationConfig>(resourcePath);
        return canonicalSource.ToDefinition(resourcePath, loader);
    }

    private static Godot.Collections.Array<GDictionary> DuplicateDictArray(
        Godot.Collections.Array<GDictionary> source
    )
    {
        Godot.Collections.Array<GDictionary> result = new();
        foreach (GDictionary item in source)
            result.Add(item.Duplicate(true));
        return result;
    }

    private void AssertDomainIs(
        ValidationDomainResult domainResult,
        string expectedDomain,
        string message
    )
    {
        _test.True(domainResult?.Domain == expectedDomain, message);
    }

    private void AssertInvalid(ValidationDomainResult domainResult, string message)
    {
        _test.True(domainResult?.ErrorCount > 0, message);
    }

    private void AssertContainsError(
        ValidationDomainResult domainResult,
        string expectedErrorPart,
        string message
    )
    {
        if (domainResult?.Errors == null)
        {
            _test.True(false, message);
            return;
        }
        foreach (string error in domainResult.Errors)
        {
            if ((error ?? "").Contains(expectedErrorPart))
            {
                _test.True(true, message);
                return;
            }
        }
        _test.True(false, $"{message} errors={FormatErrors(domainResult.Errors)}");
    }

    private void AssertIntArray(IReadOnlyList<int> actual, IReadOnlyList<int> expected, string message)
    {
        _test.Eq(actual?.Count ?? 0, expected?.Count ?? 0, $"{message} 长度应匹配。");
        if (actual == null || expected == null)
            return;
        int count = Math.Min(actual.Count, expected.Count);
        for (int index = 0; index < count; index++)
            _test.Eq(actual[index], expected[index], $"{message} index={index}。");
    }

    private void AssertStringNameListContainsAll(
        IEnumerable<StringName> actual,
        IEnumerable<StringName> expected,
        string message
    )
    {
        foreach (StringName expectedValue in expected)
        {
            bool found = false;
            if (actual != null)
            {
                foreach (StringName actualValue in actual)
                {
                    if (actualValue == expectedValue)
                    {
                        found = true;
                        break;
                    }
                }
            }
            _test.True(found, $"{message} missing={expectedValue}");
        }
    }

    private void AssertParamInt(IReadOnlyDictionary<string, object> parameters, string key, int expected)
    {
        _test.True(parameters != null && parameters.ContainsKey(key), $"Phantasmal Kill params 应包含 {key}。");
        if (parameters == null || !parameters.ContainsKey(key))
            return;
        object value = parameters[key];
        bool isPlainInteger = value is byte or short or int or long;
        _test.True(isPlainInteger, $"Phantasmal Kill params.{key} 应为 plain integer。");
        if (isPlainInteger)
            _test.Eq(Convert.ToInt32(value), expected, $"Phantasmal Kill params.{key} 应匹配。");
    }

    private void AssertParamString(
        IReadOnlyDictionary<string, object> parameters,
        string key,
        string expected
    )
    {
        _test.True(parameters != null && parameters.ContainsKey(key), $"Phantasmal Kill params 应包含 {key}。");
        if (parameters == null || !parameters.ContainsKey(key))
            return;
        object value = parameters[key];
        _test.True(value is string, $"Phantasmal Kill params.{key} 应为 plain string。");
        if (value is string text)
            _test.Eq(text, expected, $"Phantasmal Kill params.{key} 应匹配。");
    }

    private void AssertContainsText(string text, string expectedPart, string message)
    {
        _test.True((text ?? "").Contains(expectedPart), $"{message} text={text}");
    }

    private static IReadOnlyDictionary<StringName, SkillDef> BuildSkillDefIndex(GDictionary skillDefs)
    {
        Dictionary<StringName, SkillDef> result = new();
        if (skillDefs == null)
            return result;
        foreach (Variant rawKey in skillDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName skillId = rawKey.AsStringName();
            if (skillId == "")
                continue;
            if (skillDefs[rawKey].AsGodotObject() is SkillDef skillDef)
                result[skillId] = skillDef;
        }
        return result;
    }

    private static List<string> ToStringList(IEnumerable<string> values)
    {
        List<string> result = new();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value ?? "");
        return result;
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = ToStringList(errors);
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static string DictString(GDictionary dict, string key, string fallback)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        Variant value = dict[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    private static int DictInt(GDictionary dict, string key, int fallback)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        Variant value = dict[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static object GetDictValueOrDefault(GDictionary dict, string key, object fallback)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return dict[key];
    }
}
