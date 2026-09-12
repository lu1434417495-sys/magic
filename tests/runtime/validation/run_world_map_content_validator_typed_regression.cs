using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_world_map_content_validator_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        TestOfficialWorldJsonRegistryAndDefinitions();
        TestValidatorRejectsInvalidPlainDefinition();
        TestTypedValidatorRejectsDuplicateTierAndVerticalBand();
        TestStrictWorldJsonDiagnosticsPreserveRuleSourceAndPointer();
        TestRegistryRejectsMissingCrossDomainGenerationId();
        TestRegistryRejectsMissingSharedContentAndMountedCycle();
        RequestTestExit(_test.Finish("World map content validator typed regression"));
    }

    private void TestOfficialWorldJsonRegistryAndDefinitions()
    {
        var registry = new WorldContentRegistry();
        registry.Rebuild();
        _test.Eq(registry.GetValidationErrors().Count, 0, Format(registry.GetValidationErrors()));
        _test.Eq(registry.GetPresets().Count, 5, "world preset JSON 应发布 5 个稳定 ID 预设。");
        _test.Eq(registry.GetGenerations().Count, 6, "world generation JSON 应发布 6 个稳定 ID 定义。");
        _test.True(
            registry.GetPresets().TryGetValue("test", out WorldPresetDefinition testPreset)
                && testPreset.GenerationId == "test",
            "test preset 应只按 generation_id 关联 test generation。"
        );
        _test.True(
            registry.GetGenerations().TryGetValue("ashen_intersection", out WorldGenerationDefinition ashen)
                && ashen.MountedSubmaps.Count == 1
                && ashen.MountedSubmaps[0].WorldGenerationId == "ashen_wastes"
                && ashen.MountedSubmaps[0].Generation.GenerationId == "ashen_wastes",
            "mounted submap 应按稳定 ID 解析 typed child definition。"
        );

        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        var validator = new WorldMapContentValidator();
        var errors = new List<string>();
        foreach ((StringName id, WorldGenerationDefinition definition) in registry.GetGenerations())
        {
            errors.AddRange(
                validator.ValidateGenerationConfigTyped(
                    definition,
                    id.ToString(),
                    snapshot.BattleEncounters.Keys
                )
            );
        }
        _test.Eq(errors.Count, 0, $"正式 world JSON Definition 不应报错: {Format(errors)}");
    }

    private void TestValidatorRejectsInvalidPlainDefinition()
    {
        WorldGenerationDefinition invalid = TestWorldGenerationDefinitionFactory.Create(
            "invalid_world",
            worldSizeInChunks: Vector2I.Zero,
            sharedContentId: "main_world_defaults"
        );
        var validator = new WorldMapContentValidator();
        List<string> errors = validator.ValidateGenerationConfigTyped(
            invalid,
            "invalid_world",
            Array.Empty<StringName>()
        );
        _test.True(
            errors.Any(error => error.Contains("world_size_in_chunks", StringComparison.Ordinal)),
            "plain Definition validator 应拒绝非法世界尺寸。"
        );
        _test.True(
            errors.Any(error => error.Contains("name pool", StringComparison.Ordinal)),
            "声明 shared_content_id 后缺失必需 name pool 应 fail closed。"
        );
    }

    private void TestRegistryRejectsMissingCrossDomainGenerationId()
    {
        string presets = FileAccess.GetFileAsString(WorldJsonDomains.PresetDirectoryPath + "/core.json")
            .Replace("\"generation_id\": \"test\"", "\"generation_id\": \"missing_generation\"");
        var reader = new DirectoryReader(
            presets,
            FileAccess.GetFileAsString(WorldJsonDomains.GenerationDirectoryPath + "/core.json"),
            FileAccess.GetFileAsString(WorldJsonDomains.SharedDirectoryPath + "/core.json")
        );
        var registry = new WorldContentRegistry(reader);
        registry.Rebuild();
        _test.True(
            registry.GetValidationErrors().Any(error =>
                error.Contains("missing generation_id 'missing_generation'", StringComparison.Ordinal)
            ),
            "preset 引用缺失 generation_id 时 registry 应在 snapshot 发布前拒绝。"
        );
    }

    private void TestTypedValidatorRejectsDuplicateTierAndVerticalBand()
    {
        WorldGenerationDefinition source = TestWorldGenerationDefinitionFactory.Load("test");
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        var validator = new WorldMapContentValidator();

        var duplicateTierPools =
            new Dictionary<SettlementTierKind, WorldMapSettlementNamePoolDefinition>(
                source.SettlementNamePools
            );
        WorldMapSettlementNamePoolDefinition townPool =
            duplicateTierPools[SettlementTierKind.Town];
        duplicateTierPools[SettlementTierKind.Town] =
            new WorldMapSettlementNamePoolDefinition(
                SettlementTierKind.Village,
                townPool.DisplayNames
            );
        WorldGenerationDefinition duplicateTier = CloneWithRulesAndNamePools(
            source,
            source.WildMonsterDistribution,
            source.DefaultWildSpawnBundle,
            duplicateTierPools
        );
        List<string> duplicateTierErrors = validator.ValidateGenerationConfigTyped(
            duplicateTier,
            "duplicate_tier",
            snapshot.BattleEncounters.Keys
        );
        _test.True(
            duplicateTierErrors.Any(error =>
                error.Contains(
                    "duplicate settlement name pool tier Village",
                    StringComparison.Ordinal
                )
            ),
            $"typed validator 应拒绝重复 settlement tier: {Format(duplicateTierErrors)}"
        );

        IReadOnlyList<WildSpawnRuleDefinition> sourceRules =
            source.DefaultWildSpawnBundle.WildMonsterDistribution;
        var duplicateBandRules = new[]
        {
            sourceRules[0],
            CloneWithVerticalBand(sourceRules[1], sourceRules[0].VerticalBand),
        };
        WorldGenerationDefinition duplicateBand = CloneWithRulesAndNamePools(
            source,
            duplicateBandRules,
            defaultWildSpawnBundle: null,
            namePools: source.SettlementNamePools
        );
        List<string> duplicateBandErrors = validator.ValidateGenerationConfigTyped(
            duplicateBand,
            "duplicate_band",
            snapshot.BattleEncounters.Keys
        );
        _test.True(
            duplicateBandErrors.Any(error =>
                error.Contains("duplicate wild spawn vertical_band North", StringComparison.Ordinal)
            ),
            $"typed validator 应拒绝重复 vertical band: {Format(duplicateBandErrors)}"
        );
    }

    private void TestStrictWorldJsonDiagnosticsPreserveRuleSourceAndPointer()
    {
        string generations = ReadGenerations();
        string unknownMemberGenerations = generations.Replace(
            "\"generation_id\": \"test\",",
            "\"generation_id\": \"test\",\n      \"legacy_generation_path\": \"res://old_world.tres\",",
            StringComparison.Ordinal
        );
        ContentImportBatch<WorldGenerationImportModel> unknownMemberBatch =
            WorldJsonImport.CreateGenerationDescriptor(
                WorldJsonDomains.GenerationDirectoryPath,
                new DirectoryReader(ReadPresets(), unknownMemberGenerations, ReadShared())
            ).Import();
        _test.True(
            unknownMemberBatch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == WorldJsonRules.InvalidGenerationDto
                && diagnostic.SourceLabel == "core.json#test"
                && diagnostic.JsonPointer == "/entries/0/legacy_generation_path"
            ),
            "world generation 未知字段应以稳定 rule/source/pointer fail closed。"
        );

        string invalidRangeGenerations = generations.Replace(
            "\"procedural_wild_spawn_chunk_chance_denominator\": 2",
            "\"procedural_wild_spawn_chunk_chance_denominator\": 0",
            StringComparison.Ordinal
        );
        ContentImportBatch<WorldGenerationImportModel> invalidRangeBatch =
            WorldJsonImport.CreateGenerationDescriptor(
                WorldJsonDomains.GenerationDirectoryPath,
                new DirectoryReader(ReadPresets(), invalidRangeGenerations, ReadShared())
            ).Import();
        _test.True(
            invalidRangeBatch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == WorldJsonRules.ValueOutOfRange
                && diagnostic.SourceLabel == "core.json#test"
                && diagnostic.JsonPointer
                    == "/entries/0/procedural_wild_spawn_chunk_chance_denominator"
            ),
            "world generation 非法数值应以稳定 rule/source/pointer fail closed。"
        );

        string unknownTierShared = ReadShared().Replace(
            "\"settlement_tier\": \"village\"",
            "\"settlement_tier\": \"future_tier\"",
            StringComparison.Ordinal
        );
        ContentImportBatch<WorldSharedImportModel> unknownTierBatch =
            WorldJsonImport.CreateSharedDescriptor(
                WorldJsonDomains.SharedDirectoryPath,
                new DirectoryReader(ReadPresets(), generations, unknownTierShared)
            ).Import();
        _test.True(
            unknownTierBatch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == WorldJsonRules.UnknownValue
                && diagnostic.SourceLabel == "core.json#main_world_defaults"
                && diagnostic.JsonPointer
                    == "/entries/0/settlement_name_pools/0/settlement_tier"
            ),
            "world shared 未注册 settlement tier 应以稳定 rule/source/pointer fail closed。"
        );

        string duplicateTierShared = ReadShared().Replace(
            "\"settlement_tier\": \"town\"",
            "\"settlement_tier\": \"village\"",
            StringComparison.Ordinal
        );
        ContentImportBatch<WorldSharedImportModel> duplicateTierBatch =
            WorldJsonImport.CreateSharedDescriptor(
                WorldJsonDomains.SharedDirectoryPath,
                new DirectoryReader(ReadPresets(), generations, duplicateTierShared)
            ).Import();
        _test.True(
            duplicateTierBatch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == WorldJsonRules.DuplicateTypedKey
                && diagnostic.SourceLabel == "core.json#main_world_defaults"
                && diagnostic.JsonPointer
                    == "/entries/0/settlement_name_pools/1/settlement_tier"
            ),
            "world shared 重复 settlement tier 应定位到重复项字段并 fail closed。"
        );

        string duplicateBandShared = ReadShared().Replace(
            "\"vertical_band\": \"south\"",
            "\"vertical_band\": \"north\"",
            StringComparison.Ordinal
        );
        ContentImportBatch<WorldSharedImportModel> duplicateBandBatch =
            WorldJsonImport.CreateSharedDescriptor(
                WorldJsonDomains.SharedDirectoryPath,
                new DirectoryReader(ReadPresets(), generations, duplicateBandShared)
            ).Import();
        _test.True(
            duplicateBandBatch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == WorldJsonRules.DuplicateTypedKey
                && diagnostic.SourceLabel == "core.json#main_world_defaults"
                && diagnostic.JsonPointer
                    == "/entries/0/wild_monster_distribution/1/vertical_band"
            ),
            "world shared 重复 vertical band 应定位到重复项字段并 fail closed。"
        );
    }

    private void TestRegistryRejectsMissingSharedContentAndMountedCycle()
    {
        string generations = ReadGenerations();
        var missingSharedRegistry = new WorldContentRegistry(
            new DirectoryReader(
                ReadPresets(),
                generations.Replace(
                    "\"shared_content_id\": \"main_world_defaults\"",
                    "\"shared_content_id\": \"missing_shared\"",
                    StringComparison.Ordinal
                ),
                ReadShared()
            )
        );
        missingSharedRegistry.Rebuild();
        _test.True(
            missingSharedRegistry.GetValidationErrors().Any(error =>
                error.Contains("missing shared_content_id 'missing_shared'", StringComparison.Ordinal)
            ),
            "generation 引用缺失 shared_content_id 时 registry 应拒绝发布。"
        );

        string cyclicGenerations = generations.Replace(
            "\"generation_id\": \"ashen_wastes\",\n          \"return_hint_text\"",
            "\"generation_id\": \"ashen_intersection\",\n          \"return_hint_text\"",
            StringComparison.Ordinal
        );
        var cycleRegistry = new WorldContentRegistry(
            new DirectoryReader(ReadPresets(), cyclicGenerations, ReadShared())
        );
        cycleRegistry.Rebuild();
        _test.True(
            cycleRegistry.GetValidationErrors().Any(error =>
                error.Contains("generation cycle", StringComparison.Ordinal)
                && error.Contains(
                    "ashen_intersection -> ashen_intersection",
                    StringComparison.Ordinal
                )
            ),
            "mounted generation cycle 应在 snapshot 发布前拒绝。"
        );
    }

    private static string ReadPresets() =>
        FileAccess.GetFileAsString(WorldJsonDomains.PresetDirectoryPath + "/core.json");

    private static string ReadGenerations() =>
        FileAccess.GetFileAsString(WorldJsonDomains.GenerationDirectoryPath + "/core.json");

    private static string ReadShared() =>
        FileAccess.GetFileAsString(WorldJsonDomains.SharedDirectoryPath + "/core.json");

    private static WorldGenerationDefinition CloneWithRulesAndNamePools(
        WorldGenerationDefinition source,
        IReadOnlyList<WildSpawnRuleDefinition> wildSpawnRules,
        WorldMapWildSpawnBundleDefinition defaultWildSpawnBundle,
        IReadOnlyDictionary<SettlementTierKind, WorldMapSettlementNamePoolDefinition> namePools
    ) =>
        new(
            source.GenerationId,
            source.Seed,
            source.WorldSizeInChunks,
            source.ChunkSize,
            source.PlayerStartCoord,
            source.PlayerVisionRange,
            source.ProceduralGenerationEnabled,
            source.ProceduralWildSpawnChunkChanceDenominator,
            source.SharedContentId,
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
            source.GuaranteeStartingWildEncounter,
            source.StartingWildSpawnMinDistance,
            source.StartingWildSpawnMaxDistance,
            source.SettlementLibrary,
            source.FacilityLibrary,
            source.SettlementDistribution,
            wildSpawnRules,
            source.MountedSubmaps,
            source.WorldEvents,
            source.DefaultSettlementBundle,
            defaultWildSpawnBundle,
            namePools
        );

    private static WildSpawnRuleDefinition CloneWithVerticalBand(
        WildSpawnRuleDefinition source,
        WorldVerticalBandKind verticalBand
    ) =>
        new(
            source.RegionTag,
            verticalBand,
            source.MonsterName,
            source.EncounterProfileId,
            source.SettlementEncounterProfileId,
            source.SettlementEncounterDisplayName,
            source.DensityPerChunk,
            source.MinDistanceToSettlement,
            source.VisionRange,
            source.ChunkCoords
        );

    private static string Format(IEnumerable<string> errors) =>
        string.Join(" | ", errors ?? Array.Empty<string>());

    private sealed class DirectoryReader : IContentJsonSourceReader
    {
        private readonly string _presets;
        private readonly string _generations;
        private readonly string _shared;

        internal DirectoryReader(string presets, string generations, string shared)
        {
            _presets = presets;
            _generations = generations;
            _shared = shared;
        }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath)
        {
            string json = directoryPath switch
            {
                WorldJsonDomains.PresetDirectoryPath => _presets,
                WorldJsonDomains.GenerationDirectoryPath => _generations,
                WorldJsonDomains.SharedDirectoryPath => _shared,
                _ => "",
            };
            return new[] { new ContentJsonSourceText(directoryPath + "/core.json", json) };
        }
    }
}
