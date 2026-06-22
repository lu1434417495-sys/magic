using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

public partial class run_gd_logging_guard_regression : SceneTree
{
    private readonly TestHarness _test = new();

    private static readonly Regex DirectGodotLogPattern = new(
        @"\bGD\s*\.\s*(?:Print|PushError|PushWarning)\s*\(",
        RegexOptions.Compiled
    );

    public override void _Initialize()
    {
        TestSyntheticViolation();
        TestRepositorySources();

        Quit(_test.Finish("GD logging guard regression"));
    }

    private void TestSyntheticViolation()
    {
        string source = string.Join(
            "\n",
            new[]
            {
                "public partial class Probe : SceneTree",
                "{",
                "    public override void _Initialize()",
                "    {",
                $"        {DirectCall("Print")}(\"bad\");",
                $"        {DirectCall("PushError")}(\"bad\");",
                $"        {DirectCall("PushWarning")}(\"bad\");",
                "    }",
                "}",
            }
        );

        List<string> violations = FindViolationsForSource("tests/synthetic/gd_log_bad.cs", source);
        _test.Eq(
            violations.Count,
            3,
            $"synthetic direct GD logging should be rejected: {string.Join("\n", violations)}"
        );

        string compliantSource = """
            public partial class Probe : SceneTree
            {
                public override void _Initialize()
                {
                    System.Console.Out.WriteLine("ok");
                    System.Console.Error.WriteLine("ok");
                    PackedScene scene = GD.Load<PackedScene>("res://scene.tscn");
                }
            }
            """;

        violations = FindViolationsForSource("tests/synthetic/gd_log_good.cs", compliantSource);
        _test.Eq(
            violations.Count,
            0,
            $"Console output and non-log GD APIs should be allowed: {string.Join("\n", violations)}"
        );
    }

    private void TestRepositorySources()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        var scanRoots = new[] { "scripts", "tests" };
        var violations = new List<string>();

        foreach (string scanRoot in scanRoots)
        {
            string absoluteRoot = Path.Combine(repoRoot, scanRoot);
            if (!Directory.Exists(absoluteRoot))
                continue;
            foreach (string path in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string repoPath = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
                violations.AddRange(FindViolationsForSource(repoPath, File.ReadAllText(path)));
            }
        }

        if (violations.Count > 0)
            _test.Fail("Direct GD logging guard failed:\n" + string.Join("\n", violations));
    }

    private static List<string> FindViolationsForSource(string repoPath, string source)
    {
        string sanitizedSource = SanitizeSource(source);
        string[] lines = sanitizedSource.Replace("\r\n", "\n").Split('\n');
        var violations = new List<string>();

        for (int index = 0; index < lines.Length; index++)
        {
            if (DirectGodotLogPattern.IsMatch(lines[index]))
            {
                violations.Add(
                    $"{repoPath}:{index + 1}: 禁止直接调用 Godot GD 日志接口；测试与工具输出请用 Console，runtime 日志请用 GameLog。"
                );
            }
        }

        return violations;
    }

    private static string DirectCall(string methodName) => $"GD.{methodName}";

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
