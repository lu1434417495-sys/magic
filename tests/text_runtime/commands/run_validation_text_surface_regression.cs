using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_validation_text_surface_regression : LifecycleTestSceneTree
{
    private const string InvalidItemDirectory =
        "res://tests/fixtures/resource_validation/item_registry_invalid";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();

        AssertOfficialValidationSurface(runner);
        AssertInvalidQuestValidationSurface(runner);
        AssertInvalidItemValidationSurface(runner);
        AssertInvalidWorldValidationSurface(runner);

        runner.Dispose(true);
        RequestTestExit(_test.Finish("Validation text surface regression"));
    }

    private void AssertOfficialValidationSurface(GameTextCommandRunner runner)
    {
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

    private void AssertInvalidQuestValidationSurface(GameTextCommandRunner runner)
    {
        GameSession gameSession = runner.GetSession().GetGameSession();
        _test.True(gameSession != null, "quest validation surface 回归前置：GameSession 应可访问。");
        if (gameSession == null)
            return;

        var originalQuestDefs = gameSession.GetQuestDefsSnapshotForTests();
        var invalidQuest = new QuestDef
        {
            quest_id = "contract_invalid_headless_quest",
            display_name = "Invalid Headless Quest",
            provider_interaction_id = "service_contract_board",
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["objective_id"] = "submit_missing_item",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SubmitItem),
                    ["target_id"] = "missing_headless_item",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new()
                {
                    ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.Gold),
                    ["amount"] = 10,
                },
            },
        };
        Error installError = (Error)gameSession.InstallTestContentDef(
            "quest",
            invalidQuest.quest_id,
            invalidQuest
        );
        _test.Eq(installError, Error.Ok, "测试应能注入非法 quest 内容。");
        gameSession.RefreshContentValidationSnapshot();

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

        gameSession.ReplaceQuestDefsForTests(originalQuestDefs);
        gameSession.RefreshContentValidationSnapshot();
    }

    private void AssertInvalidItemValidationSurface(GameTextCommandRunner runner)
    {
        GameSession gameSession = runner.GetSession().GetGameSession();
        _test.True(gameSession != null, "validation surface 回归前置：GameSession 应可访问。");
        if (gameSession == null)
            return;

        ItemContentRegistry originalRegistry = gameSession.GetItemContentRegistryForTests();
        var invalidItemRegistry = new ItemContentRegistry();
        invalidItemRegistry.RebuildFromDirectories(new GArray { InvalidItemDirectory }, new GArray());
        gameSession.SetItemContentRegistryForTests(invalidItemRegistry);
        gameSession.RefreshContentValidationSnapshot();

        GameTextCommandResult snapshotResult = RunCommand(runner, "snapshot");
        IReadOnlyDictionary<string, object> validationSnapshot = Dict(
            snapshotResult.SnapshotTyped,
            "validation"
        );
        IReadOnlyDictionary<string, object> domains = Dict(validationSnapshot, "domains");
        IReadOnlyDictionary<string, object> itemDomain = Dict(domains, "item");
        IReadOnlyList<object> itemErrors = ArrayValue(itemDomain, "errors");

        _test.False(DictBool(validationSnapshot, "ok", true), "非法 item registry 应让 headless validation 快照标记失败。");
        _test.Eq(DictInt(itemDomain, "error_count", 0), 6, "非法 item registry 应稳定暴露 6 条 item 校验错误。");
        AssertErrorContains(itemErrors, "is missing item_id", "headless validation 快照应暴露缺失 item_id。");
        AssertErrorContains(itemErrors, "Duplicate item_id registered: duplicate_item", "headless validation 快照应暴露重复 item_id。");
        AssertErrorContains(itemErrors, "declares invalid slot phantom_slot", "headless validation 快照应暴露非法槽位引用。");
        AssertErrorContains(itemErrors, "references missing template weapon_type_longsword_base", "headless validation 快照不应借用官方 item template cache。");
        _test.True(snapshotResult.snapshot_text.Contains("domain=item | errors=6"), "headless 文本快照应稳定渲染 item validation 错误计数。");
        _test.True(snapshotResult.snapshot_text.Contains("is missing item_id"), "headless 文本快照应渲染缺失 item_id 错误。");
        _test.True(snapshotResult.snapshot_text.Contains("Duplicate item_id registered: duplicate_item"), "headless 文本快照应渲染重复 item_id 错误。");
        _test.True(snapshotResult.snapshot_text.Contains("declares invalid slot phantom_slot"), "headless 文本快照应渲染非法槽位错误。");
        _test.True(FindLogEntry(snapshotResult.SnapshotTyped, "session.content.item_validation_failed").Count == 0, "validation surface 回归不应依赖额外日志注入来暴露 item 错误。");

        RunCommand(runner, "expect field validation.ok == false");
        RunCommand(runner, "expect field validation.domains.item.error_count == 6");

        gameSession.SetItemContentRegistryForTests(originalRegistry);
        invalidItemRegistry.Dispose();
        invalidItemRegistry = null;
        originalRegistry = null;
        gameSession.RefreshContentValidationSnapshot();
    }

    private void AssertInvalidWorldValidationSurface(GameTextCommandRunner runner)
    {
        GameSession gameSession = runner.GetSession().GetGameSession();
        _test.True(gameSession != null, "world validation surface 回归前置：GameSession 应可访问。");
        if (gameSession == null)
            return;

        WorldMapContentValidator originalValidator = gameSession.GetWorldContentValidatorForTests();
        WorldMapContentValidator invalidValidator = new InvalidWorldContentValidator();
        gameSession.SetWorldContentValidatorForTests(invalidValidator);
        gameSession.RefreshContentValidationSnapshot();

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

        gameSession.SetWorldContentValidatorForTests(originalValidator);
        invalidValidator.Dispose();
        gameSession.RefreshContentValidationSnapshot();
    }

    private GameTextCommandResult RunCommand(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return result;
        GD.Print(result.Render());
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

    private sealed partial class InvalidWorldContentValidator : WorldMapContentValidator
    {
        public override Godot.Collections.Array<string> ValidateWorldPresets(
            GDictionary enemy_templates = null,
            GDictionary wild_encounter_rosters = null
        )
        {
            return new Godot.Collections.Array<string>
            {
                "World preset fixture references missing settlement missing_settlement.",
            };
        }
    }
}
