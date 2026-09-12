#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;

public partial class run_skill_definition_projector_parity_regression
    : LifecycleTestSceneTree
{
    private const string ExpectedDefinitionGoldenSha256 =
        "D7AAC4D932184171477982D4F1FC64B8013869CFBDA95C17D567B58C622B8512";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            var validator = new SkillImportModelValidator();
            var goldenRows = new List<string>();
            var observedPayloadKinds = new HashSet<CombatEffectPayloadKind>();
            ContentImportBatch<SkillImportModel> batch =
                SkillContentJsonAuthoringDomain.CreateImportDescriptor(
                    "res://data/configs/json/skills",
                    new GodotContentJsonSourceReader()
                ).Import();
            _test.False(batch.HasErrors, $"skill JSON catalog should import: {FormatDiagnostics(batch.Diagnostics)}");
            foreach (ContentImportEntry<SkillImportModel> entry in batch.Entries)
            {
                SkillImportModel import = entry.Import;
                string skillId = import.SkillId.Value;
                IReadOnlyList<string> jsonValidation = validator.ValidateMessages(import);
                _test.Eq(
                    jsonValidation.Count,
                    0,
                    $"{skillId} JSON import should satisfy the migrated validator: {string.Join(" | ", jsonValidation)}"
                );

                SkillDefinition jsonDefinition = SkillDefinitionProjector.Project(import);
                AssertTypedPayloadGraph(skillId, jsonDefinition, observedPayloadKinds);
                goldenRows.Add($"{skillId}\t{Serialize(ToCanonicalValue(jsonDefinition))}");
            }
            foreach (CombatEffectPayloadKind payloadKind in Enum.GetValues<CombatEffectPayloadKind>())
            {
                _test.True(
                    observedPayloadKinds.Contains(payloadKind),
                    $"production skill catalog should exercise typed payload kind {payloadKind}"
                );
            }
            TestProductionProjectionPaths();
            _test.Eq(
                goldenRows.Count,
                706,
                "definition projection golden must include every migrated skill"
            );
            goldenRows.Sort(StringComparer.Ordinal);
            string goldenSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", goldenRows)))
            );
            _test.Eq(
                goldenSha256,
                ExpectedDefinitionGoldenSha256,
                "stage-2 full-domain skill Definition SHA256 golden"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Skill definition projector parity regression"));
    }

    private void AssertTypedPayloadGraph(
        string skillId,
        SkillDefinition definition,
        ISet<CombatEffectPayloadKind> observedPayloadKinds
    )
    {
        CombatSkillDefinition? profile = definition?.CombatProfile;
        if (profile == null)
            return;
        AssertTypedPayloads(skillId, "effects", profile.EffectDefinitions, observedPayloadKinds);
        AssertTypedPayloads(
            skillId,
            "passive_effects",
            profile.PassiveEffectDefinitions,
            observedPayloadKinds
        );
        foreach (CombatCastVariantDefinition variant in profile.CastVariants)
        {
            AssertTypedPayloads(
                skillId,
                $"cast_variant[{variant.VariantId}]",
                variant.EffectDefinitions,
                observedPayloadKinds
            );
        }
    }

    private void AssertTypedPayloads(
        string skillId,
        string owner,
        IReadOnlyList<CombatEffectDefinition> effects,
        ISet<CombatEffectPayloadKind> observedPayloadKinds
    )
    {
        for (int index = 0; index < effects.Count; index += 1)
        {
            CombatEffectDefinition effect = effects[index];
            _test.True(
                PayloadMatchesEffectKind(effect),
                $"{skillId} {owner}[{index}] {effect.EffectType} should own its closed typed payload"
            );
            observedPayloadKinds.Add(effect.Payload.Kind);
            foreach (CombatWeightedStatusOutcomeDefinition outcome in effect.SaveFailureStatusOutcomes)
            {
                AssertTypedPayloads(
                    skillId,
                    $"{owner}[{index}].save_failure_status_outcomes[{outcome.OutcomeId}]",
                    new[] { outcome.StatusEffect },
                    observedPayloadKinds
                );
            }
        }
    }

    private static bool PayloadMatchesEffectKind(CombatEffectDefinition effect) =>
        effect.EffectKind switch
        {
            BattleEffectKind.Status or BattleEffectKind.ApplyStatus =>
                effect.Payload is StatusEffectPayloadDefinition,
            BattleEffectKind.Heal => effect.Payload is HealEffectPayloadDefinition,
            BattleEffectKind.EquipmentDurabilityDamage =>
                effect.Payload is EquipmentDurabilityDamageEffectPayloadDefinition,
            BattleEffectKind.RepeatAttackUntilFail =>
                effect.Payload is RepeatAttackUntilFailEffectPayloadDefinition,
            BattleEffectKind.LayeredBarrier =>
                effect.Payload is LayeredBarrierEffectPayloadDefinition,
            BattleEffectKind.GradedSaveExecute =>
                effect.Payload is GradedSaveExecuteEffectPayloadDefinition,
            BattleEffectKind.DispelMagic =>
                effect.Payload is DispelMagicEffectPayloadDefinition,
            BattleEffectKind.OnKillGainResources =>
                effect.Payload is OnKillGainResourcesEffectPayloadDefinition,
            _ => effect.Payload is EmptyCombatEffectPayloadDefinition,
        };

    private void TestProductionProjectionPaths()
    {
        using var registry = new SkillContentRegistry();
        IReadOnlyDictionary<StringName, SkillDefinition> snapshotDefinitions =
            registry.GetSkillDefinitionsTyped();
        using var validationRegistry = new ProgressionContentRegistry(loadDefaultContent: false);
        validationRegistry.ReplaceDefinitionsForValidation(
            new ProgressionDefinitionSources { SkillDefinitions = snapshotDefinitions }
        );
        IReadOnlyDictionary<StringName, SkillDefinition> reprojectionDefinitions =
            validationRegistry.GetSkillDefinitionsTyped();
        _test.Eq(
            snapshotDefinitions.Count,
            reprojectionDefinitions.Count,
            "snapshot publication and validation replacement re-projection must expose the same skill count"
        );
        foreach ((StringName skillId, SkillDefinition snapshotDefinition) in snapshotDefinitions)
        {
            _test.True(
                reprojectionDefinitions.TryGetValue(
                    skillId,
                    out SkillDefinition? reprojectionDefinition
                ),
                $"re-projection path should contain {skillId}"
            );
            if (reprojectionDefinition == null)
                continue;
            string? difference = FindFirstDifference(
                ToCanonicalValue(snapshotDefinition),
                ToCanonicalValue(reprojectionDefinition),
                ""
            );
            _test.True(
                difference == null,
                $"snapshot and validation replacement re-projection must agree for {skillId}"
                    + (difference == null ? "" : $": {difference}")
            );
        }
    }

    private static string? FindFirstDifference(
        object? expected,
        object? actual,
        string pointer
    )
    {
        if (expected == null || actual == null)
        {
            return Equals(expected, actual)
                ? null
                : $"{Pointer(pointer)} expected={Serialize(expected)} actual={Serialize(actual)}";
        }
        if (
            expected is IReadOnlyDictionary<string, object?> expectedMap
            && actual is IReadOnlyDictionary<string, object?> actualMap
        )
        {
            foreach (string key in expectedMap.Keys.Union(actualMap.Keys).OrderBy(
                value => value,
                StringComparer.Ordinal
            ))
            {
                if (!expectedMap.TryGetValue(key, out object? expectedValue))
                    return $"{Pointer(pointer)}/{EscapePointerToken(key)} is unexpected";
                if (!actualMap.TryGetValue(key, out object? actualValue))
                    return $"{Pointer(pointer)}/{EscapePointerToken(key)} is missing";
                string? difference = FindFirstDifference(
                    expectedValue,
                    actualValue,
                    $"{pointer}/{EscapePointerToken(key)}"
                );
                if (difference != null)
                    return difference;
            }
            return null;
        }
        if (expected is IReadOnlyList<object?> expectedList && actual is IReadOnlyList<object?> actualList)
        {
            if (expectedList.Count != actualList.Count)
                return $"{Pointer(pointer)} expected_count={expectedList.Count} actual_count={actualList.Count}";
            for (int index = 0; index < expectedList.Count; index++)
            {
                string? difference = FindFirstDifference(
                    expectedList[index],
                    actualList[index],
                    $"{pointer}/{index}"
                );
                if (difference != null)
                    return difference;
            }
            return null;
        }
        return Equals(expected, actual)
            ? null
            : $"{Pointer(pointer)} expected={Serialize(expected)} actual={Serialize(actual)}";
    }

    private static string Pointer(string value) => value.Length == 0 ? "/" : value;

    private static string EscapePointerToken(string value) => value.Replace("~", "~0").Replace("/", "~1");

    private static string Serialize(object? value) => JsonSerializer.Serialize(value);

    private static object? ToCanonicalValue(object? value)
    {
        if (value == null)
            return null;
        if (value is StringName stringName)
            return stringName.ToString();
        Type type = value.GetType();
        if (
            value is string
            || value is bool
            || value is byte
            || value is short
            || value is int
            || value is long
            || value is float
            || value is double
            || value is decimal
        )
            return value;
        if (type.IsEnum)
            return value.ToString();

        Type? dictionaryInterface = type.GetInterfaces()
            .FirstOrDefault(candidate =>
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition()
                    == typeof(IReadOnlyDictionary<,>)
            );
        if (dictionaryInterface != null)
        {
            var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (object entry in (IEnumerable)value)
            {
                PropertyInfo keyProperty = entry.GetType().GetProperty("Key")!;
                PropertyInfo valueProperty = entry.GetType().GetProperty("Value")!;
                object? key = keyProperty.GetValue(entry);
                result[CanonicalKey(key)] = ToCanonicalValue(
                    valueProperty.GetValue(entry)
                );
            }
            return result;
        }

        if (value is IEnumerable enumerable)
        {
            var result = new List<object?>();
            foreach (object? item in enumerable)
                result.Add(ToCanonicalValue(item));
            return result;
        }

        var objectResult = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (
            PropertyInfo property in type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            ).OrderBy(property => property.Name, StringComparer.Ordinal)
        )
        {
            if (property.GetIndexParameters().Length == 0 && property.CanRead)
                objectResult[property.Name] = ToCanonicalValue(property.GetValue(value));
        }
        return objectResult;
    }

    private static string CanonicalKey(object? key) => key switch
    {
        null => "",
        StringName stringName => stringName.ToString(),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => key.ToString() ?? "",
    };

    private static string FormatDiagnostics(
        IReadOnlyList<ContentJsonDiagnostic> diagnostics
    ) => string.Join(
        " | ",
        diagnostics.Select(value => $"{value.RuleId}@{value.JsonPointer}:{value.Message}")
    );
}
