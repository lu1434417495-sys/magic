using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleEnvironmentSnapshot
{
    private static readonly StringName NightTag = "night";

    private readonly List<StringName> _globalEnvironmentTags;

    private BattleEnvironmentSnapshot(
        IEnumerable<StringName> globalEnvironmentTags,
        int worldStep = -1
    )
    {
        _globalEnvironmentTags = NormalizeTags(globalEnvironmentTags);
        WorldStep = worldStep;
        Revision = ComputeRevision(_globalEnvironmentTags, WorldStep);
    }

    public int Revision { get; }

    public int WorldStep { get; }

    public IReadOnlyList<StringName> GlobalEnvironmentTags => _globalEnvironmentTags;

    public static BattleEnvironmentSnapshot Empty() => new(Array.Empty<StringName>(), -1);

    internal static BattleEnvironmentSnapshot FromGlobalTags(
        IEnumerable<StringName> globalEnvironmentTags,
        int worldStep = -1
    ) =>
        new(globalEnvironmentTags, worldStep);

    public static BattleEnvironmentSnapshot FromBattleStartContext(
        GDictionary context,
        StringName terrainProfileId = default
    )
    {
        int worldStep = TryReadInt(context, "world_step", out int readWorldStep)
            ? readWorldStep
            : -1;
        if (context != null && context.ContainsKey("global_environment_tags"))
        {
            return new BattleEnvironmentSnapshot(
                ReadStringNameArray(context["global_environment_tags"]),
                worldStep
            );
        }

        List<StringName> tags = new();
        if (WorldTimeSystem.IsNightStep(worldStep))
        {
            tags.Add(NightTag);
        }
        return new BattleEnvironmentSnapshot(tags, worldStep);
    }

    public BattleEnvironmentSnapshot DuplicateState() =>
        new(_globalEnvironmentTags, WorldStep);

    public bool HasGlobalTag(StringName tag)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(tag);
        return normalized != "" && _globalEnvironmentTags.Contains(normalized);
    }

    public GArray ProjectGlobalEnvironmentTags()
    {
        GArray result = new();
        foreach (StringName tag in _globalEnvironmentTags)
            result.Add(tag);
        return result;
    }

    private static List<StringName> NormalizeTags(IEnumerable<StringName> tags)
    {
        HashSet<StringName> seen = new();
        List<StringName> result = new();
        foreach (StringName tag in tags ?? Array.Empty<StringName>())
        {
            StringName normalized = ProgressionDataUtils.to_string_name(tag);
            if (normalized != "" && seen.Add(normalized))
                result.Add(normalized);
        }
        result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return result;
    }

    private static int ComputeRevision(IReadOnlyList<StringName> tags, int worldStep)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + worldStep;
            foreach (StringName tag in tags ?? Array.Empty<StringName>())
                hash = hash * 31 + tag.ToString().GetHashCode(StringComparison.Ordinal);
            return hash;
        }
    }

    private static IEnumerable<StringName> ReadStringNameArray(Variant value)
    {
        if (value.VariantType != Variant.Type.Array)
            yield break;

        GArray values = value.AsGodotArray();
        foreach (Variant rawValue in values)
        {
            StringName tag = ProgressionDataUtils.to_string_name(rawValue);
            if (tag != "")
                yield return tag;
        }
    }

    private static bool TryReadInt(GDictionary context, string key, out int value)
    {
        value = 0;
        if (context == null || string.IsNullOrEmpty(key) || !context.ContainsKey(key))
            return false;
        Variant rawValue = context[key];
        if (rawValue.VariantType != Variant.Type.Int)
            return false;
        value = rawValue.AsInt32();
        return true;
    }
}
