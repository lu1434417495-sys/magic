using System;
using System.Collections.Generic;
using Godot;

public partial class run_validation_text_surface_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        AssertOfficialValidationSurface();
        AssertInvalidQuestValidationSurface();
        AssertInvalidItemValidationSurface();
        AssertInvalidWorldValidationSurface();
        RequestTestExit(_test.Finish("Validation text surface regression"));
    }

    private void AssertOfficialValidationSurface()
    {
        using GameTextCommandRunner runner = CreateRunner();
        GameTextCommandResult snapshotResult = RunCommand(runner, "snapshot");
        IReadOnlyDictionary<string, object> validationSnapshot = Dict(
            snapshotResult.SnapshotTyped,
            "validation"
        );
        IReadOnlyDictionary<string, object> domains = Dict(validationSnapshot, "domains");
        IReadOnlyDictionary<string, object> progressionDomain = Dict(domains, "progression");
        IReadOnlyDictionary<string, object> itemDomain = Dict(domains, "item");
        IReadOnlyDictionary<string, object> questDomain = Dict(domains, "quest");
        IReadOnlyDictionary<string, object> worldDomain = Dict(domains, "world");
        IReadOnlyList<object> progressionErrors = ArrayValue(progressionDomain, "errors");

        _test.True(DictBool(validationSnapshot, "ok", false), "正式 headless validation 快照应通过。");
        _test.Eq(DictInt(validationSnapshot, "error_count", -1), 0, "正式 headless validation 快照不应有错误。");
        _test.Eq(DictInt(progressionDomain, "error_count", -1), 0, "正式 progression validation domain 不应有错误。");
        _test.Eq(progressionErrors.Count, 0, "正式 progression validation domain 不应返回错误列表。");
        _test.Eq(DictInt(itemDomain, "error_count", -1), 0, "正式 item validation domain 不应有错误。");
        _test.Eq(DictInt(questDomain, "error_count", -1), 0, "正式 quest validation domain 不应有错误。");
        _test.Eq(DictInt(worldDomain, "error_count", -1), 0, "正式 world validation domain 不应有错误。");
        _test.True(snapshotResult.snapshot_text.Contains("[VALIDATION]"), "headless 文本快照应包含 VALIDATION 分段。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=progression | errors=0"), "文本快照应稳定渲染 progression validation 摘要。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=item | errors=0"), "文本快照应稳定渲染 item validation 摘要。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=quest | errors=0"), "文本快照应稳定渲染 quest validation 摘要。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=world | errors=0"), "文本快照应稳定渲染 world validation 摘要。");
        _test.True(FindLogEntry(snapshotResult.SnapshotTyped, "session.content.item_validation_failed").Count == 0, "正式内容不应依赖 item validation 错误日志。");

        RunCommand(runner, "expect field validation.ok == true");
        RunCommand(runner, "expect field validation.error_count == 0");
    }

    private void AssertInvalidQuestValidationSurface()
    {
        var invalidQuest = new QuestDefinition(
            "contract_invalid_headless_quest",
            "Invalid Headless Quest",
            "Invalid quest fixture for validation text.",
            "service_contract_board",
            System.Array.Empty<StringName>(),
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            new QuestObjectiveDefinition[]
            {
                new("submit_missing_item", "submit_item", "missing_headless_item", 1),
            },
            new QuestRewardDefinition[]
            {
                new(
                    "gold",
                    10,
                    "",
                    0,
                    "",
                    System.Array.Empty<QuestPendingRewardEntryDefinition>()
                ),
            },
            false,
            "service_contract_board",
            new StringName[] { "contract_board" },
            "",
            "",
            "",
            ""
        );
        using GameTextCommandRunner runner = CreateRunner(seed =>
        {
            seed.Quests = new Dictionary<StringName, QuestDefinition>(seed.Quests)
            {
                [invalidQuest.QuestId] = invalidQuest,
            };
        });

        GameTextCommandResult snapshotResult = RunCommand(runner, "snapshot");
        IReadOnlyDictionary<string, object> validationSnapshot = Dict(
            snapshotResult.SnapshotTyped,
            "validation"
        );
        IReadOnlyDictionary<string, object> domains = Dict(validationSnapshot, "domains");
        IReadOnlyDictionary<string, object> progressionDomain = Dict(domains, "progression");
        IReadOnlyDictionary<string, object> questDomain = Dict(domains, "quest");
        IReadOnlyList<object> questErrors = ArrayValue(questDomain, "errors");

        _test.False(DictBool(validationSnapshot, "ok", true), "非法 quest 应让 headless validation 快照标记失败。");
        _test.Eq(DictInt(progressionDomain, "error_count", -1), 0, "非法 quest 不应污染 progression validation domain。");
        _test.True(DictInt(questDomain, "error_count", 0) > 0, "非法 quest 应进入正式 quest validation domain。");
        AssertErrorContains(questErrors, "references missing item missing_headless_item", "headless validation 快照应暴露 quest 物品跨表引用错误。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=quest | errors="), "headless 文本快照应稳定渲染 quest validation 摘要。");
        _test.True(snapshotResult.snapshot_text.Contains("references missing item missing_headless_item"), "headless 文本快照应渲染 quest validation 错误。");

    }

    private void AssertInvalidItemValidationSurface()
    {
        ItemDefinition invalidSkillBook = BuildInvalidSkillBookDefinition();
        using GameTextCommandRunner runner = CreateRunner(seed =>
        {
            seed.Items = new Dictionary<StringName, ItemDefinition>(seed.Items)
            {
                [invalidSkillBook.ItemId] = invalidSkillBook,
            };
        });

        GameTextCommandResult snapshotResult = RunCommand(runner, "snapshot");
        IReadOnlyDictionary<string, object> validationSnapshot = Dict(
            snapshotResult.SnapshotTyped,
            "validation"
        );
        IReadOnlyDictionary<string, object> domains = Dict(validationSnapshot, "domains");
        IReadOnlyDictionary<string, object> itemDomain = Dict(domains, "item");
        IReadOnlyList<object> itemErrors = ArrayValue(itemDomain, "errors");

        _test.False(DictBool(validationSnapshot, "ok", true), "非法 item registry 应让 headless validation 快照标记失败。");
        _test.Eq(DictInt(itemDomain, "error_count", 0), 1, "非法 skill-book definition 应稳定暴露 1 条 item 校验错误。");
        AssertErrorContains(itemErrors, "references missing skill missing_headless_skill", "headless validation 快照应暴露技能书跨表引用错误。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=item | errors=1"), "headless 文本快照应稳定渲染 item validation 错误计数。");
        _test.True(snapshotResult.snapshot_text.Contains("references missing skill missing_headless_skill"), "headless 文本快照应渲染技能书跨表引用错误。");
        _test.True(
            FindLogEntry(
                snapshotResult.SnapshotTyped,
                "session.content.item_validation_failed"
            ).Count > 0,
            "synthetic invalid item snapshot 应在 bind-time 记录正式 validation 日志。"
        );

        RunCommand(runner, "expect field validation.ok == false");
        RunCommand(runner, "expect field validation.domains.item.error_count == 1");
    }

    private void AssertInvalidWorldValidationSurface()
    {
        using GameTextCommandRunner runner = CreateRunner(seed =>
            seed.WorldGenerations = BuildInvalidWorldGenerations(seed.WorldGenerations)
        );

        GameTextCommandResult snapshotResult = RunCommand(runner, "snapshot");
        IReadOnlyDictionary<string, object> validationSnapshot = Dict(
            snapshotResult.SnapshotTyped,
            "validation"
        );
        IReadOnlyDictionary<string, object> domains = Dict(validationSnapshot, "domains");
        IReadOnlyDictionary<string, object> worldDomain = Dict(domains, "world");
        IReadOnlyList<object> worldErrors = ArrayValue(worldDomain, "errors");

        _test.False(DictBool(validationSnapshot, "ok", true), "非法 world preset 应让 headless validation 快照标记失败。");
        _test.Eq(DictInt(worldDomain, "error_count", 0), 1, "非法 world validator 应稳定暴露 1 条 world 校验错误。");
        AssertErrorContains(worldErrors, "references missing settlement missing_settlement", "headless validation 快照应暴露 world preset 错误。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=world | errors=1"), "headless 文本快照应稳定渲染 world validation 错误计数。");
        _test.True(snapshotResult.snapshot_text.Contains("references missing settlement missing_settlement"), "headless 文本快照应渲染 world validation 错误。");

    }

    private static GameTextCommandRunner CreateRunner(
        Action<SyntheticContentSnapshotSeed> configure = null
    )
    {
        var runner = new GameTextCommandRunner();
        GameSessionTestFactory.CreateSynthetic(
            runner.GetSession(),
            configure
        );
        runner.initialize();
        return runner;
    }

    private static ItemDefinition BuildInvalidSkillBookDefinition() =>
        new(
            "contract_invalid_skill_book",
            "",
            "Invalid Skill Book",
            "Invalid skill-book fixture for validation text.",
            "",
            true,
            0,
            0,
            0,
            true,
            1,
            ItemDefinition.ToStringName(ItemCategoryKind.SkillBook),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitRollGroupDefinition>(),
            Array.Empty<string>(),
            Array.Empty<AttributeModifierDefinition>(),
            "missing_headless_skill",
            Array.Empty<string>(),
            null,
            "",
            null,
            -1
        );

    private static IReadOnlyDictionary<string, WorldGenerationDefinition> BuildInvalidWorldGenerations(
        IReadOnlyDictionary<string, WorldGenerationDefinition> source
    )
    {
        var result = new Dictionary<string, WorldGenerationDefinition>(
            source ?? new Dictionary<string, WorldGenerationDefinition>(),
            StringComparer.Ordinal
        );
        foreach ((string path, WorldGenerationDefinition definition) in result)
        {
            result[path] = CloneWithMissingSettlement(definition);
            return result;
        }
        throw new InvalidOperationException(
            "Validation text fixture requires at least one process world definition."
        );
    }

    private static WorldGenerationDefinition CloneWithMissingSettlement(
        WorldGenerationDefinition source
    ) =>
        new(
            source.CanonicalPath,
            source.Seed,
            source.WorldSizeInChunks,
            source.ChunkSize,
            source.PlayerStartCoord,
            source.PlayerVisionRange,
            source.ProceduralGenerationEnabled,
            source.ProceduralWildSpawnChunkChanceDenominator,
            source.InjectDefaultMainWorldContent,
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
            new[]
            {
                new SettlementDistributionDefinition(
                    "missing_settlement",
                    Vector2I.Zero,
                    "test_faction",
                    "test_country"
                ),
            },
            source.WildMonsterDistribution,
            source.MountedSubmaps,
            source.WorldEvents,
            source.DefaultSettlementBundle,
            source.DefaultWildSpawnBundle,
            source.SettlementNamePools
        );

    private GameTextCommandResult RunCommand(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return result;
        ConsoleProcessOutput.WriteStandard(result.Render());
        _test.True(result.ok, $"命令失败：{commandText} | {result.message}");
        return result;
    }

    private void AssertErrorContains(IReadOnlyList<object> errors, string fragment, string message)
    {
        foreach (object error in errors)
        {
            if (error is string errorText && errorText.Contains(fragment, StringComparison.Ordinal))
                return;
        }
        _test.Fail(message);
    }

    private static IReadOnlyDictionary<string, object> FindLogEntry(
        IReadOnlyDictionary<string, object> snapshot,
        string eventId
    )
    {
        foreach (object entryValue in ArrayValue(Dict(snapshot, "logs"), "entries"))
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (DictString(entry, "event_id") == eventId)
                return entry;
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<object> ArrayValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> list
            ? list
            : System.Array.Empty<object>();
    }

    private static IReadOnlyDictionary<string, object> Dict(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyDictionary<string, object> nested
            ? nested
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static bool DictBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        bool fallback
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is bool value
            ? value
            : fallback;
    }

    private static int DictInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback
    )
    {
        if (dictionary == null || !dictionary.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => fallback,
        };
    }

    private static string DictString(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        if (dictionary == null || !dictionary.TryGetValue(key, out object rawValue))
            return "";
        return rawValue switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => "",
        };
    }

}
