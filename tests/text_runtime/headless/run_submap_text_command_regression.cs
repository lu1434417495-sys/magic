using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_submap_text_command_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSubmapTextCommandFlowUsesTypedWorldRuntime();

        Quit(_test.Finish("Submap text command regression"));
    }

    private void TestSubmapTextCommandFlowUsesTypedWorldRuntime()
    {
        MethodInfo confirmSubmapTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandConfirmSubmapEntryTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo cancelSubmapTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandCancelSubmapEntryTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo returnSubmapTyped = typeof(GameRuntimeFacade).GetMethod(
            "CommandReturnFromSubmapTyped",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.Eq(
            confirmSubmapTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade submap confirm 应提供 typed runtime result。"
        );
        _test.Eq(
            cancelSubmapTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade submap cancel 应提供 typed runtime result。"
        );
        _test.Eq(
            returnSubmapTyped?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade submap return 应提供 typed runtime result。"
        );

        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            AssertCommandOk(runner.ExecuteLine("game new ashen_intersection"), "game new ashen_intersection 应创建成功。");

            GameTextCommandResult moveResult = runner.ExecuteLine("world move right 3");
            AssertCommandOk(moveResult, "world move right 3 应成功触发子地图确认。");
            _test.Eq(
                SnapshotString(moveResult.snapshot, "modal", "id"),
                "submap_confirm",
                "world move 到入口后应弹出 submap_confirm。"
            );
            _test.True(
                SnapshotBool(moveResult.snapshot, "submap", "confirm_visible"),
                "world move 到入口后应显示 submap confirm。"
            );

            GameTextCommandResult cancelResult = runner.ExecuteLine("submap cancel");
            AssertCommandOk(cancelResult, "submap cancel 应成功。");
            _test.False(
                SnapshotBool(cancelResult.snapshot, "submap", "confirm_visible"),
                "submap cancel 后应关闭 submap confirm。"
            );
            _test.Eq(
                SnapshotString(cancelResult.snapshot, "modal", "id"),
                "",
                "submap cancel 后应清空 modal。"
            );

            GameTextCommandResult leaveEntryResult = runner.ExecuteLine("world move right 1");
            AssertCommandOk(leaveEntryResult, "submap cancel 后离开入口应成功。");

            moveResult = runner.ExecuteLine("world move left 1");
            AssertCommandOk(moveResult, "submap cancel 后再次踩到入口应成功。");
            _test.Eq(
                SnapshotString(moveResult.snapshot, "modal", "id"),
                "submap_confirm",
                "再次踩到入口后应重新弹出 submap_confirm。"
            );

            GameTextCommandResult confirmResult = runner.ExecuteLine("submap confirm");
            AssertCommandOk(confirmResult, "submap confirm 应成功。");
            _test.True(
                SnapshotBool(confirmResult.snapshot, "world", "is_submap"),
                "submap confirm 后当前地图应切到子地图。"
            );
            _test.Eq(
                SnapshotString(confirmResult.snapshot, "world", "map_id"),
                "ashen_ashlands",
                "submap confirm 后 map_id 应切到灰烬地图。"
            );

            GameTextCommandResult returnResult = runner.ExecuteLine("submap return");
            AssertCommandOk(returnResult, "submap return 应成功。");
            _test.False(
                SnapshotBool(returnResult.snapshot, "world", "is_submap"),
                "submap return 后应回到主世界。"
            );
            _test.Eq(
                SnapshotInt(returnResult.snapshot, "world", "player_coord.x"),
                52,
                "submap return 后 world.player_coord.x 应恢复到入口返回点。"
            );
            _test.Eq(
                SnapshotInt(returnResult.snapshot, "world", "player_coord.y"),
                49,
                "submap return 后 world.player_coord.y 应恢复到入口返回点。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static string SnapshotString(GDictionary snapshot, string topLevelKey, string nestedKey) =>
        DictString(Dict(snapshot, topLevelKey), nestedKey, "");

    private static bool SnapshotBool(GDictionary snapshot, string topLevelKey, string nestedKey) =>
        DictBool(Dict(snapshot, topLevelKey), nestedKey, false);

    private static int SnapshotInt(GDictionary snapshot, string topLevelKey, string path)
    {
        string[] segments = path.Split('.');
        GDictionary current = Dict(snapshot, topLevelKey);
        for (int index = 0; index < segments.Length - 1; index++)
            current = Dict(current, segments[index]);
        return DictInt(current, segments[^1], 0);
    }

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private static bool DictBool(GDictionary dictionary, string key, bool fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
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
