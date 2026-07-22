using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_shared_content_injection_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private const string SmallWorldConfig = "res://data/configs/world_map/small_world_map_config.tres";
    private const string MediumWorldConfig = "res://data/configs/world_map/medium_world_map_config.tres";
    private const string DemoWorldConfig = "res://data/configs/world_map/demo_world_map_config.tres";
    private const string SharedSettlementBundlePath =
        "res://data/configs/world_map/shared/main_world_default_settlement_bundle.tres";
    private const string SharedSettlementNamePoolPath =
        "res://data/configs/world_map/shared/main_world_settlement_name_pool.tres";
    private const string SharedTownNamePoolPath =
        "res://data/configs/world_map/shared/main_world_town_name_pool.tres";
    private const string SharedCityNamePoolPath =
        "res://data/configs/world_map/shared/main_world_city_name_pool.tres";
    private const string SharedCapitalNamePoolPath =
        "res://data/configs/world_map/shared/main_world_capital_name_pool.tres";
    private const string SharedMetropolisNamePoolPath =
        "res://data/configs/world_map/shared/main_world_metropolis_name_pool.tres";

    private readonly TestHarness _test = new();
    private readonly List<GodotProjectionLease<GDictionary>> _worldDataLeases = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestGenericMainWorldPresetsKeepTemplateShape();
        TestSharedSettlementBundleUsesGenericTemplateNames();
        TestSharedSettlementNamePoolExposes1000UniqueNames();
        TestSharedTownNamePoolExposes500UniqueNames();
        TestSharedCityNamePoolExposes300UniqueNames();
        TestSharedCapitalNamePoolExposes100UniqueNames();
        TestSharedMetropolisNamePoolExposes50UniqueNames();
        TestNewWorldGenerationRecordsRuntimeMapSeed();
        TestFailedWorldSwitchRestoresGenerationBinding();
        TestWorldGenerationInjectsSharedMainWorldContent();
        TestWorldStrongholdInstancesKeepStrongholdSemantics();
        TestDemoWorldGenerationIncludesMetropolisInstances();
        TestProceduralWildSpawnDensityCanBeConfigured();
        TestProceduralWildSpawnRegionTagsIgnoreRuleOrder();
        TestSmallWorldGenerationAssignsUniqueDisplayNames();

        DisposeWorldDataLeases();
        RequestTestExit(_test.Finish("World map shared content injection regression"));
    }

    private GDictionary ProjectWorldData(GameSession session)
    {
        GodotProjectionLease<GDictionary> lease = session.GetWorldDataLease();
        _worldDataLeases.Add(lease);
        return lease.Value;
    }

    private void DisposeWorldDataLeases()
    {
        for (int index = _worldDataLeases.Count - 1; index >= 0; index--)
            _worldDataLeases[index].Dispose();
        _worldDataLeases.Clear();
    }

    private void TestGenericMainWorldPresetsKeepTemplateShape()
    {
        foreach (
            string configPath in new[]
            {
                TestWorldConfig,
                SmallWorldConfig,
                MediumWorldConfig,
                DemoWorldConfig,
            }
        )
        {
            WorldGenerationDefinition definition = GetProcessWorldDefinition(configPath);
            _test.True(definition != null, $"主世界预设 {configPath} 应能正常投影。");
            if (definition == null)
            {
                continue;
            }

            _test.True(
                definition.InjectDefaultMainWorldContent,
                $"{configPath} 应开启共享主世界内容注入。"
            );
            _test.Eq(
                definition.ProceduralWildSpawnChunkChanceDenominator,
                2,
                $"{configPath} 应使用更密集的主世界野怪 chunk 抽签配置。"
            );
            _test.True(
                definition.GuaranteeStartingWildEncounter,
                $"{configPath} 应保底在起始区域附近生成野外遭遇。"
            );
            _test.Eq(definition.SettlementLibrary.Count, 0, $"{configPath} 不应再内嵌通用据点模板。");
            _test.Eq(definition.FacilityLibrary.Count, 0, $"{configPath} 不应再内嵌通用设施模板。");
            _test.Eq(
                definition.WildMonsterDistribution.Count,
                0,
                $"{configPath} 不应再内嵌通用野怪规则。"
            );
        }
    }

    private void TestWorldGenerationInjectsSharedMainWorldContent()
    {
        GameSession gameSession = CreateWorld(
            TestWorldConfig,
            "shared_content_injection",
            "共享内容注入验证",
            "共享内容注入验证世界应能成功创建。"
        );
        if (gameSession == null)
        {
            return;
        }

        try
        {
            GDictionary worldData = ProjectWorldData(gameSession);
            bool foundWorldStronghold = false;
            bool foundMasterReforgeService = false;
            foreach (Variant settlementValue in ArrayValue(worldData, "settlements"))
            {
                if (!TryAsDictionary(settlementValue, out GDictionary settlement))
                {
                    continue;
                }
                if (DictInt(settlement, "tier", -1) == 4)
                {
                    foundWorldStronghold = true;
                }
                foreach (Variant serviceValue in ArrayValue(settlement, "available_services"))
                {
                    if (
                        TryAsDictionary(serviceValue, out GDictionary service)
                        && DictString(service, "interaction_script_id", "")
                            == "service_master_reforge"
                    )
                    {
                        foundMasterReforgeService = true;
                    }
                }
            }

            bool foundNorthWild = false;
            bool foundSouthMistHollow = false;
            foreach (Variant encounterValue in ArrayValue(worldData, "encounter_anchors"))
            {
                EncounterAnchorData encounterAnchor = ReadEncounterAnchor(encounterValue);
                if (encounterAnchor == null)
                {
                    continue;
                }
                if (encounterAnchor.region_tag.ToString() == "north_wilds")
                {
                    foundNorthWild = true;
                }
                if (
                    encounterAnchor.region_tag.ToString() == "south_wilds"
                    && encounterAnchor.encounter_profile_id.ToString() == "mist_hollow"
                )
                {
                    foundSouthMistHollow = true;
                }
            }

            _test.True(foundWorldStronghold, "共享内容注入后应生成世界据点模板。");
            _test.True(foundMasterReforgeService, "共享内容注入后应暴露大师重铸服务。");
            _test.True(foundNorthWild, "共享内容注入后应生成 north_wilds 遭遇。");
            _test.True(
                foundSouthMistHollow,
                "共享内容注入后应生成指向 mist_hollow 的 south_wilds 遭遇。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestNewWorldGenerationRecordsRuntimeMapSeed()
    {
        WorldGenerationDefinition definition = GetProcessWorldDefinition(TestWorldConfig);
        _test.True(definition != null, "runtime map seed 回归需要可投影的测试世界配置。");
        if (definition == null)
        {
            return;
        }

        GameSession gameSession = CreateWorld(
            TestWorldConfig,
            "runtime_map_seed",
            "运行时地图 seed 验证",
            "新世界应能成功创建以验证 runtime map seed。"
        );
        if (gameSession == null)
        {
            return;
        }

        try
        {
            long mapSeed = DictInt64(ProjectWorldData(gameSession), "map_seed", 0L);
            _test.True(mapSeed > 0, "新世界 world_data 应记录由真随机接口分配的 map_seed。");
            _test.True(mapSeed != definition.Seed, "运行时 map_seed 不应直接沿用 world config 的固定 seed。");
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestFailedWorldSwitchRestoresGenerationBinding()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            int createError = gameSession.CreateNewSave(
                TestWorldConfig,
                "generation_rollback_original",
                "生成定义回滚原世界"
            );
            _test.Eq(createError, (int)Error.Ok, "generation definition 回滚测试应先创建原世界。");
            if (createError != (int)Error.Ok)
                return;

            WorldGenerationDefinition originalDefinition =
                gameSession.GetGenerationDefinition();
            string originalSaveId = gameSession.GetActiveSaveId();
            string originalPath = gameSession.GetGenerationConfigPath();

            int failedCreateError = gameSession.CreateNewSave(
                SmallWorldConfig,
                "generation_rollback_candidate",
                "生成定义回滚候选世界",
                new GDictionary { ["bloodline_id"] = "invalid_unpaired_bloodline" }
            );

            _test.Eq(
                failedCreateError,
                (int)Error.InvalidData,
                "非法的单边 bloodline payload 应让候选世界创建失败。"
            );
            _test.Eq(
                gameSession.GetGenerationConfigPath(),
                originalPath,
                "候选世界失败后应恢复原 active generation path。"
            );
            _test.True(
                ReferenceEquals(gameSession.GetGenerationDefinition(), originalDefinition),
                "候选世界失败后应恢复原 borrowed generation definition。"
            );
            _test.Eq(
                gameSession.GetActiveSaveId(),
                originalSaveId,
                "候选世界失败后应恢复原 active save id。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestWorldStrongholdInstancesKeepStrongholdSemantics()
    {
        GameSession gameSession = CreateWorld(
            TestWorldConfig,
            "shared_stronghold_names",
            "世界据点命名验证",
            "测试世界应能成功创建以验证世界据点命名。"
        );
        if (gameSession == null)
        {
            return;
        }

        try
        {
            bool foundWorldStronghold = false;
            foreach (Variant settlementValue in ArrayValue(ProjectWorldData(gameSession), "settlements"))
            {
                if (!TryAsDictionary(settlementValue, out GDictionary settlement))
                {
                    continue;
                }
                if (DictInt(settlement, "tier", -1) != 4)
                {
                    continue;
                }
                foundWorldStronghold = true;
                string displayName = DictString(settlement, "display_name", "").Trim();
                _test.True(
                    displayName.StartsWith("世界据点", StringComparison.Ordinal),
                    "世界据点实例应保留 world stronghold 语义，而不是回退到通用村名池。"
                );
                _test.False(
                    displayName.EndsWith("村", StringComparison.Ordinal),
                    "世界据点实例不应使用以村结尾的通用名称。"
                );
            }
            _test.True(foundWorldStronghold, "测试世界应至少生成一个世界据点实例。");
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestDemoWorldGenerationIncludesMetropolisInstances()
    {
        GameSession gameSession = CreateWorld(
            DemoWorldConfig,
            "shared_demo_metropolis",
            "巨型世界都会验证",
            "demo 世界应能成功创建以验证都会生成。"
        );
        if (gameSession == null)
        {
            return;
        }

        try
        {
            bool foundMetropolisInstance = false;
            foreach (Variant settlementValue in ArrayValue(ProjectWorldData(gameSession), "settlements"))
            {
                if (!TryAsDictionary(settlementValue, out GDictionary settlement))
                {
                    continue;
                }
                if (DictInt(settlement, "tier", -1) != 5)
                {
                    continue;
                }
                foundMetropolisInstance = true;
                string displayName = DictString(settlement, "display_name", "").Trim();
                _test.True(
                    displayName.EndsWith("帝都", StringComparison.Ordinal),
                    "demo 世界里的 metropolis 实例应继续使用都会名称池。"
                );
            }
            _test.True(foundMetropolisInstance, "demo 世界应至少生成一个 metropolis 实例。");
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestProceduralWildSpawnRegionTagsIgnoreRuleOrder()
    {
        WorldGenerationDefinition definition = BuildWildSpawnDensityDefinition(
            "res://tests/world_map/runtime/wild_spawn_region_order.tres",
            new[]
            {
                BuildWildSpawnRuleDefinition(
                    "south_wilds",
                    "南境雾兽",
                    "mist_hollow"
                ),
                BuildWildSpawnRuleDefinition(
                    "north_wilds",
                    "北境狼群",
                    "wolf_wilds"
                ),
            }
        );
        if (definition == null)
            return;

        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        WorldMapSpawnSystem spawnSystem = new();
        WorldMapSpawnSystem.WorldBuildData worldBuild = spawnSystem.BuildWorldTyped(
            definition,
            gridSystem
        );

        int midpointChunkY = definition.WorldSizeInChunks.Y / 2;
        bool foundNorthInNorth = false;
        bool foundSouthInSouth = false;
        bool misplacedNorth = false;
        bool misplacedSouth = false;
        foreach (EncounterAnchorData encounterAnchor in worldBuild.EncounterAnchors)
        {
            if (encounterAnchor == null)
            {
                continue;
            }
            if (encounterAnchor.encounter_kind != EncounterAnchorData.ToStringName(EncounterAnchorKind.Single))
            {
                continue;
            }

            Vector2I chunkCoord = gridSystem.GetChunkCoord(encounterAnchor.world_coord);
            if (encounterAnchor.region_tag.ToString() == "north_wilds")
            {
                if (chunkCoord.Y < midpointChunkY)
                {
                    foundNorthInNorth = true;
                }
                else
                {
                    misplacedNorth = true;
                }
            }
            else if (encounterAnchor.region_tag.ToString() == "south_wilds")
            {
                if (chunkCoord.Y >= midpointChunkY)
                {
                    foundSouthInSouth = true;
                }
                else
                {
                    misplacedSouth = true;
                }
            }
        }

        _test.True(foundNorthInNorth, "north_wilds 应仍然出现在世界北半区。");
        _test.True(foundSouthInSouth, "south_wilds 应仍然出现在世界南半区。");
        _test.False(misplacedNorth, "north_wilds 不应因为数组顺序变化而跑到南半区。");
        _test.False(misplacedSouth, "south_wilds 不应因为数组顺序变化而跑到北半区。");
    }

    private void TestProceduralWildSpawnDensityCanBeConfigured()
    {
        WorldGenerationDefinition definition = BuildWildSpawnDensityDefinition(
            "res://tests/world_map/runtime/wild_spawn_density.tres",
            new[]
            {
                BuildWildSpawnRuleDefinition(
                    "north_wilds",
                    "北境狼群",
                    "wolf_wilds"
                ),
                BuildWildSpawnRuleDefinition(
                    "south_wilds",
                    "南境雾兽",
                    "mist_hollow"
                ),
            }
        );
        if (definition == null)
            return;

        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        WorldMapSpawnSystem spawnSystem = new();
        WorldMapSpawnSystem.WorldBuildData worldBuild = spawnSystem.BuildWorldTyped(
            definition,
            gridSystem
        );

        int singleEncounterCount = 0;
        foreach (EncounterAnchorData encounterAnchor in worldBuild.EncounterAnchors)
        {
            if (
                encounterAnchor != null
                && encounterAnchor.encounter_kind == EncounterAnchorData.ToStringName(EncounterAnchorKind.Single)
            )
            {
                singleEncounterCount++;
            }
        }

        int expectedMinimum =
            definition.WorldSizeInChunks.X * definition.WorldSizeInChunks.Y;
        _test.True(
            singleEncounterCount >= expectedMinimum,
            "chunk 抽签分母为 1 时，每个 chunk 至少应生成一组单体野外遭遇。"
                + $"actual={singleEncounterCount} expected_minimum={expectedMinimum}"
        );
    }

    private void TestSmallWorldGenerationAssignsUniqueDisplayNames()
    {
        GameSession gameSession = CreateWorld(
            SmallWorldConfig,
            "shared_name_pool",
            "共享名称池验证",
            "small 世界应能成功创建以验证据点名称池。"
        );
        if (gameSession == null)
        {
            return;
        }

        try
        {
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            var genericPlaceholderNames = new HashSet<string>(
                new[] { "村落", "城镇", "城市", "主城", "世界据点", "都会" },
                StringComparer.Ordinal
            );
            bool foundTownInstance = false;
            bool foundCityInstance = false;
            bool foundCapitalInstance = false;
            bool foundMetropolisInstance = false;
            foreach (Variant settlementValue in ArrayValue(ProjectWorldData(gameSession), "settlements"))
            {
                if (!TryAsDictionary(settlementValue, out GDictionary settlement))
                {
                    continue;
                }

                string displayName = DictString(settlement, "display_name", "").Trim();
                int tier = DictInt(settlement, "tier", -1);
                _test.True(displayName.Length > 0, "small 世界里的据点实例展示名不应为空。");
                if (tier == 4)
                {
                    _test.True(
                        displayName.StartsWith("世界据点", StringComparison.Ordinal),
                        "small 世界里的世界据点实例应保留 stronghold 语义名。"
                    );
                }
                else
                {
                    _test.False(
                        genericPlaceholderNames.Contains(displayName),
                        "small 世界里的据点实例展示名应来自名称池而不是模板占位名。"
                    );
                }
                _test.True(seenNames.Add(displayName), "small 世界里的据点实例展示名不应重复。");

                if (tier > 0)
                {
                    bool hasGuildHall = false;
                    foreach (Variant facilityValue in ArrayValue(settlement, "facilities"))
                    {
                        if (
                            TryAsDictionary(facilityValue, out GDictionary facility)
                            && DictString(facility, "template_id", "") == "guild_hall"
                        )
                        {
                            hasGuildHall = true;
                        }
                    }
                    _test.True(
                        hasGuildHall,
                        $"非村庄据点实例必须生成行会大厅（悬赏板入口）。settlement={displayName} tier={tier}"
                    );
                }

                if (tier == 1)
                {
                    foundTownInstance = true;
                    _test.True(
                        displayName.EndsWith("镇", StringComparison.Ordinal),
                        "small 世界里的 town 实例应使用城镇名称池并以镇结尾。"
                    );
                }
                if (tier == 2)
                {
                    foundCityInstance = true;
                    _test.True(
                        displayName.EndsWith("城", StringComparison.Ordinal),
                        "small 世界里的 city 实例应使用城市名称池并以城结尾。"
                    );
                }
                if (tier == 3)
                {
                    foundCapitalInstance = true;
                    _test.True(
                        displayName.EndsWith("王都", StringComparison.Ordinal),
                        "small 世界里的 capital 实例应使用主城名称池并以王都结尾。"
                    );
                    _test.True(
                        displayName.IndexOf("王国", StringComparison.Ordinal) > 0,
                        "small 世界里的 capital 实例应使用 XX王国...王都 语义。"
                    );
                }
                if (tier == 5)
                {
                    foundMetropolisInstance = true;
                    _test.True(
                        displayName.EndsWith("帝都", StringComparison.Ordinal),
                        "small 世界里的 metropolis 实例应使用都会名称池并以帝都结尾。"
                    );
                    _test.True(
                        displayName.IndexOf("帝国", StringComparison.Ordinal) > 0,
                        "small 世界里的 metropolis 实例应使用 XX帝国...帝都 语义。"
                    );
                }
            }

            _test.True(foundTownInstance, "small 世界里应至少生成一个 town 实例以验证城镇名称池。");
            _test.True(foundCityInstance, "small 世界里应至少生成一个 city 实例以验证城市名称池。");
            _test.True(foundCapitalInstance, "small 世界里应至少生成一个 capital 实例以验证主城名称池。");
            _test.True(foundMetropolisInstance, "small 世界里应至少生成一个 metropolis 实例以验证都会名称池。");
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestSharedSettlementBundleUsesGenericTemplateNames()
    {
        WorldMapSettlementBundleDefinition settlementBundle =
            GetProcessWorldDefinition(TestWorldConfig).DefaultSettlementBundle;
        _test.True(settlementBundle != null, "共享据点 bundle 应能在 host build 时完成投影。");
        if (settlementBundle == null)
        {
            return;
        }

        bool seenTemplateTown = false;
        foreach (SettlementDefinition settlement in settlementBundle.SettlementLibrary)
        {
            string settlementId = settlement.TemplateId ?? "";
            string displayName = settlement.DisplayName ?? "";
            _test.True(
                settlementId.StartsWith("template_", StringComparison.Ordinal),
                "共享据点模板 ID 应使用 template_* 前缀。"
            );
            _test.True(!string.IsNullOrEmpty(displayName), "共享据点模板应提供展示名。");
            _test.False(displayName == settlementId, "共享据点模板展示名不应回退成模板 ID。");
            if (settlementId == "template_town")
            {
                seenTemplateTown = true;
            }
        }

        _test.True(seenTemplateTown, "共享据点 bundle 应包含抽象化的 town 模板。");
    }

    private void TestSharedSettlementNamePoolExposes1000UniqueNames()
    {
        AssertNamePool(SharedSettlementNamePoolPath, 1000, "", "", "共享据点名称池");
    }

    private void TestSharedTownNamePoolExposes500UniqueNames()
    {
        AssertNamePool(SharedTownNamePoolPath, 500, "镇", "", "共享城镇名称池");
    }

    private void TestSharedCityNamePoolExposes300UniqueNames()
    {
        AssertNamePool(SharedCityNamePoolPath, 300, "城", "", "共享城市名称池");
    }

    private void TestSharedCapitalNamePoolExposes100UniqueNames()
    {
        AssertNamePool(SharedCapitalNamePoolPath, 100, "王都", "王国", "共享主城名称池");
    }

    private void TestSharedMetropolisNamePoolExposes50UniqueNames()
    {
        AssertNamePool(SharedMetropolisNamePoolPath, 50, "帝都", "帝国", "共享都会名称池");
    }

    private void AssertNamePool(
        string path,
        int expectedCount,
        string expectedSuffix,
        string expectedContainedText,
        string label
    )
    {
        WorldGenerationDefinition definition = GetProcessWorldDefinition(TestWorldConfig);
        definition.SettlementNamePools.TryGetValue(
            ContentPathCanonicalizer.Canonicalize(path),
            out WorldMapSettlementNamePoolDefinition namePool
        );
        _test.True(namePool != null, $"{label}应能在 host build 时完成投影。");
        if (namePool == null)
        {
            return;
        }

        IReadOnlyList<string> displayNames = namePool.BuildUniqueDisplayNames();
        _test.Eq(displayNames.Count, expectedCount, $"{label}应提供 {expectedCount} 个唯一名称。");
        if (displayNames.Count == 0)
        {
            return;
        }

        string firstName = displayNames[0].Trim();
        _test.True(firstName.Length > 0, $"{label}首项不应为空。");
        if (!string.IsNullOrEmpty(expectedSuffix))
        {
            _test.True(
                firstName.EndsWith(expectedSuffix, StringComparison.Ordinal),
                $"{label}应使用以{expectedSuffix}结尾的名称。"
            );
        }
        if (!string.IsNullOrEmpty(expectedContainedText))
        {
            _test.True(
                firstName.IndexOf(expectedContainedText, StringComparison.Ordinal) > 0,
                $"{label}应使用 XX{expectedContainedText}...{expectedSuffix} 语义。"
            );
        }
    }

    private WorldGenerationDefinition BuildWildSpawnDensityDefinition(
        string canonicalPath,
        IReadOnlyList<WildSpawnRuleDefinition> wildSpawnRules
    )
    {
        WorldGenerationDefinition source = GetProcessWorldDefinition(TestWorldConfig);
        _test.True(source != null, "wild spawn 回归需要 process snapshot 中的测试世界 definition。");
        if (source == null)
        {
            return null;
        }
        return new WorldGenerationDefinition(
            ContentPathCanonicalizer.Canonicalize(canonicalPath),
            source.Seed,
            source.WorldSizeInChunks,
            source.ChunkSize,
            source.PlayerStartCoord,
            source.PlayerVisionRange,
            source.ProceduralGenerationEnabled,
            proceduralWildSpawnChunkChanceDenominator: 1,
            injectDefaultMainWorldContent: false,
            source.ProceduralVillageCount,
            source.ProceduralTownCount,
            source.ProceduralCityCount,
            source.ProceduralCapitalCount,
            source.ProceduralWorldStrongholdCount,
            source.ProceduralMetropolisCount,
            source.VillageSpacingCells,
            source.TownSpacingCells,
            source.CitySpacingCells,
            source.CapitalSpacingCells,
            source.WorldStrongholdSpacingCells,
            source.MetropolisSpacingCells,
            guaranteeStartingWildEncounter: false,
            source.StartingWildSpawnMinDistance,
            source.StartingWildSpawnMaxDistance,
            source.EffectiveSettlementLibrary,
            source.EffectiveFacilityLibrary,
            source.SettlementDistribution,
            wildSpawnRules,
            source.MountedSubmaps,
            source.WorldEvents,
            defaultSettlementBundle: null,
            defaultWildSpawnBundle: null,
            new Dictionary<string, WorldMapSettlementNamePoolDefinition>(
                StringComparer.Ordinal
            )
        );
    }

    private static WildSpawnRuleDefinition BuildWildSpawnRuleDefinition(
        string regionTag,
        string monsterName,
        string encounterProfileId
    )
    {
        return new WildSpawnRuleDefinition(
            regionTag,
            monsterName,
            encounterProfileId,
            "",
            "",
            densityPerChunk: 1,
            minDistanceToSettlement: 3,
            visionRange: 1,
            chunkCoords: System.Array.Empty<Vector2I>()
        );
    }

    private GameSession CreateWorld(
        string configPath,
        string saveId,
        string displayName,
        string errorMessage
    )
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(configPath, saveId, displayName);
        _test.Eq(createError, (int)Error.Ok, errorMessage);
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }
        return gameSession;
    }

    private static void CleanupGameSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static WorldGenerationDefinition GetProcessWorldDefinition(string resourcePath)
    {
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        return GameSessionTestFactory
            .GetProcessSnapshot()
            .WorldGenerations.TryGetValue(
                canonicalPath,
                out WorldGenerationDefinition definition
            )
            ? definition
            : null;
    }

    private static GArray ArrayValue(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static bool TryAsDictionary(Variant value, out GDictionary dictionary)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            dictionary = value.AsGodotDictionary();
            return true;
        }
        dictionary = new GDictionary();
        return false;
    }

    private static T AsObject<T>(Variant value)
        where T : GodotObject
    {
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
    }

    private static EncounterAnchorData ReadEncounterAnchor(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary
            ? EncounterAnchorData.FromDictionary(value.AsGodotDictionary())
            : null;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static long DictInt64(GDictionary dictionary, string key, long fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
    }
}
