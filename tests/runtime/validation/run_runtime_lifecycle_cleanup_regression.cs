using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;

public partial class run_runtime_lifecycle_cleanup_regression : LifecycleTestSceneTree
{
    private static readonly string[] ForbiddenTokens =
    {
        "RuntimeStateLifecycle",
        "MarkValueGraphFinalizerless",
        "SuppressRuntimeStateGraphsForFinalizerDrain",
        "GodotTypedResourceGraphWalker",
        "StaticStrongWrappers",
        "GodotTestRuntimeQuarantine",
        "SuppressBorrowedContentForProcessExit",
        "RetainStaticWrappersForProcessLifetime",
        "PrepareForFinalizerDrain",
        "GameSession.FinalizerSuppression",
    };

    private static readonly HashSet<string> ForbiddenSourceFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "scripts/utils/GodotTypedResourceGraphWalker.cs",
            "scripts/systems/persistence/GameSession.FinalizerSuppression.cs",
        };

    private static readonly Regex SuppressFinalizeCallPattern =
        new(
            @"\b(?:System\s*\.\s*)?GC\s*\.\s*SuppressFinalize\s*\(\s*(?<argument>[^)]*?)\s*\)",
            RegexOptions.Compiled
        );

    private static readonly Regex TypeDeclarationPattern =
        new(
            @"^(?<indent>[ \t]*)(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|ref|unsafe|file|new)\s+)*(?:class|struct|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

    private static readonly HashSet<string> SourceGateFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs",
            "tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs",
        };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        string projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        ScanRoot(projectRoot, "scripts");
        ScanRoot(projectRoot, "tests");
        RequestTestExit(_test.Finish("Runtime lifecycle cleanup regression"));
    }

    private void ScanRoot(string projectRoot, string relativeRoot)
    {
        string scanRoot = Path.Combine(projectRoot, relativeRoot);
        foreach (string filePath in Directory.GetFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(projectRoot, Path.GetFullPath(filePath))
                .Replace('\\', '/');
            if (SourceGateFiles.Contains(relativePath))
                continue;

            if (ForbiddenSourceFiles.Contains(relativePath))
            {
                _test.Fail($"{relativePath}:1: forbidden lifecycle source file remains");
            }

            string source = File.ReadAllText(filePath);
            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (string token in ForbiddenTokens)
                {
                    if (!lines[lineIndex].Contains(token, StringComparison.Ordinal))
                        continue;
                    _test.Fail($"{relativePath}:{lineIndex + 1}: forbidden lifecycle token '{token}'");
                }
            }

            ScanSuppressFinalizeCalls(relativePath, source);
        }
    }

    private void ScanSuppressFinalizeCalls(string relativePath, string source)
    {
        foreach (Match call in SuppressFinalizeCallPattern.Matches(source))
        {
            string argument = call.Groups["argument"].Value.Trim();
            if (
                string.Equals(argument, "this", StringComparison.Ordinal)
                && IsPlainClrThisTarget(source, call.Index)
            )
            {
                continue;
            }

            int lineNumber = GetLineNumber(source, call.Index);
            _test.Fail(
                $"{relativePath}:{lineNumber}: GC.SuppressFinalize target is not a proven plain CLR 'this' instance"
            );
        }
    }

    private static bool IsPlainClrThisTarget(string source, int callIndex)
    {
        string typeName = FindContainingTypeName(source, callIndex);
        if (string.IsNullOrEmpty(typeName))
            return false;

        bool foundPlainClrType = false;
        Assembly projectAssembly = typeof(run_runtime_lifecycle_cleanup_regression).Assembly;
        foreach (Type type in GetLoadableTypes(projectAssembly))
        {
            string reflectedName = type.Name.Split('`')[0];
            if (!string.Equals(reflectedName, typeName, StringComparison.Ordinal))
                continue;
            if (typeof(GodotObject).IsAssignableFrom(type))
                return false;
            foundPlainClrType = true;
        }

        return foundPlainClrType;
    }

    private static string FindContainingTypeName(string source, int callIndex)
    {
        string typeName = string.Empty;
        int topLevelIndent = int.MaxValue;
        foreach (Match declaration in TypeDeclarationPattern.Matches(source))
        {
            if (declaration.Index >= callIndex)
                break;

            int indent = declaration.Groups["indent"].Value.Length;
            if (indent > topLevelIndent)
                continue;
            topLevelIndent = indent;
            typeName = declaration.Groups["name"].Value;
        }
        return typeName;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loadableTypes = new List<Type>();
            foreach (Type type in exception.Types)
            {
                if (type != null)
                    loadableTypes.Add(type);
            }
            return loadableTypes;
        }
    }

    private static int GetLineNumber(string source, int characterIndex)
    {
        int lineNumber = 1;
        for (int index = 0; index < characterIndex; index++)
        {
            if (source[index] == '\n')
                lineNumber++;
        }
        return lineNumber;
    }
}
