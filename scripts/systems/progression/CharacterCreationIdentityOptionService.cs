using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public static class CharacterCreationIdentityOptionService
{
    public static IReadOnlyList<StringName> CollectCreationRaceIds(
        ProgressionContentRegistry contentSource
    )
    {
        var ids = new List<StringName>();
        Dictionary<StringName, RaceDef> raceDefs = ReadBucket<RaceDef>(
            contentSource?.get_race_defs()
        );
        foreach (StringName raceId in SortedBucketIds(raceDefs))
        {
            if (CollectSubraceIdsForRace(contentSource, raceId).Count > 0)
                ids.Add(raceId);
        }
        return ids;
    }

    public static GStringNameArray collect_creation_race_ids(
        ProgressionContentRegistry content_source
    )
    {
        return ToStringNameArray(CollectCreationRaceIds(content_source));
    }

    public static IReadOnlyList<StringName> CollectSubraceIdsForRace(
        ProgressionContentRegistry contentSource,
        StringName raceId
    )
    {
        var ids = new List<StringName>();
        if (contentSource == null || raceId == "")
            return ids;

        Dictionary<StringName, RaceDef> raceDefs = ReadBucket<RaceDef>(
            contentSource.get_race_defs()
        );
        if (!raceDefs.TryGetValue(raceId, out RaceDef raceDef) || raceDef == null)
            return ids;

        Dictionary<StringName, SubraceDef> subraceDefs = ReadBucket<SubraceDef>(
            contentSource.get_subrace_defs()
        );
        var seen = new HashSet<StringName>();
        foreach (StringName subraceId in raceDef.subrace_ids)
        {
            if (subraceId == "" || !seen.Add(subraceId))
                continue;
            if (
                !subraceDefs.TryGetValue(subraceId, out SubraceDef subraceDef)
                || subraceDef == null
            )
                continue;
            if (subraceDef.parent_race_id != raceId)
                continue;
            if (!IsValidCreationRaceSubracePair(raceDefs, subraceDefs, raceId, subraceId))
                continue;
            ids.Add(subraceId);
        }
        SortStringNames(ids);
        return ids;
    }

    public static GStringNameArray collect_subrace_ids_for_race(
        ProgressionContentRegistry content_source,
        StringName race_id
    )
    {
        return ToStringNameArray(CollectSubraceIdsForRace(content_source, race_id));
    }

    public static StringName ChooseRaceId(
        ProgressionContentRegistry contentSource,
        StringName currentId,
        StringName defaultId
    )
    {
        IReadOnlyList<StringName> candidates = CollectCreationRaceIds(contentSource);
        if (currentId != "" && ContainsId(candidates, currentId))
            return currentId;
        if (defaultId != "" && ContainsId(candidates, defaultId))
            return defaultId;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static StringName choose_race_id(
        ProgressionContentRegistry content_source,
        StringName current_id,
        StringName default_id
    )
    {
        return ChooseRaceId(content_source, current_id, default_id);
    }

    public static StringName ChooseSubraceId(
        ProgressionContentRegistry contentSource,
        StringName raceId,
        StringName currentId
    )
    {
        IReadOnlyList<StringName> candidates = CollectSubraceIdsForRace(contentSource, raceId);
        if (currentId != "" && ContainsId(candidates, currentId))
            return currentId;

        Dictionary<StringName, RaceDef> raceDefs = ReadBucket<RaceDef>(
            contentSource?.get_race_defs()
        );
        raceDefs.TryGetValue(raceId, out RaceDef raceDef);
        StringName defaultSubraceId = raceDef?.default_subrace_id ?? new StringName("");
        if (defaultSubraceId != "" && ContainsId(candidates, defaultSubraceId))
            return defaultSubraceId;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static StringName choose_subrace_id(
        ProgressionContentRegistry content_source,
        StringName race_id,
        StringName current_id
    )
    {
        return ChooseSubraceId(content_source, race_id, current_id);
    }

    public static bool IsValidCreationRaceSubracePair(
        ProgressionContentRegistry contentSource,
        StringName raceId,
        StringName subraceId
    )
    {
        if (contentSource == null)
            return false;

        return IsValidCreationRaceSubracePair(
            ReadBucket<RaceDef>(contentSource.get_race_defs()),
            ReadBucket<SubraceDef>(contentSource.get_subrace_defs()),
            raceId,
            subraceId
        );
    }

    public static bool is_valid_creation_race_subrace_pair(
        ProgressionContentRegistry content_source,
        StringName race_id,
        StringName subrace_id
    )
    {
        return IsValidCreationRaceSubracePair(content_source, race_id, subrace_id);
    }

    private static bool IsValidCreationRaceSubracePair(
        IReadOnlyDictionary<StringName, RaceDef> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs,
        StringName raceId,
        StringName subraceId
    )
    {
        if (raceId == "" || subraceId == "")
            return false;
        if (!raceDefs.TryGetValue(raceId, out RaceDef raceDef) || raceDef == null)
            return false;
        if (!subraceDefs.TryGetValue(subraceId, out SubraceDef subraceDef) || subraceDef == null)
            return false;
        return subraceDef.parent_race_id == raceId && ContainsId(raceDef.subrace_ids, subraceId);
    }

    private static Dictionary<StringName, T> ReadBucket<T>(GDictionary bucket)
        where T : class
    {
        var entries = new Dictionary<StringName, T>();
        if (bucket == null)
            return entries;

        foreach (Variant rawKey in bucket.Keys)
        {
            StringName id = ToStringName(rawKey);
            if (id == "" || entries.ContainsKey(id))
                continue;
            T value = ReadObject<T>(bucket[rawKey]);
            if (value != null)
                entries[id] = value;
        }
        return entries;
    }

    private static IReadOnlyList<StringName> SortedBucketIds<T>(
        IReadOnlyDictionary<StringName, T> bucket
    )
    {
        var ids = new List<StringName>(bucket.Keys);
        SortStringNames(ids);
        return ids;
    }

    private static void SortStringNames(List<StringName> ids)
    {
        ids.Sort((left, right) => string.CompareOrdinal((string)left, (string)right));
    }

    private static bool ContainsId(IEnumerable<StringName> ids, StringName targetId)
    {
        foreach (StringName id in ids)
        {
            if (id == targetId)
                return true;
        }
        return false;
    }

    private static T ReadObject<T>(Variant rawValue)
        where T : class
    {
        return rawValue.VariantType == Variant.Type.Object ? rawValue.AsGodotObject() as T : null;
    }

    private static StringName ToStringName(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.StringName => rawKey.AsStringName(),
            Variant.Type.String => new StringName(rawKey.AsString()),
            _ => new StringName(rawKey.AsString()),
        };
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> ids)
    {
        var result = new GStringNameArray();
        foreach (StringName id in ids)
            result.Add(id);
        return result;
    }
}
