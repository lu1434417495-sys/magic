using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class run_contingency_autocast_no_known_spoof_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        string path = Path.Combine(
            repoRoot,
            "scripts",
            "systems",
            "battle",
            "runtime",
            "BattleSkillExecutionOrchestrator.AutoCast.cs"
        );
        string source = File.ReadAllText(path);
        string executeAutoCastBody = ExtractMethodBody(source, "ExecuteAutoCast");

        foreach (string forbiddenCall in ForbiddenKnownSkillSpoofCalls())
        {
            _test.False(
                executeAutoCastBody.Contains(forbiddenCall, StringComparison.Ordinal),
                $"ExecuteAutoCast must not spoof known-skill access with {forbiddenCall}."
            );
        }

        RequestTestExit(_test.Finish("Contingency auto-cast known-skill spoof guard"));
    }

    private static IEnumerable<string> ForbiddenKnownSkillSpoofCalls()
    {
        yield return "AddKnownActiveSkill(";
        yield return "SetKnownActiveSkillIds(";
        yield return "SetKnownSkillLevelTyped(";
        yield return "RemoveKnownSkillLevelTyped(";
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        string needle = methodName + "(";
        int nameIndex = source.IndexOf(needle, StringComparison.Ordinal);
        if (nameIndex < 0)
            return "";
        int braceStart = source.IndexOf('{', nameIndex);
        if (braceStart < 0)
            return "";

        int depth = 0;
        for (int index = braceStart; index < source.Length; index++)
        {
            char current = source[index];
            if (current == '{')
                depth++;
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceStart..(index + 1)];
            }
        }
        return source[braceStart..];
    }
}
