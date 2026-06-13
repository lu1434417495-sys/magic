using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_world_party_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestGameTextCommandResultStaysPlainAndProjectsAssertions();
        TestPartyCommandsUseTypedRuntimeBoundary();

        Quit(_test.Finish("Text command party regression"));
    }

    private void TestGameTextCommandResultStaysPlainAndProjectsAssertions()
    {
        Type resultType = typeof(GameTextCommandResult);
        Type runnerType = typeof(GameTextCommandRunner);
        MethodInfo executeExpect = runnerType.GetMethod(
            "ExecuteExpect",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo executeCommand = runnerType.GetMethod(
            "ExecuteCommand",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Type commandOutcomeType = runnerType.GetNestedType(
            "CommandOutcome",
            BindingFlags.NonPublic
        );
        Type expectationResultType = runnerType.GetNestedType(
            "ExpectationResult",
            BindingFlags.NonPublic
        );
        MethodInfo ensureWorldContext = runnerType.GetMethod(
            "EnsureWorldContext",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo missingWorldError = runnerType.GetMethod(
            "MissingWorldError",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo parseIntArgument = runnerType.GetMethod(
            "ParseIntArgument",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo parseCoordArgument = runnerType.GetMethod(
            "ParseCoordArgument",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo setSnapshot = resultType.GetMethod(
            "SetSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo addAssertion = resultType.GetMethod(
            "AddAssertion",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        PropertyInfo runtimeOutcomeCode = typeof(GameRuntimeFacade.RuntimeCommandResult).GetProperty(
            "Code",
            BindingFlags.Instance | BindingFlags.Public
        );
        FieldInfo commandOutcomeCode = commandOutcomeType?.GetField(
            "Code",
            BindingFlags.Instance | BindingFlags.Public
        );
        FieldInfo expectationResultMessage = expectationResultType?.GetField(
            "Message",
            BindingFlags.Instance | BindingFlags.Public
        );
        FieldInfo resultCode = resultType.GetField("code", BindingFlags.Instance | BindingFlags.Public);
        _test.Eq(
            executeExpect?.GetParameters()[1].ParameterType,
            typeof(IReadOnlyDictionary<string, object>),
            "GameTextCommandRunner expect 路径应直接消费 typed snapshot，不应回读 GDictionary。"
        );
        _test.False(
            executeExpect?.ReturnType == typeof(GDictionary),
            "GameTextCommandRunner expect 路径不应继续把断言结果退回 GDictionary。"
        );
        _test.False(
            executeCommand?.ReturnType == typeof(GDictionary),
            "GameTextCommandRunner 内部命令分发不应继续返回 GDictionary。"
        );
        _test.False(
            ensureWorldContext?.ReturnType == typeof(GDictionary),
            "GameTextCommandRunner world context gate 不应继续返回 GDictionary。"
        );
        _test.False(
            missingWorldError?.ReturnType == typeof(GDictionary),
            "GameTextCommandRunner missing-world helper 不应继续返回 GDictionary。"
        );
        _test.False(
            parseIntArgument?.ReturnType == typeof(GDictionary),
            "GameTextCommandRunner 内部整数解析 helper 不应继续返回 GDictionary。"
        );
        _test.False(
            parseCoordArgument?.ReturnType == typeof(GDictionary),
            "GameTextCommandRunner 内部坐标解析 helper 不应继续返回 GDictionary。"
        );
        _test.Eq(
            setSnapshot?.GetParameters()[0].ParameterType,
            typeof(IReadOnlyDictionary<string, object>),
            "GameTextCommandResult.SetSnapshot 应继续直接消费 typed snapshot。"
        );
        _test.Eq(
            runtimeOutcomeCode?.PropertyType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameRuntimeFacade.RuntimeCommandResult 应提供统一的 enum code。"
        );
        _test.Eq(
            commandOutcomeCode?.FieldType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameTextCommandRunner.CommandOutcome 应保留统一的 enum code。"
        );
        _test.Eq(
            resultCode?.FieldType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameTextCommandResult 应向外暴露统一的 enum code。"
        );
        _test.True(
            expectationResultMessage == null,
            "GameTextCommandRunner.ExpectationResult 不应继续在 owner 内部搬运 message 字符串。"
        );
        _test.True(
            addAssertion != null
                && addAssertion.GetParameters().Length == 5
                && addAssertion.GetParameters()[0].ParameterType == typeof(bool),
            "GameTextCommandResult.AddAssertion 应继续以 typed 字段维护 assertion backing。"
        );

        GameTextCommandResult result = new()
        {
            command_text = "expect field modal.id == party",
            ok = false,
            message = "Expectation failed.",
        };
        result.SetSnapshot(
            new Dictionary<string, object>
            {
                ["modal"] = new Dictionary<string, object> { ["id"] = "party" },
                ["world"] = new Dictionary<string, object> { ["step"] = 3 },
                ["entries"] = new List<object>
                {
                    new Dictionary<string, object> { ["id"] = "alpha" }
                },
            }
        );
        result.AddAssertion(
            false,
            "Expectation failed.",
            "field modal.id == party",
            "warehouse",
            "party"
        );

        Godot.Collections.Array<GDictionary> firstProjection = result.assertions;
        _test.Eq(firstProjection.Count, 1, "GameTextCommandResult.assertions projection 应保留一条断言。");
        firstProjection.Add(
            new GDictionary
            {
                ["summary"] = "mutated",
                ["actual"] = "",
                ["expected"] = "",
            }
        );
        _test.Eq(
            result.assertions.Count,
            1,
            "GameTextCommandResult.assertions 不应把外部对 projection 的追加写回 owner state。"
        );

        GDictionary firstProjectionEntry = firstProjection[0];
        firstProjectionEntry["summary"] = "mutated summary";
        GDictionary secondProjectionEntry = result.assertions[0];
        _test.Eq(
            secondProjectionEntry["summary"].AsString(),
            "field modal.id == party",
            "GameTextCommandResult.assertions 不应把 projection entry 的原地修改写回 owner state。"
        );
        GDictionary firstSnapshotProjection = result.snapshot;
        firstSnapshotProjection["modal"] = new GDictionary { ["id"] = "warehouse" };
        firstSnapshotProjection["world"].AsGodotDictionary()["step"] = 99;
        firstSnapshotProjection["entries"].AsGodotArray()[0] = new GDictionary { ["id"] = "beta" };
        GDictionary secondSnapshotProjection = result.snapshot;
        _test.Eq(
            Dict(secondSnapshotProjection, "modal")["id"].AsString(),
            "party",
            "GameTextCommandResult.snapshot 不应把外部替换 top-level projection 的修改写回 owner state。"
        );
        _test.Eq(
            Dict(secondSnapshotProjection, "world")["step"].AsInt32(),
            3,
            "GameTextCommandResult.snapshot 不应把外部修改 nested dictionary projection 写回 owner state。"
        );
        _test.Eq(
            secondSnapshotProjection["entries"].AsGodotArray()[0].AsGodotDictionary()["id"].AsString(),
            "alpha",
            "GameTextCommandResult.snapshot 不应把外部修改 array projection 写回 owner state。"
        );
    }

    private void TestPartyCommandsUseTypedRuntimeBoundary()
    {
        MethodInfo worldMoveTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandWorldMoveTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo worldSelectTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandWorldSelectTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo openPartyTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandOpenPartyTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo selectPartyMemberTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandSelectPartyMemberTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo setPartyLeaderTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandSetPartyLeaderTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo moveMemberToActiveTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandMoveMemberToActiveTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo moveMemberToReserveTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandMoveMemberToReserveTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo openPartyWarehouseTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandOpenPartyWarehouseTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo openSettlementTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandOpenSettlementTyped",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Vector2I) },
            modifiers: null
        );
        MethodInfo openSettlementTypedNoArgs = typeof(GameRuntimeFacade).GetMethod(
            "CommandOpenSettlementTyped",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: System.Type.EmptyTypes,
            modifiers: null
        );
        MethodInfo worldInspectTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandWorldInspectTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo selectWorldCellTyped = typeof(GameRuntimeFacade).GetMethod(
            "SelectWorldCellTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.Eq(
            worldMoveTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world move 应提供 typed runtime result。"
        );
        _test.Eq(
            openPartyTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party open 应提供 typed runtime result。"
        );
        _test.Eq(
            selectPartyMemberTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party select 应提供 typed runtime result。"
        );
        _test.Eq(
            setPartyLeaderTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party leader 应提供 typed runtime result。"
        );
        _test.Eq(
            moveMemberToActiveTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party activate 应提供 typed runtime result。"
        );
        _test.Eq(
            moveMemberToReserveTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party reserve 应提供 typed runtime result。"
        );
        _test.Eq(
            openPartyWarehouseTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party warehouse 应提供 typed runtime result。"
        );
        _test.Eq(
            worldSelectTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world select 应提供 typed runtime result。"
        );
        _test.Eq(
            openSettlementTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world open(coord) 应提供 typed runtime result。"
        );
        _test.Eq(
            openSettlementTypedNoArgs?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world open() 应提供 typed runtime result。"
        );
        _test.Eq(
            worldInspectTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world inspect 应提供 typed runtime result。"
        );
        _test.Eq(
            selectWorldCellTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world click/select 应提供 typed runtime result。"
        );

        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            GameTextCommandResult newGameResult = runner.ExecuteLine("game new test");
            _test.True(newGameResult.ok, $"headless 文本命令应能创建测试世界。message={newGameResult.message}");
            _test.Eq(
                newGameResult.code,
                GameRuntimeFacade.RuntimeCommandCode.Ok,
                "成功的文本命令结果应暴露 Ok enum code。"
            );

            HeadlessGameTestSession session = runner.GetSession();
            _test.True(session != null, "GameTextCommandRunner 应返回 headless session。");
            _test.True(
                session?.GetRuntimeFacadeTyped() != null,
                "GameTextCommandRunner party regression 应拿到 typed runtime。"
            );

            GameTextCommandResult worldSelectResult = runner.ExecuteLine("world select 50 50");
            _test.True(worldSelectResult.ok, $"world select 应成功。message={worldSelectResult.message}");
            _test.Eq(
                worldSelectResult.code,
                GameRuntimeFacade.RuntimeCommandCode.Ok,
                "world select 成功时应暴露 Ok enum code。"
            );
            GDictionary worldSnapshot = Dict(session?.BuildSnapshot(), "world");
            _test.Eq(
                DictInt(Dict(worldSnapshot, "selected_coord"), "x", -1),
                50,
                "world select 应更新 selected_coord.x。"
            );
            _test.Eq(
                DictInt(Dict(worldSnapshot, "selected_coord"), "y", -1),
                50,
                "world select 应更新 selected_coord.y。"
            );

            GameTextCommandResult openPartyResult = runner.ExecuteLine("party open");
            _test.True(openPartyResult.ok, $"party open 应成功。message={openPartyResult.message}");
            _test.Eq(
                SnapshotString(session?.BuildSnapshot(), "modal", "id"),
                "party",
                "party open 应打开队伍窗口。"
            );
            GameTextCommandResult expectPartyWindowResult = runner.ExecuteLine("expect window == party");
            _test.True(
                expectPartyWindowResult.ok,
                $"expect window == party 应成功。message={expectPartyWindowResult.message}"
            );
            _test.Eq(
                expectPartyWindowResult.message,
                "",
                "成功的 expect 结果不应继续携带冗余 message 字符串。"
            );
            _test.Eq(
                expectPartyWindowResult.assertions.Count,
                1,
                "expect 命令应通过 GameTextCommandResult owner helper 记录一条断言。"
            );

            GameTextCommandResult selectPartyResult = runner.ExecuteLine("party select player_sword_01");
            _test.True(selectPartyResult.ok, $"party select 应成功。message={selectPartyResult.message}");
            _test.Eq(
                SnapshotString(session?.BuildSnapshot(), "party", "selected_member_id"),
                "player_sword_01",
                "party select 应更新选中成员。"
            );

            GameTextCommandResult leaderResult = runner.ExecuteLine("party leader player_sword_01");
            _test.True(leaderResult.ok, $"party leader 应成功。message={leaderResult.message}");

            GameTextCommandResult openWarehouseResult = runner.ExecuteLine("party warehouse");
            _test.True(
                openWarehouseResult.ok,
                $"party warehouse 应成功。message={openWarehouseResult.message}"
            );
            _test.Eq(
                SnapshotString(session?.BuildSnapshot(), "modal", "id"),
                "warehouse",
                "party warehouse 应打开共享仓库窗口。"
            );

            GameTextCommandResult invalidWorldMoveResult = runner.ExecuteLine("world move");
            _test.False(invalidWorldMoveResult.ok, "world move 缺少参数时应失败。");
            _test.Eq(
                invalidWorldMoveResult.code,
                GameRuntimeFacade.RuntimeCommandCode.Failed,
                "失败的文本命令结果应暴露非字符串 enum code。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static string SnapshotString(GDictionary snapshot, string topLevelKey, string nestedKey)
    {
        return DictString(Dict(snapshot, topLevelKey), nestedKey, "");
    }

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private static int DictInt(GDictionary dictionary, string key, int fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }
}
