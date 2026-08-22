#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using Godot;

internal static class BattleSimProfileDefinitionProjector
{
    internal static BattleSimProfileDefinition Project(BattleSimProfileImportModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var patches = new List<BattleSimOverridePatchDefinition>(source.OverridePatches.Count);
        foreach (BattleSimOverridePatchImportModel patch in source.OverridePatches)
        {
            patches.Add(new BattleSimOverridePatchDefinition(
                patch.TargetType,
                patch.TargetId,
                patch.StateId,
                patch.ActionId,
                patch.Path,
                patch.Value
            ));
        }
        return new BattleSimProfileDefinition(
            source.ProfileId,
            source.DisplayName,
            source.Description,
            ProjectScoreProfile(source.AiScoreProfile),
            patches
        );
    }

    private static BattleAiScoreProfileDefinition ProjectScoreProfile(BattleAiScoreProfileJsonDto source)
    {
        BattleAiScoreProfileDefinition result = BattleAiScoreProfileDefinition.Default;
        foreach (PropertyInfo property in typeof(BattleAiScoreProfileJsonDto).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string path = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "";
            if (property.GetValue(source) is int scalar
                && result.TryWithScalar(path, scalar, out BattleAiScoreProfileDefinition patched))
                result = patched;
        }
        return result with
        {
            MeteorFriendlyFireProfile = source.MeteorFriendlyFireProfile,
            ActionBaseScores = FreezeIntDictionary(source.ActionBaseScores),
            BucketPriorities = FreezeIntDictionary(source.BucketPriorities),
        };
    }

    private static IReadOnlyDictionary<StringName, int> FreezeIntDictionary(IReadOnlyDictionary<string, int> source)
    {
        var values = new Dictionary<StringName, int>();
        foreach ((string key, int value) in source) values[key] = value;
        return EnemyDefinitionCollections.FreezeDictionary(values);
    }
}
