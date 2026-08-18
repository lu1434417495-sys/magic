#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class run_skill_tres_to_json_converter : SceneTree
{
    private const string InPlaceVirtualDirectory = "res://data/configs/json/skills";
    private const string DefaultDirectoryName = "magic-skill-tres-to-json";

    public override void _Initialize()
    {
        int exitCode;
        try
        {
            exitCode = Run(OS.GetCmdlineUserArgs());
        }
        catch (Exception exception)
        {
            ConsoleProcessOutput.WriteFailure(
                $"Skill tres-to-JSON converter failed: {exception.Message}"
            );
            exitCode = 1;
        }

        Quit(exitCode);
    }

    private static int Run(string[] args)
    {
        ConverterArguments arguments;
        try
        {
            arguments = ParseArguments(args);
        }
        catch (ArgumentException exception)
        {
            ConsoleProcessOutput.WriteFailure(exception.Message);
            return 2;
        }

        try
        {
            SkillTresToJsonExportResult result =
                new SkillTresToJsonConverter().ConvertAndPublish(arguments.OutputDirectory);
            ConsoleProcessOutput.WriteStandard(
                $"WROTE skill tres-to-json: entries={result.EntryCount} "
                    + $"output_dir={result.OutputDirectory} mode={arguments.Mode}"
            );
            return 0;
        }
        catch (Exception exception)
        {
            ConsoleProcessOutput.WriteFailure(
                $"Skill tres-to-JSON conversion failed: {exception.Message}"
            );
            return 1;
        }
    }

    internal static ConverterArguments ParseArguments(
        IReadOnlyList<string> args,
        Func<string>? createTemporaryOutputDirectoryForTests = null,
        Func<string, FileAttributes?>? tryGetAttributesForTests = null
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        bool sawInPlace = false;
        bool sawOutputDirectory = false;
        string? requestedOutputDirectory = null;

        foreach (string argument in args)
        {
            if (argument == "--in-place")
            {
                if (sawInPlace)
                    throw UsageError("Duplicate --in-place argument.");
                sawInPlace = true;
                continue;
            }

            if (argument.StartsWith("--output-dir=", StringComparison.Ordinal))
            {
                if (sawOutputDirectory)
                    throw UsageError("Duplicate --output-dir argument.");
                sawOutputDirectory = true;
                requestedOutputDirectory = argument["--output-dir=".Length..];
                if (string.IsNullOrWhiteSpace(requestedOutputDirectory))
                    throw UsageError("--output-dir requires a non-empty host path.");
                continue;
            }

            throw UsageError($"Unknown converter argument '{argument}'.");
        }

        if (sawInPlace && sawOutputDirectory)
            throw UsageError("--in-place and --output-dir are mutually exclusive.");

        if (sawInPlace)
        {
            return new ConverterArguments(
                Path.GetFullPath(ProjectSettings.GlobalizePath(InPlaceVirtualDirectory)),
                "in-place"
            );
        }

        if (sawOutputDirectory)
        {
            if (requestedOutputDirectory!.Contains("://", StringComparison.Ordinal))
                throw UsageError("--output-dir requires a host filesystem path, not a Godot URI.");

            string output = Path.GetFullPath(requestedOutputDirectory);
            string explicitProjectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            string projectData = Path.GetFullPath(ProjectSettings.GlobalizePath("res://data"));
            if (PathsOverlap(output, explicitProjectRoot) || PathsOverlap(output, projectData))
            {
                throw UsageError(
                    "--output-dir cannot overlap the project root or project data; "
                        + "use --in-place explicitly for the canonical project target."
                );
            }
            string? projectReparsePoint =
                SkillTresToJsonOutputPathRules.FindExistingReparsePointAncestor(
                    explicitProjectRoot,
                    tryGetAttributesForTests
                );
            if (projectReparsePoint != null)
            {
                throw UsageError(
                    $"--output-dir cannot be used while the project root traverses "
                        + $"reparse-point ancestor '{projectReparsePoint}'."
                );
            }
            string? reparsePoint = SkillTresToJsonOutputPathRules.FindExistingReparsePointAncestor(
                output,
                tryGetAttributesForTests
            );
            if (reparsePoint != null)
            {
                throw UsageError(
                    $"--output-dir cannot traverse reparse-point ancestor '{reparsePoint}'."
                );
            }
            return new ConverterArguments(output, "output-dir");
        }

        string projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string temporaryOutput = Path.GetFullPath(
            createTemporaryOutputDirectoryForTests?.Invoke()
                ?? Path.Combine(
                    Path.GetTempPath(),
                    $"{DefaultDirectoryName}-{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}"
                )
        );
        if (IsSameOrDescendant(temporaryOutput, projectRoot))
        {
            throw new InvalidOperationException(
                "System temporary directory resolves inside the project; refusing default output."
            );
        }
        string? temporaryReparsePoint =
            SkillTresToJsonOutputPathRules.FindExistingReparsePointAncestor(
                temporaryOutput,
                tryGetAttributesForTests
            );
        if (temporaryReparsePoint != null)
        {
            throw new InvalidOperationException(
                $"System temporary output traverses reparse-point ancestor "
                    + $"'{temporaryReparsePoint}'; refusing default output."
            );
        }
        return new ConverterArguments(temporaryOutput, "temporary");
    }

    private static bool IsSameOrDescendant(string candidatePath, string rootPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        if (string.Equals(candidate, root, comparison))
            return true;
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison)
            || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, comparison);
    }

    private static bool PathsOverlap(string leftPath, string rightPath) =>
        IsSameOrDescendant(leftPath, rightPath)
        || IsSameOrDescendant(rightPath, leftPath);

    private static ArgumentException UsageError(string message) =>
        new(
            message
                + " Usage: run_skill_tres_to_json_converter [--output-dir=<host-path> | --in-place]."
        );

    internal sealed record ConverterArguments(string OutputDirectory, string Mode);
}

internal static class SkillTresToJsonOutputPathRules
{
    internal static string? FindExistingReparsePointAncestor(
        string candidatePath,
        Func<string, FileAttributes?>? tryGetAttributes = null
    )
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
            throw new ArgumentException("Output path is required.", nameof(candidatePath));

        tryGetAttributes ??= TryGetAttributes;
        string current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        while (!string.IsNullOrEmpty(current))
        {
            FileAttributes? attributes = tryGetAttributes(current);
            if (
                attributes.HasValue
                && (attributes.Value & FileAttributes.ReparsePoint) != 0
            )
            {
                return current;
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, comparison))
                break;
            current = parent;
        }

        return null;
    }

    private static FileAttributes? TryGetAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
