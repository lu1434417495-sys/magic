#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;

internal static class SkillValidatorDiagnosticMigrationHarness
{
    private const string FixtureRoot =
        "res://tests/fixtures/skill_validator_diagnostic_golden/cases";
    private const string ExactGoldenPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/exact_diagnostic_golden.tsv";
    private const string CrosswalkPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/stage1b_import_boundary_crosswalk.tsv";
    private const int ExpectedFixtureCount = 87;
    private const int ExpectedDirectImportFixtureCount = 48;
    private const int ExpectedCrosswalkFixtureCount = 39;
    private const int ExpectedStrictBoundaryDiagnosticCount = 230;
    private const int ExpectedInterceptedOccurrenceCount = 350;
    private const int ExpectedSanitizerInducedDiagnosticCount = 52;

    private sealed record CrosswalkRow(
        string FixtureId,
        string StrictAdapterSha256,
        IReadOnlySet<string> InterceptedOccurrenceKeys,
        IReadOnlyList<string> SanitizerInducedDiagnostics
    );

    internal static void AssertMigratedRuleHitSet()
    {
        IReadOnlyDictionary<string, List<ExpectedOccurrence>> expectedByFixture =
            ReadExpectedOccurrences();
        IReadOnlyDictionary<string, CrosswalkRow> crosswalk = ReadCrosswalk();
        string[] fixtureIds = EnumerateDirectories(FixtureRoot)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        RequireCount("negative fixture directories", fixtureIds.Length, ExpectedFixtureCount);
        if (!new HashSet<string>(fixtureIds, StringComparer.Ordinal).SetEquals(expectedByFixture.Keys))
            throw new InvalidOperationException(
                "Stage 1b diagnostic fixture directory and exact-golden fixture sets differ."
            );

        var seenCrosswalkFixtures = new HashSet<string>(StringComparer.Ordinal);
        int directImportFixtureCount = 0;
        int strictBoundaryDiagnosticCount = 0;
        int interceptedOccurrenceCount = 0;
        int sanitizerInducedDiagnosticCount = 0;
        using var loader = new TestContentResourceLoader();
        var validator = new SkillImportModelValidator();

        foreach (string fixtureId in fixtureIds)
        {
            var boundaryMessages = new List<string>();
            var strictAdapterSignatures = new List<string>();
            var imports = new Dictionary<StringName, SkillImportModel>();
            foreach (string path in EnumerateResourcePaths($"{FixtureRoot}/{fixtureId}"))
            {
                Resource? resource = loader.LoadCanonical<Resource>(path);
                if (resource is not SkillDef skill)
                {
                    boundaryMessages.Add($"Skill config {path} is not a SkillDef.");
                    continue;
                }
                StringName skillId = skill.skill_id;
                if (skillId == "")
                {
                    boundaryMessages.Add($"Skill config {path} is missing skill_id.");
                    continue;
                }
                if (imports.ContainsKey(skillId))
                {
                    boundaryMessages.Add($"Duplicate skill_id registered: {skillId}");
                    continue;
                }

                var context = new JsonContentEntryContext(
                    SkillContentJsonAuthoringDomain.DomainId,
                    skillId.ToString(),
                    path,
                    "/entries/0"
                );
                ContentImportStageResult<SkillImportModel> adapted =
                    SkillResourceProjectionAdapter.TryAdapt(context, skill);
                if (!adapted.HasValue)
                {
                    strictAdapterSignatures.AddRange(
                        adapted.Diagnostics.Select(CanonicalAdapterDiagnostic)
                    );
                    using SkillDef sanitized = (SkillDef)skill.Duplicate(true);
                    adapted = AdaptAfterStructuralSanitization(
                        context,
                        sanitized,
                        out string? sanitationFailure
                    );
                    if (!adapted.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Fixture {fixtureId} cannot reach the import-model validator after structural repair: {sanitationFailure}"
                        );
                    }
                }
                imports.Add(skillId, adapted.Value);
            }

            var actual = new List<string>(boundaryMessages);
            actual.AddRange(validator.ValidateBatchMessages(imports));
            actual.Sort(StringComparer.Ordinal);

            crosswalk.TryGetValue(fixtureId, out CrosswalkRow? row);
            if (strictAdapterSignatures.Count == 0)
            {
                directImportFixtureCount++;
                if (row != null)
                    throw new InvalidOperationException(
                        $"Fixture {fixtureId} no longer has a strict import-boundary intercept but remains in {CrosswalkPath}."
                    );
            }
            else
            {
                if (row == null)
                    throw new InvalidOperationException(
                        $"Fixture {fixtureId} is rejected by the strict import boundary but has no reviewed crosswalk row."
                    );
                seenCrosswalkFixtures.Add(fixtureId);
                strictBoundaryDiagnosticCount += strictAdapterSignatures.Count;
                string actualSha = Sha256OfSorted(strictAdapterSignatures);
                if (!string.Equals(actualSha, row.StrictAdapterSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Fixture {fixtureId} strict import-boundary diagnostic signature changed: expected {row.StrictAdapterSha256}, got {actualSha}."
                    );
                }
                foreach (string induced in row.SanitizerInducedDiagnostics)
                {
                    int index = actual.FindIndex(value => value == induced);
                    if (index < 0)
                    {
                        throw new InvalidOperationException(
                            $"Fixture {fixtureId} no longer emits reviewed sanitizer-only diagnostic: {induced}"
                        );
                    }
                    actual.RemoveAt(index);
                }
                sanitizerInducedDiagnosticCount += row.SanitizerInducedDiagnostics.Count;
            }

            List<ExpectedOccurrence> expectedRows = expectedByFixture[fixtureId];
            var expectedOccurrenceKeys = new HashSet<string>(
                expectedRows.Select(value => value.OccurrenceKey),
                StringComparer.Ordinal
            );
            IReadOnlySet<string> intercepted =
                row?.InterceptedOccurrenceKeys
                ?? new HashSet<string>(StringComparer.Ordinal);
            if (!intercepted.IsSubsetOf(expectedOccurrenceKeys))
            {
                throw new InvalidOperationException(
                    $"Fixture {fixtureId} crosswalk intercepts an occurrence outside its unchanged T1a.7 golden."
                );
            }
            interceptedOccurrenceCount += intercepted.Count;
            var expectedActive = expectedRows
                .Where(value => !intercepted.Contains(value.OccurrenceKey))
                .Select(value => value.Message)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            AssertExactMultiset(fixtureId, actual, expectedActive);
        }

        if (!seenCrosswalkFixtures.SetEquals(crosswalk.Keys))
            throw new InvalidOperationException(
                $"{CrosswalkPath} fixture set does not equal the current strict-boundary intercept set."
            );
        RequireCount("direct import-model fixtures", directImportFixtureCount, ExpectedDirectImportFixtureCount);
        RequireCount("crosswalk fixtures", crosswalk.Count, ExpectedCrosswalkFixtureCount);
        RequireCount(
            "strict import-boundary diagnostics",
            strictBoundaryDiagnosticCount,
            ExpectedStrictBoundaryDiagnosticCount
        );
        RequireCount(
            "intercepted T1a.7 occurrences",
            interceptedOccurrenceCount,
            ExpectedInterceptedOccurrenceCount
        );
        RequireCount(
            "sanitizer-induced diagnostics",
            sanitizerInducedDiagnosticCount,
            ExpectedSanitizerInducedDiagnosticCount
        );
    }

    private static IReadOnlyDictionary<string, CrosswalkRow> ReadCrosswalk()
    {
        using FileAccess file = FileAccess.Open(CrosswalkPath, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"Missing Stage 1b diagnostic crosswalk {CrosswalkPath}.");
        var rows = new Dictionary<string, CrosswalkRow>(StringComparer.Ordinal);
        int lineNumber = 0;
        while (!file.EofReached())
        {
            lineNumber++;
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 4)
                throw new InvalidOperationException(
                    $"{CrosswalkPath}:{lineNumber} must contain four tab-separated columns."
                );
            var intercepted = new HashSet<string>(
                fields[2].Length == 0
                    ? Array.Empty<string>()
                    : fields[2].Split(',', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal
            );
            var induced = new List<string>();
            if (fields[3] is not ("" or "-"))
            {
                foreach (string encoded in fields[3].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    induced.Add(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            }
            if (!rows.TryAdd(
                fields[0],
                new CrosswalkRow(fields[0], fields[1], intercepted, induced)
            ))
            {
                throw new InvalidOperationException(
                    $"{CrosswalkPath}:{lineNumber} duplicates fixture {fields[0]}."
                );
            }
        }
        return rows;
    }

    private static string CanonicalAdapterDiagnostic(ContentJsonDiagnostic value) =>
        $"{value.RuleId}@{value.JsonPointer}:{value.Message}";

    private static string Sha256OfSorted(IEnumerable<string> values) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    string.Join(
                        "\n",
                        values.OrderBy(value => value, StringComparer.Ordinal)
                    )
                )
            )
        );

    private static void AssertExactMultiset(
        string fixtureId,
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    )
    {
        if (actual.Count == expected.Count)
        {
            bool equal = true;
            for (int index = 0; index < actual.Count; index++)
                equal &= string.Equals(actual[index], expected[index], StringComparison.Ordinal);
            if (equal)
                return;
        }
        List<string> missing = MultisetExcept(expected, actual);
        List<string> unexpected = MultisetExcept(actual, expected);
        throw new InvalidOperationException(
            $"Fixture {fixtureId} migrated rule-hit multiset changed. missing=[{string.Join(" | ", missing)}] unexpected=[{string.Join(" | ", unexpected)}]"
        );
    }

    private static void RequireCount(string label, int actual, int expected)
    {
        if (actual != expected)
            throw new InvalidOperationException(
                $"Stage 1b {label} count mismatch: expected {expected}, got {actual}."
            );
    }

    private static IReadOnlyDictionary<string, List<string>> ReadExpected()
    {
        using FileAccess file = FileAccess.Open(ExactGoldenPath, FileAccess.ModeFlags.Read);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        while (!file.EofReached())
        {
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 6 || fields[1].StartsWith('@'))
                continue;
            if (!result.TryGetValue(fields[1], out List<string>? messages))
            {
                messages = new List<string>();
                result.Add(fields[1], messages);
            }
            messages.Add(Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])));
        }
        return result;
    }

    private sealed record ExpectedOccurrence(string OccurrenceKey, string Message);

    private static IReadOnlyDictionary<string, List<ExpectedOccurrence>> ReadExpectedOccurrences()
    {
        using FileAccess file = FileAccess.Open(ExactGoldenPath, FileAccess.ModeFlags.Read);
        var result = new Dictionary<string, List<ExpectedOccurrence>>(StringComparer.Ordinal);
        while (!file.EofReached())
        {
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 6 || fields[1].StartsWith('@'))
                continue;
            if (!result.TryGetValue(fields[1], out List<ExpectedOccurrence>? rows))
            {
                rows = new List<ExpectedOccurrence>();
                result.Add(fields[1], rows);
            }
            rows.Add(
                new ExpectedOccurrence(
                    fields[0],
                    Encoding.UTF8.GetString(Convert.FromBase64String(fields[2]))
                )
            );
        }
        return result;
    }

    private static int CountWitnessRules(string fixtureId)
    {
        var rules = new HashSet<string>(StringComparer.Ordinal);
        CollectWitnessRules(fixtureId, rules);
        return rules.Count;
    }

    private static void CollectWitnessRules(string fixtureId, HashSet<string> rules)
    {
        using FileAccess file = FileAccess.Open(ExactGoldenPath, FileAccess.ModeFlags.Read);
        while (!file.EofReached())
        {
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (
                fields.Length == 6
                && fields[1] == fixtureId
                && fields[3] == "RULE_WITNESS"
            )
            {
                rules.Add(fields[4]);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string path)
    {
        using DirAccess? directory = DirAccess.Open(path);
        if (directory == null)
            yield break;
        directory.ListDirBegin();
        while (true)
        {
            string name = directory.GetNext();
            if (name.Length == 0)
                break;
            if (directory.CurrentIsDir() && name is not ("." or ".."))
                yield return name;
        }
        directory.ListDirEnd();
    }

    private static IEnumerable<string> EnumerateResourcePaths(string path)
    {
        using DirAccess? directory = DirAccess.Open(path);
        if (directory == null)
            yield break;
        directory.ListDirBegin();
        while (true)
        {
            string name = directory.GetNext();
            if (name.Length == 0)
                break;
            if (directory.CurrentIsDir() || name is "." or "..")
                continue;
            if (name.EndsWith(".tres", StringComparison.Ordinal))
                yield return $"{path}/{name}";
        }
        directory.ListDirEnd();
    }

    private static ContentImportStageResult<SkillImportModel> AdaptAfterStructuralSanitization(
        JsonContentEntryContext context,
        SkillDef skill,
        out string? failure
    )
    {
        ContentImportStageResult<SkillImportModel> result =
            SkillResourceProjectionAdapter.TryAdapt(context, skill);
        for (int round = 0; !result.HasValue && round < 64; round++)
        {
            int applied = 0;
            foreach (ContentJsonDiagnostic diagnostic in result.Diagnostics)
            {
                if (ApplyStructuralRepair(skill, diagnostic))
                    applied++;
            }
            if (applied == 0)
            {
                failure = $"no repair for {string.Join(" | ", result.Diagnostics.Select(value => $"{value.RuleId}@{value.JsonPointer}:{value.Message}"))}";
                return result;
            }
            result = SkillResourceProjectionAdapter.TryAdapt(context, skill);
        }
        failure = result.HasValue
            ? null
            : $"repair limit exceeded: {string.Join(" | ", result.Diagnostics.Select(value => $"{value.RuleId}@{value.JsonPointer}:{value.Message}"))}";
        return result;
    }

    private static bool ApplyStructuralRepair(
        SkillDef skill,
        ContentJsonDiagnostic diagnostic
    )
    {
        string pointer = diagnostic.JsonPointer;
        const string prefix = "/entries/0";
        if (pointer.StartsWith(prefix, StringComparison.Ordinal))
            pointer = pointer[prefix.Length..];
        string[] parts = pointer.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapePointerToken)
            .ToArray();
        if (parts.Length == 0)
            return false;

        if (Array.IndexOf(parts, "payload") is int payloadIndex && payloadIndex >= 0)
        {
            CombatEffectDef? effect = ResolveEffect(skill, parts, payloadIndex);
            if (effect == null || payloadIndex + 1 >= parts.Length)
                return false;
            string key = parts[payloadIndex + 1];
            if (
                diagnostic.Message.Contains("must be an Int32 integer", StringComparison.Ordinal)
                || diagnostic.Message.Contains("Required effect payload member is missing", StringComparison.Ordinal)
            )
            {
                effect.@params[key] = key == "profile_id"
                    ? Variant.From(new StringName("graded_save_execute"))
                    : Variant.From(SafePayloadInteger(key));
            }
            else if (effect.@params.ContainsKey(key))
                effect.@params.Remove(key);
            else
                return false;
            return true;
        }

        if (parts[0] == "level_description_configs")
        {
            skill.level_description_configs = new Godot.Collections.Dictionary();
            return true;
        }
        if (
            parts[0] is "skill_level_requirements"
                or "attribute_requirements"
                or "attribute_growth_progress"
        )
        {
            NormalizeStringIntDictionary(skill, parts[0]);
            return true;
        }
        if (parts.Length >= 2 && parts[0] == "combat_profile" && parts[1] == "level_overrides")
        {
            NormalizeLevelOverrides(skill.combat_profile);
            return true;
        }
        if (parts.Contains("save_failure_status_outcomes"))
            return RepairWeightedOutcome(skill, parts);
        if (RepairNullEffectChild(skill, parts))
            return true;
        if (parts.Length >= 3 && parts[0] == "combat_profile" && parts[1] == "effect_defs")
        {
            if (!int.TryParse(parts[2], out int effectIndex) || skill.combat_profile == null)
                return false;
            if (effectIndex < 0 || effectIndex >= skill.combat_profile.effect_defs.Count)
                return false;
            if (skill.combat_profile.effect_defs[effectIndex] == null)
            {
                skill.combat_profile.effect_defs[effectIndex] = BenignEffect();
                return true;
            }
        }
        if (parts.Length >= 3 && parts[0] == "combat_profile" && parts[1] == "cast_variants")
        {
            if (!int.TryParse(parts[2], out int variantIndex) || skill.combat_profile == null)
                return false;
            if (variantIndex < 0 || variantIndex >= skill.combat_profile.cast_variants.Count)
                return false;
            if (skill.combat_profile.cast_variants[variantIndex] == null)
            {
                skill.combat_profile.cast_variants[variantIndex] = BenignVariant();
                return true;
            }
        }

        if (diagnostic.RuleId == SkillJsonImportRules.InvalidId)
            return SetStringNameAtPath(skill, parts, "diagnostic_value");
        if (diagnostic.RuleId == SkillJsonImportRules.UnknownEffectKind)
            return SetStringNameAtPath(skill, parts, "position_swap");
        if (
            diagnostic.RuleId == SkillJsonImportRules.InvalidEffectPayload
            && parts.Length > 0
        )
        {
            string field = parts[^1];
            if (parts.Length > 1 && int.TryParse(field, out int arrayIndex))
                return RemoveArrayValueAtPath(skill, parts[..^1], arrayIndex);
            return SetStringNameAtPath(
                skill,
                parts,
                parts[^1] switch
                {
                    "lifetime_policy" => "timed",
                    "path_step_area_pattern" => "diamond",
                    "save_dc_mode" => "static",
                    _ => "",
                }
            );
        }
        if (diagnostic.RuleId.EndsWith(".unknown", StringComparison.Ordinal))
            return ResetPropertyToDefault(skill, parts);
        return false;
    }

    private static CombatEffectDef? ResolveEffect(
        SkillDef skill,
        IReadOnlyList<string> parts,
        int payloadIndex
    )
    {
        if (
            parts.Count >= 3
            && parts[0] == "combat_profile"
            && skill.combat_profile != null
        )
        {
            if (
                parts[1] == "effect_defs"
                && int.TryParse(parts[2], out int effectIndex)
                && effectIndex >= 0
                && effectIndex < skill.combat_profile.effect_defs.Count
            )
                return skill.combat_profile.effect_defs[effectIndex];
            if (
                parts[1] == "passive_effect_defs"
                && int.TryParse(parts[2], out int passiveIndex)
                && passiveIndex >= 0
                && passiveIndex < skill.combat_profile.passive_effect_defs.Count
            )
                return skill.combat_profile.passive_effect_defs[passiveIndex];
            if (
                parts.Count >= 5
                && parts[1] == "cast_variants"
                && int.TryParse(parts[2], out int variantIndex)
                && variantIndex >= 0
                && variantIndex < skill.combat_profile.cast_variants.Count
                && skill.combat_profile.cast_variants[variantIndex] != null
                && parts[3] == "effect_defs"
                && int.TryParse(parts[4], out int variantEffectIndex)
                && variantEffectIndex >= 0
                && variantEffectIndex < skill.combat_profile.cast_variants[variantIndex].effect_defs.Count
            )
                return skill.combat_profile.cast_variants[variantIndex].effect_defs[variantEffectIndex];
        }
        object? current = skill;
        for (int index = 0; index < payloadIndex; index++)
            current = ReadPathPart(current, parts[index]);
        return current as CombatEffectDef;
    }

    private static object? ReadPathPart(object? current, string part)
    {
        if (current is GodotObject godotObject)
            return FromVariant(godotObject.Get(part));
        if (current is Godot.Collections.Array array && int.TryParse(part, out int index))
            return index >= 0 && index < array.Count ? FromVariant(array[index]) : null;
        if (current is Godot.Collections.Dictionary dictionary)
            return dictionary.ContainsKey(part) ? FromVariant(dictionary[part]) : null;
        return null;
    }

    private static object? FromVariant(Variant value) => value.VariantType switch
    {
        Variant.Type.Object => value.AsGodotObject(),
        Variant.Type.Array => value.AsGodotArray(),
        Variant.Type.Dictionary => value.AsGodotDictionary(),
        _ => value,
    };

    private static bool SetStringNameAtPath(
        SkillDef skill,
        IReadOnlyList<string> parts,
        string value
    )
    {
        GodotObject? owner = ResolveFieldOwner(skill, parts);
        if (owner == null)
            return false;
        owner.Set(new StringName(parts[^1]), Variant.From(new StringName(value)));
        return true;
    }

    private static bool ResetPropertyToDefault(
        SkillDef skill,
        IReadOnlyList<string> parts
    )
    {
        GodotObject? owner = ResolveFieldOwner(skill, parts);
        if (owner == null)
            return false;
        using GodotObject? defaults = Activator.CreateInstance(owner.GetType()) as GodotObject;
        if (defaults == null)
            return false;
        owner.Set(new StringName(parts[^1]), defaults.Get(new StringName(parts[^1])));
        return true;
    }

    private static GodotObject? ResolveFieldOwner(
        SkillDef skill,
        IReadOnlyList<string> parts
    )
    {
        if (parts.Count == 1)
            return skill;
        if (parts[0] != "combat_profile" || skill.combat_profile == null)
            return null;
        CombatSkillDef combat = skill.combat_profile;
        if (parts.Count == 2)
            return combat;
        if (
            parts[1] == "effect_defs"
            && int.TryParse(parts[2], out int effectIndex)
            && effectIndex >= 0
            && effectIndex < combat.effect_defs.Count
        )
            return combat.effect_defs[effectIndex];
        if (
            parts[1] == "passive_effect_defs"
            && int.TryParse(parts[2], out int passiveIndex)
            && passiveIndex >= 0
            && passiveIndex < combat.passive_effect_defs.Count
        )
            return combat.passive_effect_defs[passiveIndex];
        if (
            parts[1] == "cast_variants"
            && int.TryParse(parts[2], out int variantIndex)
            && variantIndex >= 0
            && variantIndex < combat.cast_variants.Count
        )
        {
            CombatCastVariantDef? variant = combat.cast_variants[variantIndex];
            if (variant == null)
                return null;
            if (parts.Count == 4)
                return variant;
            if (
                parts.Count >= 6
                && parts[3] == "effect_defs"
                && int.TryParse(parts[4], out int variantEffectIndex)
                && variantEffectIndex >= 0
                && variantEffectIndex < variant.effect_defs.Count
            )
                return variant.effect_defs[variantEffectIndex];
        }
        if (parts[1] == "spell_reaction_profile" && parts.Count == 3)
            return combat.spell_reaction_profile;
        if (parts[1] == "ranged_weapon_reaction_profile" && parts.Count == 3)
            return combat.ranged_weapon_reaction_profile;
        return null;
    }

    private static bool RemoveArrayValueAtPath(
        SkillDef skill,
        IReadOnlyList<string> parts,
        int index
    )
    {
        if (parts.Count == 0)
            return false;
        GodotObject? owner = ResolveFieldOwner(skill, parts.Concat(new[] { "_item" }).ToArray());
        if (owner == null)
            return false;
        Variant rawValues = owner.Get(new StringName(parts[^1]));
        if (rawValues.VariantType != Variant.Type.Array)
            return false;
        using Godot.Collections.Array values = rawValues.AsGodotArray();
        if (index < 0 || index >= values.Count)
            return false;
        values.RemoveAt(index);
        return true;
    }

    private static void NormalizeStringIntDictionary(SkillDef skill, string property)
    {
        Godot.Collections.Dictionary source = property switch
        {
            "skill_level_requirements" => skill.skill_level_requirements,
            "attribute_requirements" => skill.attribute_requirements,
            _ => skill.attribute_growth_progress,
        };
        var repaired = new Godot.Collections.Dictionary();
        foreach (Variant key in source.Keys)
        {
            string name = key.VariantType switch
            {
                Variant.Type.String => key.AsString(),
                Variant.Type.StringName => key.AsStringName().ToString(),
                _ => "",
            };
            if (name.Length == 0)
                continue;
            Variant rawValue = source[key];
            if (rawValue.VariantType != Variant.Type.Int)
                continue;
            repaired[name] = rawValue;
        }
        switch (property)
        {
            case "skill_level_requirements": skill.skill_level_requirements = repaired; break;
            case "attribute_requirements": skill.attribute_requirements = repaired; break;
            default: skill.attribute_growth_progress = repaired; break;
        }
    }

    private static void NormalizeLevelOverrides(CombatSkillDef? combat)
    {
        if (combat == null)
            return;
        var repaired = new Godot.Collections.Dictionary();
        foreach (Variant rawKey in combat.level_overrides.Keys)
        {
            if (rawKey.VariantType != Variant.Type.Int)
                continue;
            if (rawKey.AsInt64() < 0)
                continue;
            Variant rawValue = combat.level_overrides[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
                continue;
            using Godot.Collections.Dictionary source = rawValue.AsGodotDictionary();
            var value = new Godot.Collections.Dictionary();
            foreach (Variant fieldKey in source.Keys)
            {
                if (fieldKey.VariantType != Variant.Type.String)
                    continue;
                string field = fieldKey.AsString();
                if (!SupportedLevelOverrideFields.Contains(field))
                    continue;
                Variant fieldValue = source[fieldKey];
                if (IntegerLevelOverrideFields.Contains(field))
                {
                    if (fieldValue.VariantType == Variant.Type.Int)
                        value[field] = fieldValue;
                    continue;
                }
                if (fieldValue.VariantType is Variant.Type.String or Variant.Type.StringName)
                {
                    string wire = fieldValue.VariantType == Variant.Type.String
                        ? fieldValue.AsString()
                        : fieldValue.AsStringName().ToString();
                    if (IsValidLevelOverrideWire(field, wire))
                        value[field] = fieldValue;
                }
            }
            repaired[rawKey] = value;
        }
        combat.level_overrides = repaired;
    }

    private static bool IsValidLevelOverrideWire(string field, string wire) => field switch
    {
        "pending_cast_binding_mode" => wire is "soft_anchor" or "hard_anchor" or "ground_bind",
        "attack_resolution_mode" => wire is "" or "auto" or "direct_effect" or "fate_attack" or "force_hit_no_crit",
        "attack_defense_mode" => wire is "" or "normal" or "touch" or "flat_footed",
        "area_pattern" => wire is "single" or "self" or "diamond" or "square" or "radius" or "cross" or "line" or "cone" or "narrow_cone" or "front_arc",
        _ => false,
    };

    private static readonly HashSet<string> SupportedLevelOverrideFields = new(StringComparer.Ordinal)
    {
        "ap_cost", "mp_cost", "stamina_cost", "mp_cost_per_target_slot",
        "stamina_cost_per_target_slot", "aura_cost", "cooldown_tu", "casting_time_tu",
        "casting_maintenance_dc", "casting_spell_control_dc", "pending_cast_binding_mode",
        "attack_roll_bonus", "attack_resolution_mode", "attack_defense_mode", "area_value",
        "range_value", "area_pattern", "max_target_count", "random_chain_attack_count",
    };

    private static readonly HashSet<string> IntegerLevelOverrideFields = new(StringComparer.Ordinal)
    {
        "ap_cost", "mp_cost", "stamina_cost", "mp_cost_per_target_slot",
        "stamina_cost_per_target_slot", "aura_cost", "cooldown_tu", "casting_time_tu",
        "casting_maintenance_dc", "casting_spell_control_dc", "attack_roll_bonus", "area_value",
        "range_value", "max_target_count", "random_chain_attack_count",
    };

    private static bool RepairWeightedOutcome(SkillDef skill, IReadOnlyList<string> parts)
    {
        int marker = -1;
        for (int partIndex = 0; partIndex < parts.Count; partIndex++)
        {
            if (parts[partIndex] == "save_failure_status_outcomes")
            {
                marker = partIndex;
                break;
            }
        }
        if (marker < 0 || marker + 1 >= parts.Count || !int.TryParse(parts[marker + 1], out int index))
            return false;
        CombatEffectDef? effect = ResolveEffect(skill, parts, marker);
        if (effect == null || index < 0 || index >= effect.save_failure_status_outcomes.Count)
            return false;
        effect.save_failure_status_outcomes[index] = new CombatWeightedStatusOutcomeDef
        {
            outcome_id = $"diagnostic_outcome_{index}",
            weight = 1,
            status_effect = new CombatEffectDef
            {
                effect_type = "status",
                status_id = "diagnostic_status",
                duration_tu = 5,
            },
        };
        return true;
    }

    private static bool RepairNullEffectChild(
        SkillDef skill,
        IReadOnlyList<string> parts
    )
    {
        string[] collections =
        {
            "extra_damage_segments",
            "target_damage_multiplier_rules",
            "equipment_durability_slot_weights",
        };
        int marker = -1;
        foreach (string collection in collections)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                if (parts[index] == collection)
                {
                    marker = index;
                    break;
                }
            }
            if (marker >= 0)
                break;
        }
        if (marker < 0 || marker + 1 >= parts.Count || !int.TryParse(parts[marker + 1], out int itemIndex))
            return false;
        CombatEffectDef? effect = ResolveEffect(skill, parts, marker);
        if (effect == null)
            return false;
        switch (parts[marker])
        {
            case "extra_damage_segments":
                if (itemIndex < 0 || itemIndex >= effect.extra_damage_segments.Count)
                    return false;
                effect.extra_damage_segments[itemIndex] = new CombatDamageSegmentDef
                {
                    damage_tag = "physical_slash",
                    power = 1,
                };
                return true;
            case "target_damage_multiplier_rules":
                if (itemIndex < 0 || itemIndex >= effect.target_damage_multiplier_rules.Count)
                    return false;
                effect.target_damage_multiplier_rules[itemIndex] = new CombatTargetDamageMultiplierRuleDef
                {
                    any_creature_type_tags = new Godot.Collections.Array<StringName> { "dragon" },
                    multiplier_percent = 100,
                };
                return true;
            case "equipment_durability_slot_weights":
                if (itemIndex < 0 || itemIndex >= effect.equipment_durability_slot_weights.Count)
                    return false;
                effect.equipment_durability_slot_weights[itemIndex] = new CombatEffectSlotWeightDef
                {
                    slot_id = "main_hand",
                    weight = 1,
                };
                return true;
            default:
                return false;
        }
    }

    private static CombatEffectDef BenignEffect() => new()
    {
        effect_type = "position_swap",
    };

    private static CombatCastVariantDef BenignVariant() => new()
    {
        variant_id = "diagnostic_variant",
        target_mode = "unit",
        footprint_pattern = "single",
        required_coord_count = 1,
    };

    private static int SafePayloadInteger(string key) =>
        key.Contains("duration_tu", StringComparison.Ordinal) ? 5 : 1;

    private static string UnescapePointerToken(string value) =>
        value.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

    private static List<string> MultisetExcept(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right
    )
    {
        var remaining = new List<string>(right);
        var result = new List<string>();
        foreach (string value in left)
        {
            int index = remaining.FindIndex(candidate => candidate == value);
            if (index >= 0)
                remaining.RemoveAt(index);
            else
                result.Add(value);
        }
        return result;
    }
}
