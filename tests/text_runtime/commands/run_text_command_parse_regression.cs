using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_parse_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();

        RunCommand(runner, "game new test");
        AssertInvalidScalarInputsFailWithoutStateDrift(runner);

        runner.Dispose(true);
        Quit(_test.Finish("Text command parse regression"));
    }

    private void AssertInvalidScalarInputsFailWithoutStateDrift(GameTextCommandRunner runner)
    {
        GDictionary beforeSnapshot = runner.GetSession().BuildSnapshot();
        GDictionary beforeCoord = Dict(Dict(beforeSnapshot, "world"), "player_coord");

        GameTextCommandResult badMove = RunCommandExpectFail(runner, "world move right nope");
        _test.True(badMove.message.Contains("移动次数"), "非法 world move count 应返回明确整数校验错误。");
        // GDictionary 是引用相等，坐标必须逐分量按值比较。
        GDictionary afterCoord = Dict(Dict(badMove.snapshot, "world"), "player_coord");
        _test.Eq(
            DictInt(afterCoord, "x"),
            DictInt(beforeCoord, "x"),
            "非法 world move count 不应漂移玩家坐标 X。"
        );
        _test.Eq(
            DictInt(afterCoord, "y"),
            DictInt(beforeCoord, "y"),
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
        GD.Print(result.Render());
        _test.True(result.ok, $"命令失败：{commandText} | {result.message}");
    }

    private GameTextCommandResult RunCommandExpectFail(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return result;
        GD.Print(result.Render());
        _test.False(result.ok, $"命令本应失败：{commandText}");
        return result;
    }

    private static GDictionary Dict(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = int.MinValue)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
    }
}
