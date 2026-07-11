using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleSimProfileDef : Resource
{
    [Export]
    public StringName profile_id = "baseline";

    [Export]
    public string display_name = "Baseline";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public BattleAiScoreProfile ai_score_profile = null;

    [Export]
    public Godot.Collections.Array override_patches = new();

    internal BattleSimProfileDefinition ToDefinition()
    {
        var patches = new List<BattleSimOverridePatchDefinition>();
        for (int index = 0; index < override_patches.Count; index++)
        {
            Variant rawPatch = override_patches[index];
            if (rawPatch.VariantType != Variant.Type.Dictionary)
            {
                throw new InvalidDataException(
                    $"Battle sim profile {profile_id} override_patches[{index}] must be a Dictionary."
                );
            }
            GDictionary patch = rawPatch.AsGodotDictionary();
            string targetType = ReadText(patch, "target_type");
            StringName targetId = ReadStringName(patch, "target_id");
            if (targetId == "" && targetType == "action")
                targetId = ReadStringName(patch, "brain_id");
            patches.Add(
                new BattleSimOverridePatchDefinition(
                    targetType,
                    targetId,
                    ReadStringName(patch, "state_id"),
                    ReadStringName(patch, "action_id"),
                    ReadText(patch, "path"),
                    ReadPlainValue(patch, "value")
                )
            );
        }

        return new BattleSimProfileDefinition(
            profile_id,
            display_name,
            description,
            BattleAiScoreProfileDefinition.FromResource(ai_score_profile),
            patches
        );
    }

    private static string ReadText(GDictionary source, string key)
    {
        Variant value = ReadVariant(source, key);
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => value.ToString(),
        };
    }

    private static StringName ReadStringName(GDictionary source, string key) =>
        new(ReadText(source, key));

    private static object ReadPlainValue(GDictionary source, string key)
    {
        Variant value = ReadVariant(source, key);
        return value.VariantType switch
        {
            Variant.Type.Nil => null,
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.Vector2I => value.AsVector2I(),
            _ => throw new InvalidDataException(
                $"Battle sim override value for '{key}' must be a plain scalar, got {value.VariantType}."
            ),
        };
    }

    private static Variant ReadVariant(GDictionary source, string key)
    {
        if (source == null)
            return default;
        Variant stringKey = Variant.From(key);
        if (source.ContainsKey(stringKey))
            return source[stringKey];
        Variant nameKey = Variant.From(new StringName(key));
        return source.ContainsKey(nameKey) ? source[nameKey] : default;
    }

}

internal static class BattleSimProfileAuthoringLoader
{
    internal static IReadOnlyDictionary<StringName, BattleSimProfileDefinition> LoadDefinitions(
        IContentResourceLoader loader,
        IReadOnlyList<string> canonicalPaths
    )
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(canonicalPaths);

        var definitions = new Dictionary<StringName, BattleSimProfileDefinition>();
        foreach (string path in canonicalPaths)
        {
            BattleSimProfileDef profile = loader.LoadCanonical<BattleSimProfileDef>(path);
            if (profile.profile_id == "")
            {
                throw new InvalidDataException(
                    $"BattleSim profile {path} must declare a non-empty profile_id."
                );
            }

            BattleSimProfileDefinition definition = profile.ToDefinition();
            if (!definitions.TryAdd(profile.profile_id, definition))
            {
                throw new InvalidDataException(
                    $"Duplicate BattleSim profile_id registered: {profile.profile_id}"
                );
            }
        }

        return new ReadOnlyDictionary<StringName, BattleSimProfileDefinition>(definitions);
    }
}
