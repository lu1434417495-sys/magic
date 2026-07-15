using System.Collections.Generic;
using Godot;

public partial class run_text_command_parse_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();

        RunCommand(runner, "game new test");
        AssertInvalidScalarInputsFailWithoutStateDrift(runner);

        runner.Dispose(true);
        RequestTestExit(_test.Finish("Text command parse regression"));
    }

    private void AssertInvalidScalarInputsFailWithoutStateDrift(GameTextCommandRunner runner)
    {
        IReadOnlyDictionary<string, object> beforeSnapshot =
            runner.GetSession().BuildSnapshotPlain();
        IReadOnlyDictionary<string, object> beforeCoord = PlainDict(
            PlainDict(beforeSnapshot, "world"),
            "player_coord"
        );

        GameTextCommandResult badMove = RunCommandExpectFail(runner, "world move right nope");
        _test.True(badMove.message.Contains("移动次数"), "非法 world move count 应返回明确整数校验错误。");
        // GDictionary 是引用相等，坐标必须逐分量按值比较。
        IReadOnlyDictionary<string, object> afterCoord = PlainDict(
            PlainDict(badMove.SnapshotTyped, "world"),
            "player_coord"
        );
        _test.Eq(
            PlainInt(afterCoord, "x"),
            PlainInt(beforeCoord, "x"),
            "非法 world move count 不应漂移玩家坐标 X。"
        );
        _test.Eq(
            PlainInt(afterCoord, "y"),
            PlainInt(beforeCoord, "y"),
            "非法 world move count 不应漂移玩家坐标 Y。"
        );

        GameTextCommandResult badSelect = RunCommandExpectFail(runner, "world select left 3");
        _test.True(badSelect.message.Contains("世界坐标 X"), "非法 world 坐标应返回明确坐标校验错误。");

        GameTextCommandResult badTick = RunCommandExpectFail(runner, "battle tick nope");
        _test.True(badTick.message.Contains("战斗推进 tick"), "非法 battle tick 秒数应返回明确数值校验错误。");

        GameTextCommandResult badCapacity = RunCommandExpectFail(runner, "warehouse capacity nope");
        _test.True(badCapacity.message.Contains("仓库容量"), "非法 warehouse capacity 应返回明确整数校验错误。");
    }

    private void RunCommand(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return;
        ConsoleProcessOutput.WriteStandard(result.Render());
        _test.True(result.ok, $"命令失败：{commandText} | {result.message}");
    }

    private GameTextCommandResult RunCommandExpectFail(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return result;
        ConsoleProcessOutput.WriteStandard(result.Render());
        _test.False(result.ok, $"命令本应失败：{commandText}");
        return result;
    }

    private static IReadOnlyDictionary<string, object> PlainDict(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is IReadOnlyDictionary<string, object> nested
            ? nested
            : new Dictionary<string, object>();

    private static int PlainInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback = int.MinValue
    ) =>
        dictionary != null && dictionary.TryGetValue(key, out object value)
            ? value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                _ => fallback,
            }
            : fallback;
}
