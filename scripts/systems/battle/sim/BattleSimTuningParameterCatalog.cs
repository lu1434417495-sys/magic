using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Godot;

internal readonly record struct BattleSimTuningParameterSpec(
    string Path,
    int MinimumValue,
    int MaximumValue
);

internal static class BattleSimTuningParameterCatalog
{
    internal const string CanonicalResourcePath =
        "res://tools/battle_sim_tuner/score_weight_space.json";

    internal static IReadOnlyList<BattleSimTuningParameterSpec> LoadCanonical()
    {
        string absolutePath = ProjectSettings.GlobalizePath(CanonicalResourcePath);
        return Load(absolutePath);
    }

    internal static IReadOnlyList<BattleSimTuningParameterSpec> Load(
        string absolutePath
    )
    {
        if (
            string.IsNullOrWhiteSpace(absolutePath)
            || !File.Exists(absolutePath)
        )
        {
            throw new FileNotFoundException(
                "BattleSim tuning parameter catalog was not found.",
                absolutePath
            );
        }

        using FileStream stream = File.OpenRead(absolutePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "BattleSim tuning parameter catalog must be a JSON array."
            );
        }

        var specs = new List<BattleSimTuningParameterSpec>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement rawSpec in document.RootElement.EnumerateArray())
        {
            if (rawSpec.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"BattleSim tuning parameter catalog entry {index} must be an object."
                );
            }
            string path = ReadRequiredString(rawSpec, "path", index);
            int minimum = ReadRequiredInt(rawSpec, "min", index);
            int maximum = ReadRequiredInt(rawSpec, "max", index);
            if (!seenPaths.Add(path))
            {
                throw new InvalidDataException(
                    $"BattleSim tuning parameter catalog path '{path}' is duplicated."
                );
            }
            if (minimum > maximum)
            {
                throw new InvalidDataException(
                    $"BattleSim tuning parameter catalog path '{path}' has min greater than max."
                );
            }
            specs.Add(
                new BattleSimTuningParameterSpec(path, minimum, maximum)
            );
            index += 1;
        }
        if (specs.Count == 0)
        {
            throw new InvalidDataException(
                "BattleSim tuning parameter catalog must not be empty."
            );
        }
        return new ReadOnlyCollection<BattleSimTuningParameterSpec>(specs);
    }

    private static string ReadRequiredString(
        JsonElement entry,
        string propertyName,
        int entryIndex
    )
    {
        if (
            !entry.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
        )
        {
            throw new InvalidDataException(
                $"BattleSim tuning parameter catalog entry {entryIndex} has invalid '{propertyName}'."
            );
        }
        return value.GetString();
    }

    private static int ReadRequiredInt(
        JsonElement entry,
        string propertyName,
        int entryIndex
    )
    {
        if (
            !entry.TryGetProperty(propertyName, out JsonElement value)
            || !value.TryGetInt32(out int result)
        )
        {
            throw new InvalidDataException(
                $"BattleSim tuning parameter catalog entry {entryIndex} has invalid '{propertyName}'."
            );
        }
        return result;
    }
}
