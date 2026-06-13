using System.Collections.Generic;
using Godot;

public static class CharacterCreationIdentityOptionService
{
    public static IReadOnlyList<StringName> CollectCreationRaceIds(
        ProgressionContentRegistry contentSource
    )
    {
        return CollectCreationRaceIds(
            contentSource?.GetRaceDefsTyped() ?? new Dictionary<StringName, RaceDef>(),
            contentSource?.GetSubraceDefsTyped() ?? new Dictionary<StringName, SubraceDef>()
        );
    }

    internal static IReadOnlyList<StringName> CollectCreationRaceIds(
        ProgressionIdentityCatalogData identityCatalog
    )
    {
        return CollectCreationRaceIds(
            identityCatalog?.RaceDefs ?? new Dictionary<StringName, RaceDef>(),
            identityCatalog?.SubraceDefs ?? new Dictionary<StringName, SubraceDef>()
        );
    }

    internal static IReadOnlyList<StringName> CollectCreationRaceIds(
        IReadOnlyDictionary<StringName, RaceDef> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs
    )
    {
        var ids = new List<StringName>();
        raceDefs ??= new Dictionary<StringName, RaceDef>();
        subraceDefs ??= new Dictionary<StringName, SubraceDef>();
        foreach (StringName raceId in SortedBucketIds(raceDefs))
        {
            if (CollectSubraceIdsForRace(raceDefs, subraceDefs, raceId).Count > 0)
                ids.Add(raceId);
        }
        return ids;
    }

    public static IReadOnlyList<StringName> CollectSubraceIdsForRace(
        ProgressionContentRegistry contentSource,
        StringName raceId
    )
    {
        var ids = new List<StringName>();
        if (contentSource == null || raceId == "")
            return ids;

        return CollectSubraceIdsForRace(
            contentSource.GetRaceDefsTyped(),
            contentSource.GetSubraceDefsTyped(),
            raceId
        );
    }

    internal static IReadOnlyList<StringName> CollectSubraceIdsForRace(
        ProgressionIdentityCatalogData identityCatalog,
        StringName raceId
    )
    {
        return CollectSubraceIdsForRace(
            identityCatalog?.RaceDefs ?? new Dictionary<StringName, RaceDef>(),
            identityCatalog?.SubraceDefs ?? new Dictionary<StringName, SubraceDef>(),
            raceId
        );
    }

    internal static IReadOnlyList<StringName> CollectSubraceIdsForRace(
        IReadOnlyDictionary<StringName, RaceDef> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs,
        StringName raceId
    )
    {
        var ids = new List<StringName>();
        if (raceId == "")
            return ids;

        raceDefs ??= new Dictionary<StringName, RaceDef>();
        subraceDefs ??= new Dictionary<StringName, SubraceDef>();
        if (!raceDefs.TryGetValue(raceId, out RaceDef raceDef) || raceDef == null)
            return ids;

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

    public static StringName ChooseRaceId(
        ProgressionContentRegistry contentSource,
        StringName currentId,
        StringName defaultId
    )
    {
        return ChooseRaceId(
            contentSource?.GetRaceDefsTyped() ?? new Dictionary<StringName, RaceDef>(),
            contentSource?.GetSubraceDefsTyped() ?? new Dictionary<StringName, SubraceDef>(),
            currentId,
            defaultId
        );
    }

    internal static StringName ChooseRaceId(
        ProgressionIdentityCatalogData identityCatalog,
        StringName currentId,
        StringName defaultId
    )
    {
        return ChooseRaceId(
            identityCatalog?.RaceDefs ?? new Dictionary<StringName, RaceDef>(),
            identityCatalog?.SubraceDefs ?? new Dictionary<StringName, SubraceDef>(),
            currentId,
            defaultId
        );
    }

    internal static StringName ChooseRaceId(
        IReadOnlyDictionary<StringName, RaceDef> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs,
        StringName currentId,
        StringName defaultId
    )
    {
        IReadOnlyList<StringName> candidates = CollectCreationRaceIds(raceDefs, subraceDefs);
        if (currentId != "" && ContainsId(candidates, currentId))
            return currentId;
        if (defaultId != "" && ContainsId(candidates, defaultId))
            return defaultId;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
    }

    public static StringName ChooseSubraceId(
        ProgressionContentRegistry contentSource,
        StringName raceId,
        StringName currentId
    )
    {
        return ChooseSubraceId(
            contentSource?.GetRaceDefsTyped() ?? new Dictionary<StringName, RaceDef>(),
            contentSource?.GetSubraceDefsTyped() ?? new Dictionary<StringName, SubraceDef>(),
            raceId,
            currentId
        );
    }

    internal static StringName ChooseSubraceId(
        ProgressionIdentityCatalogData identityCatalog,
        StringName raceId,
        StringName currentId
    )
    {
        return ChooseSubraceId(
            identityCatalog?.RaceDefs ?? new Dictionary<StringName, RaceDef>(),
            identityCatalog?.SubraceDefs ?? new Dictionary<StringName, SubraceDef>(),
            raceId,
            currentId
        );
    }

    internal static StringName ChooseSubraceId(
        IReadOnlyDictionary<StringName, RaceDef> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs,
        StringName raceId,
        StringName currentId
    )
    {
        IReadOnlyList<StringName> candidates = CollectSubraceIdsForRace(
            raceDefs,
            subraceDefs,
            raceId
        );
        if (currentId != "" && ContainsId(candidates, currentId))
            return currentId;

        raceDefs ??= new Dictionary<StringName, RaceDef>();
        raceDefs.TryGetValue(raceId, out RaceDef raceDef);
        StringName defaultSubraceId = raceDef?.default_subrace_id ?? new StringName("");
        if (defaultSubraceId != "" && ContainsId(candidates, defaultSubraceId))
            return defaultSubraceId;
        return candidates.Count > 0 ? candidates[0] : new StringName("");
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
            contentSource.GetRaceDefsTyped(),
            contentSource.GetSubraceDefsTyped(),
            raceId,
            subraceId
        );
    }

    internal static bool IsValidCreationRaceSubracePair(
        ProgressionIdentityCatalogData identityCatalog,
        StringName raceId,
        StringName subraceId
    )
    {
        return IsValidCreationRaceSubracePair(
            identityCatalog?.RaceDefs ?? new Dictionary<StringName, RaceDef>(),
            identityCatalog?.SubraceDefs ?? new Dictionary<StringName, SubraceDef>(),
            raceId,
            subraceId
        );
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
}
