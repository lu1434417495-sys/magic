using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Godot;

internal sealed class GodotContentJsonSourceReader : IContentJsonSourceReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("JSON content directory is required.", nameof(directoryPath));

        using DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            throw new InvalidOperationException(
                $"Could not open JSON content directory '{directoryPath}': {DirAccess.GetOpenError()}."
            );
        }

        string[] fileNames = directory.GetFiles();
        Array.Sort(fileNames, StringComparer.Ordinal);
        var documents = new List<ContentJsonSourceText>();
        foreach (string fileName in fileNames)
        {
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            string filePath = JoinVirtualPath(directoryPath, fileName);
            using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                throw new InvalidOperationException(
                    $"Could not open JSON content file '{filePath}': {FileAccess.GetOpenError()}."
                );
            }

            long length = (long)file.GetLength();
            byte[] bytes = file.GetBuffer(length);
            if (bytes.LongLength != length)
            {
                throw new InvalidOperationException(
                    $"Could not read all bytes from JSON content file '{filePath}'."
                );
            }

            try
            {
                documents.Add(new ContentJsonSourceText(filePath, StrictUtf8.GetString(bytes)));
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException(
                    $"JSON content file '{filePath}' is not valid UTF-8.",
                    exception
                );
            }
        }

        return new ReadOnlyCollection<ContentJsonSourceText>(documents);
    }

    private static string JoinVirtualPath(string directoryPath, string fileName) =>
        $"{directoryPath.TrimEnd('/', '\\')}/{fileName}";
}
