using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class run_runtime_lifecycle_cleanup_regression : LifecycleTestSceneTree
{
    private static readonly string[] ForbiddenTokens =
    {
        "RuntimeStateLifecycle",
        "MarkValueGraphFinalizerless",
        "SuppressRuntimeStateGraphsForFinalizerDrain",
    };

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
        }
    }
}
