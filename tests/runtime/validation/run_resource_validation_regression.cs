using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_resource_validation_regression : SceneTree
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
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        using ProgressionContentRegistry progressionRegistry = new();
        using ItemContentRegistry itemRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();

        GDictionary skillDefs = ProjectSkillDefs(progressionRegistry.GetSkillDefsTyped());
        GDictionary itemDefs = ProjectItemDefs(itemRegistry.GetItemDefsTyped());
        GDictionary enemyTemplates = ProjectEnemyTemplates(enemyRegistry.GetEnemyTemplatesTyped());
        GDictionary wildEncounterRosters = ProjectEnemyRosters(
            enemyRegistry.GetWildEncounterRostersTyped()
        );
        IReadOnlyDictionary<StringName, ItemDef> typedItemDefs = itemRegistry.GetItemDefsTyped();
        IReadOnlyDictionary<StringName, SkillDef> typedSkillDefs =
            progressionRegistry.GetSkillDefsTyped();

        TestFormalPhantasmalKillResource(typedSkillDefs);

        ValidationDomainResult officialItemResult = ContentValidationRunner.ValidateOfficialItemContent();
        ValidationDomainResult officialEnemyResult = ContentValidationRunner.ValidateEnemySeed(
            OFFICIAL_ENEMY_SEED_PATH
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
                    typedSkillDefs
                ),
                officialItemResult,
                ContentValidationRunner.ValidateRecipeDirectory(OFFICIAL_RECIPE_DIRECTORY, itemDefs),
                officialEnemyResult,
                ContentValidationRunner.ValidateWorldPresets(enemyTemplates, wildEncounterRosters),
                ContentValidationRunner.ValidateQuestEntries(
                    "official_quests",
                    BuildQuestEntriesFromTyped(
                        progressionRegistry.GetQuestDefsTyped(),
                        "progression_seed"
                    ),
                    typedItemDefs,
                    typedSkillDefs,
                    enemyRegistry.GetEnemyTemplatesTyped()
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
            ENEMY_MISSING_ID_SEED_PATH
        );
        ValidationDomainResult enemyDuplicateResult = ContentValidationRunner.ValidateEnemySeed(
            ENEMY_DUPLICATE_ID_SEED_PATH
        );
        ValidationDomainResult enemyInvalidReferenceResult =
            ContentValidationRunner.ValidateEnemySeed(ENEMY_INVALID_REFERENCE_SEED_PATH);
        ValidationDomainResult enemyIncompleteSeedResult =
            ContentValidationRunner.ValidateEnemySeedWithDirectoryCompleteness(
                ENEMY_INCOMPLETE_SEED_PATH,
                ENEMY_INCOMPLETE_TEMPLATE_DIRECTORY,
                ENEMY_INCOMPLETE_BRAIN_DIRECTORY,
                ENEMY_INCOMPLETE_ROSTER_DIRECTORY
            );
        ValidationDomainResult enemyInvalidInitialStageResult =
            ContentValidationRunner.ValidateEnemySeed(ENEMY_INVALID_INITIAL_STAGE_SEED_PATH);
        ValidationDomainResult enemyInvalidSkillLevelMapResult =
            ContentValidationRunner.ValidateEnemySeed(ENEMY_INVALID_SKILL_LEVEL_MAP_SEED_PATH);
        ValidationDomainResult battleSpecialMissingManifestResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_missing_manifest",
                typedSkillDefs,
                PrepareEmptyBattleSpecialProfileManifestDir("missing_manifest")
            );
        ValidationDomainResult battleSpecialUnknownProfileResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_unknown_profile_missing_manifest",
                BuildSingleSpecialProfileSkillDefs("phantom_special_skill", "phantom_profile"),
                PrepareEmptyBattleSpecialProfileManifestDir("unknown_profile_missing_manifest")
            );
        ValidationDomainResult battleSpecialDuplicateProfileResult =
            ContentValidationRunner.ValidateBattleSpecialProfileRegistry(
                "battle_special_profile_duplicate_profile",
                typedSkillDefs,
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
                typedSkillDefs,
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
                typedSkillDefs,
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
                typedSkillDefs,
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
                typedSkillDefs,
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
                typedSkillDefs,
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
            BuildInvalidWorldGenerationConfig(),
            enemyTemplates,
            wildEncounterRosters
        );
        ValidationDomainResult questResult = ContentValidationRunner.ValidateQuestEntries(
            "invalid_quest_entries",
            BuildInvalidQuestEntries(),
            typedItemDefs,
            typedSkillDefs,
            enemyRegistry.GetEnemyTemplatesTyped()
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
            GD.Print(reportText);

        Quit(_test.Finish("Resource validation regression"));
    }

    private static List<QuestValidationEntry> BuildQuestEntriesFromTyped(
        IReadOnlyDictionary<StringName, QuestDef> questDefs,
        string sourcePrefix
    )
    {
        List<QuestValidationEntry> entries = new();
        foreach (StringName questId in SortedStringNameKeys(questDefs))
        {
            entries.Add(
                new QuestValidationEntry(
                    $"{sourcePrefix}::{questId}",
                    questDefs.TryGetValue(questId, out QuestDef questDef) ? questDef : null
                )
            );
        }
        return entries;
    }

    private static List<StringName> SortedStringNameKeys(
        IReadOnlyDictionary<StringName, QuestDef> source
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
        using ItemContentRegistry registry = new();
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
        IReadOnlyDictionary<StringName, SkillDef> typedSkillDefs
    )
    {
        StringName skillId = "mage_phantasmal_kill";
        _test.True(
            typedSkillDefs != null && typedSkillDefs.ContainsKey(skillId),
            "正式技能目录应包含 mage_phantasmal_kill。"
        );
        if (typedSkillDefs == null || !typedSkillDefs.TryGetValue(skillId, out SkillDef skill))
            return;

        _test.Eq(skill.skill_id, skillId, "Phantasmal Kill skill_id 应匹配。");
        _test.Eq(skill.display_name, "怪影杀戮", "Phantasmal Kill display_name 应匹配。");
        _test.Eq(skill.icon_id, skillId, "Phantasmal Kill icon_id 应匹配。");
        _test.Eq(skill.skill_type, new StringName("active"), "Phantasmal Kill 应是 active 技能。");
        _test.Eq(skill.max_level, 9, "Phantasmal Kill max_level 应为 9。");
        _test.Eq(skill.non_core_max_level, 7, "Phantasmal Kill non_core_max_level 应为 7。");
        AssertIntArray(
            skill.mastery_curve,
            new[] { 360, 900, 1980, 3600, 5760, 8600, 12000, 16000, 21000 },
            "Phantasmal Kill mastery_curve 应匹配正式 9 级曲线。"
        );
        AssertStringNameListContainsAll(
            skill.TagsTyped,
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
        _test.Eq(skill.learn_source, new StringName("book"), "Phantasmal Kill learn_source 应为 book。");
        _test.Eq(skill.growth_tier, new StringName("ultimate"), "Phantasmal Kill growth_tier 应为 ultimate。");
        _test.Eq(
            skill.AttributeGrowthProgressTyped.TryGetValue("intelligence", out int intelligence)
                ? intelligence
                : 0,
            160,
            "Phantasmal Kill intelligence growth 应为 160。"
        );
        _test.Eq(
            skill.AttributeGrowthProgressTyped.TryGetValue("willpower", out int willpower)
                ? willpower
                : 0,
            80,
            "Phantasmal Kill willpower growth 应为 80。"
        );

        CombatSkillDef combat = skill.combat_profile;
        _test.True(combat != null, "Phantasmal Kill 应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.target_mode, new StringName("ground"), "Phantasmal Kill target_mode 应为 ground。");
        _test.Eq(combat.target_team_filter, new StringName("any"), "Phantasmal Kill target_team_filter 应为 any。");
        _test.Eq(
            combat.target_selection_mode,
            new StringName("single_coord"),
            "Phantasmal Kill target_selection_mode 应为 single_coord。"
        );
        _test.Eq(combat.selection_order_mode, new StringName("stable"), "Phantasmal Kill selection_order_mode 应为 stable。");
        _test.Eq(combat.range_value, 12, "Phantasmal Kill range_value 应为 12。");
        _test.Eq(combat.area_pattern, new StringName("square"), "Phantasmal Kill area_pattern 应为 square。");
        _test.Eq(combat.area_value, 3, "Phantasmal Kill area_value 应为 3，形成 7x7 区域。");
        _test.Eq(combat.ap_cost, 3, "Phantasmal Kill ap_cost 应为 3。");
        _test.Eq(combat.mp_cost, 2000, "Phantasmal Kill mp_cost 应为 2000。");
        _test.Eq(combat.aura_cost, 2, "Phantasmal Kill aura_cost 应为 2。");
        _test.Eq(combat.cooldown_tu, 600, "Phantasmal Kill cooldown_tu 应为 600。");
        _test.Eq(
            combat.special_resolution_profile_id,
            new StringName(""),
            "Phantasmal Kill 不应设置 special_resolution_profile_id。"
        );
        AssertStringNameListContainsAll(
            combat.ai_tags,
            new StringName[] { "large_aoe", "ultimate", "execute", "friendly_fire_risk" },
            "Phantasmal Kill ai_tags 应包含友伤与处决提示。"
        );
        AssertStringNameListContainsAll(
            combat.delivery_categories,
            new StringName[] { "spell", "illusion", "fear", "psychic" },
            "Phantasmal Kill delivery_categories 应匹配法术/幻象/恐惧/心灵。"
        );
        _test.Eq(combat.effect_defs?.Count ?? 0, 1, "Phantasmal Kill 应只有一个正式效果。");
        if (combat.effect_defs == null || combat.effect_defs.Count == 0)
            return;

        CombatEffectDef effect = combat.effect_defs[0];
        _test.Eq(effect.effect_type, new StringName("graded_save_execute"), "Phantasmal Kill effect_type 应匹配。");
        _test.Eq(effect.effect_target_team_filter, new StringName("any"), "Phantasmal Kill effect target filter 应为 any。");
        _test.Eq(effect.damage_tag, new StringName("psychic"), "Phantasmal Kill damage_tag 应为 psychic。");
        _test.Eq(effect.save_dc_mode, new StringName("caster_spell"), "Phantasmal Kill save_dc_mode 应为 caster_spell。");
        _test.Eq(effect.save_dc, 0, "Phantasmal Kill save_dc 应为 0。");
        _test.Eq(effect.save_dc_source_ability, new StringName("intelligence"), "Phantasmal Kill save DC 来源应为 intelligence。");
        _test.Eq(effect.save_ability, new StringName("willpower"), "Phantasmal Kill save_ability 应为 willpower。");
        _test.Eq(effect.save_tag, new StringName("illusion"), "Phantasmal Kill save_tag 应为 illusion。");
        _test.False(effect.save_partial_on_success, "Phantasmal Kill 不应启用 save_partial_on_success。");

        GDictionary parameters = effect.@params;
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
            string description = SkillLevelDescriptionFormatter.BuildLevelDescription(skill, level, new GDictionary());
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

    private static IReadOnlyDictionary<StringName, SkillDef> BuildSingleSpecialProfileSkillDefs(
        StringName skillId,
        StringName profileId
    )
    {
        CombatSkillDef combatProfile = new() { skill_id = skillId, special_resolution_profile_id = profileId };
        SkillDef skillDef = new()
        {
            skill_id = skillId,
            display_name = "Special Profile Fixture",
            icon_id = skillId,
            mastery_curve = new[] { 100 },
            combat_profile = combatProfile,
        };
        return new Dictionary<StringName, SkillDef> { [skillId] = skillDef };
    }

    private static GDictionary ProjectSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        GDictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillId == "" || skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
    }

    private static GDictionary ProjectItemDefs(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null)
                continue;
            result[itemId] = itemDef;
        }
        return result;
    }

    private static GDictionary ProjectEnemyTemplates(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        GDictionary result = new();
        if (enemyTemplates == null)
            return result;
        foreach ((StringName templateId, EnemyTemplateDef templateDef) in enemyTemplates)
        {
            if (templateId == "" || templateDef == null)
                continue;
            result[templateId] = templateDef;
        }
        return result;
    }

    private static GDictionary ProjectEnemyRosters(
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> rosters
    )
    {
        GDictionary result = new();
        if (rosters == null)
            return result;
        foreach ((StringName rosterId, WildEncounterRosterDef rosterDef) in rosters)
        {
            if (rosterId == "" || rosterDef == null)
                continue;
            result[rosterId] = rosterDef;
        }
        return result;
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
        QuestDef missingIdQuest = new()
        {
            display_name = "Missing Quest Id",
            provider_interaction_id = "service_contract_board",
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["objective_id"] = "report_once",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                    ["target_id"] = "service:training",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new() { ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Gold), ["amount"] = 10 },
            },
        };

        QuestDef duplicateA = new()
        {
            quest_id = "duplicate_quest",
            display_name = "Duplicate Quest A",
            provider_interaction_id = "service_contract_board",
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["objective_id"] = "report_once",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SettlementAction),
                    ["target_id"] = "service:training",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new() { ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Gold), ["amount"] = 10 },
            },
        };

        QuestDef duplicateB = new()
        {
            quest_id = "duplicate_quest",
            display_name = "Duplicate Quest B",
            provider_interaction_id = "service_contract_board",
            objective_defs = DuplicateDictArray(duplicateA.objective_defs),
            reward_entries = DuplicateDictArray(duplicateA.reward_entries),
        };

        QuestDef invalidReferenceQuest = new()
        {
            quest_id = "invalid_reference_quest",
            display_name = "Invalid Reference Quest",
            provider_interaction_id = "service_missing",
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["objective_id"] = "submit_missing_item",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SubmitItem),
                    ["target_id"] = "missing_item",
                    ["target_value"] = 1,
                },
                new()
                {
                    ["objective_id"] = "defeat_missing_enemy",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy),
                    ["target_id"] = "missing_enemy",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Item),
                    ["item_id"] = "missing_item",
                    ["quantity"] = 1,
                },
                new()
                {
                    ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.PendingCharacterReward),
                    ["member_id"] = "hero",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_type"] = "skill_unlock",
                            ["target_id"] = "missing_skill",
                            ["amount"] = 1,
                        },
                        new GDictionary
                        {
                            ["entry_type"] = "skill_level",
                            ["target_id"] = "charge",
                            ["amount"] = 1,
                        },
                    },
                },
            },
        };

        return
        [
            new QuestValidationEntry("fixture::missing_quest_id", missingIdQuest),
            new QuestValidationEntry("fixture::duplicate_quest_a", duplicateA),
            new QuestValidationEntry("fixture::duplicate_quest_b", duplicateB),
            new QuestValidationEntry("fixture::invalid_reference_quest", invalidReferenceQuest),
        ];
    }

    private static WorldMapGenerationConfig BuildInvalidWorldGenerationConfig()
    {
        FacilityConfig validFacility = new()
        {
            facility_id = "known_facility",
            display_name = "Known Facility",
            interaction_type = "service_known",
            allowed_slot_tags = new Godot.Collections.Array<string> { "core" },
        };
        FacilityConfig duplicateFacility = new()
        {
            facility_id = "known_facility",
            display_name = "Duplicate Facility",
            interaction_type = "service_duplicate",
            allowed_slot_tags = new Godot.Collections.Array<string> { "core" },
        };
        FacilityConfig missingIdFacility = new()
        {
            display_name = "Missing Id Facility",
            interaction_type = "service_missing_id",
            allowed_slot_tags = new Godot.Collections.Array<string> { "core" },
        };

        FacilitySlotConfig knownSlot = new() { slot_id = "core_slot", slot_tag = "core" };
        WeightedFacilityEntry optionalMissingFacility = new()
        {
            facility_id = "missing_optional_facility",
            weight = 1,
        };
        SettlementConfig settlement = new()
        {
            settlement_id = "known_settlement",
            display_name = "Known Settlement",
            facility_slots = new Godot.Collections.Array<Resource> { knownSlot },
            guaranteed_facility_ids = new Godot.Collections.Array<string> { "missing_facility" },
            optional_facility_pool = new Godot.Collections.Array<Resource> { optionalMissingFacility },
        };
        SettlementConfig duplicateSettlement = new()
        {
            settlement_id = "known_settlement",
            display_name = "Duplicate Settlement",
            facility_slots = new Godot.Collections.Array<Resource> { knownSlot },
        };
        SettlementConfig missingIdSettlement = new()
        {
            display_name = "Missing Id Settlement",
            facility_slots = new Godot.Collections.Array<Resource> { knownSlot },
        };

        SettlementDistributionRule missingDistribution = new()
        {
            settlement_id = "missing_settlement",
            faction_id = "neutral",
        };

        WildSpawnRule missingWildRule = new()
        {
            region_tag = "invalid_wilds",
            enemy_roster_template_id = "missing_enemy",
            encounter_profile_id = "missing_roster",
            density_per_chunk = 1,
        };
        WildSpawnRule wildEmptyChunks = new()
        {
            region_tag = "empty_chunks_patch",
            enemy_roster_template_id = "missing_enemy",
            density_per_chunk = 1,
            chunk_coords = new Godot.Collections.Array<Vector2I>(),
        };
        WildSpawnRule wildOobChunks = new()
        {
            region_tag = "oob_chunks_patch",
            enemy_roster_template_id = "missing_enemy",
            density_per_chunk = 1,
            chunk_coords = new Godot.Collections.Array<Vector2I>
            {
                new(-1, 0),
                new(0, -1),
                new(99, 0),
            },
        };
        WildSpawnRule wildNegativeDistance = new()
        {
            region_tag = "negative_distance_patch",
            enemy_roster_template_id = "missing_enemy",
            density_per_chunk = 1,
            min_distance_to_settlement = -3,
            chunk_coords = new Godot.Collections.Array<Vector2I> { new(0, 0) },
        };

        SettlementConfig badScriptSubmap = new();
        MountedSubmapConfig missingIdSubmap = new()
        {
            display_name = "Missing Id Submap",
            generation_config_path = "res://nonexistent/never_loaded.tres",
        };
        MountedSubmapConfig duplicateSubmapA = new()
        {
            submap_id = "duplicate_submap",
            display_name = "Duplicate Submap A",
            generation_config_path = "res://nonexistent/never_loaded_a.tres",
        };
        MountedSubmapConfig duplicateSubmapB = new()
        {
            submap_id = "duplicate_submap",
            display_name = "Duplicate Submap B",
            generation_config_path = "res://nonexistent/never_loaded_b.tres",
        };
        MountedSubmapConfig emptyPathSubmap = new()
        {
            submap_id = "empty_path_submap",
            display_name = "Empty Path Submap",
            generation_config_path = "",
        };
        MountedSubmapConfig unloadableSubmap = new()
        {
            submap_id = "unloadable_submap",
            display_name = "Unloadable Submap",
            generation_config_path = "res://does_not_exist/missing_world_config.tres",
        };

        SettlementConfig badScriptEvent = new();
        WorldEventConfig missingIdEvent = new() { world_coord = new Vector2I(0, 0) };
        WorldEventConfig duplicateEventA = new()
        {
            event_id = "duplicate_event",
            event_type = "enter_submap",
            target_submap_id = "duplicate_submap",
            world_coord = new Vector2I(0, 0),
        };
        WorldEventConfig duplicateEventB = new()
        {
            event_id = "duplicate_event",
            event_type = "enter_submap",
            target_submap_id = "duplicate_submap",
            world_coord = new Vector2I(1, 1),
        };
        WorldEventConfig oobCoordEvent = new()
        {
            event_id = "oob_coord_event",
            event_type = "enter_submap",
            target_submap_id = "duplicate_submap",
            world_coord = new Vector2I(99, 99),
        };
        WorldEventConfig missingTargetEvent = new()
        {
            event_id = "missing_target_event",
            event_type = "enter_submap",
            target_submap_id = "",
            world_coord = new Vector2I(0, 0),
        };
        WorldEventConfig unknownTargetEvent = new()
        {
            event_id = "unknown_target_event",
            event_type = "enter_submap",
            target_submap_id = "never_declared_submap",
            world_coord = new Vector2I(0, 0),
        };

        return new WorldMapGenerationConfig
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            settlement_library = new Godot.Collections.Array<Resource>
            {
                settlement,
                duplicateSettlement,
                missingIdSettlement,
            },
            facility_library = new Godot.Collections.Array<Resource>
            {
                validFacility,
                duplicateFacility,
                missingIdFacility,
            },
            settlement_distribution = new Godot.Collections.Array<Resource> { missingDistribution },
            wild_monster_distribution = new Godot.Collections.Array<Resource>
            {
                missingWildRule,
                wildEmptyChunks,
                wildOobChunks,
                wildNegativeDistance,
            },
            mounted_submaps = new Godot.Collections.Array<Resource>
            {
                badScriptSubmap,
                missingIdSubmap,
                duplicateSubmapA,
                duplicateSubmapB,
                emptyPathSubmap,
                unloadableSubmap,
            },
            world_events = new Godot.Collections.Array<Resource>
            {
                badScriptEvent,
                missingIdEvent,
                duplicateEventA,
                duplicateEventB,
                oobCoordEvent,
                missingTargetEvent,
                unknownTargetEvent,
            },
        };
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

    private void AssertIntArray(int[] actual, int[] expected, string message)
    {
        _test.Eq(actual?.Length ?? 0, expected?.Length ?? 0, $"{message} 长度应匹配。");
        if (actual == null || expected == null)
            return;
        int count = Math.Min(actual.Length, expected.Length);
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

    private void AssertParamInt(GDictionary parameters, string key, int expected)
    {
        _test.True(parameters != null && parameters.ContainsKey(key), $"Phantasmal Kill params 应包含 {key}。");
        if (parameters == null || !parameters.ContainsKey(key))
            return;
        Variant value = parameters[key];
        _test.Eq(value.VariantType, Variant.Type.Int, $"Phantasmal Kill params.{key} 应为 int。");
        if (value.VariantType == Variant.Type.Int)
            _test.Eq(value.AsInt32(), expected, $"Phantasmal Kill params.{key} 应匹配。");
    }

    private void AssertParamString(GDictionary parameters, string key, string expected)
    {
        _test.True(parameters != null && parameters.ContainsKey(key), $"Phantasmal Kill params 应包含 {key}。");
        if (parameters == null || !parameters.ContainsKey(key))
            return;
        Variant value = parameters[key];
        _test.Eq(value.VariantType, Variant.Type.String, $"Phantasmal Kill params.{key} 应为 string。");
        if (value.VariantType == Variant.Type.String)
            _test.Eq(value.AsString(), expected, $"Phantasmal Kill params.{key} 应匹配。");
    }

    private void AssertContainsText(string text, string expectedPart, string message)
    {
        _test.True((text ?? "").Contains(expectedPart), $"{message} text={text}");
    }

    private static IReadOnlyDictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
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
