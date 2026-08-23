#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;

internal static class SkillValidatorDiagnosticGoldenHarness
{
    private const string FixtureRoot =
        "res://tests/fixtures/skill_validator_diagnostic_golden/cases";
    private const string ExactGoldenPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/exact_diagnostic_golden.tsv";
    private const string CollateralPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/fixture_collateral_diagnostics.tsv";
    private const int ExpectedFixtureCount = 75;
    private const int ExpectedOccurrenceCount = 518;
    private const int ExpectedCollateralDiagnosticCount = 48;

    internal static void AssertRuleHitSet()
    {
        IReadOnlyDictionary<string, List<string>> expectedByFixture = ReadExpected();
        IReadOnlyDictionary<string, List<string>> collateralByFixture = ReadCollateral();
        string[] fixtureIds = DirAccess.GetDirectoriesAt(FixtureRoot);
        Array.Sort(fixtureIds, StringComparer.Ordinal);
        RequireCount("JSON fixture directories", fixtureIds.Length, ExpectedFixtureCount);
        if (!new HashSet<string>(fixtureIds, StringComparer.Ordinal).SetEquals(expectedByFixture.Keys))
        {
            throw new InvalidOperationException(
                "Terminal JSON diagnostic fixture and exact-golden fixture sets differ."
            );
        }
        if (!new HashSet<string>(collateralByFixture.Keys, StringComparer.Ordinal).IsSubsetOf(fixtureIds))
        {
            throw new InvalidOperationException(
                $"{CollateralPath} references a missing fixture."
            );
        }

        var validator = new SkillImportModelValidator();
        foreach (string fixtureId in fixtureIds)
        {
            IReadOnlyDictionary<StringName, SkillImportModel> imports =
                LoadFixtureImports(fixtureId);
            List<string> actual = validator.ValidateBatchMessages(imports)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            if (collateralByFixture.TryGetValue(fixtureId, out List<string>? collateral))
            {
                foreach (string diagnostic in collateral)
                {
                    int index = actual.FindIndex(value => value == diagnostic);
                    if (index < 0)
                    {
                        throw new InvalidOperationException(
                            $"Fixture {fixtureId} no longer emits reviewed construction collateral: {diagnostic}"
                        );
                    }
                    actual.RemoveAt(index);
                }
            }
            AssertExactMultiset(fixtureId, actual, expectedByFixture[fixtureId]);
        }
    }

    private static IReadOnlyDictionary<StringName, SkillImportModel> LoadFixtureImports(
        string fixtureId
    )
    {
        var imports = new Dictionary<StringName, SkillImportModel>();
        IReadOnlyList<ContentJsonSourceText> sources = new GodotContentJsonSourceReader()
            .ReadUtf8Documents($"{FixtureRoot}/{fixtureId}");
        foreach (ContentJsonSourceText source in sources)
        {
            using JsonDocument document = JsonDocument.Parse(source.Utf8Json);
            JsonElement entries = document.RootElement.GetProperty("entries");
            int entryIndex = 0;
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                SkillJsonDto dto = JsonSerializer.Deserialize(
                    entry.GetRawText(),
                    SkillJsonImportSerializerContext.Default.SkillJsonDto
                ) ?? throw new InvalidOperationException(
                    $"Fixture {fixtureId} entry {entryIndex} did not deserialize."
                );
                string entryId = dto.SkillId ?? $"fixture_{fixtureId}_{entryIndex}";
                var context = new JsonContentEntryContext(
                    SkillContentJsonAuthoringDomain.DomainId,
                    entryId,
                    $"{source.FilePath}#{entryId}",
                    $"/entries/{entryIndex}"
                );
                ContentImportStageResult<SkillImportModel> normalized =
                    SkillJsonImportParser.NormalizeDiagnosticFixture(context, dto);
                if (!normalized.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Fixture {fixtureId} cannot form an import model: "
                            + string.Join(" | ", normalized.Diagnostics.Select(value => value.ToString()))
                    );
                }
                if (!imports.TryAdd(normalized.Value.SkillId.Value, normalized.Value))
                    throw new InvalidOperationException($"Fixture {fixtureId} duplicates skill_id.");
                entryIndex++;
            }
        }
        return imports;
    }

    private static IReadOnlyDictionary<string, List<string>> ReadExpected()
    {
        using FileAccess file = OpenRequired(ExactGoldenPath);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        int occurrenceCount = 0;
        while (!file.EofReached())
        {
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 6)
                throw new InvalidOperationException($"{ExactGoldenPath} has a malformed row.");
            if (!result.TryGetValue(fields[1], out List<string>? diagnostics))
            {
                diagnostics = new List<string>();
                result.Add(fields[1], diagnostics);
            }
            diagnostics.Add(Decode(fields[2], ExactGoldenPath));
            occurrenceCount++;
        }
        RequireCount("exact diagnostic occurrences", occurrenceCount, ExpectedOccurrenceCount);
        foreach (List<string> diagnostics in result.Values)
            diagnostics.Sort(StringComparer.Ordinal);
        return result;
    }

    private static IReadOnlyDictionary<string, List<string>> ReadCollateral()
    {
        using FileAccess file = OpenRequired(CollateralPath);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        int diagnosticCount = 0;
        while (!file.EofReached())
        {
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 2 || result.ContainsKey(fields[0]))
                throw new InvalidOperationException($"{CollateralPath} has a malformed row.");
            List<string> diagnostics = fields[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => Decode(value, CollateralPath))
                .ToList();
            diagnosticCount += diagnostics.Count;
            result.Add(fields[0], diagnostics);
        }
        RequireCount(
            "fixture-construction collateral diagnostics",
            diagnosticCount,
            ExpectedCollateralDiagnosticCount
        );
        return result;
    }

    private static void AssertExactMultiset(
        string fixtureId,
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    )
    {
        if (actual.SequenceEqual(expected, StringComparer.Ordinal))
            return;
        throw new InvalidOperationException(
            $"Fixture {fixtureId} rule-hit multiset changed. "
                + $"missing=[{string.Join(" | ", MultisetExcept(expected, actual))}] "
                + $"unexpected=[{string.Join(" | ", MultisetExcept(actual, expected))}]"
        );
    }

    private static List<string> MultisetExcept(
        IReadOnlyList<string> source,
        IReadOnlyList<string> subtract
    )
    {
        var remaining = new List<string>(subtract);
        var result = new List<string>();
        foreach (string value in source)
        {
            int index = remaining.FindIndex(candidate => candidate == value);
            if (index >= 0)
                remaining.RemoveAt(index);
            else
                result.Add(value);
        }
        return result;
    }

    private static string Decode(string encoded, string path)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"{path} contains invalid base64.", exception);
        }
    }

    private static FileAccess OpenRequired(string path)
    {
        FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file ?? throw new InvalidOperationException($"Required artifact is missing: {path}.");
    }

    private static void RequireCount(string label, int actual, int expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Terminal skill diagnostic {label} count mismatch: expected {expected}, got {actual}."
            );
        }
    }
}
