#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

internal sealed class ContentJsonCliHostException : Exception
{
    internal ContentJsonCliHostException(string ruleId, string message, string sourceLabel)
        : base(message)
    {
        RuleId = ruleId;
        SourceLabel = sourceLabel;
    }

    internal string RuleId { get; }
    internal string SourceLabel { get; }
}

internal sealed class ContentJsonCliFileSystemSourceReader : IContentJsonSourceReader
{
    internal const string UnsupportedVirtualPathRule =
        "content.json.cli.unsupported_virtual_path";
    internal const string InputNotFoundRule = "content.json.cli.input_not_found";
    internal const string InvalidExtensionRule = "content.json.cli.invalid_extension";
    internal const string InvalidUtf8Rule = "content.json.cli.invalid_utf8";
    internal const string InputReadFailedRule = "content.json.cli.input_read_failed";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    private readonly string? _singleFilePath;
    private readonly string _sourceDirectory;

    private ContentJsonCliFileSystemSourceReader(
        string sourceDirectory,
        string? singleFilePath
    )
    {
        _sourceDirectory = sourceDirectory;
        _singleFilePath = singleFilePath;
    }

    internal string SourceDirectory => _sourceDirectory;

    internal static ContentJsonCliFileSystemSourceReader Create(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ContentJsonCliHostException(
                InputNotFoundRule,
                "Offline content input path is required.",
                "content_json_validation_cli"
            );
        }
        if (
            inputPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            || inputPath.StartsWith("user://", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ContentJsonCliHostException(
                UnsupportedVirtualPathRule,
                "Standalone offline validation accepts host filesystem paths, not Godot virtual paths.",
                "content_json_validation_cli"
            );
        }

        string absolutePath;
        try
        {
            absolutePath = Path.GetFullPath(inputPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException
        )
        {
            throw new ContentJsonCliHostException(
                InputNotFoundRule,
                "Offline content input path is invalid.",
                "content_json_validation_cli"
            );
        }

        if (File.Exists(absolutePath))
        {
            if (!absolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ContentJsonCliHostException(
                    InvalidExtensionRule,
                    "Offline content single-file input must end in .json.",
                    FileLabel(absolutePath)
                );
            }

            string sourceDirectory = Path.GetDirectoryName(absolutePath) ?? Directory.GetCurrentDirectory();
            return new ContentJsonCliFileSystemSourceReader(sourceDirectory, absolutePath);
        }

        if (Directory.Exists(absolutePath))
            return new ContentJsonCliFileSystemSourceReader(absolutePath, singleFilePath: null);

        throw new ContentJsonCliHostException(
            InputNotFoundRule,
            "Offline content input file or directory does not exist.",
            FileLabel(absolutePath)
        );
    }

    public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath)
    {
        if (!string.Equals(directoryPath, _sourceDirectory, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Offline source reader must be invoked with its resolved source directory."
            );
        }

        string[] filePaths;
        try
        {
            filePaths = _singleFilePath != null
                ? new[] { _singleFilePath }
                : Directory
                    .EnumerateFiles(_sourceDirectory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path =>
                        File.Exists(path)
                        && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException
                or PathTooLongException
        )
        {
            throw new ContentJsonCliHostException(
                InputReadFailedRule,
                "Offline content directory could not be enumerated.",
                FileLabel(_sourceDirectory)
            );
        }

        var sources = new List<ContentJsonSourceText>(filePaths.Length);
        foreach (string filePath in filePaths)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(filePath);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or FileNotFoundException
                    or PathTooLongException
            )
            {
                throw new ContentJsonCliHostException(
                    InputReadFailedRule,
                    "Offline content file could not be read.",
                    FileLabel(filePath)
                );
            }

            try
            {
                sources.Add(new ContentJsonSourceText(filePath, StrictUtf8.GetString(bytes)));
            }
            catch (DecoderFallbackException)
            {
                throw new ContentJsonCliHostException(
                    InvalidUtf8Rule,
                    "Offline content file is not valid UTF-8.",
                    FileLabel(filePath)
                );
            }
        }

        return new ReadOnlyCollection<ContentJsonSourceText>(sources);
    }

    private static string FileLabel(string filePath)
    {
        string normalized = (filePath ?? "").Replace('\\', '/');
        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }
}
