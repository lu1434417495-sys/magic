#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

internal enum ContentJsonCliOutputFormat
{
    Json,
    Ndjson,
}

internal sealed record ContentJsonCliOptions(
    string DomainId,
    string InputPath,
    ContentJsonCliOutputFormat OutputFormat
);

internal static class Program
{
    private const string InvalidArgumentsRule = "content.json.cli.invalid_arguments";
    private const string UnknownDomainRule = "content.json.cli.unknown_domain";
    private const string InternalFailureRule = "content.json.cli.internal_failure";

    public static int Main(string[] args)
    {
        ContentJsonCliOptions? options = null;
        try
        {
            options = ParseOptions(args ?? Array.Empty<string>());
            IContentJsonOfflineValidationDomain domain = ResolveDomain(options.DomainId);
            ContentJsonCliFileSystemSourceReader sourceReader =
                ContentJsonCliFileSystemSourceReader.Create(options.InputPath);
            ContentJsonOfflineValidationReport report = domain.Validate(
                sourceReader.SourceDirectory,
                sourceReader
            );
            WriteReport(report, options.OutputFormat);
            return report.Success ? 0 : 1;
        }
        catch (ContentJsonCliHostException exception)
        {
            WriteHostFailure(
                options?.DomainId ?? "unresolved",
                exception.RuleId,
                exception.Message,
                exception.SourceLabel,
                options?.OutputFormat ?? ContentJsonCliOutputFormat.Json
            );
            return 2;
        }
        catch (ArgumentException exception)
        {
            WriteHostFailure(
                options?.DomainId ?? "unresolved",
                InvalidArgumentsRule,
                StableArgumentMessage(exception),
                "content_json_validation_cli",
                options?.OutputFormat ?? ContentJsonCliOutputFormat.Json
            );
            return 2;
        }
        catch (Exception)
        {
            WriteHostFailure(
                options?.DomainId ?? "unresolved",
                InternalFailureRule,
                "Offline validation failed before content diagnostics could be completed.",
                "content_json_validation_cli",
                options?.OutputFormat ?? ContentJsonCliOutputFormat.Json
            );
            return 2;
        }
    }

    private static ContentJsonCliOptions ParseOptions(IReadOnlyList<string> args)
    {
        string? domainId = null;
        string? inputPath = null;
        ContentJsonCliOutputFormat outputFormat = ContentJsonCliOutputFormat.Json;
        bool formatSeen = false;

        for (int index = 0; index < args.Count; index += 1)
        {
            string option = args[index];
            if (option is not ("--domain" or "--input" or "--format"))
                throw new ArgumentException("Unknown CLI option.", nameof(args));
            if (index + 1 >= args.Count)
                throw new ArgumentException("CLI option value is missing.", nameof(args));

            string value = args[++index];
            switch (option)
            {
                case "--domain":
                    if (domainId != null)
                        throw new ArgumentException("CLI domain option is duplicated.", nameof(args));
                    domainId = value;
                    break;
                case "--input":
                    if (inputPath != null)
                        throw new ArgumentException("CLI input option is duplicated.", nameof(args));
                    inputPath = value;
                    break;
                case "--format":
                    if (formatSeen)
                        throw new ArgumentException("CLI format option is duplicated.", nameof(args));
                    outputFormat = value switch
                    {
                        "json" => ContentJsonCliOutputFormat.Json,
                        "ndjson" => ContentJsonCliOutputFormat.Ndjson,
                        _ => throw new ArgumentException(
                            "CLI format must be json or ndjson.",
                            nameof(args)
                        ),
                    };
                    formatSeen = true;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("CLI domain is required.", nameof(args));
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("CLI input is required.", nameof(args));
        return new ContentJsonCliOptions(domainId, inputPath, outputFormat);
    }

    private static IContentJsonOfflineValidationDomain ResolveDomain(string domainId)
    {
        IContentJsonOfflineValidationDomain? domain =
            ContentJsonOfflineValidationCatalog.All.SingleOrDefault(candidate =>
                string.Equals(candidate.DomainId, domainId, StringComparison.Ordinal)
            );
        if (domain != null)
            return domain;

        throw new ContentJsonCliHostException(
            UnknownDomainRule,
            "Offline content validation domain is not registered.",
            "content_json_validation_cli"
        );
    }

    private static void WriteHostFailure(
        string domainId,
        string ruleId,
        string message,
        string sourceLabel,
        ContentJsonCliOutputFormat format
    )
    {
        string stableDomain = string.IsNullOrWhiteSpace(domainId) ? "unresolved" : domainId;
        var report = new ContentJsonOfflineValidationReport(
            stableDomain,
            validatedEntryCount: 0,
            new[]
            {
                new ContentJsonDiagnostic(ruleId, message, sourceLabel, ""),
            }
        );
        WriteReport(report, format);
    }

    private static void WriteReport(
        ContentJsonOfflineValidationReport report,
        ContentJsonCliOutputFormat format
    )
    {
        string output = format == ContentJsonCliOutputFormat.Ndjson
            ? ContentJsonOfflineValidationProtocol.FormatNdjson(report)
            : ContentJsonOfflineValidationProtocol.FormatJson(report);
        Console.Out.Write(output);
    }

    private static string StableArgumentMessage(ArgumentException exception) =>
        exception.Message.StartsWith("CLI ", StringComparison.Ordinal)
            ? exception.Message.Split('\r', '\n')[0]
            : "Offline content validation arguments are invalid.";
}
