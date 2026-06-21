using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

public partial class run_test_lifecycle_style_guard_regression : SceneTree
{
    private readonly TestHarness _test = new();

    private static readonly Regex DirectNumericQuitPattern = new(
        @"\bQuit\s*\(\s*-?\d+\s*\)",
        RegexOptions.Compiled
    );

    private static readonly Regex ManualExitQuitPattern = new(
        @"\bQuit\s*\(\s*(?:exitCode|runner\s*\.\s*RunForWrapper\s*\(\s*\))\s*\)",
        RegexOptions.Compiled
    );

    private static readonly Regex FreePattern = new(
        @"\.\s*Free\s*\(",
        RegexOptions.Compiled
    );

    private static readonly Regex ExitCodeAssignmentPattern = new(
        @"(?:\b(?:int|var)\s+)?exitCode\s*=\s*(?<expr>[^;]+);",
        RegexOptions.Compiled
    );

    public override void _Initialize()
    {
        TestSyntheticViolations();
        TestSyntheticCompliantExits();
        TestRepositoryTests();

        Quit(_test.Finish("Test lifecycle style guard regression"));
    }

    private void TestSyntheticViolations()
    {
        string source = """
            public partial class Probe : SceneTree
            {
                public override void _Initialize()
                {
                    Quit(1);
                    node.Free();
                }
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/bad.cs", source);
        _test.Eq(violations.Count, 2, $"synthetic bad exits should be rejected: {string.Join("\n", violations)}");

        string mixedSource = """
            public partial class Probe : SceneTree
            {
                private readonly TestHarness _test = new();

                public override void _Initialize()
                {
                    int exitCode = RawExit();
                    Quit(exitCode);
                }

                private int RawExit()
                {
                    return 1;
                }

                private int OtherExit()
                {
                    GodotSharpCleanup.CollectPendingFinalizers();
                    return _test.Finish("other");
                }
            }
            """;

        violations = FindViolationsForSource("tests/synthetic/mixed.cs", mixedSource);
        _test.Eq(
            violations.Count,
            1,
            $"cleanup in another method should not bless an unsafe manual Quit(exitCode): {string.Join("\n", violations)}"
        );
    }

    private void TestSyntheticCompliantExits()
    {
        string source = """
            public partial class Probe : SceneTree
            {
                private readonly TestHarness _test = new();

                public override void _Initialize()
                {
                    int exitCode = Run();
                    GodotSharpCleanup.CollectPendingFinalizers();
                    Quit(exitCode);
                }

                private int Run() => _test.Finish("probe");
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/good.cs", source);
        _test.Eq(violations.Count, 0, $"synthetic compliant exits should pass: {string.Join("\n", violations)}");

        string finishReturnedSource = """
            public partial class Probe : SceneTree
            {
                private readonly TestHarness _test = new();

                public override void _Initialize()
                {
                    int exitCode = Run();
                    Quit(exitCode);
                }

                private int Run()
                {
                    return _test.Finish("probe");
                }
            }
            """;

        violations = FindViolationsForSource("tests/synthetic/finish_returned.cs", finishReturnedSource);
        _test.Eq(
            violations.Count,
            1,
            $"manual Quit(exitCode) should still drain after Run() returns: {string.Join("\n", violations)}"
        );
    }

    private void TestRepositoryTests()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        string testsRoot = Path.Combine(repoRoot, "tests");
        var violations = new List<string>();

        foreach (string path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string repoPath = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
            violations.AddRange(FindViolationsForSource(repoPath, File.ReadAllText(path)));
        }

        if (violations.Count > 0)
            _test.Fail("Test lifecycle style guard failed:\n" + string.Join("\n", violations));
    }

    private static List<string> FindViolationsForSource(string repoPath, string source)
    {
        string sanitizedSource = SanitizeSource(source);
        string[] lines = sanitizedSource.Replace("\r\n", "\n").Split('\n');
        var violations = new List<string>();

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (DirectNumericQuitPattern.IsMatch(line))
            {
                violations.Add(
                    $"{repoPath}:{index + 1}: 禁止裸数字 Quit(...)；失败路径也必须走 TestHarness.Finish 或显式 cleanup。"
                );
            }

            if (FreePattern.IsMatch(line))
            {
                violations.Add(
                    $"{repoPath}:{index + 1}: 测试中禁止裸 .Free()；请使用 QueueFree 或 GodotSharpCleanup/fixture helper。"
                );
            }

            if (ManualExitQuitPattern.IsMatch(line) && !IsManualExitQuitCompliant(lines, index))
            {
                violations.Add(
                    $"{repoPath}:{index + 1}: 手写 Quit(exitCode) 前必须在当前方法调用 GodotSharpCleanup.CollectPendingFinalizers() 收口。"
                );
            }
        }

        return violations;
    }

    private static bool IsManualExitQuitCompliant(string[] lines, int quitLineIndex)
    {
        (int assignmentLineIndex, _) = FindLastExitCodeAssignment(lines, quitLineIndex);
        if (HasFinalizerDrainBetween(lines, assignmentLineIndex, quitLineIndex))
            return true;

        return false;
    }

    private static (int LineIndex, string Expression) FindLastExitCodeAssignment(
        string[] lines,
        int beforeLineIndex
    )
    {
        for (int index = beforeLineIndex - 1; index >= 0; index--)
        {
            if (lines[index].Contains("Quit(", StringComparison.Ordinal))
                break;

            Match match = ExitCodeAssignmentPattern.Match(lines[index]);
            if (match.Success)
                return (index, match.Groups["expr"].Value.Trim());
        }

        return (-1, "");
    }

    private static bool HasFinalizerDrainBetween(string[] lines, int startLineIndex, int quitLineIndex)
    {
        int firstLine = Math.Max(startLineIndex + 1, 0);
        for (int index = firstLine; index < quitLineIndex; index++)
        {
            if (lines[index].Contains("GodotSharpCleanup.CollectPendingFinalizers", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string SanitizeSource(string source)
    {
        if (string.IsNullOrEmpty(source))
            return "";

        var builder = new StringBuilder(source.Length);
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;

        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n')
                {
                    inLineComment = false;
                    builder.Append('\n');
                }
                else
                {
                    builder.Append(' ');
                }
                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    builder.Append("  ");
                    index++;
                    inBlockComment = false;
                }
                else
                {
                    builder.Append(current == '\n' ? '\n' : ' ');
                }
                continue;
            }

            if (inString)
            {
                if (current == '\\' && next != '\0')
                {
                    builder.Append("  ");
                    index++;
                    continue;
                }
                if (current == '"')
                    inString = false;
                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (inVerbatimString)
            {
                if (current == '"' && next == '"')
                {
                    builder.Append("  ");
                    index++;
                    continue;
                }
                if (current == '"')
                    inVerbatimString = false;
                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (inChar)
            {
                if (current == '\\' && next != '\0')
                {
                    builder.Append("  ");
                    index++;
                    continue;
                }
                if (current == '\'')
                    inChar = false;
                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (current == '/' && next == '/')
            {
                builder.Append("  ");
                index++;
                inLineComment = true;
                continue;
            }
            if (current == '/' && next == '*')
            {
                builder.Append("  ");
                index++;
                inBlockComment = true;
                continue;
            }
            if (current == '@' && next == '"')
            {
                builder.Append("  ");
                index++;
                inVerbatimString = true;
                continue;
            }
            if (current == '$' && next == '"')
            {
                builder.Append("  ");
                index++;
                inString = true;
                continue;
            }
            if (
                current == '$'
                && next == '@'
                && index + 2 < source.Length
                && source[index + 2] == '"'
            )
            {
                builder.Append("   ");
                index += 2;
                inVerbatimString = true;
                continue;
            }
            if (
                current == '@'
                && next == '$'
                && index + 2 < source.Length
                && source[index + 2] == '"'
            )
            {
                builder.Append("   ");
                index += 2;
                inVerbatimString = true;
                continue;
            }
            if (current == '"')
            {
                builder.Append(' ');
                inString = true;
                continue;
            }
            if (current == '\'')
            {
                builder.Append(' ');
                inChar = true;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
