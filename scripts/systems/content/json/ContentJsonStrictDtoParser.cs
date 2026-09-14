#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

internal static class ContentJsonStrictDtoParser
{
    internal static ContentImportStageResult<TDto> Parse<TDto>(
        JsonContentEntryContext context,
        string json,
        JsonTypeInfo<TDto> jsonTypeInfo,
        string invalidDtoRuleId
    )
        where TDto : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        if (string.IsNullOrWhiteSpace(invalidDtoRuleId))
            throw new ArgumentException("Strict DTO rule ID is required.", nameof(invalidDtoRuleId));

        try
        {
            TDto? value = JsonSerializer.Deserialize(json ?? "", jsonTypeInfo);
            if (value != null)
                return ContentImportStageResult<TDto>.Success(value);
        }
        catch (JsonException exception)
        {
            return Failure<TDto>(
                context,
                invalidDtoRuleId,
                JsonPathToPointer(exception.Path)
            );
        }

        return Failure<TDto>(context, invalidDtoRuleId, "");
    }

    private static ContentImportStageResult<TDto> Failure<TDto>(
        JsonContentEntryContext context,
        string ruleId,
        string relativePointer
    )
        where TDto : notnull
    {
        string pointer = string.IsNullOrEmpty(relativePointer)
            ? context.JsonPointer
            : $"{context.JsonPointer}{relativePointer}";
        return ContentImportStageResult<TDto>.Failure(
            new ContentJsonDiagnostic(
                ruleId,
                "JSON entry does not match its registered strict DTO contract.",
                context.SourceLabel,
                pointer
            )
        );
    }

    private static string JsonPathToPointer(string? path)
    {
        if (string.IsNullOrEmpty(path) || string.Equals(path, "$", StringComparison.Ordinal))
            return "";

        var tokens = new List<string>();
        int index = path[0] == '$' ? 1 : 0;
        while (index < path.Length)
        {
            if (path[index] == '.')
            {
                index += 1;
                int start = index;
                while (index < path.Length && path[index] != '.' && path[index] != '[')
                    index += 1;
                if (index > start)
                    tokens.Add(path[start..index]);
                continue;
            }

            if (path[index] == '[')
            {
                int close = path.IndexOf(']', index + 1);
                if (close < 0)
                    return "";
                string token = path[(index + 1)..close].Trim('"', '\'');
                if (token.Length > 0)
                    tokens.Add(token);
                index = close + 1;
                continue;
            }

            return "";
        }

        if (tokens.Count == 0)
            return "";

        var pointer = new StringBuilder();
        foreach (string token in tokens)
        {
            pointer.Append('/');
            pointer.Append(
                token.Replace("~", "~0", StringComparison.Ordinal)
                    .Replace("/", "~1", StringComparison.Ordinal)
            );
        }
        return pointer.ToString();
    }
}
