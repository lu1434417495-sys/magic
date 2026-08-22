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

        string unknownPoolShared = ReadShared().Replace(
            "\"pool_id\": \"village\"",
            "\"pool_id\": \"future_pool\"",
            StringComparison.Ordinal
        );
        ContentImportBatch<WorldSharedImportModel> unknownPoolBatch =
            WorldJsonImport.CreateSharedDescriptor(
                WorldJsonDomains.SharedDirectoryPath,
                new DirectoryReader(ReadPresets(), generations, unknownPoolShared)
            ).Import();
        _test.True(
            unknownPoolBatch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == WorldJsonRules.UnknownValue
                && diagnostic.SourceLabel == "core.json#main_world_defaults"
                && diagnostic.JsonPointer == "/entries/0/settlement_name_pools/0/pool_id"
            ),
            "world shared 未注册 name-pool ID 应以稳定 rule/source/pointer fail closed。"
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
