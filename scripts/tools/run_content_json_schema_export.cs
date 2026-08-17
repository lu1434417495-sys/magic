using System;
using System.Collections.Generic;
using System.Text;
using Godot;

public partial class run_content_json_schema_export : SceneTree
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

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
                $"Content JSON schema export failed: {exception}"
            );
            exitCode = 1;
        }

        Quit(exitCode);
    }

    private static int Run(string[] args)
    {
        bool write = false;
        string requestedDomain = "";
        foreach (string argument in args)
        {
            if (argument == "--write")
            {
                write = true;
            }
            else if (argument == "--check")
            {
                write = false;
            }
            else if (argument.StartsWith("--domain=", StringComparison.Ordinal))
            {
                requestedDomain = argument["--domain=".Length..];
            }
            else
            {
                ConsoleProcessOutput.WriteFailure(
                    $"Unknown content JSON schema exporter argument '{argument}'. "
                        + "Use --check (default), --write, and optional --domain=<id>."
                );
                return 2;
            }
        }

        IReadOnlyList<ContentJsonSchemaDomainRegistration> registrations =
            string.IsNullOrWhiteSpace(requestedDomain)
                ? ContentJsonSchemaCatalog.All
                : new[] { ContentJsonSchemaCatalog.Require(requestedDomain) };
        var exporter = new ContentJsonSchemaExporter();
        int mismatchCount = 0;
        foreach (ContentJsonSchemaDomainRegistration registration in registrations)
        {
            string generated = exporter.Export(registration);
            if (write)
            {
                WriteSchema(registration.TrackedSchemaPath, generated);
                ConsoleProcessOutput.WriteStandard(
                    $"WROTE content JSON schema {registration.DomainId}: "
                        + registration.TrackedSchemaPath
                );
                continue;
            }

            string tracked = ReadSchema(registration.TrackedSchemaPath);
            if (!string.Equals(generated, tracked, StringComparison.Ordinal))
            {
                mismatchCount += 1;
                ConsoleProcessOutput.WriteFailure(
                    $"DRIFT content JSON schema {registration.DomainId}: "
                        + $"{registration.TrackedSchemaPath}. Regenerate with "
                        + "godot --headless -s res://scripts/tools/run_content_json_schema_export.cs "
                        + "-- --write."
                );
            }
            else
            {
                ConsoleProcessOutput.WriteStandard(
                    $"OK content JSON schema {registration.DomainId}: "
                        + registration.TrackedSchemaPath
                );
            }
        }

        return mismatchCount == 0 ? 0 : 1;
    }

    private static string ReadSchema(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new InvalidOperationException(
                $"Could not open tracked schema '{path}' for reading: "
                    + FileAccess.GetOpenError()
            );
        }
        long length = (long)file.GetLength();
        byte[] bytes = file.GetBuffer(length);
        if (bytes.LongLength != length)
        {
            throw new InvalidOperationException(
                $"Could not read all bytes from tracked schema '{path}'."
            );
        }
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException(
                $"Tracked schema '{path}' is not valid UTF-8.",
                exception
            );
        }
    }

    private static void WriteSchema(string path, string content)
    {
        int separator = path.LastIndexOf('/');
        if (separator <= "res://".Length)
            throw new InvalidOperationException($"Schema path '{path}' has no parent directory.");
        string directory = path[..separator];
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (directoryError != Error.Ok && directoryError != Error.AlreadyExists)
        {
            throw new InvalidOperationException(
                $"Could not create schema directory '{directory}': {directoryError}."
            );
        }

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new InvalidOperationException(
                $"Could not open schema '{path}' for writing: {FileAccess.GetOpenError()}"
            );
        }
        byte[] bytes = StrictUtf8.GetBytes(content);
        file.StoreBuffer(bytes);
        file.Flush();
        if (file.GetError() != Error.Ok || (long)file.GetLength() != bytes.LongLength)
            throw new InvalidOperationException($"Failed to write complete schema '{path}'.");
    }
}
