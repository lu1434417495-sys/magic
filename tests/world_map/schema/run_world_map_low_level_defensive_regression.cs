using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_low_level_defensive_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestGridSystemNoLongerRequiresGodotObjectRegistration();
        TestRuntimeBattleSelectionStateUsesPlainCollections();
        TestRuntimeCommandLoggerKeepsScopeTypedAndInternal();
        TestRuntimeCommandHandlersNoLongerRequireGodotRegistration();
        TestRuntimeSnapshotBuilderUsesTypedSourceBoundary();
        TestWorldGenerationHelpersNoLongerRequireGodotRegistration();
        TestWorldPresetHelpersNoLongerRequireGodotRegistration();
        TestHeadlessSnapshotHelpersNoLongerRequireGodotRegistration();
        TestUtilityHelpersNoLongerRequireGodotRegistration();
        TestProgressionContentRuleHelpersNoLongerRequireGodotRegistration();
        TestProgressionRuleHelpersNoLongerRequireGodotRegistration();
        TestGridFootprintStateUsesPublicBehavior();
        TestGridCellSurfaceKeepsMinimalRuntimeContract();
        TestVisibilityRebuildIgnoresForeignFactionSources();
        TestFogRevealExportLoadKeepsRevealedCells();

        if (_failures.Count == 0)
        {
            GD.Print("World map low-level defensive regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map low-level defensive regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestGridSystemNoLongerRequiresGodotObjectRegistration()
    {
        Type gridType = typeof(WorldMapGridSystem);
        Type cellType = typeof(WorldMapCellData);
        Type fogType = typeof(WorldMapFogSystem);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(gridType),
            "WorldMapGridSystem 应是普通 C# 运行时对象，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            gridType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "WorldMapGridSystem 不应继续注册为 Godot GlobalClass。"
        );
        AssertEq(
            gridType.GetMethod("get_neighbors_4")?.ReturnType,
            typeof(List<Vector2I>),
            "WorldMapGridSystem 邻居查询不应继续返回 Godot Array。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(cellType),
            "WorldMapCellData 应是普通 C# 数据对象，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            cellType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "WorldMapCellData 不应继续注册为 Godot GlobalClass。"
        );
        Type visionSourceType = typeof(VisionSourceData);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(visionSourceType),
            "VisionSourceData 应是普通 C# 数据对象，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            visionSourceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "VisionSourceData 不应继续注册为 Godot GlobalClass。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(fogType),
            "WorldMapFogSystem 应是普通 C# 迷雾 service，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            fogType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "WorldMapFogSystem 不应继续注册为 Godot GlobalClass。"
        );
        AssertEq(
            fogType.GetMethod(
                "RevealDiamond",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(List<Vector2I>),
            "WorldMapFogSystem paid reveal 不应继续返回 Godot Array。"
        );
    }

    private void TestRuntimeBattleSelectionStateUsesPlainCollections()
    {
        Type selectionStateType = typeof(GameRuntimeBattleSelectionState);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(selectionStateType),
            "GameRuntimeBattleSelectionState 应是普通 C# 运行时状态，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            selectionStateType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "GameRuntimeBattleSelectionState 不应继续注册为 Godot GlobalClass。"
        );
        AssertEq(
            selectionStateType.GetProperty("queued_target_coords")?.PropertyType,
            typeof(List<Vector2I>),
            "GameRuntimeBattleSelectionState 目标坐标队列不应继续使用 Godot Array。"
        );
        AssertEq(
            selectionStateType.GetProperty("queued_target_unit_ids")?.PropertyType,
            typeof(List<StringName>),
            "GameRuntimeBattleSelectionState 目标单位队列不应继续使用 Godot Array。"
        );
    }

    private void TestRuntimeCommandLoggerKeepsScopeTypedAndInternal()
    {
        Type loggerType = typeof(GameRuntimeCommandLogger);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(loggerType),
            "GameRuntimeCommandLogger 应是普通 C# helper，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            loggerType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "GameRuntimeCommandLogger 不应继续注册为 Godot GlobalClass。"
        );
        AssertTrue(
            loggerType.GetNestedType("CommandLogScope", BindingFlags.NonPublic) != null,
            "GameRuntimeCommandLogger 应用内部 typed scope 保存 active command 日志状态。"
        );
        AssertTrue(
            typeof(GameRuntimeFacade).GetField(
                "_active_command_log_scope",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ) == null,
            "GameRuntimeFacade 不应继续暴露 active command log Dictionary scope。"
        );
    }

    private void TestRuntimeCommandHandlersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeHelper<GameRuntimePartyCommandHandler>(
            "GameRuntimePartyCommandHandler"
        );
        AssertPlainRuntimeHelper<GameRuntimeWarehouseHandler>(
            "GameRuntimeWarehouseHandler"
        );
        AssertPlainRuntimeHelper<GameRuntimeQuestCommandHandler>(
            "GameRuntimeQuestCommandHandler"
        );
        AssertPlainRuntimeHelper<GameRuntimeRewardFlowHandler>(
            "GameRuntimeRewardFlowHandler"
        );
        AssertPlainRuntimeHelper<WorldMapRuntimeProxy>(
            "WorldMapRuntimeProxy"
        );
    }

    private void TestRuntimeSnapshotBuilderUsesTypedSourceBoundary()
    {
        AssertPlainRuntimeHelper<GameRuntimeSnapshotBuilder>(
            "GameRuntimeSnapshotBuilder"
        );
        AssertTrue(
            typeof(IGameRuntimeSnapshotSource).IsAssignableFrom(typeof(GameRuntimeFacade)),
            "GameRuntimeFacade 应通过 typed snapshot source 接口供 SnapshotBuilder 消费。"
        );
        AssertEq(
            typeof(GameRuntimeSnapshotBuilder)
                .GetMethod("Setup", BindingFlags.Public | BindingFlags.Instance)
                ?.GetParameters()[0]
                .ParameterType,
            typeof(IGameRuntimeSnapshotSource),
            "GameRuntimeSnapshotBuilder.Setup 不应继续绑死具体 GameRuntimeFacade。"
        );
    }

    private void TestWorldGenerationHelpersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeHelper<WorldMapSpawnSystem>(
            "WorldMapSpawnSystem"
        );
        AssertPlainRuntimeHelper<WorldMapDataContext>(
            "WorldMapDataContext"
        );
        AssertPlainRuntimeHelper<WildEncounterGrowthSystem>(
            "WildEncounterGrowthSystem"
        );
        AssertEq(
            typeof(WildEncounterGrowthSystem)
                .GetMethod("ApplyStepAdvance")
                ?.GetParameters()[0]
                .ParameterType,
            typeof(IEnumerable<EncounterAnchorData>),
            "WildEncounterGrowthSystem 成长推进入口不应继续接收 world_data Godot Dictionary。"
        );
        AssertEq(
            typeof(WildEncounterGrowthSystem)
                .GetMethod("ApplyBattleVictory")
                ?.GetParameters()[2]
                .ParameterType,
            typeof(IReadOnlyDictionary<StringName, WildEncounterRosterDef>),
            "WildEncounterGrowthSystem 战后压制入口不应继续接收 roster Godot Dictionary。"
        );
    }

    private void TestWorldPresetHelpersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeType(
            typeof(WorldPresetRegistry),
            "WorldPresetRegistry"
        );
        AssertPlainRuntimeType(
            typeof(WorldPresetRegistry.WorldPresetInfo),
            "WorldPresetInfo"
        );
        AssertEq(
            typeof(WorldPresetRegistry).GetMethod("ListPresetsTyped")?.ReturnType,
            typeof(IReadOnlyList<WorldPresetRegistry.WorldPresetInfo>),
            "WorldPresetRegistry typed 目录读取面不应继续返回 Godot Array。"
        );

        IReadOnlyList<WorldPresetRegistry.WorldPresetInfo> presets =
            WorldPresetRegistry.ListPresetsTyped();
        AssertTrue(presets.Count > 0, "WorldPresetRegistry typed 目录应继续暴露预设列表。");
        AssertTrue(
            WorldPresetRegistry.TryGetPresetTyped("test", out var testPreset),
            "WorldPresetRegistry typed 查询应继续找到 test 预设。"
        );
        AssertEq(
            testPreset?.DisplayName,
            "测试",
            "WorldPresetRegistry typed 查询应保留 test 预设名称。"
        );
        GDictionary projectedTestPreset = WorldPresetRegistry.get_preset("test");
        AssertEq(
            projectedTestPreset["display_name"].AsString(),
            testPreset?.DisplayName,
            "WorldPresetRegistry Dictionary 投影应只反映 typed 预设数据。"
        );
    }

    private void TestHeadlessSnapshotHelpersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeType(
            typeof(GameTextSnapshotRenderer),
            "GameTextSnapshotRenderer"
        );
        AssertTrue(
            typeof(GameTextSnapshotRenderer).GetMethod("render_snapshot") == null,
            "GameTextSnapshotRenderer 不应继续保留无调用方的实例渲染入口。"
        );
    }

    private void TestUtilityHelpersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeType(
            typeof(TrueRandomSeedService),
            "TrueRandomSeedService"
        );
        long seed = TrueRandomSeedService.GenerateSeed();
        AssertTrue(seed > 0, "TrueRandomSeedService.GenerateSeed() 应继续返回正数 seed。");
        int roll = TrueRandomSeedService.RandiRange(3, 1);
        AssertTrue(
            roll >= 1 && roll <= 3,
            "TrueRandomSeedService.RandiRange() 应继续规范化上下限并返回范围内结果。"
        );

        AssertPlainRuntimeType(
            typeof(DisplaySettingsService),
            "DisplaySettingsService"
        );
        AssertPlainRuntimeType(
            typeof(DisplaySettingsService.DisplaySettings),
            "DisplaySettings"
        );
        AssertPlainRuntimeType(
            typeof(DisplaySettingsService.ResolutionOption),
            "ResolutionOption"
        );
        AssertEq(
            typeof(DisplaySettingsService).GetMethod("ListResolutionOptions")?.ReturnType,
            typeof(IReadOnlyList<DisplaySettingsService.ResolutionOption>),
            "DisplaySettingsService 分辨率选项不应继续返回 Godot Array。"
        );
        AssertEq(
            typeof(DisplaySettingsService).GetMethod("SaveSettings")?.GetParameters()[0].ParameterType,
            typeof(DisplaySettingsService.DisplaySettings),
            "DisplaySettingsService 保存入口不应继续接收 Godot Dictionary。"
        );
        AssertTrue(
            typeof(DisplaySettingsService).GetMethod("save_settings") == null,
            "DisplaySettingsService 不应继续暴露给 GDScript 调用的旧 save_settings() 入口。"
        );
    }

    private void TestProgressionContentRuleHelpersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeType(
            typeof(CombatTargetTeamContentRules),
            "CombatTargetTeamContentRules"
        );
        AssertPlainRuntimeType(
            typeof(DamageTagContentRules),
            "DamageTagContentRules"
        );
        AssertPlainRuntimeType(
            typeof(BattleSaveContentRules),
            "BattleSaveContentRules"
        );
        AssertPlainRuntimeType(
            typeof(BattleExecuteContentRules),
            "BattleExecuteContentRules"
        );
        AssertPlainRuntimeType(
            typeof(AttributeGrowthContentRules),
            "AttributeGrowthContentRules"
        );
        AssertPlainRuntimeType(
            typeof(BodySizeContentRules),
            "BodySizeContentRules"
        );
        AssertPlainRuntimeType(
            typeof(QuestProviderContentRules),
            "QuestProviderContentRules"
        );
        AssertPlainRuntimeType(
            typeof(PendingCharacterRewardContentRules),
            "PendingCharacterRewardContentRules"
        );
        AssertPlainRuntimeType(
            typeof(CombatSkillTargetingContentRules),
            "CombatSkillTargetingContentRules"
        );
        AssertPlainRuntimeType(
            typeof(TraitTriggerContentRules),
            "TraitTriggerContentRules"
        );
        AssertPlainRuntimeType(
            typeof(SkillLevelDescriptionContentRules),
            "SkillLevelDescriptionContentRules"
        );
        AssertPlainRuntimeType(
            typeof(ProgressionDataUtils),
            "ProgressionDataUtils"
        );
        AssertEq(
            typeof(ProgressionDataUtils)
                .GetMethod(
                    "sorted_string_keys",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(List<string>),
            "ProgressionDataUtils.sorted_string_keys() 应返回 C# List<string>，不应继续返回 Godot Array。"
        );
        AssertEq(
            typeof(ProgressionDataUtils)
                .GetMethod(
                    "to_string_name_int_dictionary",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(Dictionary<StringName, int>),
            "ProgressionDataUtils.to_string_name_int_dictionary() 应返回 C# Dictionary<StringName,int>。"
        );
        AssertEq(
            typeof(SkillLevelDescriptionContentRules)
                .GetMethod(nameof(SkillLevelDescriptionContentRules.CollectValidationErrors))
                ?.ReturnType,
            typeof(List<string>),
            "SkillLevelDescriptionContentRules 校验核心应返回 C# List<string>。"
        );
        AssertTrue(
            typeof(SkillLevelDescriptionContentRules).GetMethod("append_validation_errors") == null,
            "SkillLevelDescriptionContentRules 不应保留 Godot Array 追加式校验入口。"
        );
        AssertPlainRuntimeType(
            typeof(SkillLevelDescriptionFormatter),
            "SkillLevelDescriptionFormatter"
        );
        AssertEq(
            typeof(DamageTagContentRules)
                .GetField("VALID_DAMAGE_TAGS", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "DamageTagContentRules 不应继续用 Godot Dictionary 保存合法 damage tag 集合。"
        );
        AssertEq(
            typeof(BattleSaveContentRules)
                .GetField("VALID_SAVE_TAGS", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "BattleSaveContentRules 不应继续用 Godot Dictionary 保存合法 save tag 集合。"
        );
        AssertEq(
            typeof(BattleExecuteContentRules)
                .GetField("REQUIRED_PARAM_TYPES", BindingFlags.Public | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlyDictionary<string, Type>),
            "BattleExecuteContentRules required params 不应继续用 Godot Dictionary / Variant.Type 描述。"
        );
        AssertTrue(
            CombatTargetTeamContentRules.is_valid_skill_target_team_filter("enemy"),
            "CombatTargetTeamContentRules 应继续接受 enemy target team。"
        );
        AssertTrue(
            DamageTagContentRules.is_valid_damage_tag("negative_energy"),
            "DamageTagContentRules 应继续接受 negative_energy damage tag。"
        );
        AssertTrue(
            BattleSaveContentRules.is_valid_save_tag(BattleSaveContentRules.SAVE_TAG_EXECUTE),
            "BattleSaveContentRules 应继续接受 execute save tag。"
        );
        AssertEq(
            BattleExecuteContentRules.REQUIRED_PARAM_TYPES[
                BattleExecuteContentRules.PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT()
            ],
            typeof(int),
            "BattleExecuteContentRules execute 阈值参数应继续声明为 int。"
        );
        AssertEq(
            typeof(AttributeGrowthContentRules)
                .GetField("ValidGrowthTiers", BindingFlags.Public | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "AttributeGrowthContentRules growth tier budget 不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(BodySizeContentRules)
                .GetField("ValidBodySizeCategories", BindingFlags.Public | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlySet<StringName>),
            "BodySizeContentRules body size category 集合不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(BodySizeContentRules)
                .GetField("CategoryToBodySize", BindingFlags.Public | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "BodySizeContentRules category -> size 映射不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(BodySizeContentRules)
                .GetField("BodySizeToFootprint", BindingFlags.Public | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlyDictionary<int, Vector2I>),
            "BodySizeContentRules footprint 映射不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(QuestProviderContentRules)
                .GetField("SUPPORTED_PROVIDER_IDS", BindingFlags.Public | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlySet<StringName>),
            "QuestProviderContentRules provider 白名单不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(PendingCharacterRewardContentRules)
                .GetField("SUPPORTED_ENTRY_TYPES", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "PendingCharacterRewardContentRules entry type 集合不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(CombatSkillTargetingContentRules)
                .GetField("VALID_AREA_PATTERNS", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "CombatSkillTargetingContentRules area pattern 集合不应继续用 Godot Dictionary。"
        );
        AssertEq(
            typeof(TraitTriggerContentRules)
                .GetField("DISPATCH_TRIGGER_TYPES", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(IReadOnlyDictionary<StringName, IReadOnlyDictionary<StringName, string>>),
            "TraitTriggerContentRules dispatch 表不应继续用嵌套 Godot Dictionary。"
        );
        AssertEq(
            typeof(SkillLevelDescriptionFormatter)
                .GetMethod(
                    "_collect_level_effect_defs",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(List<CombatEffectDef>),
            "SkillLevelDescriptionFormatter 内部 effect 收集不应继续返回 Godot Array。"
        );
        AssertEq(
            typeof(SkillLevelDescriptionFormatter)
                .GetMethod(
                    "RenderTemplate",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.GetParameters()[1]
                .ParameterType,
            typeof(Dictionary<string, Variant>),
            "SkillLevelDescriptionFormatter 内部模板渲染不应继续消费 Godot Dictionary。"
        );
        AssertEq(
            AttributeGrowthContentRules.get_tier_budget("basic"),
            60,
            "AttributeGrowthContentRules 应继续返回 basic 成长预算。"
        );
        AssertEq(
            BodySizeContentRules.GetBodySizeForCategory("large"),
            3,
            "BodySizeContentRules 应继续把 large 映射到 body_size=3。"
        );
        AssertEq(
            BodySizeContentRules.GetFootprintForCategory("gargantuan"),
            new Vector2I(3, 3),
            "BodySizeContentRules 应继续把 gargantuan 映射到 3x3 footprint。"
        );
        AssertTrue(
            QuestProviderContentRules.is_supported_provider_id("service_contract_board"),
            "QuestProviderContentRules 应继续接受任务板 provider。"
        );
        AssertTrue(
            PendingCharacterRewardContentRules.requires_skill_target(
                PendingCharacterRewardContentRules.ENTRY_SKILL_MASTERY
            ),
            "PendingCharacterRewardContentRules 应继续识别需要 skill target 的奖励条目。"
        );
        AssertTrue(
            CombatSkillTargetingContentRules.is_valid_area_pattern("cone"),
            "CombatSkillTargetingContentRules 应继续接受 cone area pattern。"
        );
        AssertEq(
            TraitTriggerContentRules.get_dispatch_method_name(
                TraitTriggerContentRules.TRAIT_HALFLING_LUCK(),
                TraitTriggerContentRules.TRIGGER_ON_NATURAL_ONE()
            ),
            "_handle_halfling_luck",
            "TraitTriggerContentRules 应继续解析 halfling luck dispatch 方法。"
        );
        AssertEq(
            string.Join(
                ",",
                ProgressionDataUtils.sorted_string_keys(
                    new GDictionary { ["beta"] = 2, ["alpha"] = 1 }
                )
            ),
            "alpha,beta",
            "ProgressionDataUtils.sorted_string_keys() 应继续按 ordinal 文本顺序返回 key。"
        );
        AssertEq(
            ProgressionDataUtils.sorted_string_keys(null).Count,
            0,
            "ProgressionDataUtils.sorted_string_keys(null) 应返回空 typed list。"
        );
        Dictionary<StringName, int> normalizedIntMap =
            ProgressionDataUtils.to_string_name_int_dictionary(
                new GDictionary { ["alpha"] = 2, [new StringName("beta")] = 3 }
            );
        AssertEq(
            normalizedIntMap[new StringName("alpha")],
            2,
            "ProgressionDataUtils.to_string_name_int_dictionary() 应规范化 string key。"
        );
        AssertEq(
            normalizedIntMap[new StringName("beta")],
            3,
            "ProgressionDataUtils.to_string_name_int_dictionary() 应保留 StringName key。"
        );

        List<string> levelDescriptionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                "level_description_test",
                new SkillDef
                {
                    max_level = 1,
                    level_description_template = "等级{value}",
                    level_description_configs = new GDictionary
                    {
                        ["0"] = new GDictionary { ["value"] = "0" },
                        ["2"] = "旧格式",
                    },
                }
            );
        AssertTrue(
            HasErrorFragment(
                levelDescriptionErrors,
                "level_description_configs[2] must be a Dictionary"
            ),
            "SkillLevelDescriptionContentRules 应继续拒绝旧字符串等级描述配置。"
        );
        AssertTrue(
            HasErrorFragment(
                levelDescriptionErrors,
                "level_description_configs[2] must be <= max_level 1"
            ),
            "SkillLevelDescriptionContentRules 应继续拒绝超过 max_level 的等级描述配置。"
        );
        AssertTrue(
            HasErrorFragment(
                levelDescriptionErrors,
                "level_description_configs must include level 1"
            ),
            "SkillLevelDescriptionContentRules 应继续拒绝等级描述断档。"
        );
    }

    private void TestProgressionRuleHelpersNoLongerRequireGodotRegistration()
    {
        AssertPlainRuntimeType(
            typeof(SkillEffectiveMaxLevelRules),
            "SkillEffectiveMaxLevelRules"
        );
        AssertPlainRuntimeType(
            typeof(AgeStageResolver),
            "AgeStageResolver"
        );
        AssertPlainRuntimeType(
            typeof(LevelGrowthEvaluationService),
            "LevelGrowthEvaluationService"
        );
        AssertPlainRuntimeType(
            typeof(PracticeGrowthService),
            "PracticeGrowthService"
        );
        AssertPlainRuntimeType(
            typeof(SkillMergeService),
            "SkillMergeService"
        );
        AssertEq(
            typeof(SkillEffectiveMaxLevelRules)
                .GetMethod("get_effective_max_level")
                ?.ReturnType,
            typeof(int),
            "SkillEffectiveMaxLevelRules effective max level 查询应继续返回 int。"
        );
        AssertEq(
            typeof(SkillEffectiveMaxLevelRules)
                .GetMethod("is_at_effective_max_level")
                ?.ReturnType,
            typeof(bool),
            "SkillEffectiveMaxLevelRules max level 判定应继续返回 bool。"
        );
        AssertEq(
            typeof(AgeStageResolver)
                .GetMethod(nameof(AgeStageResolver.resolve_effective_stage))
                ?.GetParameters()[2]
                .ParameterType,
            typeof(IEnumerable<StageAdvancementModifier>),
            "AgeStageResolver 有效阶段解析入口不应继续接收 Godot Array。"
        );
        AssertEq(
            typeof(CharacterManagementModule)
                .GetMethod(
                    "_collect_active_stage_advancement_modifiers",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )
                ?.ReturnType,
            typeof(List<StageAdvancementModifier>),
            "CharacterManagementModule 收集 active stage advancement 时不应继续用 Godot Array 作为中间状态。"
        );
        AssertEq(
            typeof(LevelGrowthEvaluationService)
                .GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "LevelGrowthEvaluationService 技能定义缓存不应继续使用 Godot Dictionary。"
        );
        AssertEq(
            typeof(PracticeGrowthService)
                .GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "PracticeGrowthService 技能定义缓存不应继续使用 Godot Dictionary。"
        );
        AssertEq(
            typeof(PracticeGrowthService)
                .GetField("PracticeTracks", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "PracticeGrowthService 功法轨道集合不应继续使用 Godot Array。"
        );
        AssertEq(
            typeof(SkillMergeService)
                .GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "SkillMergeService 技能定义缓存不应继续使用 Godot Dictionary。"
        );
        AssertEq(
            typeof(SkillMergeService)
                .GetMethod(
                    "NormalizeSourceSkillIds",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(List<StringName>),
            "SkillMergeService 来源技能规范化不应继续返回 Godot Array。"
        );
    }

    private void TestGridFootprintStateUsesPublicBehavior()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.setup(new Vector2I(2, 2), new Vector2I(4, 4));

        AssertFalse(
            gridSystem.register_footprint("", new Vector2I(1, 1), Vector2I.One),
            "空 entity_id 不应注册 footprint。"
        );
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "", "空 entity_id 注册失败后不应占格。");

        AssertTrue(
            gridSystem.register_footprint("camp", new Vector2I(1, 1), new Vector2I(2, 2)),
            "合法 footprint 应可注册。"
        );
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "camp", "注册后 origin 应暴露占位根。");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(2, 2)), "camp", "注册后 footprint 覆盖格应暴露占位根。");

        AssertFalse(
            gridSystem.can_place_footprint(new Vector2I(2, 2), Vector2I.One),
            "已有 footprint 的格子不应允许再次占用。"
        );
        AssertFalse(
            gridSystem.register_footprint("camp", new Vector2I(7, 7), new Vector2I(2, 2)),
            "同一 entity 移动到越界 footprint 应失败。"
        );
        AssertEq(
            gridSystem.get_occupant_root(new Vector2I(1, 1)),
            "camp",
            "同一 entity 移动失败后应恢复原 footprint。"
        );

        gridSystem.clear_footprint("camp");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "", "清理 footprint 后 origin 不应继续占格。");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(2, 2)), "", "清理 footprint 后覆盖格不应继续占格。");
    }

    private void TestGridCellSurfaceKeepsMinimalRuntimeContract()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.setup(new Vector2I(2, 2), new Vector2I(4, 4));
        gridSystem.register_footprint("camp", new Vector2I(5, 6), Vector2I.One);

        WorldMapCellData cell = gridSystem.get_cell(new Vector2I(5, 6));
        AssertTrue(cell != null, "世界地图格子读取面应继续返回有效格子对象。");
        AssertEq(cell.coord, new Vector2I(5, 6), "格子读取面应继续暴露正式坐标。");
        AssertEq(cell.chunk_coord, new Vector2I(1, 1), "格子读取面应继续暴露区块坐标。");
        AssertEq(cell.occupant_id, "camp", "格子读取面应继续暴露占用者 id。");
        AssertEq(cell.footprint_root_id, "camp", "格子读取面应继续暴露占位根 id。");
        AssertFalse(
            typeof(WorldMapCellData).GetMember(
                "terrain_visual_type",
                BindingFlags.Public | BindingFlags.Instance
            ).Length > 0,
            "WorldMapCellData 不应继续暴露未消费的 terrain_visual_type 字段。"
        );
        AssertFalse(
            typeof(WorldMapGridSystem).GetMethod(
                "get_cells_in_rect",
                BindingFlags.Public | BindingFlags.Instance
            ) != null,
            "WorldMapGridSystem 不应继续保留无调用方的 get_cells_in_rect()。"
        );
    }

    private void TestVisibilityRebuildIgnoresForeignFactionSources()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.setup(new Vector2I(8, 8));

        var playerSource = new VisionSourceData("scout", new Vector2I(2, 2), 1, "player");
        var hostileSource = new VisionSourceData("raider", new Vector2I(5, 5), 1, "hostile");

        fogSystem.RebuildVisibilityForFaction("player", new[] { playerSource, hostileSource });

        AssertTrue(
            fogSystem.is_visible(new Vector2I(2, 2), "player"),
            "玩家阵营的自有视野源应继续正常生效。"
        );
        AssertFalse(
            fogSystem.is_visible(new Vector2I(5, 5), "player"),
            "foreign faction 的视野源不应污染当前阵营可见区。"
        );
    }

    private void TestFogRevealExportLoadKeepsRevealedCells()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.setup(new Vector2I(8, 8));

        List<Vector2I> revealedCoords = fogSystem.RevealDiamond(
            new Vector2I(3, 3),
            1,
            "player"
        );
        AssertTrue(revealedCoords.Contains(new Vector2I(3, 3)), "迷雾揭示应返回中心格。");

        GDictionary persistedState = fogSystem.export_persistent_state();
        var restoredFogSystem = new WorldMapFogSystem();
        restoredFogSystem.setup(new Vector2I(8, 8), persistedState);

        AssertTrue(
            restoredFogSystem.is_explored(new Vector2I(3, 3), "player"),
            "持久化恢复后 paid reveal 中心格应保持已探索。"
        );
        AssertFalse(
            restoredFogSystem.is_visible(new Vector2I(3, 3), "player"),
            "持久化恢复不应把 paid reveal 误当作当前可见。"
        );

        var distantSource = new VisionSourceData("scout", new Vector2I(7, 7), 0, "player");
        restoredFogSystem.RebuildVisibilityForFaction("player", new[] { distantSource });
        AssertTrue(
            restoredFogSystem.is_explored(new Vector2I(3, 3), "player"),
            "后续可见性刷新不应清除已持久化的 paid reveal。"
        );
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertPlainRuntimeHelper<T>(string typeName)
    {
        AssertPlainRuntimeType(typeof(T), typeName);
    }

    private void AssertPlainRuntimeType(Type targetType, string typeName)
    {
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(targetType),
            $"{typeName} 应是普通 C# runtime helper，不应继续继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            targetType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            $"{typeName} 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private static bool HasErrorFragment(IEnumerable<string> errors, string fragment)
    {
        foreach (string error in errors)
        {
            if (error.Contains(fragment, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
