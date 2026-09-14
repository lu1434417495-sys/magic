using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_resource_validation_regression : LifecycleTestSceneTree
{
    private const string OFFICIAL_SKILL_DIRECTORY = "res://data/configs/json/skills";
    private const string OFFICIAL_PROFESSION_DIRECTORY =
        "res://data/configs/json/professions";
    private const string OFFICIAL_RECIPE_DIRECTORY = "res://data/configs/json/recipes";
    private const string BATTLE_SPECIAL_PROFILE_FIXTURE_ROOT = "user://rv/bsp";

    private readonly TestHarness _test = new();
    private readonly List<string> _reports = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        using ProgressionContentRegistry progressionRegistry = new();
        using ItemContentRegistry itemRegistry = new();

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
        ValidationDomainResult officialEnemyResult = ContentValidationRunner.ValidateEnemyJson(
            typedItemDefs,
            typedSkillDefinitions
        );

        ValidationRunReport officialReport = ContentValidationRunner.BuildRunReport(
            "official_content",
            new[]
            {
                ContentValidationRunner.ValidateSkillDirectory(OFFICIAL_SKILL_DIRECTORY),
                ContentValidationRunner.ValidateProfessionDirectory(
                    OFFICIAL_PROFESSION_DIRECTORY,
                    typedSkillDefinitions
                ),
                ContentValidationRunner.ValidateIdentityContent(
                    "official_identity",
                    typedSkillDefinitions
                ),
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
                battleSpecialMissingManifestResult,
                battleSpecialUnknownProfileResult,
                worldResult,
                questResult,
            }
        );
        _reports.Add(ContentValidationRunner.FormatReport(invalidFixtureReport));

        AssertContainsErrors(
            battleSpecialMissingManifestResult,
            "特殊技能 missing-manifest fixture 必须命中目标规则。",
            "Battle special profile meteor_swarm is missing manifest for skill mage_meteor_swarm."
        );
        AssertContainsErrors(
            battleSpecialUnknownProfileResult,
            "特殊技能 unknown-profile fixture 必须命中目标规则。",
            "Battle special profile phantom_profile is missing manifest for skill phantom_special_skill."
        );
        AssertContainsErrors(
            worldResult,
            "非法世界 fixture 的三条独立边界都必须被命中。",
            "invalid world_size_in_chunks (0, 0)",
            "invalid chunk_size (0, 0)",
            "starting_wild_spawn_min_distance greater than max distance"
        );

        _test.True(questResult.Domain == "quest", "任务 validation runner 应稳定归入 quest domain。");
        AssertContainsErrors(
            questResult,
            "非法任务 entry 集合中的每条独立规则都必须被命中。",
            "fixture::missing_quest_id is missing quest_id",
            "Duplicate quest_id registered: duplicate_quest",
            "invalid_reference_quest has unsupported pending_character_reward entry_type skill_level",
            "invalid_reference_quest references missing provider_interaction_id service_missing",
            "provider_kind 'service_contract_board' 要求 provider_interaction_id 为 'service_contract_board'",
            "submit_missing_item references missing item missing_item",
            "defeat_missing_enemy references missing enemy missing_enemy",
            "reward references missing item missing_item",
            "pending_character_reward references missing skill missing_skill"
        );

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
        _test.Eq(
            skill.IconId,
            skillId,
            "Phantasmal Kill 应投影正式配置的 icon asset ID。"
        );
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

        GradedSaveExecuteEffectPayloadDefinition payload =
            effect.Payload as GradedSaveExecuteEffectPayloadDefinition;
        _test.True(payload != null, "Phantasmal Kill 应投影为 graded-save typed payload。");
        if (payload == null)
            return;
        _test.Eq(payload.ProfileId, new StringName("phantasmal_kill"), "Phantasmal Kill profile_id 应匹配。");
        _test.Eq(payload.FailureExecuteThresholdFixed, 50, "Phantasmal Kill 失败固定处决阈值应匹配。");
        _test.Eq(payload.FailureExecuteThresholdMaxHpPercent, 25, "Phantasmal Kill 失败生命比例阈值应匹配。");
        _test.Eq(payload.FailureDamageDiceCount, 6, "Phantasmal Kill 失败伤害骰数量应匹配。");
        _test.Eq(payload.FailureDamageDiceSides, 6, "Phantasmal Kill 失败伤害骰面数应匹配。");
        _test.Eq(payload.FailureFrightenedDurationTu, 60, "Phantasmal Kill 失败恐惧时长应匹配。");
        _test.Eq(payload.FailureReactionLockDurationTu, 30, "Phantasmal Kill 失败反应锁时长应匹配。");
        _test.Eq(payload.CriticalFailureExecuteThresholdMaxHpPercent, 35, "Phantasmal Kill 大失败生命比例阈值应匹配。");
        _test.Eq(payload.CriticalFailureDamageDiceCount, 10, "Phantasmal Kill 大失败伤害骰数量应匹配。");
        _test.Eq(payload.CriticalFailureDamageDiceSides, 6, "Phantasmal Kill 大失败伤害骰面数应匹配。");
        _test.Eq(payload.CriticalFailureFrightenedDurationTu, 90, "Phantasmal Kill 大失败恐惧时长应匹配。");
        _test.Eq(payload.CriticalFailureStunnedDurationTu, 30, "Phantasmal Kill 大失败震慑时长应匹配。");
        _test.Eq(payload.SuccessAftershockDurationTu, 30, "Phantasmal Kill 成功余震时长应匹配。");

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
            AssertContainsText(description, "当前生命值不高于50点，或不高于最大生命值的25%", $"Phantasmal Kill level {level} 应以自然语言说明失败时满足任一阈值即可处决。");
            AssertContainsText(description, "当前生命值不高于最大生命值的35%", $"Phantasmal Kill level {level} 应以自然语言说明大失败阈值。");
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
        string fixtureRoot = $"{BATTLE_SPECIAL_PROFILE_FIXTURE_ROOT}/{fixtureId}";
        RemoveDirRecursive(fixtureRoot);
        string manifestDir = $"{fixtureRoot}/manifests";
        Error manifestError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(manifestDir)
        );
        _test.True(manifestError == Error.Ok, "应能创建 battle special profile manifest fixture 目录。");
        return manifestDir;
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
        return TestWorldGenerationDefinitionFactory.Create(
            "resource_validation_invalid_world",
            worldSizeInChunks: Vector2I.Zero,
            chunkSize: Vector2I.Zero,
            startingWildSpawnMinDistance: 2,
            startingWildSpawnMaxDistance: 1
        );
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

    private void AssertContainsErrors(
        ValidationDomainResult domainResult,
        string message,
        params string[] expectedErrorParts
    )
    {
        if (domainResult?.Errors == null)
        {
            _test.Fail($"{message} validation result did not expose errors.");
            return;
        }
        foreach (string expectedErrorPart in expectedErrorParts)
        {
            bool found = false;
            foreach (string error in domainResult.Errors)
            {
                if ((error ?? "").Contains(expectedErrorPart, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }
            _test.True(
                found,
                $"{message} missing='{expectedErrorPart}' errors={FormatErrors(domainResult.Errors)}"
            );
        }
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
